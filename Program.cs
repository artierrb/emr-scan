using System;
using System.Windows.Forms;

namespace EMRScan
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            AppConfig.Load();

            // Check if launched via emrscan:// protocol with token
            string token = "";
            if (args.Length > 0)
            {
                string arg = args[0];
                // emrscan://launch?token=xxx
                var m = System.Text.RegularExpressions.Regex.Match(arg, @"token=([^&]+)");
                if (m.Success) token = Uri.UnescapeDataString(m.Groups[1].Value);
            }

            if (!string.IsNullOrEmpty(token))
            {
                // Verify token with Spring Boot
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
    }
}
