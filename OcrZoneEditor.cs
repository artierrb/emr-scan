using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace EMRScan
{
    /// <summary>
    /// Visual editor for OCR crop zone.
    /// User loads a sample image, drags/resizes the red rectangle to define the OCR area,
    /// then clicks Preview to verify OCR result, and Save to persist.
    /// </summary>
    public class OcrZoneEditor : Form
    {
        // Current zone in ratio (0..1)
        private float _zx, _zy, _zw, _zh;

        // Drag state
        private bool   _dragging, _resizing;
        private Point  _dragStart;
        private PointF _zoneStart;   // zone origin at drag start
        private SizeF  _sizeStart;   // zone size at drag start
        private int    _resizeHandle; // 0=none 1=BR corner

        private Bitmap _image;
        private Panel  _canvas;
        private Label  _lblRatios, _lblOcrResult, _lblStatus;
        private Button _btnLoad, _btnPreview, _btnSave, _btnReset;

        const int HANDLE_SIZE = 10;

        static readonly Color Navy    = Color.FromArgb(26,  55,  92);
        static readonly Color Green   = Color.FromArgb(22, 163,  74);
        static readonly Color GrayBtn = Color.FromArgb(71,  85, 105);
        static readonly Color BgPage  = Color.FromArgb(241, 245, 249);

        public OcrZoneEditor()
        {
            _zx = AppConfig.OcrCropX;
            _zy = AppConfig.OcrCropY;
            _zw = AppConfig.OcrCropW;
            _zh = AppConfig.OcrCropH;

            Text            = "OCR Zone Editor";
            Size            = new Size(900, 680);
            MinimumSize     = new Size(700, 500);
            StartPosition   = FormStartPosition.CenterParent;
            BackColor       = BgPage;
            Font            = new Font("Segoe UI", 9f);
            BuildUI();
        }

        private void BuildUI()
        {
            SuspendLayout();

            // ── Header ────────────────────────────────────────────────────
            var header = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Navy };
            var lblTitle = new Label
            {
                Text = "OCR Zone Editor", ForeColor = Color.White,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                AutoSize = true, Location = new Point(14, 12)
            };
            var lblSub = new Label
            {
                Text = "ลากกรอบสีแดงเพื่อกำหนดตำแหน่ง OCR  •  ลาก BR corner เพื่อปรับขนาด",
                ForeColor = Color.FromArgb(180, 210, 240),
                Font = new Font("Segoe UI", 8f),
                AutoSize = true, Location = new Point(14, 30)
            };
            header.Controls.Add(lblTitle);
            header.Controls.Add(lblSub);

            // ── Right panel (controls) ─────────────────────────────────────
            var rightPanel = new Panel
            {
                Dock = DockStyle.Right, Width = 220,
                BackColor = Color.White, Padding = new Padding(12)
            };
            rightPanel.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Color.FromArgb(214, 219, 230)),
                    0, 0, 0, rightPanel.Height);

            int ry = 12;

            _btnLoad = MakeBtn("📂  โหลดภาพตัวอย่าง", Navy, new Point(12, ry), 192);
            _btnLoad.Click += BtnLoad_Click;
            ry += 42;

            var sep1 = new Label
            {
                Text = "ค่า Ratio ปัจจุบัน",
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(12, ry), AutoSize = true
            };
            ry += 20;

            _lblRatios = new Label
            {
                Text      = GetRatioText(),
                Font      = new Font("Courier New", 8.5f),
                ForeColor = Color.FromArgb(30, 64, 175),
                Location  = new Point(12, ry),
                Size      = new Size(196, 80),
                BackColor = Color.FromArgb(239, 246, 255)
            };
            ry += 88;

            _btnPreview = MakeBtn("🔍  Preview OCR", Color.FromArgb(109, 40, 217), new Point(12, ry), 192);
            _btnPreview.Enabled = false;
            _btnPreview.Click  += BtnPreview_Click;
            ry += 42;

            var sep2 = new Label
            {
                Text = "ผลลัพธ์ OCR",
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(12, ry), AutoSize = true
            };
            ry += 20;

            _lblOcrResult = new Label
            {
                Text      = "—",
                Font      = new Font("Courier New", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(22, 163, 74),
                Location  = new Point(12, ry),
                Size      = new Size(196, 40),
                BackColor = Color.FromArgb(240, 253, 244)
            };
            ry += 48;

            _btnReset = MakeBtn("↺  Reset Default", GrayBtn, new Point(12, ry), 192);
            _btnReset.Click += (s, e) =>
            {
                _zx = 0.65f; _zy = 0.01f; _zw = 0.35f; _zh = 0.06f;
                UpdateRatioLabel();
                _canvas.Invalidate();
            };
            ry += 42;

            _btnSave = MakeBtn("💾  บันทึก", Green, new Point(12, ry), 192);
            _btnSave.Click += BtnSave_Click;
            ry += 42;

            _lblStatus = new Label
            {
                Text = "", ForeColor = Color.FromArgb(22, 163, 74),
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(12, ry), AutoSize = true
            };

            rightPanel.Controls.AddRange(new Control[]
            {
                _btnLoad, sep1, _lblRatios, _btnPreview, sep2,
                _lblOcrResult, _btnReset, _btnSave, _lblStatus
            });

            // ── Canvas ────────────────────────────────────────────────────
            _canvas = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 45),
                Cursor    = Cursors.Default
            };
            _canvas.Paint     += Canvas_Paint;
            _canvas.MouseDown += Canvas_MouseDown;
            _canvas.MouseMove += Canvas_MouseMove;
            _canvas.MouseUp   += Canvas_MouseUp;

            Controls.Add(_canvas);
            Controls.Add(rightPanel);
            Controls.Add(header);

            ResumeLayout(false);
        }

        // ── Zone rect in canvas pixels ────────────────────────────────────
        private RectangleF GetZoneRect()
        {
            if (_image == null) return RectangleF.Empty;
            var imgRect = GetImageRect();
            return new RectangleF(
                imgRect.X + _zx * imgRect.Width,
                imgRect.Y + _zy * imgRect.Height,
                _zw * imgRect.Width,
                _zh * imgRect.Height);
        }

        // Image drawn with Zoom fit inside canvas
        private RectangleF GetImageRect()
        {
            if (_image == null) return RectangleF.Empty;
            float sx = (float)_canvas.Width  / _image.Width;
            float sy = (float)_canvas.Height / _image.Height;
            float s  = Math.Min(sx, sy) * 0.97f;
            float w  = _image.Width  * s;
            float h  = _image.Height * s;
            return new RectangleF(
                (_canvas.Width  - w) / 2f,
                (_canvas.Height - h) / 2f, w, h);
        }

        // ── Paint ─────────────────────────────────────────────────────────
        private void Canvas_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (_image == null)
            {
                var hint = "โหลดภาพตัวอย่างก่อน\nกดปุ่ม 📂 โหลดภาพตัวอย่าง";
                var sf   = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(hint, new Font("Segoe UI", 12f), new SolidBrush(Color.FromArgb(80, 100, 130)),
                    _canvas.ClientRectangle, sf);
                return;
            }

            // Draw image
            var imgRect = GetImageRect();
            g.DrawImage(_image, imgRect);

            // Dim overlay outside zone
            var zone = GetZoneRect();
            using var dimBrush = new SolidBrush(Color.FromArgb(100, 0, 0, 0));
            g.FillRectangle(dimBrush, imgRect.X, imgRect.Y, imgRect.Width, zone.Y - imgRect.Y);
            g.FillRectangle(dimBrush, imgRect.X, zone.Bottom, imgRect.Width, imgRect.Bottom - zone.Bottom);
            g.FillRectangle(dimBrush, imgRect.X, zone.Y, zone.X - imgRect.X, zone.Height);
            g.FillRectangle(dimBrush, zone.Right, zone.Y, imgRect.Right - zone.Right, zone.Height);

            // Zone border
            using var pen = new Pen(Color.FromArgb(239, 68, 68), 2f);
            pen.DashStyle = DashStyle.Dash;
            g.DrawRectangle(pen, zone.X, zone.Y, zone.Width, zone.Height);

            // Solid top border for clarity
            using var solidPen = new Pen(Color.FromArgb(239, 68, 68), 2f);
            g.DrawLine(solidPen, zone.X, zone.Y, zone.Right, zone.Y);

            // BR resize handle
            var handle = GetHandleRect(zone);
            g.FillRectangle(Brushes.White, handle);
            g.DrawRectangle(new Pen(Color.FromArgb(239, 68, 68), 1.5f), handle.X, handle.Y, handle.Width, handle.Height);

            // Dimension label
            string dimText = $"X:{_zx:F3}  Y:{_zy:F3}\nW:{_zw:F3}  H:{_zh:F3}";
            var labelFont = new Font("Segoe UI", 7.5f);
            var labelSize = g.MeasureString(dimText, labelFont);
            var labelRect = new RectangleF(zone.X + 2, zone.Y + 2, labelSize.Width + 4, labelSize.Height + 2);
            g.FillRectangle(new SolidBrush(Color.FromArgb(200, 0, 0, 0)), labelRect);
            g.DrawString(dimText, labelFont, Brushes.White, labelRect.X + 2, labelRect.Y + 1);
        }

        private RectangleF GetHandleRect(RectangleF zone)
            => new RectangleF(zone.Right - HANDLE_SIZE, zone.Bottom - HANDLE_SIZE, HANDLE_SIZE, HANDLE_SIZE);

        // ── Mouse ─────────────────────────────────────────────────────────
        private void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (_image == null || e.Button != MouseButtons.Left) return;
            var zone   = GetZoneRect();
            var handle = GetHandleRect(zone);

            if (handle.Contains(e.Location))
            {
                _resizing    = true;
                _dragStart   = e.Location;
                _sizeStart   = new SizeF(_zw, _zh);
                _zoneStart   = new PointF(_zx, _zy);
            }
            else if (zone.Contains(e.Location))
            {
                _dragging    = true;
                _dragStart   = e.Location;
                _zoneStart   = new PointF(_zx, _zy);
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_image == null) return;

            var imgRect = GetImageRect();
            if (imgRect.Width <= 0 || imgRect.Height <= 0) return;

            float dx = (e.X - _dragStart.X) / imgRect.Width;
            float dy = (e.Y - _dragStart.Y) / imgRect.Height;

            if (_dragging)
            {
                _zx = Clamp(_zoneStart.X + dx, 0f, 1f - _zw);
                _zy = Clamp(_zoneStart.Y + dy, 0f, 1f - _zh);
                UpdateRatioLabel();
                _canvas.Invalidate();
                _canvas.Cursor = Cursors.SizeAll;
            }
            else if (_resizing)
            {
                _zw = Clamp(_sizeStart.Width  + dx, 0.02f, 1f - _zx);
                _zh = Clamp(_sizeStart.Height + dy, 0.01f, 1f - _zy);
                UpdateRatioLabel();
                _canvas.Invalidate();
                _canvas.Cursor = Cursors.SizeNWSE;
            }
            else
            {
                // Cursor hint
                var zone   = GetZoneRect();
                var handle = GetHandleRect(zone);
                _canvas.Cursor = handle.Contains(e.Location) ? Cursors.SizeNWSE
                               : zone.Contains(e.Location)   ? Cursors.SizeAll
                               : Cursors.Default;
            }
        }

        private void Canvas_MouseUp(object sender, MouseEventArgs e)
        {
            _dragging  = false;
            _resizing  = false;
            _canvas.Cursor = Cursors.Default;
        }

        // ── Button handlers ───────────────────────────────────────────────
        private void BtnLoad_Click(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title  = "เลือกภาพตัวอย่าง",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.tif;*.tiff"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try
            {
                _image?.Dispose();
                _image = new Bitmap(new MemoryStream(File.ReadAllBytes(dlg.FileName)));
                _btnPreview.Enabled = true;
                _canvas.Invalidate();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"โหลดภาพไม่ได้: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnPreview_Click(object sender, EventArgs e)
        {
            if (_image == null) return;
            _btnPreview.Enabled  = false;
            _lblOcrResult.Text   = "กำลัง OCR...";
            _lblOcrResult.ForeColor = Color.FromArgb(100, 116, 139);

            try
            {
                // Save image to temp file
                string tmpPath = Path.Combine(Path.GetTempPath(), $"emrscan_zone_{Guid.NewGuid():N}.jpg");
                _image.Save(tmpPath, System.Drawing.Imaging.ImageFormat.Jpeg);

                string result = OcrHelper.ReadOcrPk(tmpPath, _zx, _zy, _zw, _zh);
                File.Delete(tmpPath);

                if (string.IsNullOrEmpty(result))
                {
                    _lblOcrResult.Text      = "ไม่พบ OCRPK";
                    _lblOcrResult.ForeColor = Color.FromArgb(220, 38, 38);
                }
                else
                {
                    _lblOcrResult.Text      = result;
                    _lblOcrResult.ForeColor = Color.FromArgb(22, 163, 74);
                }
            }
            catch (Exception ex)
            {
                _lblOcrResult.Text      = $"Error: {ex.Message}";
                _lblOcrResult.ForeColor = Color.FromArgb(220, 38, 38);
            }
            finally
            {
                _btnPreview.Enabled = true;
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            AppConfig.SaveOcrZone(_zx, _zy, _zw, _zh);
            _lblStatus.Text      = "✔ บันทึกแล้ว";
            _lblStatus.ForeColor = Color.FromArgb(22, 163, 74);
        }

        private void UpdateRatioLabel()
        {
            _lblRatios.Text = GetRatioText();
        }

        private string GetRatioText()
            => $"X = {_zx:F4}\nY = {_zy:F4}\nW = {_zw:F4}\nH = {_zh:F4}";

        private static float Clamp(float v, float min, float max)
            => v < min ? min : v > max ? max : v;

        private Button MakeBtn(string text, Color back, Point loc, int w)
        {
            var b = new Button
            {
                Text = text, Location = loc, Size = new Size(w, 32),
                BackColor = back, ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _image?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
