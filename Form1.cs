using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json; // For serialization
using System.Windows.Forms;
using System.Drawing;   // For Font, Brushes, etc.

namespace ai_clipboard
{
    public class Form1 : Form
    {
        // Where we store user preferences
        private const string ConfigPath = "userconfig.json";

        // We'll track the user-chosen root folder for relative paths
        private string? selectedRootFolder = null;

        // For saving the original copy button text
        private readonly string originalCopyButtonText;

        // A list of the top-level buttons/panels
        private TableLayoutPanel topPanel; // TableLayoutPanel for a 2-row layout
        private Button selectFolderButton;
        private Button resetButton;
        private Button selectAllButton;
        private Button copyButton;
        private Button optionsButton;
        private Button refreshButton;

        // The main TreeView
        private TreeView fileTree;

        // Timer for “Files copied!” feedback
        private System.Windows.Forms.Timer? copyFeedbackTimer;

        // We'll keep a reference to the user's config once loaded
        private UserConfig userConfig = new UserConfig();

        // ComboBox for previously selected projects
        private ComboBox projectHistoryComboBox;

        public Form1()
        {
            // Basic window setup
            this.Text = "AI Clipboard";
            this.Width = 800;
            this.Height = 600;

            // =============== TOP PANEL (a TableLayoutPanel) ===============
            topPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 5,
                RowCount = 2,
                Padding = new Padding(0)
            };

            // First row: 5 buttons
            // Second row: the ComboBox
            for (int i = 0; i < 5; i++)
            {
                // Each column is auto-size for the buttons
                topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            }

