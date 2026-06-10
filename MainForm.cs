using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EMRScan
{
    public class MainForm : Form
    {
        private string _userId, _userName;
        private DataGridView  _grid;
        private PictureBox    _preview;
        private Button        _btnScan, _btnConfirm, _btnClear, _btnSettings;
        private Label         _lblUser, _lblStatus;
        private ProgressBar   _progress;
        private List<ScanItem> _items = new List<ScanItem>();
        public int  ScanDpi   { get; set; } = 300;
        public bool ScanColor { get; set; } = true;

        public MainForm(string userId, string userName)
        {
            _userId = userId; _userName = userName;
            Text          = "EMR Scan";
            Size          = new Size(1024, 700);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize   = new Size(800, 600);
            BackColor     = Color.FromArgb(245, 247, 250);
            BuildUI();
        }

        private void BuildUI()
        {
            // WinForms Dock rule: add Fill first, then Bottom, then Top (reverse visual order)

            // ── SplitContainer (Fill) ─────────────────────────────────────
            var split = new SplitContainer
            {
                Dock             = DockStyle.Fill,
                Orientation      = Orientation.Horizontal,
                SplitterDistance = 300,
                Panel1MinSize    = 150,
                Panel2MinSize    = 100,
            };

            _grid = new DataGridView
            {
                Dock                  = DockStyle.Fill,
                BackgroundColor       = Color.White,
                BorderStyle           = BorderStyle.None,
                RowHeadersVisible     = false,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                ReadOnly              = false,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
                Font                  = new Font("Segoe UI", 9),
                GridColor             = Color.FromArgb(229, 231, 235)
            };
            _grid.DefaultCellStyle.SelectionBackColor        = Color.FromArgb(219, 234, 254);
            _grid.DefaultCellStyle.SelectionForeColor        = Color.FromArgb(30, 64, 175);
            _grid.ColumnHeadersDefaultCellStyle.BackColor    = Color.FromArgb(26, 79, 122);
            _grid.ColumnHeadersDefaultCellStyle.ForeColor    = Color.White;
            _grid.ColumnHeadersDefaultCellStyle.Font         = new Font("Segoe UI", 9, FontStyle.Bold);
            _grid.EnableHeadersVisualStyles = false;
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColOcrPk",  HeaderText = "OCR Code",   FillWeight = 20, ReadOnly = false });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColHn",     HeaderText = "HN",         FillWeight = 20, ReadOnly = true  });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColForm",   HeaderText = "FORMCODE",   FillWeight = 25, ReadOnly = true  });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColStatus", HeaderText = "OCR Result", FillWeight = 15, ReadOnly = true  });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColPath",   HeaderText = "ไฟล์",       FillWeight = 20, ReadOnly = true  });
            _grid.SelectionChanged += Grid_SelectionChanged;
            _grid.CellEndEdit      += Grid_CellEndEdit;
            split.Panel1.Controls.Add(_grid);

            var previewPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(30, 30, 30) };
            _preview = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(30, 30, 30) };
            previewPanel.Controls.Add(_preview);
            split.Panel2.Controls.Add(previewPanel);

            // ── Status bar (Bottom) ───────────────────────────────────────
            var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = Color.FromArgb(229, 231, 235) };
            _lblStatus = new Label { Text = "พร้อมใช้งาน", AutoSize = true, Location = new Point(8, 6), Font = new Font("Segoe UI", 8.5f) };
            _progress  = new ProgressBar { Location = new Point(300, 5), Size = new Size(200, 18), Visible = false };
            statusBar.Controls.Add(_lblStatus);
            statusBar.Controls.Add(_progress);

            // ── Button bar (Top) ──────────────────────────────────────────
            var btnBar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.White, Padding = new Padding(8, 6, 8, 6) };
            _btnScan    = MakeButton("Scan",            Color.FromArgb(26, 79, 122),  new Point(0,   0));
            _btnConfirm = MakeButton("Confirm & Save",  Color.FromArgb(34, 197, 94),  new Point(110, 0));
            _btnClear   = MakeButton("Clear",           Color.FromArgb(107, 114, 128),new Point(270, 0));
            _btnSettings= MakeButton("Settings",        Color.FromArgb(107, 114, 128),new Point(380, 0));
            _btnConfirm.Enabled = false;
            _btnScan.Click     += BtnScan_Click;
            _btnConfirm.Click  += BtnConfirm_Click;
            _btnClear.Click    += BtnClear_Click;
            _btnSettings.Click += BtnSettings_Click;
            btnBar.Controls.AddRange(new Control[]{ _btnScan, _btnConfirm, _btnClear, _btnSettings });

            // ── Toolbar (Top) ─────────────────────────────────────────────
            var toolbar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Color.FromArgb(26, 79, 122) };
            var lblTitle = new Label { Text = "EMR Scan", ForeColor = Color.White, Font = new Font("Segoe UI", 12, FontStyle.Bold), AutoSize = true, Location = new Point(12, 12) };
            _lblUser = new Label { Text = $"{_userName} ({_userId})", ForeColor = Color.FromArgb(180, 210, 240), Font = new Font("Segoe UI", 9), AutoSize = true };
            _lblUser.Location = new Point(this.ClientSize.Width - 200, 15);
            _lblUser.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            toolbar.Controls.Add(lblTitle);
            toolbar.Controls.Add(_lblUser);

            // Add in correct Dock order: Fill → Bottom → Top (last Top added = topmost)
            Controls.Add(split);
            Controls.Add(statusBar);
            Controls.Add(btnBar);
            Controls.Add(toolbar);
        }

        private Button MakeButton(string text, Color back, Point loc)
        {
            var b = new Button
            {
                Text = text, Size = new Size(100, 32), Location = loc,
                BackColor = back, ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        private async void BtnScan_Click(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title = "เลือกไฟล์ภาพ", Filter = "Image Files|*.jpg;*.jpeg;*.png;*.tif;*.tiff", Multiselect = true
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            SetStatus($"กำลังประมวลผล {dlg.FileNames.Length} ไฟล์...", true);
            _btnScan.Enabled = false;

            foreach (string path in dlg.FileNames)
            {
                var item = await Task.Run(() =>
                {
                    var i = new ScanItem { ImagePath = path };
                    string ocrPk = OcrHelper.ReadOcrPk(path);
                    i.OcrPk = ocrPk;
                    if (!string.IsNullOrEmpty(ocrPk))
                    {
                        var info = DbHelper.VerifyOcrPk(ocrPk);
                        if (info != null) { i.Hn = info.Value.hn; i.FormCode = info.Value.formCode; i.Status = "Approve"; }
                        else i.Status = "Not Found";
                    }
                    else i.Status = "Not Found";
                    return i;
                });
                _items.Add(item);
                RefreshGrid();
            }

            SetStatus($"พบ {_items.Count} รายการ — Approve {_items.Count(i => i.Status == "Approve")} รายการ");
            _btnScan.Enabled    = true;
            _btnConfirm.Enabled = _items.Any(i => i.Status == "Approve");
        }

        private void RefreshGrid()
        {
            if (InvokeRequired) { Invoke((Action)RefreshGrid); return; }
            _grid.Rows.Clear();
            foreach (var item in _items)
            {
                int idx = _grid.Rows.Add(item.OcrPk, item.Hn, item.FormCode, item.Status, Path.GetFileName(item.ImagePath));
                var row = _grid.Rows[idx];
                row.Tag = item;
                if (item.Status == "Approve")
                { row.DefaultCellStyle.ForeColor = Color.FromArgb(22, 163, 74); row.DefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold); }
                else
                { row.DefaultCellStyle.ForeColor = Color.FromArgb(220, 38, 38); }
            }
        }

        private void Grid_SelectionChanged(object sender, EventArgs e)
        {
            if (_grid.SelectedRows.Count == 0) { _preview.Image = null; return; }
            var item = _grid.SelectedRows[0].Tag as ScanItem;
            if (item == null || !File.Exists(item.ImagePath)) { _preview.Image = null; return; }
            try
            {
                using var fs = new FileStream(item.ImagePath, FileMode.Open, FileAccess.Read);
                _preview.Image = Image.FromStream(fs);
            }
            catch { _preview.Image = null; }
        }

        private async void Grid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex != 0) return;
            var row  = _grid.Rows[e.RowIndex];
            var item = row.Tag as ScanItem;
            if (item == null) return;
            string newOcrPk = row.Cells[0].Value?.ToString()?.Trim() ?? "";
            if (newOcrPk == item.OcrPk) return;
            item.OcrPk = newOcrPk;
            if (!string.IsNullOrEmpty(newOcrPk))
            {
                var info = await Task.Run(() => DbHelper.VerifyOcrPk(newOcrPk));
                if (info != null) { item.Hn = info.Value.hn; item.FormCode = info.Value.formCode; item.Status = "Approve"; }
                else { item.Hn = item.FormCode = ""; item.Status = "Not Found"; }
            }
            else item.Status = "Skipped";
            RefreshGrid();
            _btnConfirm.Enabled = _items.Any(i => i.Status == "Approve");
        }

        private async void BtnConfirm_Click(object sender, EventArgs e)
        {
            var approved = _items.Where(i => i.Status == "Approve").ToList();
            if (approved.Count == 0) return;
            if (MessageBox.Show($"บันทึก {approved.Count} รายการ?", "ยืนยัน",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;

            _btnConfirm.Enabled = false;
            SetStatus("กำลังบันทึก...", true);

            var pathInfo = DbHelper.GetActivePath();
            if (pathInfo == null)
            {
                MessageBox.Show("ไม่พบ PATHT ที่ active", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _btnConfirm.Enabled = true; return;
            }

            int success = 0, fail = 0;
            string lastTreatNo = "", lastFormCode = "";

            foreach (var item in approved)
            {
                try
                {
                    long? pageNo = DbHelper.GetPageNo(item.OcrPk);
                    if (pageNo == null) { fail++; continue; }
                    string folder = Path.Combine(pathInfo.Value.localPath, pageNo.Value.ToString().PadLeft(4, '0'));
                    Directory.CreateDirectory(folder);
                    string dest = Path.Combine(folder, pageNo.Value + ".jpg");
                    File.Copy(item.ImagePath, dest, true);
                    DbHelper.UpdatePagetFile(item.OcrPk, "jpg", new FileInfo(dest).Length);
                    item.Status = "Saved";
                    lastTreatNo = item.TreatNo ?? ""; lastFormCode = item.FormCode ?? "";
                    success++;
                }
                catch { fail++; }
            }

            if (success > 0 && !string.IsNullOrEmpty(lastTreatNo))
                await ApiHelper.NotifyScanComplete(lastTreatNo, lastFormCode, _userId);

            RefreshGrid();
            SetStatus($"บันทึกสำเร็จ {success} รายการ" + (fail > 0 ? $" ล้มเหลว {fail}" : ""));
            MessageBox.Show($"บันทึกสำเร็จ {success} รายการ" + (fail > 0 ? $"\nล้มเหลว {fail} รายการ" : ""),
                "เสร็จสิ้น", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnClear_Click(object sender, EventArgs e)
        {
            _items.Clear(); _grid.Rows.Clear(); _preview.Image = null; _btnConfirm.Enabled = false;
            SetStatus("พร้อมใช้งาน");
        }

        private void BtnSettings_Click(object sender, EventArgs e)
        {
            using var dlg = new SettingsForm(ScanDpi, ScanColor);
            if (dlg.ShowDialog() == DialogResult.OK) { ScanDpi = dlg.SelectedDpi; ScanColor = dlg.SelectedColor; }
        }

        private void SetStatus(string msg, bool showProgress = false)
        {
            if (InvokeRequired) { Invoke((Action)(() => SetStatus(msg, showProgress))); return; }
            _lblStatus.Text = msg; _progress.Visible = showProgress;
        }
    }
}
