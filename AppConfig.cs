using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace EMRScan
{
    public static class AppConfig
    {
        public static string DbServer   { get; private set; }
        public static string DbName     { get; private set; }
        public static string DbUser     { get; private set; }
        public static string DbPass     { get; private set; }
        public static string ApiBaseUrl { get; private set; }

        // OCR crop zone ratios (relative to image size)
        public static float OcrCropX { get; private set; } = 0.65f;
        public static float OcrCropY { get; private set; } = 0.01f;
        public static float OcrCropW { get; private set; } = 0.35f;
        public static float OcrCropH { get; private set; } = 0.06f;

        // Scanner settings
        public static int    ScanDpi     { get; set; } = 300;
        public static bool   ScanColor   { get; set; } = true;
        public static string ScannerName { get; set; } = "";
        public static string PaperSize   { get; set; } = "";   // "" = ใช้ค่า default ของ scanner

        public static string ConnStr =>
            $"Server={DbServer};Database={DbName};User Id={DbUser};Password={DbPass};" +
            "TrustServerCertificate=true;Encrypt=false;";

        public static void Load()
        {
            string cfgPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "application.properties");

            if (!File.Exists(cfgPath))
                throw new FileNotFoundException(
                    $"ไม่พบไฟล์ application.properties\nกรุณาสร้างไฟล์ที่:\n{cfgPath}");

            var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadAllLines(cfgPath))
            {
                var t = line.Trim();
                if (t.StartsWith("#") || !t.Contains("=")) continue;
                int idx = t.IndexOf('=');
                props[t.Substring(0, idx).Trim()] = t.Substring(idx + 1).Trim();
            }

            // DB — required
            if (props.TryGetValue("DB_SERVER", out var sv)) DbServer = sv;
            else if (props.TryGetValue("spring.datasource.url", out var url) && !url.Contains("${"))
            {
                var hm = Regex.Match(url, @"sqlserver://([^;]+)");
                if (hm.Success) DbServer = hm.Groups[1].Value;
                var dm = Regex.Match(url, @"databaseName=([^;]+)");
                if (dm.Success) DbName = dm.Groups[1].Value;
            }
            if (props.TryGetValue("DB_NAME", out var dn)) DbName = dn;
            if (props.TryGetValue("DB_USER", out var du) ||
                props.TryGetValue("spring.datasource.username", out du)) DbUser = du;
            if (props.TryGetValue("DB_PASS", out var dp) ||
                props.TryGetValue("spring.datasource.password", out dp)) DbPass = dp;

            // Validate required DB fields
            if (string.IsNullOrEmpty(DbServer)) throw new Exception("application.properties: DB_SERVER ไม่ได้ตั้งค่า");
            if (string.IsNullOrEmpty(DbName))   throw new Exception("application.properties: DB_NAME ไม่ได้ตั้งค่า");
            if (string.IsNullOrEmpty(DbUser))   throw new Exception("application.properties: DB_USER ไม่ได้ตั้งค่า");
            if (string.IsNullOrEmpty(DbPass))   throw new Exception("application.properties: DB_PASS ไม่ได้ตั้งค่า");

            // API
            if (props.TryGetValue("EMR_API_URL", out var api)) ApiBaseUrl = api;
            if (string.IsNullOrEmpty(ApiBaseUrl)) throw new Exception("application.properties: EMR_API_URL ไม่ได้ตั้งค่า");

            // OCR crop zone (optional — uses defaults if not set)
            if (props.TryGetValue("OCR_CROP_X", out var cx) && float.TryParse(cx, out var fx)) OcrCropX = fx;
            if (props.TryGetValue("OCR_CROP_Y", out var cy) && float.TryParse(cy, out var fy)) OcrCropY = fy;
            if (props.TryGetValue("OCR_CROP_W", out var cw) && float.TryParse(cw, out var fw)) OcrCropW = fw;
            if (props.TryGetValue("OCR_CROP_H", out var ch) && float.TryParse(ch, out var fh)) OcrCropH = fh;

            // Scanner settings (optional — uses defaults if not set)
            if (props.TryGetValue("SCAN_DPI", out var sdpi) && int.TryParse(sdpi, out var idpi)) ScanDpi = idpi;
            if (props.TryGetValue("SCAN_COLOR", out var scol) && bool.TryParse(scol, out var bcol)) ScanColor = bcol;
            if (props.TryGetValue("SCANNER_NAME", out var snam)) ScannerName = snam;
            if (props.TryGetValue("PAPER_SIZE", out var psz)) PaperSize = psz;
        }

        public static void SaveOcrZone(float x, float y, float w, float h)
        {
            OcrCropX = x; OcrCropY = y; OcrCropW = w; OcrCropH = h;

            WriteKeys(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["OCR_CROP_X"] = x.ToString("F4"),
                ["OCR_CROP_Y"] = y.ToString("F4"),
                ["OCR_CROP_W"] = w.ToString("F4"),
                ["OCR_CROP_H"] = h.ToString("F4"),
            });
        }

        public static void SaveScannerSettings(int dpi, bool color, string scannerName, string paperSize)
        {
            ScanDpi = dpi; ScanColor = color; ScannerName = scannerName; PaperSize = paperSize;

            WriteKeys(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["SCAN_DPI"]     = dpi.ToString(),
                ["SCAN_COLOR"]   = color.ToString().ToLower(),
                ["SCANNER_NAME"] = scannerName ?? "",
                ["PAPER_SIZE"]   = paperSize ?? "",
            });
        }

        // เขียน key/value กลับไฟล์ application.properties — update ของเดิม หรือ append ถ้ายังไม่มี
        private static void WriteKeys(Dictionary<string, string> updates)
        {
            string cfgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "application.properties");

            var lines = File.Exists(cfgPath)
                ? new List<string>(File.ReadAllLines(cfgPath))
                : new List<string>();

            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < lines.Count; i++)
            {
                var t = lines[i].Trim();
                if (t.StartsWith("#") || !t.Contains("=")) continue;
                string key = t.Substring(0, t.IndexOf('=')).Trim();
                if (updates.TryGetValue(key, out var val))
                {
                    lines[i] = $"{key}={val}";
                    written.Add(key);
                }
            }

            foreach (var kv in updates)
                if (!written.Contains(kv.Key))
                    lines.Add($"{kv.Key}={kv.Value}");

            File.WriteAllLines(cfgPath, lines);
        }
    }
}
