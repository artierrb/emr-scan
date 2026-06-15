using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using NTwain;
using NTwain.Data;

namespace EMRScan
{
    public static class ScannerHelper
    {
        public static event Action<string> StatusChanged;

        /// <summary>
        /// Scan all pages from ADF — must be called on UI thread (STA).
        /// Uses NTwain internal message loop (no custom hook needed).
        /// Returns list of temp JPG file paths.
        /// </summary>
        public static List<string> ScanAll(int dpi, bool color, string scannerName = "", string paperSize = "")
        {
            var results   = new List<string>();
            var done      = false;
            Exception err = null;

            // Force use of twain_32.dll (32-bit app, older DSM more compatible)
            PlatformInfo.Current.PreferNewDSM = false;

            var appId   = TWIdentity.CreateFromAssembly(DataGroups.Image,
                              System.Reflection.Assembly.GetExecutingAssembly());
            var session = new TwainSession(appId);

            session.TransferReady    += (s, e) => { e.CancelAll = false; };
            session.SourceDisabled   += (s, e) => { done = true; };
            session.TransferError    += (s, e) =>
            {
                err  = new ScannerException($"Transfer error: {e.Exception?.Message}");
                done = true;
            };

            session.DataTransferred += (s, e) =>
            {
                try
                {
                    using var stream = e.GetNativeImageStream();
                    if (stream != null)
                    {
                        using var bmp = new Bitmap(stream);
                        string path = Path.Combine(Path.GetTempPath(),
                            $"emrscan_{Guid.NewGuid():N}.jpg");
                        SaveJpeg(bmp, path, 90);
                        results.Add(path);
                        StatusChanged?.Invoke($"สแกนแล้ว {results.Count} หน้า...");
                    }
                }
                catch (Exception ex) { err = ex; }
            };

            // Open without custom hook — NTwain uses its own internal loop
            var rc = session.Open();
            if (rc != ReturnCode.Success)
                throw new ScannerException($"ไม่สามารถเปิด TWAIN session: {rc}");

            try
            {
                var ds = FindScanner(session, scannerName);
                if (ds == null)
                    throw new ScannerException("ไม่พบ Scanner กรุณาติดตั้ง driver ก่อน");

                rc = ds.Open();
                if (rc != ReturnCode.Success)
                    throw new ScannerException($"ไม่สามารถเปิด Scanner: {rc}");

                try
                {
                    ConfigureScanner(ds, dpi, color, paperSize);

                    rc = ds.Enable(SourceEnableMode.NoUI, false, IntPtr.Zero);
                    if (rc != ReturnCode.Success)
                        throw new ScannerException($"ไม่สามารถเริ่มสแกน: {rc}");

                    // Pump messages until ADF finishes (max 120s)
                    var deadline = DateTime.Now.AddSeconds(120);
                    while (!done && DateTime.Now < deadline)
                    {
                        Application.DoEvents();
                        System.Threading.Thread.Sleep(30);
                    }

                    if (err  != null) throw err;
                    if (!done) throw new ScannerException("Scanner timeout — ไม่ตอบสนองใน 120 วินาที");
                }
                finally
                {
                    try { if (ds.IsOpen) ds.Close(); } catch { }
                }
            }
            finally
            {
                try { session.Close(); } catch { }
            }

            return results;
        }

        /// <summary>
        /// ดึงรายการขนาดกระดาษที่ scanner รองรับ (ICapSupportedSizes)
        /// คืน list ของชื่อขนาด เช่น "A4", "Letter", "Legal" — ใช้เติม dropdown ใน Settings
        /// คืน list ว่างถ้าหา scanner ไม่เจอหรือ query ไม่ได้
        /// </summary>
        public static List<string> GetSupportedSizes(string scannerName = "")
        {
            var sizes = new List<string>();

            PlatformInfo.Current.PreferNewDSM = false;
            var appId   = TWIdentity.CreateFromAssembly(DataGroups.Image,
                              System.Reflection.Assembly.GetExecutingAssembly());
            var session = new TwainSession(appId);

            var rc = session.Open();
            if (rc != ReturnCode.Success)
                return sizes;

            try
            {
                var ds = FindScanner(session, scannerName);
                if (ds == null) return sizes;

                rc = ds.Open();
                if (rc != ReturnCode.Success) return sizes;

                try
                {
                    foreach (var v in ds.Capabilities.ICapSupportedSizes.GetValues())
                    {
                        // v เป็น enum SupportedSize เช่น A4, USLetter, USLegal
                        string label = SizeToLabel(v);
                        if (!string.IsNullOrEmpty(label) && !sizes.Contains(label))
                            sizes.Add(label);
                    }
                }
                catch { }
                finally
                {
                    try { if (ds.IsOpen) ds.Close(); } catch { }
                }
            }
            finally
            {
                try { session.Close(); } catch { }
            }

            return sizes;
        }

