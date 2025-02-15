using System;
using System.Linq;
using System.Windows.Forms;

namespace ai_clipboard
{
    public class OptionsForm : Form
    {
        private CheckBox includeBinariesCheckBox;
        private TextBox ignorePatternsTextBox;
        private Button saveButton;
        private Button cancelButton;

        // Reference to the calling form's config
        private UserConfig configRef;

        public OptionsForm(UserConfig config)
        {
            configRef = config;

            this.Text = "Options";
            this.Width = 600;
            this.Height = 400;
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // =============== Layout ===============
            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };
            this.Controls.Add(mainPanel);

            // We'll use a TableLayoutPanel or just a FlowLayoutPanel
            var layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true
            };
            mainPanel.Controls.Add(layout);

            // 1) Include Binaries
            includeBinariesCheckBox = new CheckBox
            {
                Text = "Include binary files?",
                AutoSize = true,
                Checked = configRef.IncludeBinaries // load from config
            };
            layout.Controls.Add(includeBinariesCheckBox);

            // 2) Ignore Patterns (multiline)
            var label = new Label
            {
                Text = "Ignore Patterns (.gitignore style):",
                AutoSize = true
            };
            layout.Controls.Add(label);

            ignorePatternsTextBox = new TextBox
            {
                Multiline = true,
                Width = 550,
                Height = 200,
                ScrollBars = ScrollBars.Vertical
            };
            // Convert the list of ignore patterns into lines
            if (configRef.IgnorePatterns != null && configRef.IgnorePatterns.Count > 0)
            {
                ignorePatternsTextBox.Text = string.Join(Environment.NewLine, configRef.IgnorePatterns);
            }
            layout.Controls.Add(ignorePatternsTextBox);

            // 3) Buttons at bottom
            var buttonsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 40
            };
            mainPanel.Controls.Add(buttonsPanel);

            saveButton = new Button
            {
                Text = "Save",
                Width = 80,
                Height = 30
            };
            saveButton.Click += SaveButton_Click!;
            buttonsPanel.Controls.Add(saveButton);

            cancelButton = new Button
            {
                Text = "Cancel",
                Width = 80,
                Height = 30
            };
            cancelButton.Click += CancelButton_Click!;
            buttonsPanel.Controls.Add(cancelButton);
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            // Update configRef with the new settings
            configRef.IncludeBinaries = includeBinariesCheckBox.Checked;

            // Parse the multiline text for ignore patterns
            var lines = ignorePatternsTextBox.Text.Split(
                new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries
            ).Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();

            configRef.IgnorePatterns = lines;

            // Return OK result
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void CancelButton_Click(object? sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
