using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ai_clipboard
{
    // Single-file approach: no partial keyword, no .Designer.cs file
    public class Form1 : Form
    {
        private Button selectFolderButton;
        private TreeView fileTree;
        private Button copyButton;

        public Form1()
        {
            // Basic window setup
            this.Text = "AI Clipboard";
            this.Width = 800;
            this.Height = 600;

            // 1) "Select Folder" button
            selectFolderButton = new Button
            {
                Text = "Select Folder...",
                Dock = DockStyle.Top,
                Height = 40
            };
            // NOTE: using (object? sender, EventArgs e) to match nullability warnings
            selectFolderButton.Click += SelectFolderButton_Click!;
            this.Controls.Add(selectFolderButton);

            // 2) TreeView for file listing
            fileTree = new TreeView
            {
                Dock = DockStyle.Fill,
                CheckBoxes = true
            };
            this.Controls.Add(fileTree);

            // 3) "Copy" button
            copyButton = new Button
            {
                Text = "Copy Selected Files to Clipboard",
                Dock = DockStyle.Bottom,
                Height = 40
            };
            copyButton.Click += CopyButton_Click!;
            this.Controls.Add(copyButton);
        }

        // Fired when user clicks "Select Folder"
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

                // Expand all so user can immediately see subnodes
                fileTree.ExpandAll();
            }
        }

        // Recursively load directories + files into the tree
        private void LoadDirectoryIntoTree(string path, TreeNodeCollection parentNodes)
        {
            if (!Directory.Exists(path)) return;

            // If path is a drive root like "C:\", Path.GetFileName(path) = "".
            // We'll use a fallback if the name is empty.
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
                foreach (string directory in Directory.GetDirectories(path))
                {
                    LoadDirectoryIntoTree(directory, dirNode.Nodes);
                }
            }
            catch
            {
                // Some dirs may be inaccessible. We ignore errors for brevity.
            }

            // Add files
            try
            {
                foreach (string file in Directory.GetFiles(path))
                {
                    // We'll display just the file name in the tree
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
                // Ignore file access errors
            }
        }

        // Fired when user clicks "Copy Selected Files to Clipboard"
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

        // Helper: Recursively visit checked nodes
        private void CollectCheckedFiles(TreeNode node, StringBuilder sb)
        {
            // If node is checked AND Tag is a file
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
    }
}
