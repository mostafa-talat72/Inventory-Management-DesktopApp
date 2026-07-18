using System;
using System.Diagnostics;
using System.IO;

namespace ProductApp.Services;

public static class PdfExportService
{
    public static bool ExportHtmlToPdf(string html, string outputPdfPath)
    {
        try
        {
            var tempHtml = Path.Combine(Path.GetTempPath(), $"pdf_export_{Guid.NewGuid():N}.html");
            File.WriteAllText(tempHtml, html);

            var edge = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
            var chrome = @"C:\Program Files\Google\Chrome\Application\chrome.exe";

            string? browserPath = null;
            if (File.Exists(edge)) browserPath = edge;
            else if (File.Exists(chrome)) browserPath = chrome;

            if (browserPath == null)
                return false;

            var psi = new ProcessStartInfo(browserPath)
            {
                Arguments = $"--headless --print-to-pdf=\"{outputPdfPath}\" \"{tempHtml}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process == null) return false;
            process.WaitForExit(10000);

            try { File.Delete(tempHtml); } catch { }
            return File.Exists(outputPdfPath);
        }
        catch
        {
            return false;
        }
    }
}
