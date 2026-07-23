using System;
using System.Diagnostics;
using System.IO;

namespace ProductApp.Services;

public static class PdfExportService
{
    private static string? FindBrowser()
    {
        var candidates = new[]
        {
            Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe"),
            Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Microsoft\Edge\Application\msedge.exe"),
            Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe"),
            Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Google\Chrome\Application\chrome.exe"),
            Environment.ExpandEnvironmentVariables(@"%LocalAppData%\Microsoft\Edge\Application\msedge.exe"),
            Environment.ExpandEnvironmentVariables(@"%LocalAppData%\Google\Chrome\Application\chrome.exe"),
            "/usr/bin/chromium-browser",
            "/usr/bin/google-chrome",
            "/usr/bin/microsoft-edge"
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    public static bool ExportHtmlToPdf(string html, string outputPdfPath)
    {
        try
        {
            var tempHtml = Path.Combine(Path.GetTempPath(), $"pdf_export_{Guid.NewGuid():N}.html");
            File.WriteAllText(tempHtml, html);

            var browserPath = FindBrowser();
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
