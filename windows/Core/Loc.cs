using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace ZXPManager
{
    /// <summary>
    /// Translations, loaded from the shared i18n/*.json files embedded in the exe.
    /// The JSON uses macOS-style placeholders (%@, %d), converted here to {0}, {1}…
    /// </summary>
    public static class Loc
    {
        private static Dictionary<string, string> strings = new Dictionary<string, string>();
        private static Dictionary<string, string> english = new Dictionary<string, string>();

        public static string Language { get; private set; } = "en";

        public static bool IsRightToLeft =>
            Language == "ar" || Language == "fa" || Language == "ur" || Language == "ps";

        public static void Init() => Init(CultureInfo.CurrentUICulture);

        public static void Init(CultureInfo culture)
        {
            english = Load("en") ?? new Dictionary<string, string>();
            foreach (var candidate in Candidates(culture))
            {
                var table = Load(candidate);
                if (table != null)
                {
                    strings = table;
                    Language = candidate;
                    return;
                }
            }
            strings = english;
            Language = "en";
        }

        /// <summary>Language files to try, most specific first.</summary>
        public static IEnumerable<string> Candidates(CultureInfo culture)
        {
            var name = culture.Name;
            if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                bool traditional = name.Contains("Hant") || name.EndsWith("TW") || name.EndsWith("HK") || name.EndsWith("MO");
                yield return traditional ? "zh-Hant" : "zh-Hans";
            }
            if (!string.IsNullOrEmpty(name)) yield return name;
            yield return culture.TwoLetterISOLanguageName;
            yield return culture.ThreeLetterISOLanguageName;
        }

        /// <summary>Translated text for a key; falls back to English, then to the key itself.</summary>
        public static string T(string key)
        {
            if (strings.TryGetValue(key, out var value)) return value;
            if (english.TryGetValue(key, out value)) return value;
            return key;
        }

        /// <summary>Translated text with arguments.</summary>
        public static string F(string key, params object[] args)
        {
            try { return string.Format(CultureInfo.CurrentCulture, T(key), args); }
            catch (FormatException) { return T(key); }
        }

        public static IEnumerable<string> AvailableLanguages()
        {
            foreach (var res in Assembly.GetExecutingAssembly().GetManifestResourceNames())
                if (res.StartsWith("i18n.") && res.EndsWith(".json"))
                    yield return res.Substring(5, res.Length - 10);
        }

        private static Dictionary<string, string> Load(string language)
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("i18n." + language + ".json");
            if (stream == null) return null;
            using (stream)
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                var result = new Dictionary<string, string>();
                if (Json.Parse(reader.ReadToEnd()) is Dictionary<string, object> raw)
                    foreach (var pair in raw)
                        if (pair.Value is string text) result[pair.Key] = ConvertPlaceholders(text);
                return result;
            }
        }

        /// <summary>"%@ (%d)" → "{0} ({1})", escaping literal braces.</summary>
        public static string ConvertPlaceholders(string text)
        {
            var escaped = text.Replace("{", "{{").Replace("}", "}}");
            int index = 0;
            return Regex.Replace(escaped, "%[@d]", _ => "{" + (index++) + "}");
        }
    }
}
