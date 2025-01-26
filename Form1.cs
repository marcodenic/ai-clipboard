using System;
using System.IO;
using System.Text;
using System.Text.Json; // For serialization
using System.Windows.Forms;

namespace ai_clipboard
{
    public class Form1 : Form
    {
        // Where we store user preferences
        private const string ConfigPath = "userconfig.json";

        private Button selectFolderButton;
        private Button copyButton;
        private TreeView fileTree;

        public Form1()
        {
            // Basic window setup
            this.Text = "AI Clipboard";
            this.Width = 800;
            this.Height = 600;

            // =============== LAYOUT SETUP ===============

            // 1) "Select Folder" button docked at TOP
            selectFolderButton = new Button
            {
                Text = "Select Folder...",
                Dock = DockStyle.Top,
                Height = 40
            };
            selectFolderButton.Click += SelectFolderButton_Click!;
            this.Controls.Add(selectFolderButton);

            // 2) "Copy Selected Files to Clipboard" button docked at BOTTOM
            copyButton = new Button
            {
                Text = "Copy Selected Files to Clipboard",
                Dock = DockStyle.Bottom,
                Height = 40
            };
            copyButton.Click += CopyButton_Click!;
            this.Controls.Add(copyButton);

            // 3) A Panel that fills the remaining space, with top & bottom padding
            var treePanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 40, 0, 40) 
                // ^ 40px on top, 40px on bottom. Increase/decrease as needed.
            };
            this.Controls.Add(treePanel);

            // 4) The TreeView, docked "Fill" inside the panel
            fileTree = new TreeView
            {
                Dock = DockStyle.Fill,
                CheckBoxes = true
            };
            treePanel.Controls.Add(fileTree);

            // Hook up events to load/save user settings
            this.Load += Form1_Load;
            this.FormClosing += Form1_FormClosing;
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

                // Recursively load the chosen folder
                LoadDirectoryIntoTree(folderDialog.SelectedPath, fileTree.Nodes);

                // Expand all so user can see subnodes immediately
                fileTree.ExpandAll();
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

            // Create a node for the folder
            var dirNode = new TreeNode(folderName)
            {
                Tag = path
            };
            parentNodes.Add(dirNode);

            // Add subdirectories
            try
            {
                foreach (var directory in Directory.GetDirectories(path))
                {
                    LoadDirectoryIntoTree(directory, dirNode.Nodes);
                }
            }
            catch
            {
                // Some dirs may be inaccessible. Ignore errors for brevity.
            }

            // Add files
            try
            {
                foreach (var file in Directory.GetFiles(path))
                {
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

        // =============== COPY CHECKED FILES ===============

        private void CopyButton_Click(object? sender, EventArgs e)
        {
            var sb = new StringBuilder();

            // Recursively gather text from checked files
            foreach (TreeNode rootNode in fileTree.Nodes)
            {
                CollectCheckedFiles(rootNode, sb);
            }

            if (sb.Length > 0)
            {
                Clipboard.SetText(sb.ToString());
                MessageBox.Show("Selected file contents copied to clipboard!",
                                "AI Clipboard",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("No files were checked or no files found in the folder.",
                                "AI Clipboard",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
            }
        }

        // Recursively visit checked nodes and append their text
        private void CollectCheckedFiles(TreeNode node, StringBuilder sb)
        {
            if (node.Checked && node.Tag is string filePath && File.Exists(filePath))
            {
                try
                {
                    sb.AppendLine($"========== {filePath} ==========");
                    sb.AppendLine(File.ReadAllText(filePath));
                    sb.AppendLine();
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Error reading {filePath}: {ex.Message}");
                }
            }

            // Recurse children
            foreach (TreeNode child in node.Nodes)
            {
                CollectCheckedFiles(child, sb);
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
                            fileTree.Nodes.Clear();
                            LoadDirectoryIntoTree(config.LastFolder, fileTree.Nodes);
                            fileTree.ExpandAll();

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
