using System;
using System.Drawing;
using System.Windows.Forms;

namespace EMRScan
{
    public class LoginForm : Form
    {
        public string LoggedInUserId { get; private set; }
        public string LoggedInName   { get; private set; }
        public string LoggedInAuth   { get; private set; }

        private TextBox _txtUser, _txtPass;
        private Button  _btnLogin;
        private Label   _lblError;

        public LoginForm(string prefilledUserId = "")
        {
            Text            = "EMR Scan — เข้าสู่ระบบ";
            Size            = new Size(360, 260);
            StartPosition   = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            BackColor       = Color.White;

            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(30, 20, 30, 20) };
            Controls.Add(panel);

            var lblTitle = new Label
            {
                Text      = "EMR Document System",
                Font      = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = Color.FromArgb(26, 79, 122),
                AutoSize  = true,
                Location  = new Point(0, 10)
            };
            panel.Controls.Add(lblTitle);

            var lblSub = new Label
            {
                Text      = "Scanner Module",
                Font      = new Font("Segoe UI", 9),
                ForeColor = Color.Gray,
                AutoSize  = true,
                Location  = new Point(0, 38)
            };
            panel.Controls.Add(lblSub);

            var lblUser = new Label { Text = "รหัสผู้ใช้", Location = new Point(0, 70), AutoSize = true };
            panel.Controls.Add(lblUser);

            _txtUser = new TextBox
            {
                Location  = new Point(0, 88),
                Width     = 280,
                Text      = prefilledUserId,
                CharacterCasing = CharacterCasing.Upper
            };
            panel.Controls.Add(_txtUser);

            var lblPass = new Label { Text = "รหัสผ่าน", Location = new Point(0, 116), AutoSize = true };
            panel.Controls.Add(lblPass);

            _txtPass = new TextBox
            {
                Location     = new Point(0, 134),
                Width        = 280,
                PasswordChar = '●'
            };
            panel.Controls.Add(_txtPass);

            _lblError = new Label
            {
                Text      = "",
                ForeColor = Color.Red,
                Location  = new Point(0, 162),
                Size      = new Size(280, 20),
                Font      = new Font("Segoe UI", 8.5f)
            };
            panel.Controls.Add(_lblError);

            _btnLogin = new Button
            {
                Text      = "เข้าสู่ระบบ",
                Location  = new Point(0, 182),
                Size      = new Size(280, 34),
                BackColor = Color.FromArgb(26, 79, 122),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            _btnLogin.FlatAppearance.BorderSize = 0;
            _btnLogin.Click += BtnLogin_Click;
            panel.Controls.Add(_btnLogin);

            AcceptButton = _btnLogin;
            ActiveControl = string.IsNullOrEmpty(prefilledUserId) ? (Control)_txtUser : _txtPass;
        }

        private void BtnLogin_Click(object sender, EventArgs e)
        {
            _lblError.Text  = "";
            _btnLogin.Enabled = false;
            _btnLogin.Text  = "กำลังตรวจสอบ...";

            string uid  = _txtUser.Text.Trim().ToUpper();
            string pass = _txtPass.Text;

            if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(pass))
            {
                _lblError.Text    = "กรุณากรอกรหัสผู้ใช้และรหัสผ่าน";
                _btnLogin.Enabled = true;
                _btnLogin.Text    = "เข้าสู่ระบบ";
                return;
            }

            try
            {
                var result = DbHelper.Login(uid, pass);
                if (result == null)
                {
                    _lblError.Text    = "รหัสผู้ใช้หรือรหัสผ่านไม่ถูกต้อง";
                    _btnLogin.Enabled = true;
                    _btnLogin.Text    = "เข้าสู่ระบบ";
                    _txtPass.Clear();
                    _txtPass.Focus();
                    return;
                }
                LoggedInUserId = result.Value.userId;
                LoggedInName   = result.Value.name;
                LoggedInAuth   = result.Value.auth;
                DialogResult   = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                _lblError.Text    = $"เชื่อมต่อ DB ไม่ได้: {ex.Message}";
                _btnLogin.Enabled = true;
                _btnLogin.Text    = "เข้าสู่ระบบ";
            }
        }
    }
}
