using UglyToad.PdfPig;

namespace RapidoWorkingOrders.Services;

public static class PdfReaderService
{
    private const double PtToM = 0.0254 / 72.0; // 1 pt u metrima

    /// <summary>
    /// Čita PDF i vraća (broj stranica, ukupni m² svih stranica).
    /// Svaka stranica se mjeri zasebno i zbroji u ukupni m².
    /// Ako čitanje ne uspije, vraća (0, 0).
    /// </summary>
    public static (int PageCount, decimal M2) ReadPdf(string filePath)
    {
        try
        {
            using var doc = PdfDocument.Open(filePath);
            int count = doc.NumberOfPages;
            if (count == 0) return (0, 0m);

            decimal totalM2 = 0m;
            for (int i = 1; i <= count; i++)
            {
                var page = doc.GetPage(i);
                double w = page.Width  * PtToM;
                double h = page.Height * PtToM;
                totalM2 += (decimal)(w * h);
            }
            return (count, Math.Round(totalM2, 4));
        }
        catch
        {
            return (0, 0m);
        }
    }
}
