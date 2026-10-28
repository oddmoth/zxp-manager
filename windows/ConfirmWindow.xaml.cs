using System.Windows;

namespace ZXPManager
{
    /// <summary>"Remove this extension?" confirmation.</summary>
    public partial class ConfirmWindow : Window
    {
        public ConfirmWindow(string extensionName)
        {
            InitializeComponent();
            FlowDirection = Loc.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            Theme.Attach(this);
            RemoveButton.Content = Loc.F("remove.confirm.button", extensionName);
            Loaded += (s, e) => CancelButton.Focus(); // the safe choice is the default
        }

        private void Remove_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    }
}
