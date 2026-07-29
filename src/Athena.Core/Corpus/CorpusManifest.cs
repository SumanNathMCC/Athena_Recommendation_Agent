namespace Athena.Core.Corpus;

public sealed class CorpusManifest
{
    public int Version { get; set; }
    public string Description { get; set; } = string.Empty;
    public string DefaultRetrievalDate { get; set; } = string.Empty;
    public string DownloadDirectory { get; set; } = "corpus/pdfs";
    public List<CorpusDocument> Documents { get; set; } = [];
    public Dictionary<string, LineageGroupInfo> LineageGroups { get; set; } = [];
    public Dictionary<string, string> Clusters { get; set; } = [];
}

public sealed class CorpusDocument
{
    public string DocId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Cluster { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string LocalFile { get; set; } = string.Empty;
    public int PageCountApprox { get; set; }
    public DateOnly PublishedOn { get; set; }
    public string? LineageGroup { get; set; }
    public string? LineageRole { get; set; }
    public string Licence { get; set; } = string.Empty;
    public bool Fetch { get; set; } = true;
    public string? ClusterTopic { get; set; }
}

public sealed class LineageGroupInfo
{
    public string Description { get; set; } = string.Empty;
    public List<string> Members { get; set; } = [];
}
