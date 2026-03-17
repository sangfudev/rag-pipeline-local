using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace LocalRagSK;

/// <summary>
/// Extracts plain text from a PDF file page by page using PdfPig.
/// Works with text-based PDFs. For scanned/image PDFs, OCR is needed separately.
/// </summary>
public static class PdfExtractor
{
    public static string ExtractText(string pdfPath)
    {
        if (!File.Exists(pdfPath))
            throw new FileNotFoundException($"PDF not found: {pdfPath}");

        Console.WriteLine($"  Extracting text from: {Path.GetFileName(pdfPath)}");

        using var document = PdfDocument.Open(pdfPath);
        var sb = new System.Text.StringBuilder();
        int pageCount = 0;

        foreach (Page page in document.GetPages())
        {
            sb.AppendLine(page.Text);
            sb.AppendLine(); // blank line between pages
            pageCount++;
        }

        Console.WriteLine($"  Extracted {pageCount} pages.");
        return sb.ToString();
    }
}
