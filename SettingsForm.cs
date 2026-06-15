using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using NTwain;
using NTwain.Data;

namespace EMRScan
{
    public class SettingsForm : Form
    {
        public int    SelectedDpi         { get; private set; }
        public bool   SelectedColor       { get; private set; }
        public string SelectedScannerName { get; private set; }
        public string SelectedPaperSize   { get; private set; }

        private ComboBox    _cbDpi, _cbScanner, _cbPaper;
        private RadioButton _rbColor, _rbBw;
        private Label       _lblScannerStatus;

        static readonly Color Navy    = Color.FromArgb(26,  55,  92);
        static readonly Color NavyDk  = Color.FromArgb(17,  36,  61);
        static readonly Color BgPage  = Color.FromArgb(241, 245, 249);
        static readonly Color Border  = Color.FromArgb(214, 219, 230);
        static readonly Color Green   = Color.FromArgb(22,  163,  74);
        static readonly Color GrayBtn = Color.FromArgb(71,  85, 105);

        public SettingsForm(int currentDpi, bool currentColor, string currentScannerName = "", string currentPaperSize = "")
        {
            Text            = "Settings — Scanner";
            Size            = new Size(380, 540);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            BackColor       = Color.White;
            Font            = new Font("Segoe UI", 9f);

            // ── Header ────────────────────────────────────────────
            var header = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Navy };
            var lblTitle = new Label
            {
                Text = "ตั้งค่า Scanner", ForeColor = Color.White,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                AutoSize = true, Location = new Point(14, 10)
            };
            header.Controls.Add(lblTitle);

