using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;

namespace ZXPManager
{
    public partial class MainWindow : Window
    {
        private List<HostApp> apps = new List<HostApp>();
        private bool busy;
        private bool showEmptyApps;
        private bool previewOpen;

        public MainWindow()
        {
            InitializeComponent();
            FlowDirection = Loc.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            Theme.Attach(this);
            UpdateEmptyToggle();

            Loaded += async (s, e) =>
            {
                await RefreshAsync();
                if (App.StartupFiles.Length > 0)
                    await PrepareAsync(App.StartupFiles);
            };
        }

        // MARK: state

        public bool IsBusy => busy;

        public void SetBusy(bool value)
        {
            busy = value;
            BusyBar.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            AddButton.IsEnabled = !value;
            RefreshButton.IsEnabled = !value;
            UpdateRemoveButton();
        }

        private ExtensionItem SelectedExtension =>
            ExtensionList.SelectedItem is ExtensionItem item && !item.IsPlaceholder ? item : null;

        private void UpdateRemoveButton() => RemoveButton.IsEnabled = !busy && SelectedExtension != null;

        private void UpdateEmptyToggle()
        {
            var text = showEmptyApps ? Loc.T("toolbar.hideEmpty") : Loc.T("toolbar.showEmpty");
            EmptyToggle.IsChecked = showEmptyApps;
            EmptyToggle.ToolTip = text;
            AutomationProperties.SetName(EmptyToggle, text);
        }

        // MARK: list

        public async Task RefreshAsync()
        {
            SetBusy(true);
            var result = await Upia.RunAsync("/list", "all");
            apps = Upia.ParseList(result.Output);
            if (apps.Count == 0) AppendLog(Loc.T("log.listTitle"), result.Output);
            SetBusy(false);
            RebuildList();
        }

        private void RebuildList()
        {
            var items = new List<ExtensionItem>();
            foreach (var app in apps)
            {
                if (app.Extensions.Count > 0)
                    items.AddRange(app.Extensions);
                else if (showEmptyApps)
                    items.Add(new ExtensionItem { AppTitle = app.Title, Name = Loc.T("list.noExtensions"), IsPlaceholder = true });
            }

            var view = new ListCollectionView(items);
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ExtensionItem.AppTitle)));
            ExtensionList.ItemsSource = view;

            bool empty = items.Count == 0;
            EmptyState.Visibility = empty && !busy ? Visibility.Visible : Visibility.Collapsed;
            DropHint.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
            UpdateRemoveButton();
        }

        private void ExtensionList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateRemoveButton();

        private void ExtensionList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && RemoveButton.IsEnabled)
            {
                Remove_Click(sender, e);
                e.Handled = true;
            }
        }

        // MARK: log

        /// <summary>Adds a line to the details panel; <paramref name="text"/>, when given, is shown below it.</summary>
        public void AppendLog(string title, string text = null)
        {
            if (text == null)
            {
                LogBox.AppendText("▸ " + title + Environment.NewLine + Environment.NewLine);
            }
            else
            {
                var body = text.Trim();
                if (body.Length == 0) body = Loc.T("log.noOutput");
                LogBox.AppendText("▸ " + title + Environment.NewLine + body + Environment.NewLine + Environment.NewLine);
            }
            LogBox.ScrollToEnd();
            LogPlaceholder.Visibility = Visibility.Collapsed;
        }

        // MARK: install

        /// <summary>Reads the packages and opens the preview (entry point for drag &amp; drop, + and command line).</summary>
        public async Task PrepareAsync(IEnumerable<string> paths)
        {
            var all = paths.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            var valid = all.Where(PackageReader.IsSupported).ToList();
            foreach (var skipped in all.Except(valid))
                AppendLog(Loc.T("log.ignored"), Loc.F("log.ignoredDetail", Path.GetFileName(skipped)));
            if (valid.Count == 0) return;

            if (busy || previewOpen)
            {
                AppendLog(Loc.T("log.wait"), Loc.T("log.waitDetail"));
                return;
            }

            previewOpen = true;
            try
            {
                var packages = await Task.Run(() => valid.Select(PackageReader.Read).ToList());
                var installed = apps.SelectMany(a => a.Extensions).ToList();
                foreach (var package in packages)
                {
                    var match = installed.FirstOrDefault(x => string.Equals(x.Name, package.Name, StringComparison.OrdinalIgnoreCase));
                    if (match != null) package.InstalledVersion = match.Version;
                }

                var preview = new PreviewWindow(packages, apps, this) { Owner = this };
                preview.ShowDialog();
            }
            finally
            {
                previewOpen = false;
            }
        }

        private async void Add_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "*.zxp; *.ccx|*.zxp;*.ccx",
                Multiselect = true,
            };
            if (dialog.ShowDialog(this) == true)
                await PrepareAsync(dialog.FileNames);
        }

        private void DropHint_Click(object sender, MouseButtonEventArgs e)
        {
            if (!busy) Add_Click(sender, e);
        }

        // MARK: drag & drop

        private void DropArea_DragOver(object sender, DragEventArgs e)
        {
            bool files = e.Data.GetDataPresent(DataFormats.FileDrop);
            e.Effects = files ? DragDropEffects.Copy : DragDropEffects.None;
            DropOverlay.Visibility = files ? Visibility.Visible : Visibility.Collapsed;
            e.Handled = true;
        }

        private void DropArea_DragLeave(object sender, DragEventArgs e)
        {
            // DragLeave also fires when moving between child elements: hide only when really leaving
            var p = e.GetPosition(DropArea);
            if (p.X <= 0 || p.Y <= 0 || p.X >= DropArea.ActualWidth || p.Y >= DropArea.ActualHeight)
                DropOverlay.Visibility = Visibility.Collapsed;
        }

        private async void DropArea_Drop(object sender, DragEventArgs e)
        {
            DropOverlay.Visibility = Visibility.Collapsed;
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
                await PrepareAsync(files);
        }

        // MARK: remove / refresh / toggle

        private async void Remove_Click(object sender, RoutedEventArgs e)
        {
            var ext = SelectedExtension;
            if (ext == null || busy) return;

            var confirm = new ConfirmWindow(ext.Name) { Owner = this };
            if (confirm.ShowDialog() != true) return;

            SetBusy(true);
            var result = await Upia.RunAsync("/remove", ext.Name);
            if (Upia.Succeeded(result.Output))
                AppendLog(Loc.F("log.removed", ext.Name));
            else
                AppendLog(Loc.F("log.removeFailed", ext.Name), result.Output); // Adobe's message only on errors
            SetBusy(false);
            await RefreshAsync();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

        private void EmptyToggle_Click(object sender, RoutedEventArgs e)
        {
            showEmptyApps = EmptyToggle.IsChecked == true;
            UpdateEmptyToggle();
            RebuildList();
        }
    }
}