            // First row: auto-size (buttons)
            topPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            // Second row: fixed height for the ComboBox
            topPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));

            this.Controls.Add(topPanel);

            // 1) "Select Folder" button
            selectFolderButton = new Button
            {
                Text = "Select Folder...",
                Width = 120,
                Height = 36,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            selectFolderButton.Click += SelectFolderButton_Click!;
            topPanel.Controls.Add(selectFolderButton, 0, 0);

            // 2) "Reset Selections" button
            resetButton = new Button
            {
                Text = "Reset Selections",
                Width = 120,
                Height = 36,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            resetButton.Click += ResetButton_Click!;
            topPanel.Controls.Add(resetButton, 1, 0);

            // 3) "Select All" button
            selectAllButton = new Button
            {
                Text = "Select All",
                Width = 120,
                Height = 36,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            selectAllButton.Click += SelectAllButton_Click!;
            topPanel.Controls.Add(selectAllButton, 2, 0);

            // 4) "Options" button
            optionsButton = new Button
            {
                Text = "Options...",
                Width = 120,
                Height = 36,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            optionsButton.Click += OptionsButton_Click!;
            topPanel.Controls.Add(optionsButton, 3, 0);

            // 5) "Refresh" button
            refreshButton = new Button
            {
                Text = "Refresh",
                Width = 120,
                Height = 36,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            refreshButton.Click += RefreshButton_Click!;
            topPanel.Controls.Add(refreshButton, 4, 0);

            // =============== PROJECT HISTORY COMBOBOX (second row) ===============
            projectHistoryComboBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                DrawMode = DrawMode.OwnerDrawFixed,
                IntegralHeight = false,
                ItemHeight = 30  // Enough space for bold + path
            };
            projectHistoryComboBox.SelectedIndexChanged += ProjectHistoryComboBox_SelectedIndexChanged!;
            projectHistoryComboBox.DrawItem += ProjectHistoryComboBox_DrawItem!;

            // Place ComboBox in row=1, col=0, spanning 5 columns
            topPanel.SetColumnSpan(projectHistoryComboBox, 5);
            topPanel.Controls.Add(projectHistoryComboBox, 0, 1);

            // =============== "COPY" BUTTON (docked at bottom) ===============
            copyButton = new Button
            {
                Text = "Copy Selected Files to Clipboard",
                Dock = DockStyle.Bottom,
                Height = 40
            };
            copyButton.Click += CopyButton_Click!;
            this.Controls.Add(copyButton);
            originalCopyButtonText = copyButton.Text;

            // =============== MAIN PANEL (for the TreeView) ===============
            var treePanel = new Panel
            {
                Dock = DockStyle.Fill,
                // Increased top padding so the root node isn't hidden
                Padding = new Padding(0, 80, 0, 40)
            };
            this.Controls.Add(treePanel);

            // =============== TREEVIEW ===============
            fileTree = new TreeView
            {
                Dock = DockStyle.Fill,
                CheckBoxes = true
            };
            fileTree.AfterCheck += FileTree_AfterCheck!;
            treePanel.Controls.Add(fileTree);

            // =============== EVENTS FOR LOADING/SAVING USER SETTINGS ===============
            this.Load += Form1_Load!;
            this.FormClosing += Form1_FormClosing!;
        }

        // =============== FORM LOAD & SAVE ===============
        private void Form1_Load(object? sender, EventArgs e)
        {
            // Attempt to load config from disk
            if (File.Exists(ConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigPath);
                    var cfg = JsonSerializer.Deserialize<UserConfig>(json);
                    if (cfg != null)
                    {
                        userConfig = cfg;
                    }
                }
                catch
                {
                    // Ignore any errors loading config
                }
            }

            // If the user config doesn't have any ignore patterns, fill with defaults
            if (userConfig.IgnorePatterns == null || userConfig.IgnorePatterns.Count == 0)
            {
                userConfig.IgnorePatterns = UserConfig.GetDefaultIgnorePatterns();
            }

            // Load previously selected projects into ComboBox
            if (userConfig.PreviousProjects != null && userConfig.PreviousProjects.Count > 0)
            {
                foreach (var projectPath in userConfig.PreviousProjects)
                {
                    projectHistoryComboBox.Items.Add(new ProjectItem(projectPath));
                }
            }

            // If no LastFolder or doesn't exist, do nothing more
            if (!string.IsNullOrEmpty(userConfig.LastFolder) && Directory.Exists(userConfig.LastFolder))
            {
                selectedRootFolder = userConfig.LastFolder;

                fileTree.Nodes.Clear();
                LoadDirectoryIntoTree(selectedRootFolder, fileTree.Nodes);

                if (userConfig.CheckedFiles.Count > 0)
                {
                    MarkCheckedFiles(fileTree.Nodes, userConfig.CheckedFiles);
                }
            }
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            // If there's at least one root node, store its Tag as the last folder
            if (fileTree.Nodes.Count > 0 && fileTree.Nodes[0].Tag is string topPath)
            {
                userConfig.LastFolder = topPath;
            }

            // Gather a list of all checked files
            userConfig.CheckedFiles.Clear();
            GatherCheckedFilesList(fileTree.Nodes, userConfig.CheckedFiles);

            if (userConfig.PreviousProjects == null)
            {
                userConfig.PreviousProjects = new System.Collections.Generic.List<string>();
            }

            // Add the last folder if not already in the list
            if (!string.IsNullOrEmpty(userConfig.LastFolder) &&
                !userConfig.PreviousProjects.Contains(userConfig.LastFolder))
            {
                userConfig.PreviousProjects.Add(userConfig.LastFolder);
            }

            // Write out the JSON
            try
            {
                string json = JsonSerializer.Serialize(
                    userConfig, new JsonSerializerOptions { WriteIndented = true }
                );
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // Log or ignore
            }
        }

        // =============== SELECT FOLDER LOGIC ===============
        private void SelectFolderButton_Click(object? sender, EventArgs e)
        {
            using var folderDialog = new FolderBrowserDialog
            {
                Description = "Select a folder to load into the tree"
            };
            if (folderDialog.ShowDialog() == DialogResult.OK)
            {
                fileTree.Nodes.Clear();
                selectedRootFolder = folderDialog.SelectedPath;
                LoadDirectoryIntoTree(selectedRootFolder, fileTree.Nodes);
            }
        }

        // =============== LOAD DIRECTORY & FILES INTO TREE ===============
        private void LoadDirectoryIntoTree(string path, TreeNodeCollection parentNodes)
        {
            if (!Directory.Exists(path)) return;

            string folderName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(folderName))
            {
                folderName = path; // e.g. "C:\"
            }

            if (ShouldIgnoreDirectory(folderName, path))
            {
                return;
            }

            var dirNode = new TreeNode(folderName) { Tag = path };
            parentNodes.Add(dirNode);

            string[] subDirs = Array.Empty<string>();
            try
            {
                subDirs = Directory.GetDirectories(path);
                foreach (var directory in subDirs)
                {
                    LoadDirectoryIntoTree(directory, dirNode.Nodes);
                }
            }
            catch
            {
                // ignore
            }

            if (subDirs.Length <= 10)
            {
                dirNode.Expand();
            }

            try
            {
                foreach (var file in Directory.GetFiles(path))
                {
                    if (ShouldIgnoreFile(file))
                    {
                        continue;
                    }

                    if (!userConfig.IncludeBinaries && !IsLikelyTextFile(file))
                    {
                        continue;
                    }

                    string fileName = Path.GetFileName(file);
                    var fileNode = new TreeNode(fileName) { Tag = file };
                    dirNode.Nodes.Add(fileNode);
                }
            }
            catch
            {
                // ignore
            }
        }

        // =============== IGNORE CHECKS ===============
        private bool ShouldIgnoreDirectory(string folderName, string fullPath)
        {
            if (userConfig.IgnorePatterns == null) return false;

            string pathForCheck = fullPath.Replace('\\', '/').ToLowerInvariant();

            foreach (var pattern in userConfig.IgnorePatterns)
            {
                string p = pattern.Trim().ToLowerInvariant();

                if (p.StartsWith("/") || p.Contains("/"))
                {
                    if (pathForCheck.Contains(p))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool ShouldIgnoreFile(string filePath)
        {
            if (userConfig.IgnorePatterns == null) return false;

            string lowerFile = filePath.Replace('\\', '/').ToLowerInvariant();
            string fileName = Path.GetFileName(lowerFile);

            foreach (var pattern in userConfig.IgnorePatterns)
            {
                string p = pattern.Trim().ToLowerInvariant();

                if (p.Contains("/"))
                {
                    if (lowerFile.Contains(p))
                    {
                        return true;
                    }
                }
                else
                {
                    if (fileName.EndsWith(p) || fileName.Equals(p))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // =============== HELPER: DETERMINE IF A FILE IS LIKELY TEXT ===============
        private bool IsLikelyTextFile(string filePath)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                int length = (int)Math.Min(fs.Length, 1024);
                byte[] buffer = new byte[length];

                int readCount = fs.Read(buffer, 0, length);

                int nonTextCount = 0;
                for (int i = 0; i < readCount; i++)
                {
                    byte b = buffer[i];
                    if (b == 0) return false;
                    bool isNormalTextChar =
                        (b >= 32 && b <= 126) ||
                        b == 9 || b == 10 || b == 13;
                    if (!isNormalTextChar)
                    {
                        nonTextCount++;
                    }
                }

                double ratio = (double)nonTextCount / readCount;
                return ratio <= 0.20;
            }
            catch
            {
                // If we can't read it, treat as non-text
                return false;
            }
        }

        // =============== TREEVIEW AFTER-CHECK (recursively check/uncheck children) ===============
        private void FileTree_AfterCheck(object? sender, TreeViewEventArgs e)
        {
            if (e.Action == TreeViewAction.Unknown) return;
            if (e.Node == null) return;

            CheckAllChildren(e.Node, e.Node.Checked);
        }

        private void CheckAllChildren(TreeNode node, bool isChecked)
        {
            foreach (TreeNode child in node.Nodes)
            {
                child.Checked = isChecked;
                CheckAllChildren(child, isChecked);
            }
        }

        // =============== COPY CHECKED FILES ===============
        private void CopyButton_Click(object? sender, EventArgs e)
        {
            var sb = new StringBuilder();
            int fileCount = 0;

            foreach (TreeNode rootNode in fileTree.Nodes)
            {
                fileCount += CollectCheckedFiles(rootNode, sb);
            }

            if (fileCount > 0)
            {
                Clipboard.SetText(sb.ToString());
                ShowCopyFeedback("Files copied!");
            }
            else
            {
                ShowCopyFeedback("No files copied.");
            }
        }

        private int CollectCheckedFiles(TreeNode node, StringBuilder sb)
        {
            int count = 0;

            if (node.Checked && node.Tag is string filePath && File.Exists(filePath))
            {
                try
                {
                    string rootName = Path.GetFileName(selectedRootFolder ?? "");
                    if (string.IsNullOrEmpty(rootName))
                    {
                        rootName = selectedRootFolder ?? "";
                    }

                    string relativePart = filePath;
                    if (!string.IsNullOrEmpty(selectedRootFolder))
                    {
                        relativePart = Path.GetRelativePath(selectedRootFolder, filePath);
                    }

                    string displayPath = Path.Combine(rootName, relativePart);

                    sb.AppendLine($"### START {displayPath}");
                    sb.AppendLine(File.ReadAllText(filePath));
                    sb.AppendLine($"### END {displayPath}");
                    sb.AppendLine();

                    count++;
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Error reading {filePath}: {ex.Message}");
                }
            }

            foreach (TreeNode child in node.Nodes)
            {
                count += CollectCheckedFiles(child, sb);
            }

            return count;
        }

        private void ShowCopyFeedback(string message)
        {
            copyButton.Text = message;

            if (copyFeedbackTimer == null)
            {
                copyFeedbackTimer = new System.Windows.Forms.Timer
                {
                    Interval = 2000
                };
                copyFeedbackTimer.Tick += (s, e) =>
                {
                    copyButton.Text = originalCopyButtonText;
                    copyFeedbackTimer.Stop();
                };
            }
            copyFeedbackTimer.Start();
        }

        // =============== RESET SELECTIONS ===============
        private void ResetButton_Click(object? sender, EventArgs e)
        {
            UncheckAllNodes(fileTree.Nodes);
        }

        private void UncheckAllNodes(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                node.Checked = false;
                UncheckAllNodes(node.Nodes);
            }
        }

        // =============== SELECT ALL ===============
        private void SelectAllButton_Click(object? sender, EventArgs e)
        {
            CheckAllNodes(fileTree.Nodes);
        }

        private void CheckAllNodes(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                node.Checked = true;
                CheckAllNodes(node.Nodes);
            }
        }

        // =============== OPTIONS BUTTON CLICK ===============
        private void OptionsButton_Click(object? sender, EventArgs e)
        {
            using var form = new OptionsForm(userConfig);
            if (form.ShowDialog() == DialogResult.OK)
            {
                if (!string.IsNullOrEmpty(selectedRootFolder) && Directory.Exists(selectedRootFolder))
                {
                    fileTree.Nodes.Clear();
                    LoadDirectoryIntoTree(selectedRootFolder, fileTree.Nodes);
                }
            }
        }

        // =============== REFRESH BUTTON CLICK ===============
        private void RefreshButton_Click(object? sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(selectedRootFolder) && Directory.Exists(selectedRootFolder))
            {
                var checkedFiles = new System.Collections.Generic.List<string>();
                GatherCheckedFilesList(fileTree.Nodes, checkedFiles);

                fileTree.Nodes.Clear();
                LoadDirectoryIntoTree(selectedRootFolder, fileTree.Nodes);

                MarkCheckedFiles(fileTree.Nodes, checkedFiles);
            }
        }

        // =============== GATHER + MARK CHECKED FILES ===============
        private void GatherCheckedFilesList(TreeNodeCollection nodes, System.Collections.Generic.List<string> list)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Checked && node.Tag is string filePath && File.Exists(filePath))
                {
                    list.Add(filePath);
                }
                GatherCheckedFilesList(node.Nodes, list);
            }
        }

        private void MarkCheckedFiles(TreeNodeCollection nodes, System.Collections.Generic.List<string> checkedPaths)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag is string path && checkedPaths.Contains(path))
                {
                    node.Checked = true;
                }
                MarkCheckedFiles(node.Nodes, checkedPaths);
            }
        }

        // =============== OWNER-DRAW COMBOBOX ===============
        private void ProjectHistoryComboBox_DrawItem(object? sender, DrawItemEventArgs e)
        {
            // If the event is out of range, bail
            if (e.Index < 0) return;
            // If the sender isn't a ComboBox, bail
            if (sender is not ComboBox combo) return;
            // If index is somehow outside the item range, bail
            if (e.Index >= combo.Items.Count) return;

            e.DrawBackground();

            // Safely retrieve the item
            var rawItem = combo.Items[e.Index];
            if (rawItem is not ProjectItem item)
            {
                e.DrawFocusRectangle();
                return;
            }

            // e.Font can be null, so we coalesce to a default
            var usedFont = e.Font ?? SystemFonts.DefaultFont;
            // Then create a bold font
            using var boldFont = new Font(usedFont, FontStyle.Bold);

            // Safely get the title/path strings
            string title = item.ProjectName ?? "";
            string path = item.ProjectPath ?? "";

            // Align text vertically centered
            var sf = new StringFormat
            {
                LineAlignment = StringAlignment.Center,
                Alignment = StringAlignment.Near
            };

            // Measure the bold portion
            var titleSize = e.Graphics.MeasureString(title, boldFont);

            var titleRect = new RectangleF(e.Bounds.X, e.Bounds.Y, e.Bounds.Width, e.Bounds.Height);
            e.Graphics.DrawString(title, boldFont, Brushes.Black, titleRect, sf);

            float offsetX = e.Bounds.X + titleSize.Width;
            var pathRect = new RectangleF(offsetX, e.Bounds.Y, e.Bounds.Width - offsetX, e.Bounds.Height);
            e.Graphics.DrawString(" - " + path, usedFont, Brushes.Black, pathRect, sf);

            e.DrawFocusRectangle();
        }

        // =============== PROJECT HISTORY COMBOBOX SELECTION ===============
        private void ProjectHistoryComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (projectHistoryComboBox.SelectedItem is ProjectItem selectedItem &&
                Directory.Exists(selectedItem.ProjectPath))
            {
                selectedRootFolder = selectedItem.ProjectPath;

                fileTree.Nodes.Clear();
                LoadDirectoryIntoTree(selectedRootFolder, fileTree.Nodes);
            }
        }

        // =============== INTERNAL CLASS FOR COMBOBOX ITEMS ===============
        private class ProjectItem
        {
            public string ProjectName { get; }
            public string ProjectPath { get; }

            public ProjectItem(string fullPath)
            {
                ProjectPath = fullPath;
                string name = Path.GetFileName(fullPath.TrimEnd('\\', '/'));
                if (string.IsNullOrEmpty(name))
                {
                    name = fullPath;
                }
                ProjectName = name;
            }

            public override string ToString()
            {
                return $"{ProjectName} - {ProjectPath}";
            }
        }
    }
}
