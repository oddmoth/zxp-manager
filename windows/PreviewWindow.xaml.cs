using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ZXPManager
{
    /// <summary>Shows what is inside the packages before installing them, then the result of each one.</summary>
    public partial class PreviewWindow : Window
    {
        private readonly List<PackageInfo> packages;
        private readonly List<HostApp> apps;
        private readonly MainWindow main;
        private readonly Dictionary<PackageInfo, CardParts> cards = new Dictionary<PackageInfo, CardParts>();
        private bool running;

        private class CardParts
        {
            public ContentControl Status;
            public UIElement AlreadyInstalled;
            public TextBox Failure;
            public UIElement RestartHint;
        }

        private static string RestartHint(PackageInfo package) =>
            Loc.RestartHint(package.Hosts.Select(h => h.DisplayName));

        public PreviewWindow(List<PackageInfo> packages, List<HostApp> apps, MainWindow main)
        {
            InitializeComponent();
            FlowDirection = Loc.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            Theme.Attach(this);

            this.packages = packages;
            this.apps = apps;
            this.main = main;

            TitleText.Text = packages.Count == 1 ? Loc.T("preview.title.one") : Loc.F("preview.title.many", packages.Count);
            foreach (var package in packages)
                Cards.Children.Add(BuildCard(package));

            Closing += (s, e) => { if (running) e.Cancel = true; };
        }

        // MARK: install

        private async void Install_Click(object sender, RoutedEventArgs e)
        {
            running = true;
            InstallButton.IsEnabled = false;
            CancelButton.IsEnabled = false;
            ProgressPanel.Visibility = Visibility.Visible;
            main.SetBusy(true);

            foreach (var package in packages)
            {
                package.Outcome = Outcome.Running;
                UpdateCard(package);

                var result = await Upia.RunAsync("/install", package.Path);
                if (Upia.Succeeded(result.Output))
                {
                    main.AppendLog(Loc.F("log.installed", package.Name), RestartHint(package));
                    package.Outcome = Outcome.Success;
                }
                else
                {
                    // Adobe's own message is only shown when something goes wrong
                    main.AppendLog(Loc.F("log.installFailed", package.Name), result.Output);
                    package.Outcome = Outcome.Failure;
                    package.FailureMessage = Upia.LastLine(result.Output);
                }
                UpdateCard(package);
            }

            main.SetBusy(false);
            running = false;

            ProgressPanel.Visibility = Visibility.Collapsed;
            TitleText.Text = Loc.T("preview.title.done");
            InstallButton.Visibility = Visibility.Collapsed;
            CancelButton.Visibility = Visibility.Collapsed;
            CloseButton.Visibility = Visibility.Visible;
            CloseButton.IsDefault = true;
            CloseButton.Focus();

            await main.RefreshAsync();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        // MARK: cards

        private static TextBlock Glyph(string glyph, double size, string brushKey)
        {
            var tb = new TextBlock
            {
                Text = glyph,
                FontSize = size,
                FontFamily = (FontFamily)Application.Current.Resources["GlyphFont"],
                VerticalAlignment = VerticalAlignment.Center,
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
            return tb;
        }

        private static TextBlock Text(string text, string brushKey = "Fg", double size = 13)
        {
            var tb = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            tb.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
            return tb;
        }

        /// <summary>Glyph + text on one line.</summary>
        private static StackPanel Line(string glyph, string glyphBrush, UIElement content, double top = 4)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, top, 0, 0) };
            var icon = Glyph(glyph, 13, glyphBrush);
            icon.Margin = new Thickness(0, 1, 7, 0);
            row.Children.Add(icon);
            row.Children.Add(content);
            return row;
        }

        private UIElement BuildCard(PackageInfo package)
        {
            var parts = new CardParts();
            cards[package] = parts;

            var border = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 0, 10),
                BorderThickness = new Thickness(1),
            };
            border.SetResourceReference(Border.BackgroundProperty, "CardBg");
            border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            border.Child = grid;

            var icon = Glyph("", 28, "Accent");
            icon.VerticalAlignment = VerticalAlignment.Top;
            icon.Margin = new Thickness(0, 2, 14, 0);
            grid.Children.Add(icon);

            var body = new StackPanel();
            Grid.SetColumn(body, 1);
            grid.Children.Add(body);

            // Name, version and status
            var header = new DockPanel();
            parts.Status = new ContentControl { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            DockPanel.SetDock(parts.Status, Dock.Right);
            header.Children.Add(parts.Status);

            var title = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            title.Inlines.Add(new Run(package.Name) { FontWeight = FontWeights.SemiBold, FontSize = 15 });
            if (!string.IsNullOrEmpty(package.Version))
            {
                var version = new Run("   " + package.Version) { FontSize = 14 };
                version.SetResourceReference(TextElement.ForegroundProperty, "FgSecondary");
                title.Inlines.Add(version);
            }
            header.Children.Add(title);
            body.Children.Add(header);

            var id = Text(package.BundleId ?? package.FileName, "FgSecondary", 12);
            id.Margin = new Thickness(0, 2, 0, 0);
            body.Children.Add(id);

            if (package.ReadError != null)
                body.Children.Add(Line("", "Warning", Text(package.ReadError, "Warning", 12), 6));

            if (package.Hosts.Count > 0)
            {
                var label = Text(Loc.T("preview.installsIn"), "FgSecondary", 12);
                label.Margin = new Thickness(0, 8, 0, 0);
                body.Children.Add(label);

                foreach (var host in package.Hosts)
                {
                    bool installed = apps.Any(a => a.Name.IndexOf(host.DisplayName, StringComparison.OrdinalIgnoreCase) >= 0);
                    var text = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
                    text.SetResourceReference(TextBlock.ForegroundProperty, "Fg");
                    text.Inlines.Add(new Run(host.DisplayName + (host.Range != null ? " (" + host.Range + ")" : "")));
                    if (!installed)
                    {
                        var missing = new Run("   " + Loc.T("preview.notInstalled"));
                        missing.SetResourceReference(TextElement.ForegroundProperty, "Warning");
                        text.Inlines.Add(missing);
                    }
                    body.Children.Add(Line(installed ? "" : "", installed ? "Success" : "Warning", text));
                }
            }

            if (package.InstalledVersion != null)
            {
                parts.AlreadyInstalled = Line("", "FgSecondary",
                    Text(Loc.F("preview.alreadyInstalled", package.InstalledVersion), "FgSecondary", 12), 8);
                body.Children.Add(parts.AlreadyInstalled);
            }

            parts.Failure = new TextBox
            {
                IsReadOnly = true,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Margin = new Thickness(-2, 6, 0, 0),
                Visibility = Visibility.Collapsed,
            };
            parts.Failure.SetResourceReference(TextBox.ForegroundProperty, "Danger");
            body.Children.Add(parts.Failure);

            parts.RestartHint = Line("", "FgSecondary", Text(RestartHint(package), "FgSecondary", 12), 8);
            parts.RestartHint.Visibility = Visibility.Collapsed;
            body.Children.Add(parts.RestartHint);

            return border;
        }

        private void UpdateCard(PackageInfo package)
        {
            var parts = cards[package];
            switch (package.Outcome)
            {
                case Outcome.Running:
                    var bar = new ProgressBar { Width = 50, Height = 4, IsIndeterminate = true, BorderThickness = new Thickness(0), Background = Brushes.Transparent };
                    bar.SetResourceReference(ProgressBar.ForegroundProperty, "Accent");
                    parts.Status.Content = bar;
                    break;
                case Outcome.Success:
                    parts.Status.Content = Line("", "Success", Text(Loc.T("preview.status.installed"), "Success", 12), 0);
                    parts.RestartHint.Visibility = Visibility.Visible;
                    break;
                case Outcome.Failure:
                    parts.Status.Content = Line("", "Danger", Text(Loc.T("preview.status.error"), "Danger", 12), 0);
                    parts.Failure.Text = package.FailureMessage;
                    parts.Failure.Visibility = Visibility.Visible;
                    break;
                default:
                    parts.Status.Content = null;
                    break;
            }
            if (parts.AlreadyInstalled != null && package.Outcome != Outcome.Pending)
                parts.AlreadyInstalled.Visibility = Visibility.Collapsed;
        }
    }
}
