using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;

namespace EMRScan
{
    public static class OcrHelper
    {
        // Tesseract path from application.properties or default
        public static string TesseractPath { get; set; } = @"C:\HNT.RDB\OCR\tesseract.exe";
        public static string TessData      { get; set; } = @"C:\HNT.RDB\OCR\tessdata";

        // Crop top-right corner (default 20% width, 8% height) then OCR
        public static string ReadOcrPk(string imagePath, float cropXRatio = 0.72f,
                                                          float cropYRatio = 0.0f,
                                                          float cropWRatio = 0.28f,
                                                          float cropHRatio = 0.08f)
        {
            try
            {
                string cropPath = Path.Combine(Path.GetTempPath(),
                    $"emrscan_crop_{Guid.NewGuid():N}.jpg");
                try
                {
                    // Crop image
                    using (var bmp = new Bitmap(imagePath))
                    {
                        int x = (int)(bmp.Width  * cropXRatio);
                        int y = (int)(bmp.Height * cropYRatio);
                        int w = (int)(bmp.Width  * cropWRatio);
                        int h = (int)(bmp.Height * cropHRatio);
                        var rect = new Rectangle(x, y, Math.Min(w, bmp.Width - x),
                                                        Math.Min(h, bmp.Height - y));
                        using var cropped = bmp.Clone(rect, bmp.PixelFormat);
                        cropped.Save(cropPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                    }

                    // Run Tesseract
                    string outBase = Path.Combine(Path.GetTempPath(),
                        $"emrscan_ocr_{Guid.NewGuid():N}");
                    var psi = new ProcessStartInfo(TesseractPath)
                    {
                        Arguments = $"\"{cropPath}\" \"{outBase}\" --tessdata-dir \"{TessData}\" -l eng --psm 7 digits",
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        UseShellExecute        = false,
                        CreateNoWindow         = true
                    };
                    using var proc = Process.Start(psi);
                    proc.WaitForExit(10000);

                    string txtPath = outBase + ".txt";
                    if (!File.Exists(txtPath)) return "";
                    string text = File.ReadAllText(txtPath).Trim();
                    File.Delete(txtPath);

                    // Log raw OCR output for debugging
                    System.Diagnostics.Debug.WriteLine($"[OCR] raw: {text}");

                    // Try strict pattern first: +\d{13}+
                    var m = Regex.Match(text, @"\+(\d{13})\+");
                    if (m.Success) return m.Groups[1].Value;

                    // Fallback: any 13-digit sequence
                    m = Regex.Match(text, @"(\d{13})");
                    if (m.Success) return m.Groups[1].Value;

                    return "";
                }
                finally
                {
                    if (File.Exists(cropPath)) File.Delete(cropPath);
                }
            }
            catch { return ""; }
        }
    }
}
