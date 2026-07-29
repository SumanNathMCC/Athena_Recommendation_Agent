using Athena.Ingestion;
using Athena.Ingestion.Extraction;
using Athena.Ingestion.Fetch;
using Athena.Ingestion.Pipeline;
using Athena.Web.Components;
using Athena.Web.Services;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<DocumentIntelligenceOptions>(
    builder.Configuration.GetSection(DocumentIntelligenceOptions.SectionName));
builder.Services.AddAthenaIngestion(builder.Configuration);
builder.Services.AddSingleton<IDocumentMarkdownExtractor>(sp =>
    new DocumentIntelligenceMarkdownExtractor(
        sp.GetRequiredService<IOptions<DocumentIntelligenceOptions>>().Value));

builder.Services.AddSingleton<ICorpusManifestReader, CorpusManifestReader>();
builder.Services.AddHttpClient<ICorpusFetcher, CorpusFetcher>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Athena-CorpusFetcher/1.0 (educational assignment)");
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/pdf"));
});
builder.Services.AddSingleton<ICorpusExtractor, CorpusExtractor>();
builder.Services.AddSingleton<ICorpusInjector, CorpusInjector>();
builder.Services.AddScoped<ICorpusPipelineService>(sp =>
{
    var environment = sp.GetRequiredService<IWebHostEnvironment>();
    var repoRoot = RepoRootLocator.Find(environment.ContentRootPath);
    return new CorpusPipelineService(
        sp.GetRequiredService<ICorpusManifestReader>(),
        sp.GetRequiredService<ICorpusFetcher>(),
        sp.GetRequiredService<ICorpusExtractor>(),
        sp.GetRequiredService<ICorpusInjector>(),
        repoRoot);
});
builder.Services.AddScoped<ICorpusSetupService, CorpusSetupService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