            // ── Footer (OK / Cancel) — dock ล่างสุด กันปุ่มหลุด ─────
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = BgPage };
            footer.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, 0, footer.Width, 0);

            var btnOk = MakeBtn("ตกลง", Navy, new Point(20, 12), 200);
            btnOk.Click += (s, e) =>
            {
                SelectedDpi         = (int)(_cbDpi.SelectedItem ?? 300);
                SelectedColor       = _rbColor.Checked;
                SelectedScannerName = _cbScanner.SelectedItem?.ToString() ?? "";
                SelectedPaperSize   = _cbPaper.Enabled ? (_cbPaper.SelectedItem?.ToString() ?? "") : "";
                if (SelectedPaperSize == AutoSizeLabel) SelectedPaperSize = "";  // "อัตโนมัติ" = ไม่กำหนด
                DialogResult        = DialogResult.OK;
                Close();
            };

            var btnCancel = MakeBtn("ยกเลิก", GrayBtn, new Point(230, 12), 120);
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            footer.Controls.Add(btnOk);
            footer.Controls.Add(btnCancel);

            // ── Content ───────────────────────────────────────────
            var panel = new Panel
            {
                Dock    = DockStyle.Fill,
                Padding = new Padding(20, 16, 20, 0),
                AutoScroll = true
            };

            int y = 16;

            // Scanner selection
            AddLabel(panel, "Scanner", 0, y);
            y += 22;

            _cbScanner = new ComboBox
            {
                Location      = new Point(20, y),
                Width         = 300,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Segoe UI", 9f)
            };
            // เปลี่ยน scanner แล้ว reload paper size ตามเครื่องที่เลือก
            _cbScanner.SelectedIndexChanged += (s, e) => LoadPaperSizes(currentPaperSize);
            panel.Controls.Add(_cbScanner);
            y += 28;

            _lblScannerStatus = new Label
            {
                Location  = new Point(20, y),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(100, 116, 139)
            };
            panel.Controls.Add(_lblScannerStatus);

            var btnRefresh = new Button
            {
                Text      = "⟳ Refresh",
                Location  = new Point(230, y - 4),
                Size      = new Size(90, 24),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 8f),
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = GrayBtn,
                Cursor    = Cursors.Hand
            };
            btnRefresh.FlatAppearance.BorderColor = Border;
            btnRefresh.Click += (s, e) => LoadScanners(currentScannerName);
            panel.Controls.Add(btnRefresh);
            y += 30;

            // DPI
            AddLabel(panel, "Resolution (DPI)", 0, y);
            y += 22;
            _cbDpi = new ComboBox
            {
                Location      = new Point(20, y),
                Width         = 200,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Segoe UI", 9f)
            };
            _cbDpi.Items.AddRange(new object[] { 150, 200, 300, 400, 600 });
            _cbDpi.SelectedItem = currentDpi;
            if (_cbDpi.SelectedIndex < 0) _cbDpi.SelectedIndex = 2; // default 300
            panel.Controls.Add(_cbDpi);
            y += 36;

            // Paper size
            AddLabel(panel, "ขนาดกระดาษ", 0, y);
            y += 22;
            _cbPaper = new ComboBox
            {
                Location      = new Point(20, y),
                Width         = 200,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Segoe UI", 9f)
            };
            panel.Controls.Add(_cbPaper);
            y += 36;

            // Color mode
            AddLabel(panel, "โหมดสี", 0, y);
            y += 22;
            _rbColor = new RadioButton { Text = "สี (Color)",         Location = new Point(20, y),    AutoSize = true, Checked = currentColor };
            _rbBw    = new RadioButton { Text = "ขาวดำ (Grayscale)", Location = new Point(20, y + 24), AutoSize = true, Checked = !currentColor };
            panel.Controls.Add(_rbColor);
            panel.Controls.Add(_rbBw);
            y += 62;

            // OCR Zone Editor
            var btnZone = MakeBtn("🎯  ปรับ OCR Zone", Color.FromArgb(109, 40, 217), new Point(20, y), 300);
            btnZone.Click += (s, e) => { using var dlg = new OcrZoneEditor(); dlg.ShowDialog(this); };
            panel.Controls.Add(btnZone);
            y += 42;

            // ── Dock order: header บน, footer ล่าง, panel เติมตรงกลาง ──
            Controls.Add(panel);
            Controls.Add(footer);
            Controls.Add(header);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            // Load scanner list on open (จะ trigger LoadPaperSizes ตามมา)
            LoadScanners(currentScannerName);
        }

        // label พิเศษสำหรับ "ไม่กำหนดขนาด" — map กลับเป็น "" ตอนเซฟ
        private const string AutoSizeLabel = "อัตโนมัติ (ตามค่า scanner)";

        private void LoadScanners(string selectName)
        {
            _cbScanner.Items.Clear();
            try
            {
                PlatformInfo.Current.PreferNewDSM = false;
                var appId   = TWIdentity.CreateFromAssembly(DataGroups.Image,
                                  System.Reflection.Assembly.GetExecutingAssembly());
                var session = new TwainSession(appId);
                var rc      = session.Open();
                if (rc == ReturnCode.Success)
                {
                    foreach (var ds in session.GetSources())
                        _cbScanner.Items.Add(ds.Name);
                    session.Close();
                }
            }
            catch { }

            if (_cbScanner.Items.Count == 0)
            {
                _cbScanner.Items.Add("— ไม่พบ Scanner —");
                _cbScanner.SelectedIndex = 0;
                _cbScanner.Enabled       = false;
                _lblScannerStatus.Text      = "ไม่พบ Scanner กรุณาติดตั้ง driver ก่อน";
                _lblScannerStatus.ForeColor = Color.FromArgb(220, 38, 38);
            }
            else
            {
                _cbScanner.Enabled = true;
                // Select saved scanner or first (จะ trigger SelectedIndexChanged -> LoadPaperSizes)
                int idx = _cbScanner.FindStringExact(selectName);
                _cbScanner.SelectedIndex = idx >= 0 ? idx : 0;
                _lblScannerStatus.Text      = $"พบ {_cbScanner.Items.Count} Scanner";
                _lblScannerStatus.ForeColor = Color.FromArgb(22, 163, 74);
            }
        }

        // เติม dropdown ขนาดกระดาษจากสิ่งที่ scanner ที่เลือกรองรับจริง
        private void LoadPaperSizes(string selectSize)
        {
            _cbPaper.Items.Clear();

            // มีตัวเลือก "อัตโนมัติ" เป็นค่าแรกเสมอ (= ไม่กำหนด ใช้ค่า default ของ scanner)
            _cbPaper.Items.Add(AutoSizeLabel);

            if (_cbScanner.Enabled)
            {
                try
                {
                    string scanner = _cbScanner.SelectedItem?.ToString() ?? "";
                    foreach (var sz in ScannerHelper.GetSupportedSizes(scanner))
                        _cbPaper.Items.Add(sz);
                }
                catch { }
            }

            // เลือกค่าที่เคยบันทึก — ถ้าไม่มี/ว่าง ใช้ "อัตโนมัติ"
            int idx = string.IsNullOrEmpty(selectSize) ? -1 : _cbPaper.FindStringExact(selectSize);
            _cbPaper.SelectedIndex = idx >= 0 ? idx : 0;
            _cbPaper.Enabled = true;
        }

        private void AddLabel(Panel p, string text, int x, int y)
        {
            p.Controls.Add(new Label
            {
                Text      = text,
                Location  = new Point(x + 20, y),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(51, 65, 85)
            });
        }

        private Button MakeBtn(string text, Color back, Point loc, int w)
        {
            var b = new Button
            {
                Text      = text, Location = loc, Size = new Size(w, 32),
                BackColor = back, ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }
    }
}
