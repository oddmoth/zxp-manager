using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml;

namespace ZXPManager
{
    /// <summary>Reads name, version and target apps from a .zxp (CEP) or .ccx (UXP) package.</summary>
    public static class PackageReader
    {
        public static readonly string[] SupportedExtensions = { ".zxp", ".ccx" };

        public static bool IsSupported(string path) =>
            SupportedExtensions.Contains(Path.GetExtension(path ?? "").ToLowerInvariant());

        public static PackageInfo Read(string path)
        {
            var info = new PackageInfo
            {
                Path = path,
                FileName = Path.GetFileName(path),
                Name = Path.GetFileNameWithoutExtension(path),
            };
            try
            {
                switch (Path.GetExtension(path).ToLowerInvariant())
                {
                    case ".zxp": ReadZxp(path, info); break;
                    case ".ccx": ReadCcx(path, info); break;
                    default: info.ReadError = Loc.T("preview.unknownFormat"); break;
                }
            }
            catch (Exception)
            {
                info.ReadError = Loc.T("preview.readError");
            }
            return info;
        }

        private static string Usable(string s)
        {
            s = s?.Trim();
            return string.IsNullOrEmpty(s) || s.StartsWith("%") ? null : s; // "%key" = localized placeholder
        }

        private static string ReadEntry(string zipPath, string entryName)
        {
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                var entry = zip.Entries.FirstOrDefault(e =>
                    e.FullName.Replace('\\', '/').Equals(entryName, StringComparison.OrdinalIgnoreCase));
                if (entry == null) return null;
                using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
                    return reader.ReadToEnd();
            }
        }

        private static void ReadZxp(string path, PackageInfo info)
        {
            var xml = ReadEntry(path, "CSXS/manifest.xml");
            if (xml == null) { info.ReadError = Loc.T("preview.readError"); return; }

            var doc = new XmlDocument { XmlResolver = null };
            doc.LoadXml(xml);
            var root = doc.DocumentElement;

            var bundleName = root?.GetAttribute("ExtensionBundleName");
            var menu = doc.SelectSingleNode("//*[local-name()='DispatchInfo']/*[local-name()='UI']/*[local-name()='Menu']")?.InnerText;
            info.Name = Usable(bundleName) ?? Usable(menu) ?? info.Name;
            info.Version = Usable(root?.GetAttribute("ExtensionBundleVersion"));
            info.BundleId = Usable(root?.GetAttribute("ExtensionBundleId"));

            var seen = new HashSet<string>();
            foreach (XmlElement host in doc.SelectNodes("//*[local-name()='HostList']/*[local-name()='Host']"))
            {
                var code = host.GetAttribute("Name");
                if (string.IsNullOrEmpty(code)) continue;
                var target = new HostTarget { Code = code, Range = HostCodes.FormatRange(host.GetAttribute("Version")) };
                if (seen.Add(target.DisplayName)) info.Hosts.Add(target);
            }
        }

        private static void ReadCcx(string path, PackageInfo info)
        {
            var json = ReadEntry(path, "manifest.json");
            if (json == null) { info.ReadError = Loc.T("preview.readError"); return; }

            if (!(new JavaScriptSerializer().DeserializeObject(json) is IDictionary<string, object> obj))
            {
                info.ReadError = Loc.T("preview.readError");
                return;
            }

            if (obj.TryGetValue("name", out var name))
            {
                if (name is string s) info.Name = s;
                else if (name is IDictionary<string, object> localized && localized.TryGetValue("default", out var d) && d is string ds)
                    info.Name = ds;
            }
            if (obj.TryGetValue("version", out var version)) info.Version = version as string;
            if (obj.TryGetValue("id", out var id)) info.BundleId = id as string;

            if (!obj.TryGetValue("host", out var hostValue)) return;
            var hosts = new List<IDictionary<string, object>>();
            if (hostValue is IDictionary<string, object> single) hosts.Add(single);
            else if (hostValue is IEnumerable many)
                foreach (var h in many)
                    if (h is IDictionary<string, object> hd) hosts.Add(hd);

            foreach (var h in hosts)
            {
                if (!h.TryGetValue("app", out var app) || !(app is string code)) continue;
                h.TryGetValue("minVersion", out var min);
                info.Hosts.Add(new HostTarget { Code = code, Range = min is string m ? "≥ " + m : null });
            }
        }
    }
}
