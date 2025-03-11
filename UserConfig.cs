using System.Collections.Generic;

namespace ai_clipboard
{
    // A small class to hold persisted user settings (folder path + checked file paths).
    public class UserConfig
    {
        public string? LastFolder { get; set; }
        public List<string> CheckedFiles { get; set; } = new();

        // New fields for ignoring + binary inclusion
        public bool IncludeBinaries { get; set; } = false;  // default: false
        public List<string> IgnorePatterns { get; set; } = new();

        // New field for previously selected projects
        public List<string> PreviousProjects { get; set; } = new();

        // Helper to provide defaults for ignoring
        public static List<string> GetDefaultIgnorePatterns()
        {
            // This matches your desired list:
            // .png, .jpg, .jpeg, .gif, .bmp, .tif, .tiff, .webp, .svg, .ico, .woff2
            // /.git, /obj, /bin, /node_modules, .github, /.next, package-lock.json, etc.
            return new List<string>
            {
                ".png",
                ".jpg",
                ".jpeg",
                ".gif",
                ".bmp",
                ".tif",
                ".tiff",
                ".webp",
                ".svg",
                ".ico",
                ".woff2",
                "/.git",
                "/obj",
                "/bin",
                "/node_modules",
                ".github",
                "/.next",
                "package-lock.json"
            };
        }
    }
}
