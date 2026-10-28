using System;
using System.Windows.Markup;

namespace ZXPManager
{
    /// <summary>XAML shortcut for translated text: Text="{local:Tr toolbar.install}"</summary>
    [MarkupExtensionReturnType(typeof(string))]
    public class TrExtension : MarkupExtension
    {
        public TrExtension() { }

        public TrExtension(string key) { Key = key; }

        [ConstructorArgument("key")]
        public string Key { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
    }
}
