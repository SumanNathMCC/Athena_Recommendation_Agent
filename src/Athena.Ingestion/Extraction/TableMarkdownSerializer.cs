using System.Text;
using Azure.AI.DocumentIntelligence;

namespace Athena.Ingestion.Extraction;

internal static class TableMarkdownSerializer
{
    public static string ToMarkdown(DocumentTable table)
    {
        if (table.RowCount == 0 || table.ColumnCount == 0)
        {
            return string.Empty;
        }

        var grid = new string[table.RowCount, table.ColumnCount];

        foreach (var cell in table.Cells)
        {
            var row = cell.RowIndex;
            var column = cell.ColumnIndex;
            var content = cell.Content?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(grid[row, column]))
            {
                grid[row, column] = content;
            }
            else if (!string.IsNullOrEmpty(content))
            {
                grid[row, column] = $"{grid[row, column]} {content}".Trim();
            }

            var rowSpan = cell.RowSpan ?? 1;
            var columnSpan = cell.ColumnSpan ?? 1;

            for (var rowOffset = 0; rowOffset < rowSpan; rowOffset++)
            {
                for (var columnOffset = 0; columnOffset < columnSpan; columnOffset++)
                {
                    if (rowOffset == 0 && columnOffset == 0)
                    {
                        continue;
                    }

                    var targetRow = row + rowOffset;
                    var targetColumn = column + columnOffset;

                    if (targetRow < table.RowCount && targetColumn < table.ColumnCount &&
                        string.IsNullOrEmpty(grid[targetRow, targetColumn]))
                    {
                        grid[targetRow, targetColumn] = content;
                    }
                }
            }
        }

        var builder = new StringBuilder();

        for (var column = 0; column < table.ColumnCount; column++)
        {
            builder.Append('|').Append(Escape(grid[0, column])).Append(' ');
        }

        builder.AppendLine("|");

        for (var column = 0; column < table.ColumnCount; column++)
        {
            builder.Append("|---");
        }

        builder.AppendLine("|");

        for (var row = 1; row < table.RowCount; row++)
        {
            for (var column = 0; column < table.ColumnCount; column++)
            {
                builder.Append('|').Append(Escape(grid[row, column])).Append(' ');
            }

            builder.AppendLine("|");
        }

        return builder.ToString().TrimEnd();
    }

    private static string Escape(string? value) =>
        (value ?? string.Empty).Replace("|", "\\|", StringComparison.Ordinal);
}
