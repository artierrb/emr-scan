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
        public static string ReadOcrPk(string imagePath, float cropXRatio = -1f,
                                                          float cropYRatio = -1f,
                                                          float cropWRatio = -1f,
                                                          float cropHRatio = -1f)
        {
            // Use AppConfig values when not explicitly provided
            if (cropXRatio < 0) cropXRatio = AppConfig.OcrCropX;
            if (cropYRatio < 0) cropYRatio = AppConfig.OcrCropY;
            if (cropWRatio < 0) cropWRatio = AppConfig.OcrCropW;
            if (cropHRatio < 0) cropHRatio = AppConfig.OcrCropH;
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

                    // Tesseract (--psm 7 digits) reads + as 4
                    // Strict: [+4] ล้อมรอบ 13 หลัก
                    var m = Regex.Match(text, @"[+4]\s*(\d{13})\s*[+4]");
                    if (m.Success) return m.Groups[1].Value;

                    // Fallback: 14 หลักติดกัน ตัดหัวท้ายออก
                    m = Regex.Match(text, @"[+4](\d{13})[+4]?");
                    if (m.Success) return m.Groups[1].Value;

                    // Fallback สุดท้าย: 13 หลักล้วน
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