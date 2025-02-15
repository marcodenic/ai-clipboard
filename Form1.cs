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

        // We'll track the user-chosen root folder for relative paths
        private string? selectedRootFolder = null;

        // For saving the original copy button text
        private readonly string originalCopyButtonText;

        // A list of the top-level buttons/panels
        private FlowLayoutPanel topPanel;
        private Button selectFolderButton;
        private Button resetButton;
        private Button selectAllButton;
        private Button copyButton;
        private Button optionsButton; // New "Options" button

        // The main TreeView
        private TreeView fileTree;

        // Timer for “Files copied!” feedback
        private System.Windows.Forms.Timer? copyFeedbackTimer;

        // We'll keep a reference to the user's config once loaded
        private UserConfig userConfig = new UserConfig();

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

            // 4) New "Options" button
            optionsButton = new Button
            {
                Text = "Options...",
                Width = 120,
                Height = 36
            };
            optionsButton.Click += OptionsButton_Click!;
            topPanel.Controls.Add(optionsButton);

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

            // If no LastFolder or the folder doesn't exist, do nothing more
            if (!string.IsNullOrEmpty(userConfig.LastFolder) && Directory.Exists(userConfig.LastFolder))
            {
                selectedRootFolder = userConfig.LastFolder;

                // Load the folder tree
                fileTree.Nodes.Clear();
                LoadDirectoryIntoTree(selectedRootFolder, fileTree.Nodes);

                // Mark the previously checked nodes
                if (userConfig.CheckedFiles.Count > 0)
                {
                    MarkCheckedFiles(fileTree.Nodes, userConfig.CheckedFiles);
                }
            }
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            // If there's at least one root node, store its Tag as the last folder
            // (we'll assume the first node is the selected folder root)
            if (fileTree.Nodes.Count > 0 && fileTree.Nodes[0].Tag is string topPath)
            {
                userConfig.LastFolder = topPath;
            }

            // Gather a list of all checked files
            userConfig.CheckedFiles.Clear();
            GatherCheckedFilesList(fileTree.Nodes, userConfig.CheckedFiles);

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
                // Clear previous nodes
                fileTree.Nodes.Clear();

                // Store the newly-selected root folder for relative paths
                selectedRootFolder = folderDialog.SelectedPath;

                // Recursively load the chosen folder
                LoadDirectoryIntoTree(selectedRootFolder, fileTree.Nodes);
            }
        }

        // =============== LOAD DIRECTORY & FILES INTO TREE ===============
        private void LoadDirectoryIntoTree(string path, TreeNodeCollection parentNodes)
        {
            if (!Directory.Exists(path)) return;

            // If path is "C:\", Path.GetFileName(...) is "", so fallback
            string folderName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(folderName))
            {
                folderName = path; // e.g. "C:\"
            }

            // Check if we should ignore this directory
            if (ShouldIgnoreDirectory(folderName, path))
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
            if (subDirs.Length <= 10)
            {
                dirNode.Expand();
            }

            // Add files
            try
            {
                foreach (var file in Directory.GetFiles(path))
                {
                    // If the file is ignored by pattern, skip it
                    if (ShouldIgnoreFile(file))
                    {
                        continue;
                    }

                    // If user doesn't want binaries, skip if it's likely binary
                    if (!userConfig.IncludeBinaries && !IsLikelyTextFile(file))
                    {
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

        // =============== IGNORE CHECKS ===============

        private bool ShouldIgnoreDirectory(string folderName, string fullPath)
        {
            // We'll match each ignore pattern. If a pattern appears to reference a directory
            // (starts with "/" or includes a slash) and it matches, we skip it.
            // This is simplistic "contains" logic; you can refine if needed.

            if (userConfig.IgnorePatterns == null) return false;

            // Convert backslashes to forward slashes for consistency
            string pathForCheck = fullPath.Replace('\\', '/').ToLowerInvariant();

            foreach (var pattern in userConfig.IgnorePatterns)
            {
                string p = pattern.Trim().ToLowerInvariant();

                // If the pattern references a directory path (starts with "/" or includes a slash),
                // we can do a simple "contains" check or "ends with" check. We'll do "contains" for demonstration.
                if (p.StartsWith("/") || p.Contains("/"))
                {
                    // e.g. "/.git", "/node_modules"
                    // We'll see if pathForCheck ends with or contains that pattern
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

                // If the pattern has a slash, treat it as a substring check in the path
                if (p.Contains("/"))
                {
                    // e.g. "package-lock.json" could appear without slash, or as "folder/package-lock.json".
                    if (lowerFile.Contains(p))
                    {
                        return true;
                    }
                }
                else
                {
                    // If no slash, treat it as a direct file name or extension.
                    // Examples: ".png", ".jpg", "package-lock.json"
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
                // Read up to 1024 bytes to test
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                int length = (int)Math.Min(fs.Length, 1024);
                byte[] buffer = new byte[length];

                int readCount = fs.Read(buffer, 0, length);

                // A simple heuristic: count how many bytes are "non-text"
                // We'll say non-text if:
                // 1) It's 0x00 (null)
                // 2) It's < 32 (except tab=9, newline=10, carriage return=13)
                // 3) It's > 126 (note: real UTF-8 can have bytes > 126, but we keep it simple)
                int nonTextCount = 0;
                for (int i = 0; i < readCount; i++)
                {
                    byte b = buffer[i];
                    if (b == 0)
                    {
                        // Definitely binary-like
                        return false;
                    }
                    bool isNormalTextChar =
                        (b >= 32 && b <= 126) ||
                        b == 9 ||
                        b == 10 ||
                        b == 13;
                    if (!isNormalTextChar)
                    {
                        nonTextCount++;
                    }
                }

                // If more than 20% of the bytes are "non-text," consider it binary
                double ratio = (double)nonTextCount / readCount;
                if (ratio > 0.20)
                {
                    return false;
                }

                return true;
            }
            catch
            {
                // If we can't read it for whatever reason, treat it as non-text
                return false;
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

        // A quick message on the copy button, which reverts after a small delay
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

        // =============== OPTIONS BUTTON CLICK ===============
        private void OptionsButton_Click(object? sender, EventArgs e)
        {
            using var form = new OptionsForm(userConfig);
            if (form.ShowDialog() == DialogResult.OK)
            {
                // The user may have changed IncludeBinaries or IgnorePatterns
                // We refresh the tree if there's a selected folder
                if (!string.IsNullOrEmpty(selectedRootFolder) && Directory.Exists(selectedRootFolder))
                {
                    fileTree.Nodes.Clear();
                    LoadDirectoryIntoTree(selectedRootFolder, fileTree.Nodes);
                }
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
    }
}
