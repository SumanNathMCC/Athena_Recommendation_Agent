using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Athena.Core.Corpus;

public sealed record FetchItemResult(
    string DocId,
    string LocalFile,
    FetchItemStatus Status,
    string? Message = null);

public enum FetchItemStatus
{
    Downloaded,
    Skipped,
    Failed
}

public sealed record FetchResult(
    int Downloaded,
    int Skipped,
    int Failed,
    IReadOnlyList<FetchItemResult> Items)
{
    public bool IsSuccess => Failed == 0;
}
