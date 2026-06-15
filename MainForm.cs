using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EMRScan
{
    public class MainForm : Form
    {
        private string _userId, _userName;
        private DataGridView   _grid;
        private Panel          _previewPanel, _zoomBar;
        private Button         _btnScan, _btnScanDevice, _btnConfirm, _btnClear, _btnSettings;
        private Button         _btnZoomIn, _btnZoomOut, _btnZoomReset;
        private Label          _lblUser, _lblStatus, _lblPreviewHint, _lblZoomPct;
        private ProgressBar    _progress;
        private List<ScanItem> _items = new List<ScanItem>();

        // Zoom/pan state
        private float  _zoom      = 1.0f;
        private PointF _panOffset = PointF.Empty;
        private Point  _panStart;
        private bool   _panning   = false;
        private Image  _currentImage = null;

        public int    ScanDpi      { get; set; } = 300;
        public bool   ScanColor    { get; set; } = true;
        public string ScannerName  { get; set; } = "";
        public string ScanPaperSize { get; set; } = "";

        // Design tokens
        static readonly Color Navy       = Color.FromArgb(26,  55,  92);
        static readonly Color NavyDark   = Color.FromArgb(17,  36,  61);
        static readonly Color NavyLight  = Color.FromArgb(42,  82, 130);
        static readonly Color BgPage     = Color.FromArgb(241, 245, 249);
        static readonly Color BgCard     = Color.White;
        static readonly Color Border     = Color.FromArgb(214, 219, 230);
        static readonly Color Green      = Color.FromArgb(22,  163,  74);
        static readonly Color GreenDark  = Color.FromArgb(15,  118,  53);
        static readonly Color Red        = Color.FromArgb(220,  38,  38);
        static readonly Color GrayBtn    = Color.FromArgb(71,  85, 105);
        static readonly Color GrayBtnDk  = Color.FromArgb(51,  65,  85);
        static readonly Color GrayText   = Color.FromArgb(100, 116, 139);
        static readonly Color UserChipBg = Color.FromArgb(148, 185, 230);  // steel blue — readable on navy

        public MainForm(string userId, string userName)
        {
            _userId   = userId;
            _userName = userName;

            // โหลดค่า scanner settings ที่เคยบันทึกไว้ (จาก application.properties)
            ScanDpi       = AppConfig.ScanDpi;
            ScanColor     = AppConfig.ScanColor;
            ScannerName   = AppConfig.ScannerName;
            ScanPaperSize = AppConfig.PaperSize;

            Text          = "EMR Scan";
            Size          = new Size(1280, 740);
            MinimumSize   = new Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor     = BgPage;
            Font          = new Font("Segoe UI", 9f);
            BuildUI();
        }

        // ── Rounded button factory ────────────────────────────────────────
        private Button MakeBtn(string text, Color top, Color bottom, int x, int w = 110)
        {
            var b = new Button
            {
                Text      = text,
                Size      = new Size(w, 32),
                Location  = new Point(x, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor    = Cursors.Hand,
                BackColor = top,
                Tag       = new Color[]{ top, bottom }
            };
            b.FlatAppearance.BorderSize = 0;
            b.Paint += Btn_Paint;
            return b;
        }

        private void Btn_Paint(object sender, PaintEventArgs e)
        {
            var b   = (Button)sender;
            var rc  = new Rectangle(0, 0, b.Width, b.Height);
            var g   = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color[] cols = b.Tag as Color[];
            Color top    = cols != null ? cols[0] : b.BackColor;
            Color bot    = cols != null ? cols[1] : ControlPaint.Dark(b.BackColor, 0.15f);

            if (!b.Enabled)
            {
                top = Color.FromArgb(180, 180, 180);
                bot = Color.FromArgb(160, 160, 160);
            }

            // Gradient fill with rounded corners
            using var path = RoundedRect(rc, 6);
            using var brush = new LinearGradientBrush(rc, top, bot, LinearGradientMode.Vertical);
            g.FillPath(brush, path);

            // Subtle top highlight
            using var hl = new LinearGradientBrush(
                new Rectangle(0, 0, b.Width, b.Height / 2),
                Color.FromArgb(60, 255, 255, 255), Color.Transparent,
                LinearGradientMode.Vertical);
            g.FillPath(hl, path);

            // Text
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(b.Text, b.Font,
                b.Enabled ? Brushes.White : new SolidBrush(Color.FromArgb(220, 220, 220)),
                rc, sf);
        }

        private GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, radius * 2, radius * 2, 180, 90);
            path.AddArc(r.Right - radius * 2, r.Y, radius * 2, radius * 2, 270, 90);
            path.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(r.X, r.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void BuildUI()
        {
            SuspendLayout();

            // ── Toolbar ───────────────────────────────────────────────────
            var toolbar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Navy };
            toolbar.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Color.FromArgb(40, 0, 0, 0)), 0, toolbar.Height - 1, toolbar.Width, toolbar.Height - 1);

            var lblTitle = new Label
            {
                Text = "EMR Scan", ForeColor = Color.White,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                AutoSize = true, Location = new Point(16, 13)
            };

            // User chip — soft steel-blue, readable on navy
            var userChip = new Panel
            {
                BackColor = UserChipBg,
                Size      = new Size(200, 28),
                Anchor    = AnchorStyles.Top | AnchorStyles.Right
            };
            // rounded corners for chip
            userChip.Paint += (s, e) =>
            {
                var g  = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc = new Rectangle(0, 0, userChip.Width - 1, userChip.Height - 1);
                using var path = RoundedRect(rc, 5);
                using var br   = new SolidBrush(UserChipBg);
                g.FillPath(br, path);
            };
            userChip.Location = new Point(toolbar.Width - userChip.Width - 14, 12);
            toolbar.Resize += (s, e) =>
                userChip.Location = new Point(toolbar.Width - userChip.Width - 14, 12);

            _lblUser = new Label
            {
                Text      = $"👤  {_userName}  ({_userId})",
                ForeColor = NavyDark,
                Font      = new Font("Segoe UI", 8f, FontStyle.Bold),
                AutoSize  = false,
                Size      = new Size(196, 24),
                Location  = new Point(2, 2),
                TextAlign = ContentAlignment.MiddleCenter
            };
            userChip.Controls.Add(_lblUser);
            toolbar.Controls.Add(lblTitle);
            toolbar.Controls.Add(userChip);

            // ── Button bar ────────────────────────────────────────────────
            var btnBar = new Panel
            {
                Dock = DockStyle.Top, Height = 52,
                BackColor = BgCard, Padding = new Padding(12, 10, 12, 10)
            };
            btnBar.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, btnBar.Height - 1, btnBar.Width, btnBar.Height - 1);

            _btnScan       = MakeBtn("📂  เปิดไฟล์",       NavyLight,  Navy,      0,   108);
            _btnScanDevice = MakeBtn("🖨  Scanner",        Color.FromArgb(109, 40, 217), Color.FromArgb(76, 29, 149), 118, 108);
            _btnConfirm    = MakeBtn("✔  Confirm",        Green,      GreenDark, 236, 120);
            _btnClear      = MakeBtn("✕  Clear",          GrayBtn,    GrayBtnDk, 366, 100);
            _btnSettings   = MakeBtn("⚙  Settings",      GrayBtn,    GrayBtnDk, 476, 110);
            _btnConfirm.Enabled = false;

            _btnScan.Click       += BtnScan_Click;
            _btnScanDevice.Click += BtnScanDevice_Click;
            _btnConfirm.Click    += BtnConfirm_Click;
            _btnClear.Click      += BtnClear_Click;
            _btnSettings.Click   += BtnSettings_Click;
            btnBar.Controls.AddRange(new Control[]{ _btnScan, _btnScanDevice, _btnConfirm, _btnClear, _btnSettings });

            // ── Status bar ────────────────────────────────────────────────
            var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 30, BackColor = Color.FromArgb(226, 232, 240) };
            statusBar.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, 0, statusBar.Width, 0);

            _lblStatus = new Label
            {
                Text = "พร้อมใช้งาน", AutoSize = true,
                Location = new Point(12, 7),
                Font = new Font("Segoe UI", 8.5f), ForeColor = GrayText
            };
            _progress = new ProgressBar
            {
                Location = new Point(300, 6), Size = new Size(180, 18),
                Visible = false, Style = ProgressBarStyle.Marquee
            };
            statusBar.Controls.Add(_lblStatus);
            statusBar.Controls.Add(_progress);

            // ── SplitContainer ─────────────────────────────────────────────
            var split = new SplitContainer();
            split.Dock        = DockStyle.Fill;
            split.Orientation = Orientation.Vertical;
            split.BackColor   = BgPage;
            this.Load += (s, e) =>
            {
                try
                {
                    split.Panel1MinSize    = 300;
                    split.Panel2MinSize    = 280;
                    split.SplitterDistance = Math.Max(280, (int)(this.ClientSize.Width * 0.25));
                }
                catch { }
            };

            // ── Grid panel ─────────────────────────────────────────────────
            var gridCard = new Panel { Dock = DockStyle.Fill, BackColor = BgCard };
            gridCard.Paint += (s, e) =>
                e.Graphics.DrawRectangle(new Pen(Border), 0, 0, gridCard.Width - 1, gridCard.Height - 1);

            _grid = new DataGridView
            {
                Dock                  = DockStyle.Fill,
                BackgroundColor       = BgCard,
                BorderStyle           = BorderStyle.None,
                RowHeadersVisible     = false,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                ReadOnly              = false,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
                Font                  = new Font("Segoe UI", 9f),
                GridColor             = Color.FromArgb(241, 245, 249),
                CellBorderStyle       = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                RowTemplate           = { Height = 36 }
            };
            _grid.DefaultCellStyle.Padding              = new Padding(6, 0, 6, 0);
            _grid.DefaultCellStyle.SelectionBackColor   = Color.FromArgb(219, 234, 254);
            _grid.DefaultCellStyle.SelectionForeColor   = Color.FromArgb(29, 78, 216);
            _grid.ColumnHeadersDefaultCellStyle.BackColor = NavyDark;
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _grid.ColumnHeadersDefaultCellStyle.Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            _grid.ColumnHeadersDefaultCellStyle.Padding   = new Padding(6, 0, 6, 0);
            _grid.ColumnHeadersHeight    = 34;
            _grid.EnableHeadersVisualStyles = false;
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColOcrPk",  HeaderText = "OCR Code",  FillWeight = 22, ReadOnly = false });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColHn",     HeaderText = "HN",        FillWeight = 15, ReadOnly = true  });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColForm",   HeaderText = "Form Code", FillWeight = 25, ReadOnly = true  });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColStatus", HeaderText = "สถานะ",     FillWeight = 18, ReadOnly = true  });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColPath",   HeaderText = "ไฟล์",      FillWeight = 20, ReadOnly = true  });
            _grid.SelectionChanged += Grid_SelectionChanged;
            _grid.CellEndEdit      += Grid_CellEndEdit;
            _grid.RowPrePaint      += Grid_RowPrePaint;
            gridCard.Controls.Add(_grid);
            split.Panel1.Controls.Add(gridCard);
            split.Panel1.Padding = new Padding(8, 8, 4, 8);

            // ── Preview panel ──────────────────────────────────────────────
            var previewContainer = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(15, 23, 42) };

            // Zoom toolbar
            _zoomBar = new Panel
            {
                Dock = DockStyle.Bottom, Height = 36,
                BackColor = Color.FromArgb(25, 35, 55)
            };
            _btnZoomIn    = MakeZoomBtn("+",   new Point(8,   4));
            _btnZoomOut   = MakeZoomBtn("−",   new Point(48,  4));
            _btnZoomReset = MakeZoomBtn("1:1", new Point(88,  4));
            _lblZoomPct   = new Label
            {
                Text = "100%", ForeColor = Color.FromArgb(180, 200, 230),
                Font = new Font("Segoe UI", 8f), AutoSize = true,
                Location = new Point(140, 11)
            };
            _btnZoomIn.Click    += (s, e) => ChangeZoom(+0.25f);
            _btnZoomOut.Click   += (s, e) => ChangeZoom(-0.25f);
            _btnZoomReset.Click += (s, e) => ResetZoom();
            _zoomBar.Controls.AddRange(new Control[]{ _btnZoomIn, _btnZoomOut, _btnZoomReset, _lblZoomPct });

            // Custom paint preview with zoom/pan
            _previewPanel = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 23, 42),
                Cursor    = Cursors.Hand
            };
            _previewPanel.Paint     += PreviewPanel_Paint;
            _previewPanel.MouseDown += PreviewPanel_MouseDown;
            _previewPanel.MouseMove += PreviewPanel_MouseMove;
            _previewPanel.MouseUp   += (s, e) => _panning = false;
            _previewPanel.MouseWheel += PreviewPanel_MouseWheel;

            _lblPreviewHint = new Label
            {
                Text      = "เลือกรายการเพื่อดูภาพ",
                ForeColor = Color.FromArgb(80, 100, 130),
                Font      = new Font("Segoe UI", 10f),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock      = DockStyle.Fill
            };

            _previewPanel.Controls.Add(_lblPreviewHint);
            previewContainer.Controls.Add(_previewPanel);
            previewContainer.Controls.Add(_zoomBar);
            split.Panel2.Controls.Add(previewContainer);
            split.Panel2.Padding = new Padding(4, 8, 8, 8);

            // ── Dock order ────────────────────────────────────────────────
            Controls.Add(split);
            Controls.Add(statusBar);
            Controls.Add(btnBar);
            Controls.Add(toolbar);

            ResumeLayout(false);
        }

        private Button MakeZoomBtn(string text, Point loc)
        {
            var b = new Button
            {
                Text      = text,
                Size      = new Size(32, 28),
                Location  = loc,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(50, 70, 100),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(70, 100, 140);
            b.FlatAppearance.BorderSize  = 1;
            return b;
        }

        // ── Zoom/Pan ──────────────────────────────────────────────────────
        private void ChangeZoom(float delta)
        {
            _zoom = Math.Max(0.1f, Math.Min(5.0f, _zoom + delta));
            _lblZoomPct.Text = $"{(int)(_zoom * 100)}%";
            _previewPanel.Invalidate();
        }

        private void ResetZoom()
        {
            _panOffset = PointF.Empty;
            if (_currentImage != null && _previewPanel.Width > 0 && _previewPanel.Height > 0)
            {
                float scaleX = (float)_previewPanel.Width  / _currentImage.Width;
                float scaleY = (float)_previewPanel.Height / _currentImage.Height;
                _zoom = Math.Min(scaleX, scaleY) * 0.97f;  // 3% padding
            }
            else _zoom = 1.0f;
            _lblZoomPct.Text = $"{(int)(_zoom * 100)}%";
            _previewPanel.Invalidate();
        }

        private void PreviewPanel_Paint(object sender, PaintEventArgs e)
        {
            var g  = e.Graphics;
            var rc = _previewPanel.ClientRectangle;

            if (_currentImage == null)
                return;

            g.SmoothingMode      = SmoothingMode.AntiAlias;
            g.InterpolationMode  = InterpolationMode.HighQualityBicubic;

            float imgW = _currentImage.Width  * _zoom;
            float imgH = _currentImage.Height * _zoom;
            float x    = (rc.Width  - imgW) / 2f + _panOffset.X;
            float y    = (rc.Height - imgH) / 2f + _panOffset.Y;

            g.DrawImage(_currentImage, x, y, imgW, imgH);
        }

        private void PreviewPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _currentImage != null)
            {
                _panning  = true;
                _panStart = e.Location;
                _previewPanel.Cursor = Cursors.SizeAll;
            }
        }

        private void PreviewPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_panning) return;
            _panOffset.X += e.X - _panStart.X;
            _panOffset.Y += e.Y - _panStart.Y;
            _panStart = e.Location;
            _previewPanel.Invalidate();
        }

        private void PreviewPanel_MouseWheel(object sender, MouseEventArgs e)
        {
            float delta = e.Delta > 0 ? 0.15f : -0.15f;
            ChangeZoom(delta);
        }

        // ── Grid helpers ──────────────────────────────────────────────────
        private void Grid_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _items.Count) return;
            var item = _items[e.RowIndex];
            Color bar = item.Status == "Approve" || item.Status == "Saved" ? Green
                      : item.Status == "Skipped" ? Color.FromArgb(234, 179, 8)
                      : Red;
            var rc = _grid.GetRowDisplayRectangle(e.RowIndex, false);
            e.Graphics.FillRectangle(new SolidBrush(bar), rc.X, rc.Y, 3, rc.Height);
        }

        private void LoadPreview(ScanItem item)
        {
            if (item == null || !File.Exists(item.ImagePath))
            {
                _currentImage = null;
                _lblPreviewHint.Visible = true;
                _previewPanel.Invalidate();
                return;
            }
            try
            {
                var old = _currentImage;
                _currentImage = Image.FromStream(new MemoryStream(File.ReadAllBytes(item.ImagePath)));
                old?.Dispose();
                _lblPreviewHint.Visible = false;
                ResetZoom();
            }
            catch
            {
                _currentImage = null;
                _lblPreviewHint.Visible = true;
                _previewPanel.Invalidate();
            }
        }

        private void Grid_SelectionChanged(object sender, EventArgs e)
        {
            if (_grid.SelectedRows.Count == 0) return;
            var item = _grid.SelectedRows[0].Tag as ScanItem;
            LoadPreview(item);
        }

        private void RefreshGrid()
        {
            if (InvokeRequired) { Invoke((Action)RefreshGrid); return; }

            // Remember selected ocrPk
            string selectedOcrPk = _grid.SelectedRows.Count > 0
                ? (_grid.SelectedRows[0].Tag as ScanItem)?.OcrPk
                : null;

            _grid.Rows.Clear();
            int selectIdx = -1;
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                int idx = _grid.Rows.Add(
                    item.OcrPk, item.Hn, item.FormCode,
                    StatusLabel(item.Status),
                    Path.GetFileName(item.ImagePath));
                var row = _grid.Rows[idx];
                row.Tag = item;

                bool ok = item.Status == "Approve" || item.Status == "Saved";
                row.DefaultCellStyle.ForeColor = ok ? Green
                    : item.Status == "Skipped" ? Color.FromArgb(161, 130, 0) : Red;
                if (ok) row.DefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);

                if (item.OcrPk == selectedOcrPk) selectIdx = idx;
            }

            // Auto-select: restore previous or select last added
            if (selectIdx >= 0)
                _grid.Rows[selectIdx].Selected = true;
            else if (_grid.Rows.Count > 0)
            {
                _grid.Rows[_grid.Rows.Count - 1].Selected = true;
                _grid.FirstDisplayedScrollingRowIndex = _grid.Rows.Count - 1;
            }

            // Load preview for selected row (fixes single-image bug)
            if (_grid.SelectedRows.Count > 0)
                LoadPreview(_grid.SelectedRows[0].Tag as ScanItem);
        }

        private string StatusLabel(string s) => s switch
        {
            "Approve" => "✔ Approve",
            "Saved"   => "✔ Saved",
            "Skipped" => "— Skipped",
            _         => "✕ Not Found"
        };

        // ── Button handlers ───────────────────────────────────────────────
        private async void BtnScan_Click(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title = "เลือกไฟล์ภาพ",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.tif;*.tiff",
                Multiselect = true
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            SetStatus($"กำลังประมวลผล {dlg.FileNames.Length} ไฟล์...", true);
            _btnScan.Enabled       = false;
            _btnScanDevice.Enabled = false;

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

                // Load page info (page picker shown at Confirm time, not here)
                if (!string.IsNullOrEmpty(item.OcrPk) && item.Status == "Approve")
                {
                    var pageInfo = DbHelper.GetOcrPkPageInfo(item.OcrPk);
                    if (pageInfo != null)
                    {
                        item.PageCount = pageInfo.Value.pageCount;
                        item.TreatNo   = pageInfo.Value.treatNo.ToString();
                        item.PageSeq   = 0; // 0 = not yet assigned, will pick at Confirm
                    }
                }

                _items.Add(item);
                RefreshGrid();
            }

            SetStatus($"พบ {_items.Count} รายการ — Approve {_items.Count(i => i.Status == "Approve")} รายการ");
            _btnScan.Enabled       = true;
            _btnScanDevice.Enabled = true;
            _btnConfirm.Enabled    = _items.Any(i => i.Status == "Approve");
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

            // Show confirm dialog — let user assign page seq per file
            using var dlgConfirm = new ConfirmScanDialog(approved);
            if (dlgConfirm.ShowDialog() != DialogResult.OK) return;

            _btnConfirm.Enabled = false;
            SetStatus("กำลังบันทึก...", true);

            var pathInfo = DbHelper.GetActivePath();
            if (pathInfo == null)
            {
                MessageBox.Show("ไม่พบ PATHT ที่ active", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                _btnConfirm.Enabled = true; return;
            }

            // ประกอบ UNC base path จาก IPADDRESS + LOCALPATH (ถอด drive letter ออก)
            //   "192.168.1.40" + "H:\IMAGE" -> "\\192.168.1.40\IMAGE"
            string basePath = DbHelper.BuildStoragePath(
                pathInfo.Value.ipAddress, pathInfo.Value.localPath);

            int success = 0, fail = 0;
            string lastTreatNo = "", lastFormCode = "";
            string lastError = "";   // เก็บ error ล่าสุดไว้โชว์ถ้ามี fail

            foreach (var item in approved)
            {
                try
                {
                    long? pageNo;

                    if (item.PageCount > 1)
                    {
                        // Multi-page: overwrite existing or insert new
                        pageNo = DbHelper.GetPageNoBySeq(item.OcrPk, item.PageSeq);
                        if (pageNo == null)
                        {
                            // Insert new page record
                            pageNo = DbHelper.InsertPageRecord(
                                item.OcrPk, pathInfo.Value.pathId, _userId,
                                decimal.Parse(item.TreatNo ?? "0"),
                                item.FormCode, item.PageSeq);
                        }
                    }
                    else
                    {
                        // Single-page: use existing PAGENO or insert
                        pageNo = DbHelper.GetPageNoBySeq(item.OcrPk, 1);
                        if (pageNo == null)
                        {
                            var pi = DbHelper.GetOcrPkPageInfo(item.OcrPk);
                            pageNo = DbHelper.InsertPageRecord(
                                item.OcrPk, pathInfo.Value.pathId, _userId,
                                pi?.treatNo ?? 0, item.FormCode, 1);
                        }
                    }

                    if (pageNo == null) { fail++; lastError = $"[{item.OcrPk}] หา/สร้าง PAGENO ไม่ได้"; continue; }

                    string folder = Path.Combine(basePath,
                        pageNo.Value.ToString().PadLeft(4, '0'));
                    Directory.CreateDirectory(folder);
                    string dest = Path.Combine(folder, pageNo.Value + ".jpg");
                    File.Copy(item.ImagePath, dest, true);
                    DbHelper.UpdatePagetFile(item.OcrPk, "jpg", new FileInfo(dest).Length);
                    DbHelper.UpdateOcrPrint(item.OcrPk, _userId);
                    item.Status  = "Saved";
                    lastTreatNo  = item.TreatNo  ?? "";
                    lastFormCode = item.FormCode ?? "";
                    success++;
                }
                catch (Exception ex)
                {
                    fail++;
                    lastError = $"[{item.OcrPk}] {ex.Message}";
                    System.Diagnostics.Debug.WriteLine($"[Confirm] {item.OcrPk}: {ex}");
                }
            }

            if (success > 0 && !string.IsNullOrEmpty(lastTreatNo))
                await ApiHelper.NotifyScanComplete(lastTreatNo, lastFormCode, _userId);

            RefreshGrid();
            SetStatus($"บันทึกสำเร็จ {success} รายการ" + (fail > 0 ? $"  ล้มเหลว {fail}" : ""));

            string msg = $"บันทึกสำเร็จ {success} รายการ";
            if (fail > 0)
            {
                msg += $"\nล้มเหลว {fail} รายการ";
                if (!string.IsNullOrEmpty(lastError))
                    msg += $"\n\nสาเหตุล่าสุด:\n{lastError}\n\nปลายทาง: {basePath}";
            }
            MessageBox.Show(msg, "เสร็จสิ้น",
                MessageBoxButtons.OK,
                fail > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        private void BtnScanDevice_Click(object sender, EventArgs e)
        {
            _btnScan.Enabled       = false;
            _btnScanDevice.Enabled = false;
            SetStatus("กำลังสแกนเอกสาร...", true);

            List<string> scannedFiles;
            try
            {
                // ScanAll must run on UI thread (TWAIN STA requirement)
                scannedFiles = ScannerHelper.ScanAll(ScanDpi, ScanColor, ScannerName, ScanPaperSize);
            }
            catch (ScannerException ex)
            {
                MessageBox.Show(ex.Message, "Scanner Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SetStatus("พร้อมใช้งาน");
                _btnScan.Enabled       = true;
                _btnScanDevice.Enabled = true;
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"เกิดข้อผิดพลาด: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus("พร้อมใช้งาน");
                _btnScan.Enabled       = true;
                _btnScanDevice.Enabled = true;
                return;
            }

            if (scannedFiles.Count == 0)
            {
                SetStatus("ไม่พบเอกสารใน Scanner");
                _btnScan.Enabled       = true;
                _btnScanDevice.Enabled = true;
                return;
            }

            SetStatus($"สแกนได้ {scannedFiles.Count} หน้า กำลังประมวลผล OCR...", true);

            foreach (string path in scannedFiles)
            {
                var i2 = new ScanItem { ImagePath = path };
                string ocrPk2 = OcrHelper.ReadOcrPk(path);
                i2.OcrPk = ocrPk2;
                if (!string.IsNullOrEmpty(ocrPk2))
                {
                    var info = DbHelper.VerifyOcrPk(ocrPk2);
                    if (info != null) { i2.Hn = info.Value.hn; i2.FormCode = info.Value.formCode; i2.Status = "Approve"; }
                    else i2.Status = "Not Found";
                }
                else i2.Status = "Not Found";
                var item = i2;

                if (!string.IsNullOrEmpty(item.OcrPk) && item.Status == "Approve")
                {
                    var pageInfo = DbHelper.GetOcrPkPageInfo(item.OcrPk);
                    if (pageInfo != null)
                    {
                        item.PageCount = pageInfo.Value.pageCount;
                        item.TreatNo   = pageInfo.Value.treatNo.ToString();
                        item.PageSeq   = 0;
                    }
                }

                _items.Add(item);
                RefreshGrid();
            }

            SetStatus($"สแกนเสร็จ {scannedFiles.Count} หน้า — Approve {_items.Count(i => i.Status == "Approve")} รายการ");
            _btnScan.Enabled       = true;
            _btnScanDevice.Enabled = true;
            _btnConfirm.Enabled    = _items.Any(i => i.Status == "Approve");
        }

        private void BtnClear_Click(object sender, EventArgs e)
        {
            _currentImage?.Dispose();
            _currentImage = null;
            _lblPreviewHint.Visible = true;
            _previewPanel.Invalidate();
            _items.Clear();
            _grid.Rows.Clear();
            _btnConfirm.Enabled = false;
            SetStatus("พร้อมใช้งาน");
        }

        private void BtnSettings_Click(object sender, EventArgs e)
        {
            using var dlg = new SettingsForm(ScanDpi, ScanColor, ScannerName, ScanPaperSize);
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                ScanDpi       = dlg.SelectedDpi;
                ScanColor     = dlg.SelectedColor;
                ScannerName   = dlg.SelectedScannerName;
                ScanPaperSize = dlg.SelectedPaperSize;

                // เขียนค่ากลับ application.properties ให้ค่าคงอยู่หลังปิดโปรแกรม
                AppConfig.SaveScannerSettings(ScanDpi, ScanColor, ScannerName, ScanPaperSize);
            }
        }

        private void SetStatus(string msg, bool showProgress = false)
        {
            if (InvokeRequired) { Invoke((Action)(() => SetStatus(msg, showProgress))); return; }
            _lblStatus.Text   = msg;
            _progress.Visible = showProgress;
        }
    }
}
