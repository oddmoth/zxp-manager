using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ZXPManager
{
    /// <summary>Adobe's Unified Plugin Installer Agent (installed with the Creative Cloud app).</summary>
    public static class Upia
    {
        private const string RelativePath =
            @"Adobe\Adobe Desktop Common\RemoteComponents\UPI\UnifiedPluginInstallerAgent\UnifiedPluginInstallerAgent.exe";

        public static string ExecutablePath
        {
            get
            {
                var roots = new[]
                {
                    Environment.GetEnvironmentVariable("CommonProgramW6432"),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86),
                }.Where(r => !string.IsNullOrEmpty(r)).Distinct().ToList();

                foreach (var root in roots)
                {
                    var candidate = Path.Combine(root, RelativePath);
                    if (File.Exists(candidate)) return candidate;
                }
                return Path.Combine(roots.FirstOrDefault() ?? @"C:\Program Files\Common Files", RelativePath);
            }
        }

        public struct Result
        {
            public int ExitCode;
            public string Output;
        }

        /// <summary>Runs the UPIA with the given arguments (e.g. "/list", "all").</summary>
        public static Task<Result> RunAsync(int timeoutSeconds, params string[] args) => Task.Run(() =>
        {
            var exe = ExecutablePath;
            if (!File.Exists(exe))
                return new Result { ExitCode = -1, Output = Loc.F("error.upiaNotFound", exe) };

            var output = new StringBuilder();
            var info = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = string.Join(" ", args.Select(Quote)),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            try
            {
                using (var process = new Process { StartInfo = info })
                {
                    process.OutputDataReceived += (s, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
                    process.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // The UPIA can hang if Creative Cloud is not signed in
                    if (!process.WaitForExit(timeoutSeconds * 1000))
                    {
                        try { process.Kill(); } catch { }
                        process.WaitForExit();
                        lock (output) output.AppendLine().Append(Loc.F("error.upiaTimeout", timeoutSeconds));
                        return new Result { ExitCode = -1, Output = output.ToString() };
                    }
                    process.WaitForExit(); // flush async output
                    return new Result { ExitCode = process.ExitCode, Output = output.ToString() };
                }
            }
            catch (Exception ex)
            {
                return new Result { ExitCode = -1, Output = Loc.F("error.upiaLaunch", ex.Message) };
            }
        });

        public static Task<Result> RunAsync(params string[] args) => RunAsync(180, args);

        private static string Quote(string arg) =>
            arg.IndexOfAny(new[] { ' ', '\t', '"' }) >= 0 ? "\"" + arg.Replace("\"", "\\\"") + "\"" : arg;

        public static bool Succeeded(string output) =>
            output != null && output.IndexOf("Successful", StringComparison.OrdinalIgnoreCase) >= 0;

        public static string LastLine(string output)
        {
            var line = (output ?? "").Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.Length > 0);
            return line ?? Loc.T("error.unknown");
        }

        private static readonly Regex Header = new Regex(
            @"^\s*\d+\s+extensions?\s+installed\s+for\s+(.+?)(?:\s+\(ver\s+([^)]+)\))?\s*$",
            RegexOptions.IgnoreCase);

        private static readonly Regex Row = new Regex(
            @"^\s*(Enabled|Disabled)\s{2,}(.+?)\s{2,}(\S+)\s*$",
            RegexOptions.IgnoreCase);

        /// <summary>Parses the output of "/list all".</summary>
        public static List<HostApp> ParseList(string text)
        {
            var apps = new List<HostApp>();
            foreach (var rawLine in (text ?? "").Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                var h = Header.Match(line);
                if (h.Success)
                {
                    apps.Add(new HostApp
                    {
                        Name = h.Groups[1].Value.Trim(),
                        Version = h.Groups[2].Success ? h.Groups[2].Value.Trim() : null,
                    });
                    continue;
                }
                var r = Row.Match(line);
                if (r.Success && apps.Count > 0)
                {
                    var app = apps[apps.Count - 1];
                    app.Extensions.Add(new ExtensionItem
                    {
                        AppTitle = app.Title,
                        AppName = app.Name,
                        Name = r.Groups[2].Value.Trim(),
                        Version = r.Groups[3].Value,
                        Enabled = r.Groups[1].Value.Equals("Enabled", StringComparison.OrdinalIgnoreCase),
                    });
                }
            }
            return apps;
        }
    }
}
