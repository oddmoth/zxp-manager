using System.Collections.Generic;

namespace ZXPManager
{
    /// <summary>One row of the extension list (or a "no extensions" placeholder for an empty app).</summary>
    public class ExtensionItem
    {
        public string AppTitle { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public bool Enabled { get; set; }
        public bool IsPlaceholder { get; set; }
    }

    public class HostApp
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public List<ExtensionItem> Extensions { get; } = new List<ExtensionItem>();

        public string Title => string.IsNullOrEmpty(Version) ? Name : Name + " — " + Version;
    }

    public class HostTarget
    {
        public string Code { get; set; }
        public string Range { get; set; }
        public string DisplayName => HostCodes.Name(Code);
    }

    public enum Outcome { Pending, Running, Success, Failure }

    public class PackageInfo
    {
        public string Path { get; set; }
        public string FileName { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string BundleId { get; set; }
        public List<HostTarget> Hosts { get; } = new List<HostTarget>();
        public string ReadError { get; set; }
        public string InstalledVersion { get; set; }
        public Outcome Outcome { get; set; } = Outcome.Pending;
        public string FailureMessage { get; set; }
    }

    public static class HostCodes
    {
        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            ["AEFT"] = "After Effects", ["AEFX"] = "After Effects",
            ["PHXS"] = "Photoshop", ["PHSP"] = "Photoshop", ["PS"] = "Photoshop",
            ["ILST"] = "Illustrator", ["AI"] = "Illustrator",
            ["PPRO"] = "Premiere Pro",
            ["IDSN"] = "InDesign", ["ID"] = "InDesign",
            ["AICY"] = "InCopy",
            ["FLPR"] = "Animate",
            ["AUDT"] = "Audition",
            ["KBRG"] = "Bridge",
            ["DRWV"] = "Dreamweaver",
            ["AME"] = "Media Encoder",
            ["LTRM"] = "Lightroom Classic",
            ["XD"] = "XD",
            ["PRLD"] = "Prelude",
            ["RUSH"] = "Premiere Rush",
        };

        public static string Name(string code) =>
            code != null && Names.TryGetValue(code.ToUpperInvariant(), out var name) ? name : code;

        /// <summary>"[13.0,99.9]" → "≥ 13.0", "[13.0,18.9]" → "13.0 – 18.9"</summary>
        public static string FormatRange(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var trimmed = raw.Trim().Trim('[', ']', '(', ')', ' ');
            var parts = trimmed.Split(',');
            if (parts.Length == 2)
            {
                var low = parts[0].Trim();
                var high = parts[1].Trim();
                if (high.StartsWith("99")) return "≥ " + low;
                return low + " – " + high;
            }
            return "≥ " + trimmed;
        }
    }
}
