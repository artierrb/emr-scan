using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace EMRScan
{
    /// <summary>
    /// Shows all approved scan items with file preview thumbnails.
    /// User assigns page sequence per file before confirming save.
    /// </summary>
    public class ConfirmScanDialog : Form
    {
        private readonly List<ScanItem> _items;
        private readonly List<ComboBox> _pageComboBoxes = new List<ComboBox>();

        static readonly Color Navy     = Color.FromArgb(26,  55,  92);
        static readonly Color NavyDark = Color.FromArgb(17,  36,  61);
        static readonly Color Green    = Color.FromArgb(22,  163,  74);
        static readonly Color GreenDk  = Color.FromArgb(15,  118,  53);
        static readonly Color BgPage   = Color.FromArgb(241, 245, 249);
        static readonly Color BgCard   = Color.White;
        static readonly Color Border   = Color.FromArgb(214, 219, 230);
        static readonly Color Amber    = Color.FromArgb(217, 119,   6);
        static readonly Color AmberBg  = Color.FromArgb(254, 243, 199);
        static readonly Color GrayText = Color.FromArgb(100, 116, 139);

        public ConfirmScanDialog(List<ScanItem> items)
        {
            _items          = items;
            Text            = "ยืนยันการ Scan";
            Size            = new Size(700, 520);
            MinimumSize     = new Size(600, 400);
            StartPosition   = FormStartPosition.CenterParent;
            BackColor       = BgPage;
            Font            = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox     = false;
            BuildUI();
        }

        private void BuildUI()
        {
            SuspendLayout();

            // ── Header ────────────────────────────────────────────────────
            var header = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Navy };
            var lblTitle = new Label
            {
                Text      = $"ยืนยันการ Scan — {_items.Count} รายการ",
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 11f, FontStyle.Bold),
                AutoSize  = true, Location = new Point(14, 10)
            };
            var lblSub = new Label
            {
                Text      = "กำหนดหมายเลขหน้าให้แต่ละไฟล์ก่อนกด ยืนยัน",
                ForeColor = Color.FromArgb(180, 210, 240),
                Font      = new Font("Segoe UI", 8f),
                AutoSize  = true, Location = new Point(14, 32)
            };
            header.Controls.Add(lblTitle);
            header.Controls.Add(lblSub);

            // ── Bottom button bar ─────────────────────────────────────────
            var btnBar = new Panel
            {
                Dock = DockStyle.Bottom, Height = 52,
                BackColor = BgCard
            };
            btnBar.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, 0, btnBar.Width, 0);

            var btnOk = new Button
            {
                Text      = "✔  ยืนยัน บันทึก",
                Size      = new Size(140, 34),
                Location  = new Point(12, 9),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
                BackColor = Green, ForeColor = Color.White,
                Cursor    = Cursors.Hand
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += BtnOk_Click;

            var btnCancel = new Button
            {
                Text      = "ยกเลิก",
                Size      = new Size(90, 34),
                Location  = new Point(162, 9),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9f),
                BackColor = Color.FromArgb(71, 85, 105),
                ForeColor = Color.White,
                Cursor    = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

            btnBar.Controls.Add(btnOk);
            btnBar.Controls.Add(btnCancel);

            // ── Scrollable item list ──────────────────────────────────────
            var scroll = new Panel
            {
                Dock       = DockStyle.Fill,
                AutoScroll = true,
                BackColor  = BgPage,
                Padding    = new Padding(12, 8, 12, 8)
            };

            int yPos = 8;
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                var card = BuildItemCard(item, i, ref yPos);
                scroll.Controls.Add(card);
            }
            scroll.AutoScrollMinSize = new Size(0, yPos + 8);

            Controls.Add(scroll);
            Controls.Add(btnBar);
            Controls.Add(header);

            ResumeLayout(false);
        }

        private Panel BuildItemCard(ScanItem item, int index, ref int yPos)
        {
            var card = new Panel
            {
                Location  = new Point(8, yPos),
                Size      = new Size(640, 100),
                BackColor = BgCard,
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            card.Paint += (s, e) =>
            {
                var g  = e.Graphics;
                var rc = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                g.DrawRectangle(new Pen(Border), rc);
                // left accent bar
                g.FillRectangle(new SolidBrush(Color.FromArgb(26, 55, 92)), 0, 0, 4, card.Height);
            };

            // Thumbnail
            var thumb = new PictureBox
            {
                Location  = new Point(12, 10),
                Size      = new Size(72, 80),
                SizeMode  = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            try
            {
                if (File.Exists(item.ImagePath))
                    thumb.Image = Image.FromStream(
                        new System.IO.MemoryStream(File.ReadAllBytes(item.ImagePath)));
            }
            catch { }

            // File info
            var lblFile = new Label
            {
                Text      = Path.GetFileName(item.ImagePath),
                Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = NavyDark,
                AutoSize  = false,
                Size      = new Size(360, 20),
                Location  = new Point(94, 10)
            };
            var lblOcr = new Label
            {
                Text      = $"OCRPK: {item.OcrPk}",
                Font      = new Font("Segoe UI", 8f),
                ForeColor = GrayText,
                AutoSize  = true,
                Location  = new Point(94, 32)
            };
            var lblForm = new Label
            {
                Text      = $"Form: {item.FormCode}   HN: {item.Hn}",
                Font      = new Font("Segoe UI", 8f),
                ForeColor = GrayText,
                AutoSize  = true,
                Location  = new Point(94, 50)
            };

            // Page selector
            var lblPage = new Label
            {
                Text      = "หน้าที่:",
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = NavyDark,
                AutoSize  = true,
                Location  = new Point(94, 72)
            };

            var cbo = new ComboBox
            {
                Location      = new Point(148, 68),
                Size          = new Size(120, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Segoe UI", 9f),
                FlatStyle     = FlatStyle.Flat
            };

            // Populate page options
            for (int p = 1; p <= item.PageCount; p++)
                cbo.Items.Add($"หน้า {p}");

            // Pre-select: next unscanned page or page 1
            int preSelect = item.PageSeq > 0 ? item.PageSeq - 1 : 0;
            cbo.SelectedIndex = Math.Min(preSelect, cbo.Items.Count - 1);

            // Warn if page already scanned
            var lblWarn = new Label
            {
                Text      = "",
                Font      = new Font("Segoe UI", 7.5f),
                ForeColor = Amber,
                AutoSize  = true,
                Location  = new Point(278, 72)
            };

            // Query scanned pages for this OCRPK to show warning
            List<int> scannedPages = new List<int>();
            try
            {
                var info = DbHelper.GetOcrPkPageInfo(item.OcrPk);
                if (info != null) scannedPages = info.Value.scannedPages;
            }
            catch { }

            void UpdateWarn()
            {
                int sel = cbo.SelectedIndex + 1;
                lblWarn.Text = scannedPages.Contains(sel)
                    ? "⚠ หน้านี้ scan แล้ว (จะ overwrite)"
                    : "";
            }
            UpdateWarn();
            cbo.SelectedIndexChanged += (s, e) => UpdateWarn();

            _pageComboBoxes.Add(cbo);

            card.Controls.Add(thumb);
            card.Controls.Add(lblFile);
            card.Controls.Add(lblOcr);
            card.Controls.Add(lblForm);
            card.Controls.Add(lblPage);
            card.Controls.Add(cbo);
            card.Controls.Add(lblWarn);

            yPos += 110;
            return card;
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            // Apply selected page seq back to items
            for (int i = 0; i < _items.Count && i < _pageComboBoxes.Count; i++)
                _items[i].PageSeq = _pageComboBoxes[i].SelectedIndex + 1;

            DialogResult = DialogResult.OK;
        }
    }
}
