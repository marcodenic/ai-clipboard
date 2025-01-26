using System;
using System.Windows.Forms;

namespace ai_clipboard
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Run our single-file Form1
            Application.Run(new Form1());
        }
    }
}
