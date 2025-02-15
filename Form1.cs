using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json; // For serialization
using System.Windows.Forms;

namespace ai_clipboard
{
    public class Form1 : Form
    {
        // Where we store user preferences
        private const string ConfigPath = "userconfig.json";

        // A list of directories to skip
        private static readonly string[] IgnoredDirectories = new string[]
        {
            ".git", "obj", "bin", "node_modules", ".github", ".next"
        };

        // A list (or set) of image file extensions to skip
        private static readonly string[] ImageExtensions = new string[]
        {
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff", ".webp", ".svg"
        };

        private FlowLayoutPanel topPanel;
        private Button selectFolderButton;
        private Button resetButton;
        private Button selectAllButton;
        private Button copyButton;
        private TreeView fileTree;

        // We'll track the user-chosen root folder for relative paths
        private string? selectedRootFolder = null;

        // Use the fully qualified name so there's no ambiguity
        private System.Windows.Forms.Timer? copyFeedbackTimer;

        // For saving the original copy button text
        private readonly string originalCopyButtonText;

        public Form1()
        {
            // Basic window setup
            this.Text = "AI Clipboard";
            this.Width = 800;
            this.Height = 600;

            // =============== TOP PANEL (for multiple buttons) ===============
            topPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 40,
                FlowDirection = FlowDirection.LeftToRight
            };
            this.Controls.Add(topPanel);

            // 1) "Select Folder" button
            selectFolderButton = new Button
            {
                Text = "Select Folder...",
                Width = 120,
                Height = 36
            };
            selectFolderButton.Click += SelectFolderButton_Click!;
            topPanel.Controls.Add(selectFolderButton);

            // 2) "Reset Selections" button
            resetButton = new Button
            {
                Text = "Reset Selections",
                Width = 120,
                Height = 36
            };
            resetButton.Click += ResetButton_Click!;
            topPanel.Controls.Add(resetButton);

            // 3) "Select All" button
            selectAllButton = new Button
            {
                Text = "Select All",
                Width = 120,
                Height = 36
            };
            selectAllButton.Click += SelectAllButton_Click!;
            topPanel.Controls.Add(selectAllButton);

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
                // Provide top & bottom padding so top panel doesn't overlap
                Padding = new Padding(0, 40, 0, 40)
            };
            this.Controls.Add(treePanel);

            // =============== TREEVIEW ===============
            fileTree = new TreeView
            {
                Dock = DockStyle.Fill,
                CheckBoxes = true
            };
            // Hook up an event so if you check a folder node, it checks all children
            fileTree.AfterCheck += FileTree_AfterCheck!;
            treePanel.Controls.Add(fileTree);

            // =============== EVENTS FOR LOADING/SAVING USER SETTINGS ===============
            this.Load += Form1_Load!;
            this.FormClosing += Form1_FormClosing!;
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
                // Clear previous nodes
                fileTree.Nodes.Clear();

                // Store the newly-selected root folder for relative paths
                selectedRootFolder = folderDialog.SelectedPath;

