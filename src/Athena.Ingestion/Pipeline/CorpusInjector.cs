using System.Text.Json;

using Athena.Core.Corpus;

using Athena.Core.Records;

using Athena.Ingestion.Extraction;

using Athena.Ingestion.Fetch;

using Athena.Ingestion.Injection;



namespace Athena.Ingestion.Pipeline;



public interface ICorpusInjector

{

    Task<CorpusPipelineOperationResult> InjectAsync(

        string repoRoot,

        bool force = false,

        CancellationToken ct = default);

}



public sealed class CorpusInjector : ICorpusInjector

{

    private static readonly JsonSerializerOptions JsonOptions = new()

    {

        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

        WriteIndented = true

    };



    private readonly ICorpusManifestReader _manifestReader;



    public CorpusInjector(ICorpusManifestReader manifestReader)

    {

        _manifestReader = manifestReader;

    }



    public async Task<CorpusPipelineOperationResult> InjectAsync(

        string repoRoot,

        bool force = false,

        CancellationToken ct = default)

    {

        var manifest = await _manifestReader.ReadAsync(repoRoot, ct);

        CorpusPaths.EnsureArtifactDirectories(repoRoot);



        var documents = CorpusDocumentCatalog.GetAllDocuments(manifest);

        var statuses = new List<CorpusDocPipelineStatus>();



        foreach (var entry in documents)

        {

            ct.ThrowIfCancellationRequested();



            var pdfPath = CorpusPaths.GetPdfPath(repoRoot, manifest.DownloadDirectory, entry.LocalFile);

            var extractedPath = CorpusPaths.GetExtractedPath(repoRoot, entry.DocId);

            var ingestedPath = CorpusPaths.GetIngestedPath(repoRoot, entry.DocId);



            var downloadStatus = CorpusStageInspector.GetDownloadStatus(pdfPath);

            var extractStatus = CorpusStageInspector.GetExtractStatus(extractedPath);



            if (extractStatus != PipelineStageStatus.Succeeded)

            {

                statuses.Add(new CorpusDocPipelineStatus(

                    entry.DocId,

                    entry.Title,

                    entry.LocalFile,

                    downloadStatus,

                    extractStatus,

                    PipelineStageStatus.NotStarted,

                    ExtractMessage: extractStatus == PipelineStageStatus.NotStarted

                        ? "No extraction artifact found. Run extract first."

                        : null));

                continue;

            }



            if (!force && File.Exists(ingestedPath))

            {

                statuses.Add(new CorpusDocPipelineStatus(

                    entry.DocId,

                    entry.Title,

                    entry.LocalFile,

                    downloadStatus,

                    extractStatus,

                    PipelineStageStatus.Skipped,

                    InjectMessage: "Injection artifact already exists."));

                continue;

            }



            try

            {

                var markdown = await File.ReadAllTextAsync(extractedPath, ct);

                var extracted = ExtractedMarkdownParser.Parse(markdown);

                var injected = BuildInjectedDocument(extracted);



                await WriteInjectedAsync(ingestedPath, injected, ct);



                statuses.Add(new CorpusDocPipelineStatus(

                    entry.DocId,

                    entry.Title,

                    entry.LocalFile,

                    downloadStatus,

                    extractStatus,

                    PipelineStageStatus.Succeeded,

                    InjectMessage: $"{injected.Chunks.Count} chunk(s) staged."));

            }

            catch (Exception ex)

            {

                statuses.Add(new CorpusDocPipelineStatus(

                    entry.DocId,

                    entry.Title,

                    entry.LocalFile,

                    downloadStatus,

                    extractStatus,

                    PipelineStageStatus.Failed,

                    InjectMessage: ex.Message));

            }

        }



        return CorpusStageInspector.ToOperationResult(statuses, stage => stage.Inject);

    }



    private static InjectedDocument BuildInjectedDocument(ExtractedMarkdownDocument extracted)

    {

        var chunks = new List<InjectedChunk>();



        for (var index = 0; index < extracted.Blocks.Count; index++)

        {

            var block = extracted.Blocks[index];

            if (string.IsNullOrWhiteSpace(block.Markdown))

            {

                continue;

            }



            var kindPrefix = block.Kind switch

            {

                ChunkKind.Table => "t",

                ChunkKind.OcrProse => "o",

                _ => "p"

            };



            chunks.Add(new InjectedChunk

            {

                ChunkId = $"{extracted.DocId}-{kindPrefix}{block.PageNumber:D4}-{index:D4}",

                Text = block.Markdown,

                PageNumber = block.PageNumber,

                Kind = block.Kind

            });

        }



        return new InjectedDocument

        {

            DocId = extracted.DocId,

            Title = extracted.Title,

            InjectedAt = DateTimeOffset.UtcNow,

            Chunks = chunks

        };

    }



    private static async Task WriteInjectedAsync(

        string path,

        InjectedDocument document,

        CancellationToken ct)

    {

        await using var stream = File.Create(path);

        await JsonSerializer.SerializeAsync(stream, document, JsonOptions, ct);

    }

}


