using System;
using System.IO;
using System.Windows.Forms;

namespace EMRScan
{
    static class Program
    {
        static readonly string LogPath = @"C:\HNT.RDB\emrscan_crash.log";

        [STAThread]
        static void Main(string[] args)
        {
            Application.ThreadException += (s, e) => LogCrash(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => LogCrash(e.ExceptionObject as Exception);

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                AppConfig.Load();

                // Check if launched via emrscan:// protocol with token
                string token = "";
                if (args.Length > 0)
                {
                    string arg = args[0];
                    var m = System.Text.RegularExpressions.Regex.Match(arg, @"token=([^&]+)");
                    if (m.Success) token = Uri.UnescapeDataString(m.Groups[1].Value);
                }

                if (!string.IsNullOrEmpty(token))
                {
                    var info = ApiHelper.VerifyToken(token).GetAwaiter().GetResult();
                    if (info != null)
                    {
                        Application.Run(new MainForm(info.Value.userId, info.Value.name));
                        return;
                    }
                    MessageBox.Show("Token หมดอายุหรือไม่ถูกต้อง กรุณา login ใหม่",
                        "EMR Scan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                // Normal login
                using var login = new LoginForm();
                if (login.ShowDialog() == DialogResult.OK)
                    Application.Run(new MainForm(login.LoggedInUserId, login.LoggedInName));
            }
            catch (Exception ex)
            {
                LogCrash(ex);
            }
        }

        static void LogCrash(Exception ex)
        {
            try
            {
                string msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex?.GetType().Name}: {ex?.Message}\r\n{ex?.StackTrace}\r\n\r\n";
                File.AppendAllText(LogPath, msg);
                MessageBox.Show($"เกิดข้อผิดพลาด:\n{ex?.Message}\n\nดู log ที่: {LogPath}",
                    "EMR Scan Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
    }
}