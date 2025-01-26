using System.Collections.Generic;

namespace ai_clipboard
{
    // A small class to hold persisted user settings (folder path + checked file paths).
    public class UserConfig
    {
        public string? LastFolder { get; set; }
        public List<string> CheckedFiles { get; set; } = new();
    }
}
