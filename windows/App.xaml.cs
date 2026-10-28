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
            StartupFiles = e.Args;

            DispatcherUnhandledException += OnUnhandledException;

            Loc.Init();
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
