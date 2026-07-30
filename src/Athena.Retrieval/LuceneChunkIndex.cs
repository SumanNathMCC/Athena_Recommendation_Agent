using System.Collections.Concurrent;
using Athena.Core.Indexing;
using Athena.Core.Records;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Search.Similarities;
using Lucene.Net.Store;
using Lucene.Net.Util;

namespace Athena.Retrieval;

/// <summary>
/// Lucene BM25 index over chunk text, kept in sync via <see cref="IChunkIndexSync"/>.
/// </summary>
public sealed class LuceneChunkIndex : IChunkIndexSync, IDisposable
{
    private const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;

    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, ChunkRecord> _chunks = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _chunkIdsByDoc =
        new(StringComparer.Ordinal);

    private RAMDirectory _directory;
    private StandardAnalyzer _analyzer;
    private IndexWriter _writer;
    private SearcherManager? _searcherManager;
    private bool _disposed;

    public LuceneChunkIndex()
    {
        _directory = new RAMDirectory();
        _analyzer = new StandardAnalyzer(AppLuceneVersion);
        var config = new IndexWriterConfig(AppLuceneVersion, _analyzer)
        {
            OpenMode = OpenMode.CREATE,
            Similarity = new BM25Similarity()
        };
        _writer = new IndexWriter(_directory, config);
        _searcherManager = new SearcherManager(_writer, applyAllDeletes: true, new SearcherFactory());
    }

    public void Upsert(IReadOnlyList<ChunkRecord> chunks)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        if (chunks.Count == 0)
        {
            return;
        }

        lock (_gate)
        {
            EnsureNotDisposed();
            foreach (var chunk in chunks)
            {
                if (string.IsNullOrWhiteSpace(chunk.ChunkId))
                {
                    continue;
                }

                _chunks[chunk.ChunkId] = chunk;
                var byDoc = _chunkIdsByDoc.GetOrAdd(
                    chunk.DocId,
                    _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
                byDoc[chunk.ChunkId] = 0;

                _writer.UpdateDocument(
                    new Term("chunkId", chunk.ChunkId),
                    ToDocument(chunk));
            }

            _writer.Commit();
            _searcherManager!.MaybeRefreshBlocking();
        }
    }

    public void RemoveByDocId(string docId)
    {
        if (string.IsNullOrWhiteSpace(docId))
        {
            return;
        }

        lock (_gate)
        {
            EnsureNotDisposed();
            if (!_chunkIdsByDoc.TryRemove(docId, out var ids))
            {
                return;
            }

            foreach (var chunkId in ids.Keys)
            {
                _chunks.TryRemove(chunkId, out _);
                _writer.DeleteDocuments(new Term("chunkId", chunkId));
            }

            _writer.Commit();
            _searcherManager!.MaybeRefreshBlocking();
        }
    }

    public IReadOnlyList<Passage> Search(string query, int topK, string? docId)
    {
        if (string.IsNullOrWhiteSpace(query) || topK <= 0)
        {
            return Array.Empty<Passage>();
        }

        lock (_gate)
        {
            EnsureNotDisposed();
            if (_chunks.IsEmpty)
            {
                return Array.Empty<Passage>();
            }

            _searcherManager!.MaybeRefreshBlocking();
            var searcher = _searcherManager.Acquire();
            try
            {
                var parser = new QueryParser(AppLuceneVersion, "text", _analyzer)
                {
                    DefaultOperator = Operator.OR,
                    AllowLeadingWildcard = false
                };

                Query luceneQuery;
                try
                {
                    luceneQuery = parser.Parse(QueryParser.Escape(query.Trim()));
                }
                catch (ParseException)
                {
                    luceneQuery = new TermQuery(new Term("text", query.Trim().ToLowerInvariant()));
                }

                if (!string.IsNullOrWhiteSpace(docId))
                {
                    var filter = new TermQuery(new Term("docId", docId.Trim()));
                    luceneQuery = new BooleanQuery
                    {
                        { luceneQuery, Occur.MUST },
                        { filter, Occur.MUST }
                    };
                }

                var hits = searcher.Search(luceneQuery, topK).ScoreDocs;
                var passages = new List<Passage>(hits.Length);
                foreach (var hit in hits)
                {
                    var doc = searcher.Doc(hit.Doc);
                    var chunkId = doc.Get("chunkId");
                    if (chunkId is null || !_chunks.TryGetValue(chunkId, out var chunk))
                    {
                        continue;
                    }

                    passages.Add(new Passage(
                        chunk.ChunkId,
                        chunk.DocId,
                        chunk.Title,
                        chunk.PageNumber,
                        chunk.Section,
                        chunk.Text,
                        hit.Score));
                }

                return passages;
            }
            finally
            {
                _searcherManager.Release(searcher);
            }
        }
    }

    public bool TryGetChunk(string chunkId, out ChunkRecord? chunk) =>
        _chunks.TryGetValue(chunkId, out chunk);

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _searcherManager?.Dispose();
            _writer.Dispose();
            _analyzer.Dispose();
            _directory.Dispose();
        }
    }

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static Document ToDocument(ChunkRecord chunk)
    {
        var doc = new Document
        {
            new StringField("chunkId", chunk.ChunkId, Field.Store.YES),
            new StringField("docId", chunk.DocId, Field.Store.YES),
            new TextField("title", chunk.Title ?? string.Empty, Field.Store.NO),
            new TextField("text", chunk.Text ?? string.Empty, Field.Store.NO)
        };
        return doc;
    }
}
