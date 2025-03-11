# AI Clipboard 📋🤖

Welcome to **AI Clipboard**! This is a Windows Forms application built on .NET 9.0 that allows you to:

- Select a folder containing your project's files and directories.
- Preview and **check** which files you want to include.
- Easily **copy** the selected files' contents (with annotated "START" / "END" blocks) to your clipboard, ready to paste into discussions or documentation.
- Automatically **ignore** unwanted files (like .png, .jpg, /.git, etc.) through a configurable ignore list.
- Customize options (like whether to include binary files) via a dedicated **Options** dialog.

We hope this tool makes your life easier for quickly copying and sharing snippets of your projects. 🎉

## Getting Started 🚀

1.  git clone https://github.com/marcodenic/ai-clipboard.git
2.  **Open** the solution in Visual Studio or your favorite .NET IDE.
3.  **Restore** NuGet packages (if needed).
4.  **Build** the solution and **run** the ai-clipboard project.

You should see a Windows Forms application window:

1.  Click **"Select Folder..."** to pick the root directory of your project.
2.  Use the **TreeView** to check files/folders you want.
3.  Press **"Copy Selected Files to Clipboard"** to copy everything with annotated blocks.
4.  Paste anywhere to share your neatly wrapped code snippets! ✨

## Features & Highlights ✨

- **Ignore Patterns**: The app uses a .gitignore-like approach to skip certain files/folders (e.g., node_modules, .git, images, etc.).
- **Include Binaries Option**: Decide whether to treat potentially binary files as text. (Usually you want to skip those. 😉)
- **Options Dialog**: Fine-tune ignore patterns and your preference for binary files.
- **Persistent User Config**: Your last folder, checked files, and ignore patterns are saved in a local userconfig.json.
- **Project History**: Quickly jump between previously selected folders from the drop-down.

## Folder & File Selection 🗂️

After you pick a root folder, you'll see a nested tree:

- **Directories** show up as nodes that expand and contain files/folders.
- **Files** that are not ignored are listed as leaf nodes.
- **Check** a directory node to automatically check all its children (and vice versa).
- Press **"Select All"** or **"Reset Selections"** to manage your selections easily.

When you press **"Copy Selected Files to Clipboard,"** the app:

- Reads each checked file (assuming it’s text, unless you override).
- Wraps the file content between ### START and ### END lines.
- Places the entire combined text in your clipboard, ready to **Paste**.

## Contributing 🤝

We welcome your contributions, whether it's adding new features, squashing bugs, or suggesting improvements! Here’s how you can help:

1.  **Fork** this repository.
2.  git checkout -b feature/amazing-feature
3.  git commit -m "Add some amazing feature"
4.  git push origin feature/amazing-feature
5.  **Open a Pull Request** on GitHub, describing your feature or fix.

We love seeing new ideas and improvements, so don't be shy—**contribute away**! 💖

## Roadmap 🛣️

- **Command-Line Mode**: Optionally run the tool without UI (for automated scripts).
- **Syntax Highlighting**: Potentially highlight code within the clipboard text (maybe a partial?).
- **Advanced .gitignore Support**: Parse real .gitignore rules for more robust ignoring.
- **Multiplatform**: Possibly a cross-platform version (if we move away from WinForms).

## Stay in Touch 📨

If you find **AI Clipboard** helpful, please **star** the repo, **fork** it, or drop an issue/PR. Spread the word, and let’s make copying code snippets a breeze for everyone! 🎉💻

Happy coding! 👩‍💻👨‍💻

_Made with ❤️ by @marcode._
