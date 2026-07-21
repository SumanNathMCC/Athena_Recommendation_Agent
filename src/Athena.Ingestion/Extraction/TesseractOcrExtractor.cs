using Docnet.Core;
using Docnet.Core.Models;
using System.Drawing;
using Tesseract;

namespace Athena.Ingestion.Extraction;

public sealed class TesseractOcrExtractor : IPdfTextExtractor, IDisposable
{
    private const int RasterDpi = 200;

    private readonly TesseractEngine _engine;

    public TesseractOcrExtractor(string? tessdataPath = null)
    {
        var resolvedPath = tessdataPath ?? Path.Combine(AppContext.BaseDirectory, "Tessdata");

        if (!Directory.Exists(resolvedPath))
        {
            throw new DirectoryNotFoundException(
                $"Tessdata folder not found at '{resolvedPath}'. " +
                "Ensure eng.traineddata is copied to the output directory (CopyToOutputDirectory in the .csproj).");
        }

        _engine = new TesseractEngine(resolvedPath, "eng", EngineMode.Default);
    }

    public Task<IReadOnlyList<PageText>> ExtractAsync(string pdfPath, CancellationToken ct = default)
    {
        return Task.FromResult(ExtractCore(pdfPath, ct));
    }

    private IReadOnlyList<PageText> ExtractCore(string pdfPath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfPath);

        var pages = new List<PageText>();

        using var library = DocLib.Instance;
        using var docReader = library.GetDocReader(pdfPath, new PageDimensions(RasterDpi));

        var pageCount = docReader.GetPageCount();

        for (var i = 0; i < pageCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            using var pageReader = docReader.GetPageReader(i);
            var rawBgra = pageReader.GetImage();
            var width = pageReader.GetPageWidth();
            var height = pageReader.GetPageHeight();

            using var pix = LoadPix(rawBgra, width, height);
            using var ocrPage = _engine.Process(pix);

            var text = ocrPage.GetText()?.Trim() ?? string.Empty;
            var confidence = ocrPage.GetMeanConfidence();

            // Docnet.Core is 0-indexed;
            pages.Add(new PageText(i + 1, text, confidence));
        }

        return pages;
    }

    /// <summary>
    /// Converts Docnet.Core's raw BGRA byte buffer into a Tesseract-consumable
    /// Pix by round-tripping through a PNG-encoded in-memory bitmap.
    /// </summary>
    private static Pix LoadPix(byte[] bgraBytes, int width, int height)
    {
        using var bitmap = new Bitmap(
            width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        var bounds = new Rectangle(0, 0, width, height);
        var bitmapData = bitmap.LockBits(
            bounds, System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);

        System.Runtime.InteropServices.Marshal.Copy(bgraBytes, 0, bitmapData.Scan0, bgraBytes.Length);
        bitmap.UnlockBits(bitmapData);

        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);

        return Pix.LoadFromMemory(stream.ToArray());
    }

    public void Dispose() => _engine.Dispose();
}