                // Recursively load the chosen folder
                LoadDirectoryIntoTree(selectedRootFolder, fileTree.Nodes);
            }
        }

        // Recursively load directories + files into the tree
        private void LoadDirectoryIntoTree(string path, TreeNodeCollection parentNodes)
        {
            if (!Directory.Exists(path)) return;

            // If path is "C:\", Path.GetFileName(...) is "", so fallback
            string folderName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(folderName))
            {
                folderName = path; // e.g. "C:\"
            }

            // If the folder is in our ignored list, skip entirely
            if (IgnoredDirectories.Contains(folderName, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            // Create a node for the folder
            var dirNode = new TreeNode(folderName)
            {
                Tag = path
            };
            parentNodes.Add(dirNode);

            // Add subdirectories
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
                // Some dirs may be inaccessible. Ignore errors for brevity.
            }

            // Auto-expand only if the current folder has <= 10 subdirectories
            // but STILL display them all, so the user can expand manually.
            if (subDirs.Length <= 10)
            {
                dirNode.Expand();
            }

            // Add files
            try
            {
                foreach (var file in Directory.GetFiles(path))
                {
                    // Check extension and skip if it's an image file
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ImageExtensions.Contains(ext))
                    {
                        // Skip adding this file to the tree
                        continue;
                    }

                    // Display just the filename in the tree
                    string fileName = Path.GetFileName(file);
                    var fileNode = new TreeNode(fileName)
                    {
                        Tag = file
                    };
                    dirNode.Nodes.Add(fileNode);
                }
            }
            catch
            {
                // Ignore errors
            }
        }

        // =============== TREEVIEW AFTER-CHECK (recursively check/uncheck children) ===============

        private void FileTree_AfterCheck(object? sender, TreeViewEventArgs e)
        {
            // If the event was triggered programmatically (not user action), skip
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

            // Recursively gather text from checked files
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

        // Recursively visit checked nodes and append their text
        private int CollectCheckedFiles(TreeNode node, StringBuilder sb)
        {
            int count = 0;

            if (node.Checked && node.Tag is string filePath && File.Exists(filePath))
            {
                try
                {
                    // We'll compute a path that includes the root folder name + relative
                    // 1) Root folder's display name
                    string rootName = Path.GetFileName(selectedRootFolder ?? "");
                    if (string.IsNullOrEmpty(rootName))
                    {
                        // If it's empty (like "C:\"), fallback to the actual chosen folder
                        rootName = selectedRootFolder ?? "";
                    }

                    // 2) Relative path from root
                    string relativePart = filePath;
                    if (!string.IsNullOrEmpty(selectedRootFolder))
                    {
                        relativePart = Path.GetRelativePath(selectedRootFolder, filePath);
                    }

                    // Construct a combined display path: e.g. "myProject\src\app\page.tsx"
                    string displayPath = Path.Combine(rootName, relativePart);

                    // Use "### START" and "### END" blocks for clearer segmentation
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

            // Recurse children
            foreach (TreeNode child in node.Nodes)
            {
                count += CollectCheckedFiles(child, sb);
            }

            return count;
        }

        // Briefly change the copy button text, then revert to original
        private void ShowCopyFeedback(string message)
        {
            copyButton.Text = message;

            if (copyFeedbackTimer == null)
            {
                copyFeedbackTimer = new System.Windows.Forms.Timer
                {
                    Interval = 2000 // 2 seconds
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

        // =============== LOAD & SAVE USER CONFIG ===============

        // 1. When the form loads, read userconfig.json (if available) and restore
        private void Form1_Load(object? sender, EventArgs e)
        {
            if (File.Exists(ConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigPath);
                    var config = JsonSerializer.Deserialize<UserConfig>(json);
                    if (config != null && !string.IsNullOrEmpty(config.LastFolder))
                    {
                        if (Directory.Exists(config.LastFolder))
                        {
                            // This is the root folder from the previous session
                            selectedRootFolder = config.LastFolder;

                            fileTree.Nodes.Clear();
                            LoadDirectoryIntoTree(config.LastFolder, fileTree.Nodes);

                            // Mark the previously checked nodes
                            if (config.CheckedFiles.Count > 0)
                            {
                                MarkCheckedFiles(fileTree.Nodes, config.CheckedFiles);
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore any errors loading config
                }
            }
        }

        // 2. When the form closes, gather current data and write to userconfig.json
        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            var config = new UserConfig();

            // If there's at least one root node, store its Tag as the last folder
            if (fileTree.Nodes.Count > 0 && fileTree.Nodes[0].Tag is string topPath)
            {
                config.LastFolder = topPath;
            }

            // Gather a list of all checked files
            GatherCheckedFilesList(fileTree.Nodes, config.CheckedFiles);

            // Write out the JSON
            try
            {
                string json = JsonSerializer.Serialize(
                    config, new JsonSerializerOptions { WriteIndented = true }
                );
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // Log or ignore
            }
        }

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
    }
}
