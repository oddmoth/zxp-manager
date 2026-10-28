using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace ZXPManager
{
    /// <summary>Light/dark palette following the Windows "app mode" setting.</summary>
    public static class Theme
    {
        public static bool IsDark { get; private set; }

        private static readonly Dictionary<string, (string Light, string Dark)> Palette =
            new Dictionary<string, (string, string)>
            {
                ["WindowBg"] = ("#F3F3F3", "#202020"),
                ["PanelBg"] = ("#EAEAEA", "#1A1A1A"),
                ["CardBg"] = ("#FFFFFF", "#2D2D2D"),
                ["Fg"] = ("#1B1B1B", "#FFFFFF"),
                ["FgSecondary"] = ("#5F5F5F", "#A8A8A8"),
                ["BorderBrush"] = ("#D6D6D6", "#3D3D3D"),
                ["HoverBg"] = ("#E3E3E3", "#2D2D2D"),
                ["SelectedBg"] = ("#CCE4F7", "#1B3A55"),
                ["Accent"] = ("#0067C0", "#4CC2FF"),
                ["AccentFg"] = ("#FFFFFF", "#000000"),
                ["Success"] = ("#0F7B0F", "#6CCB5F"),
                ["Warning"] = ("#B35C00", "#FFB547"),
                ["Danger"] = ("#C42B1C", "#FF7B72"),
                ["DangerBg"] = ("#C42B1C", "#C42B1C"),
                ["ScrollThumb"] = ("#B5B5B5", "#6A6A6A"),
            };

        public static void Apply()
        {
            IsDark = ReadAppsUseDarkTheme();
            var resources = Application.Current.Resources;
            foreach (var entry in Palette)
            {
                var hex = IsDark ? entry.Value.Dark : entry.Value.Light;
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                brush.Freeze();
                resources[entry.Key] = brush;
            }
            foreach (Window window in Application.Current.Windows)
                ApplyTitleBar(window);
        }

        /// <summary>Call from each window's constructor: dark title bar when needed.</summary>
        public static void Attach(Window window)
        {
            window.SourceInitialized += (s, e) => ApplyTitleBar(window);
        }

        private static bool ReadAppsUseDarkTheme()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
            }
            catch
            {
                return false;
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        private static void ApplyTitleBar(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            int dark = IsDark ? 1 : 0;
            try
            {
                // 20 = DWMWA_USE_IMMERSIVE_DARK_MODE (Windows 10 20H1+ / 11), 19 on older Windows 10 builds
                if (DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int)) != 0)
                    DwmSetWindowAttribute(hwnd, 19, ref dark, sizeof(int));
            }
            catch
            {
                // Not available: keep the default title bar
            }
        }
    }
}
