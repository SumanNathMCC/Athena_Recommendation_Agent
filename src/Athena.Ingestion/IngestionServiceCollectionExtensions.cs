using Athena.Ingestion.Chunking;
using Athena.Ingestion.DocVectors;
using Athena.Ingestion.Embeddings;
using Athena.Ingestion.Orchestration;
using Athena.Ingestion.Summarization;
using Athena.Ingestion.VectorStore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.InMemory;
using SkKernel = Microsoft.SemanticKernel.Kernel;

namespace Athena.Ingestion;

public static class IngestionServiceCollectionExtensions
{
    public static IServiceCollection AddAthenaIngestion(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AzureFoundryOptions>()
            .Bind(configuration.GetSection(AzureFoundryOptions.SectionName));
        services.AddOptions<ChunkingOptions>()
            .Bind(configuration.GetSection(ChunkingOptions.SectionName));
        services.AddOptions<DocumentVectorOptions>()
            .Bind(configuration.GetSection(DocumentVectorOptions.SectionName));
        services.AddAthenaChunking();

        services.AddInMemoryVectorStore();
        services.AddInMemoryVectorStoreRecordCollection<string, Core.Records.ChunkRecord>(
            CorpusVectorStoreConstants.ChunksCollection);
        services.AddInMemoryVectorStoreRecordCollection<string, Core.Records.DocRecord>(
            CorpusVectorStoreConstants.DocumentsCollection);

        services.AddSingleton<SkKernel>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AzureFoundryOptions>>().Value;
            return AthenaKernelFactory.Build(options);
        });

        services.AddSingleton<IDocumentSummariser, KernelDocumentSummariser>();
        services.AddSingleton<ICorpusEmbeddingService, SkCorpusEmbeddingService>();
        services.AddSingleton<SummaryStrategy>();
        services.AddSingleton<CentroidStrategy>();
        services.AddSingleton<CompositeStrategy>();
        services.AddSingleton<IDocumentVectorStrategyFactory, DocumentVectorStrategyFactory>();
        services.AddSingleton<CorpusVectorStore>();
        services.AddSingleton<ICorpusVectorIndexer, CorpusVectorIndexer>();
        services.AddSingleton<IngestionPipeline>();

        return services;
    }
}
