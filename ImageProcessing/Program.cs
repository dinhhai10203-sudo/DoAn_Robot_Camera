using System;
using System.Windows.Forms;

namespace ImageProcessing
{
    /// <summary>
    /// Chương trình chính - Entry point của ứng dụng
    /// </summary>
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
