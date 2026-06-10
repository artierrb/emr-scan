using System;
using System.Drawing;
using System.Windows.Forms;

namespace EMRScan
{
    public class SettingsForm : Form
    {
        public int  SelectedDpi   { get; private set; }
        public bool SelectedColor { get; private set; }

        private ComboBox _cbDpi;
        private RadioButton _rbColor, _rbBw;

        public SettingsForm(int currentDpi, bool currentColor)
        {
            Text            = "Settings — Scanner";
            Size            = new Size(320, 220);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            BackColor       = Color.White;

            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };
            Controls.Add(panel);

            // DPI
            panel.Controls.Add(new Label { Text = "Resolution (DPI)", Location = new Point(0, 0), AutoSize = true });
            _cbDpi = new ComboBox
            {
                Location      = new Point(0, 22),
                Width         = 260,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cbDpi.Items.AddRange(new object[] { 150, 200, 300, 400, 600 });
            _cbDpi.SelectedItem = currentDpi;
            if (_cbDpi.SelectedIndex < 0) _cbDpi.SelectedItem = 300;
            panel.Controls.Add(_cbDpi);

            // Color mode
            panel.Controls.Add(new Label { Text = "โหมดสี", Location = new Point(0, 60), AutoSize = true });
            _rbColor = new RadioButton { Text = "สี (Color)",      Location = new Point(0,  80), AutoSize = true, Checked = currentColor };
            _rbBw    = new RadioButton { Text = "ขาวดำ (Grayscale)", Location = new Point(0, 105), AutoSize = true, Checked = !currentColor };
            panel.Controls.Add(_rbColor);
            panel.Controls.Add(_rbBw);

            // Buttons
            var btnOk = new Button
            {
                Text      = "ตกลง",
                Size      = new Size(120, 32),
                Location  = new Point(0, 140),
                BackColor = Color.FromArgb(26, 79, 122),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) =>
            {
                SelectedDpi   = (int)_cbDpi.SelectedItem;
                SelectedColor = _rbColor.Checked;
                DialogResult  = DialogResult.OK;
                Close();
            };
            panel.Controls.Add(btnOk);

            var btnCancel = new Button
            {
                Text      = "ยกเลิก",
                Size      = new Size(120, 32),
                Location  = new Point(130, 140),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9)
            };
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            panel.Controls.Add(btnCancel);

            AcceptButton = btnOk;
        }
    }
}
