using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EMRScan
{
    /// <summary>
    /// Dialog shown when scanning a multi-page form.
    /// Shows which pages are already scanned and lets user pick a page to scan/re-scan.
    /// </summary>
    public class PagePickerDialog : Form
    {
        public int SelectedPage { get; private set; } = 0;

        static readonly Color Navy      = Color.FromArgb(26,  55,  92);
        static readonly Color NavyDark  = Color.FromArgb(17,  36,  61);
        static readonly Color Green     = Color.FromArgb(22,  163,  74);
        static readonly Color Amber     = Color.FromArgb(217, 119,   6);
        static readonly Color BgPage    = Color.FromArgb(241, 245, 249);
        static readonly Color Border    = Color.FromArgb(214, 219, 230);

        public PagePickerDialog(string ocrPk, string formCode, int pageCount, List<int> scannedPages)
        {
            Text            = "เลือกหน้าที่ต้องการ scan";
            Size            = new Size(420, 320);
            MinimumSize     = new Size(380, 280);
            StartPosition   = FormStartPosition.CenterParent;
            BackColor       = BgPage;
            Font            = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;

            // Header
            var header = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Navy };
            var lblTitle = new Label
            {
                Text      = $"ฟอร์ม: {formCode}",
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 11f, FontStyle.Bold),
                AutoSize  = true,
                Location  = new Point(14, 10)
            };
            var lblSub = new Label
            {
                Text      = $"OCRPK: {ocrPk}   |   {pageCount} หน้า",
                ForeColor = Color.FromArgb(180, 210, 240),
                Font      = new Font("Segoe UI", 8f),
                AutoSize  = true,
                Location  = new Point(14, 32)
            };
            header.Controls.Add(lblTitle);
            header.Controls.Add(lblSub);

            // Instruction
            var lblInstr = new Label
            {
                Text      = "เลือกหน้าที่ต้องการ scan (สีเหลือง = scan แล้ว, สีขาว = ยังไม่ได้ scan)",
                ForeColor = Color.FromArgb(71, 85, 105),
                Font      = new Font("Segoe UI", 8.5f),
                AutoSize  = false,
                Size      = new Size(380, 36),
                Location  = new Point(14, 66),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // Page buttons panel
            var pagesPanel = new FlowLayoutPanel
            {
                Location    = new Point(14, 106),
                Size        = new Size(380, 130),
                AutoScroll  = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents  = true,
                Padding       = new Padding(4)
            };

            for (int i = 1; i <= pageCount; i++)
            {
                bool isScanned = scannedPages.Contains(i);
                int  pageNum   = i; // capture for closure

                var btn = new Button
                {
                    Text      = $"หน้า {pageNum}",
                    Size      = new Size(110, 48),
                    FlatStyle = FlatStyle.Flat,
                    Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
                    Cursor    = Cursors.Hand,
                    Margin    = new Padding(4),
                    BackColor = isScanned ? Color.FromArgb(254, 243, 199) : Color.White,
                    ForeColor = isScanned ? Amber : NavyDark,
                    Tag       = isScanned
                };
                btn.FlatAppearance.BorderColor = isScanned ? Amber : Border;
                btn.FlatAppearance.BorderSize  = isScanned ? 2 : 1;

                // Sub-label
                string subText = isScanned ? "⟳ scan ซ้ำ" : "＋ scan ใหม่";
                btn.Text = $"หน้า {pageNum}\n{subText}";

                btn.Click += (s, e) =>
                {
                    SelectedPage   = pageNum;
                    DialogResult   = DialogResult.OK;
                };
                pagesPanel.Controls.Add(btn);
            }

            // Bottom buttons
            var btnCancel = new Button
            {
                Text      = "ยกเลิก",
                Size      = new Size(90, 32),
                Location  = new Point(14, 248),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 8.5f),
                BackColor = Color.FromArgb(71, 85, 105),
                ForeColor = Color.White,
                Cursor    = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; };

            Controls.Add(header);
            Controls.Add(lblInstr);
            Controls.Add(pagesPanel);
            Controls.Add(btnCancel);
        }
    }
}
