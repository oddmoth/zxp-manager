using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ZXPManager
{
    public partial class App : Application
    {
        /// <summary>Files passed on the command line (e.g. dropped on the exe icon).</summary>
        public static string[] StartupFiles { get; private set; } = new string[0];

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // "--lang=ar" shows the app in another language (useful to check translations);
            // every other argument is a file to install
            CultureInfo language = null;
            var files = new List<string>();
            foreach (var arg in e.Args)
            {
                if (arg.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase))
                {
                    try { language = new CultureInfo(arg.Substring(7)); }
                    catch (CultureNotFoundException) { }
                }
                else
                {
                    files.Add(arg);
                }
            }
            StartupFiles = files.ToArray();

            DispatcherUnhandledException += OnUnhandledException;

            if (language != null)
            {
                Thread.CurrentThread.CurrentUICulture = language;
                Loc.Init(language);
            }
            else
            {
                Loc.Init();
            }
            Theme.Apply();
            SystemEvents.UserPreferenceChanged += (s, args) =>
            {
                if (args.Category == UserPreferenceCategory.General)
                    Dispatcher.Invoke(new System.Action(Theme.Apply));
            };

            new MainWindow().Show();
        }

        private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(e.Exception.Message, "ZXP Manager", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
