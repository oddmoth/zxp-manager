using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ZXPManager
{
    /// <summary>
    /// Keeps a single ZXP Manager window per user: a second launch (for example a
    /// double-click on another .zxp) hands its files to the window already open and quits.
    /// </summary>
    public static class SingleInstance
    {
        private static Mutex mutex;
        private static readonly string Id =
            "OddMoth.ZXPManager." + Environment.UserName + "." + Process.GetCurrentProcess().SessionId;

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int processId);
        private const int ASFW_ANY = -1;

        /// <summary>True if this is the first instance; it then keeps the lock until it quits.</summary>
        public static bool TryClaim()
        {
            mutex = new Mutex(true, "Local\\" + Id, out bool created);
            return created;
        }

        /// <summary>Sends the files to the running instance. False if it could not be reached.</summary>
        public static bool SendToRunning(string[] files)
        {
            // Let the other window come to the front (Windows allows it only with our permission)
            AllowSetForegroundWindow(ASFW_ANY);
            var message = string.Join("\n", files.Select(FullPath));

            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    using (var pipe = new NamedPipeClientStream(".", Id, PipeDirection.Out))
                    {
                        pipe.Connect(1500);
                        var bytes = Encoding.UTF8.GetBytes(message);
                        pipe.Write(bytes, 0, bytes.Length);
                        pipe.Flush();
                        return true;
                    }
                }
                catch (Exception)
                {
                    // The first window may still be starting: try again shortly
                    Thread.Sleep(300);
                }
            }
            return false;
        }

        /// <summary>Listens for later launches; <paramref name="received"/> runs on a background thread.</summary>
        public static void Listen(Action<string[]> received)
        {
            var thread = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        using (var pipe = new NamedPipeServerStream(Id, PipeDirection.In, 1))
                        {
                            pipe.WaitForConnection();
                            using (var reader = new StreamReader(pipe, Encoding.UTF8))
                            {
                                var files = reader.ReadToEnd()
                                    .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                received(files);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        Thread.Sleep(500);
                    }
                }
            })
            { IsBackground = true, Name = "SingleInstance" };
            thread.Start();
        }

        private static string FullPath(string path)
        {
            try { return Path.GetFullPath(path); }
            catch (Exception) { return path; }
        }
    }
}
