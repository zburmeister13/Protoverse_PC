using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace ProtoVerseApp
{
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += (s, e) =>
            {
                ReportCrash(e.Exception);
                e.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                ReportCrash(e.ExceptionObject as Exception ?? new Exception("Unknown fatal error"));
            };
        }

        private static void ReportCrash(Exception ex)
        {
            string text = ex.ToString();
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");

            try { File.WriteAllText(path, text); } catch { }
            try { Clipboard.SetText(text); } catch { }

            MessageBox.Show(
                $"An error occurred. Details were copied to your clipboard and saved to:\n{path}",
                "Unhandled exception",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}