        private static DataSource FindScanner(TwainSession session, string preferredName)
        {
            DataSource first = null;
            foreach (var ds in session.GetSources())
            {
                // Exact match from Settings
                if (!string.IsNullOrEmpty(preferredName) &&
                    string.Equals(ds.Name, preferredName, StringComparison.OrdinalIgnoreCase))
                    return ds;
                if (first == null) first = ds;
            }
            return first; // fallback: first available
        }

        private static void ConfigureScanner(DataSource ds, int dpi, bool color, string paperSize)
        {
            // Color mode
            try { ds.Capabilities.ICapPixelType.SetValue(color ? PixelType.RGB : PixelType.Gray); } catch { }

            // DPI — ต้อง set ทั้งแนวนอนและแนวตั้ง ด้วย TWFix32
            // บาง driver ต้องตั้ง ICapUnits เป็น Inches ก่อน ถึงจะตีความ DPI ถูก
            try { ds.Capabilities.ICapUnits.SetValue(Unit.Inches); } catch { }
            try { ds.Capabilities.ICapXResolution.SetValue((TWFix32)dpi); } catch { }
            try { ds.Capabilities.ICapYResolution.SetValue((TWFix32)dpi); } catch { }

            // Paper size — ตั้งจากชื่อที่เลือกใน Settings (ถ้าว่าง = ใช้ค่า default ของ scanner)
            if (!string.IsNullOrEmpty(paperSize))
            {
                var sz = LabelToSize(paperSize);
                if (sz.HasValue)
                    try { ds.Capabilities.ICapSupportedSizes.SetValue(sz.Value); } catch { }
            }

            // ADF
            try { ds.Capabilities.CapFeederEnabled.SetValue(BoolType.True);  } catch { }
            try { ds.Capabilities.CapAutoFeed.SetValue(BoolType.True);       } catch { }
            try { ds.Capabilities.CapDuplexEnabled.SetValue(BoolType.False); } catch { }
        }

        // ── แปลง enum SupportedSize <-> label ที่อ่านง่าย ──────────────────
        // map เฉพาะขนาดที่แน่ใจว่ามีทุกเวอร์ชัน NTwain (A-series, US sizes)
        // ขนาดอื่น (B-series ฯลฯ) ใช้ชื่อ enum ตรง ๆ ผ่าน ToString/TryParse
        // เพื่อกันปัญหาชื่อสมาชิก enum ต่างกันระหว่างเวอร์ชัน
        private static string SizeToLabel(SupportedSize sz) => sz switch
        {
            SupportedSize.A4          => "A4",
            SupportedSize.A5          => "A5",
            SupportedSize.A6          => "A6",
            SupportedSize.A3          => "A3",
            SupportedSize.USLetter    => "Letter",
            SupportedSize.USLegal     => "Legal",
            SupportedSize.USStatement => "Statement",
            SupportedSize.None        => "",          // ข้าม None (= auto/ไม่กำหนด)
            _                         => sz.ToString() // ขนาดอื่นใช้ชื่อ enum ตรง ๆ
        };

        private static SupportedSize? LabelToSize(string label) => label switch
        {
            "A4"        => SupportedSize.A4,
            "A5"        => SupportedSize.A5,
            "A6"        => SupportedSize.A6,
            "A3"        => SupportedSize.A3,
            "Letter"    => SupportedSize.USLetter,
            "Legal"     => SupportedSize.USLegal,
            "Statement" => SupportedSize.USStatement,
            _           => Enum.TryParse<SupportedSize>(label, out var s) ? s : (SupportedSize?)null
        };

        private static void SaveJpeg(Bitmap bmp, string path, long quality)
        {
            foreach (var codec in ImageCodecInfo.GetImageDecoders())
            {
                if (codec.FormatID == ImageFormat.Jpeg.Guid)
                {
                    var ep = new EncoderParameters(1);
                    ep.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                    bmp.Save(path, codec, ep);
                    return;
                }
            }
            bmp.Save(path, ImageFormat.Jpeg);
        }
    }

    public class ScannerException : Exception
    {
        public ScannerException(string message) : base(message) { }
    }
}