using Athena.Ingestion.Extraction;
using Athena.Ingestion.Fetch;
using Athena.Web.Components;
using Athena.Web.Services;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<DocumentIntelligenceOptions>(
    builder.Configuration.GetSection(DocumentIntelligenceOptions.SectionName));
builder.Services.AddSingleton<IPdfTextExtractor>(sp =>
    new DocumentIntelligenceTextExtractor(
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
builder.Services.AddScoped<ICorpusFetchService, CorpusFetchService>();

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
