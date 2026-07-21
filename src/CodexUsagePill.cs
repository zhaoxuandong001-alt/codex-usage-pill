using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle("Codex Usage Pill")]
[assembly: AssemblyDescription("A small Windows overlay for Codex rate-limit remaining percentage.")]
[assembly: AssemblyProduct("Codex Usage Pill")]
[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]

namespace CodexUsagePill
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            NativeMethods.TryEnablePerMonitorDpi();

            if (args.Any(a => string.Equals(a, "--probe", StringComparison.OrdinalIgnoreCase)))
            {
                UsageSnapshot snapshot = UsageClient.Query(TimeSpan.FromSeconds(15));
                Console.WriteLine(snapshot.ToSafeJson());
                return snapshot.Success ? 0 : 2;
            }

            if (args.Any(a => string.Equals(a, "--login", StringComparison.OrdinalIgnoreCase)))
                return UsageClient.StartInteractiveLogin() ? 0 : 2;

            bool preview = args.Any(a => string.Equals(a, "--preview", StringComparison.OrdinalIgnoreCase));
            if (preview)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new UsagePillContext(true));
                return 0;
            }

            bool createdNew;
            using (var instance = new Mutex(true, @"Local\CodexUsagePill", out createdNew))
            {
                if (!createdNew) return 0;
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new UsagePillContext(false));
                    return 0;
                }
                finally
                {
                    instance.ReleaseMutex();
                }
            }
        }
    }

    internal sealed class UsageSnapshot
    {
        public bool Success { get; set; }
        public bool AuthenticationRequired { get; set; }
        public int? PrimaryRemaining { get; set; }
        public int? SecondaryRemaining { get; set; }
        public long? PrimaryResetsAt { get; set; }
        public long? SecondaryResetsAt { get; set; }
        public int? PrimaryWindowMinutes { get; set; }
        public int? SecondaryWindowMinutes { get; set; }
        public string Error { get; set; }
        public DateTime FetchedAt { get; set; }

        public string ToSafeJson()
        {
            var payload = new Dictionary<string, object>();
            payload["success"] = Success;
            payload["authenticationRequired"] = AuthenticationRequired;
            payload["primaryRemaining"] = PrimaryRemaining;
            payload["secondaryRemaining"] = SecondaryRemaining;
            payload["primaryResetsAt"] = PrimaryResetsAt;
            payload["secondaryResetsAt"] = SecondaryResetsAt;
            payload["primaryWindowMinutes"] = PrimaryWindowMinutes;
            payload["secondaryWindowMinutes"] = SecondaryWindowMinutes;
            payload["error"] = Error;
            return new JavaScriptSerializer().Serialize(payload);
        }

        public static UsageSnapshot Failure(string message, bool authenticationRequired)
        {
            return new UsageSnapshot
            {
                Success = false,
                AuthenticationRequired = authenticationRequired,
                Error = message,
                FetchedAt = DateTime.Now
            };
        }
    }

    internal static class CodexLocator
    {
        public static string FindCli()
        {
            string binRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenAI", "Codex", "bin");

            if (Directory.Exists(binRoot))
            {
                string match = Directory.GetFiles(binRoot, "codex.exe", SearchOption.AllDirectories)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(match)) return match;
            }

            string pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string folder in pathValue.Split(Path.PathSeparator))
            {
                try
                {
                    string candidate = Path.Combine(folder.Trim(), "codex.exe");
                    if (File.Exists(candidate) && candidate.IndexOf("WindowsApps", StringComparison.OrdinalIgnoreCase) < 0)
                        return candidate;
                }
                catch
                {
                    // Ignore malformed PATH entries.
                }
            }

            return null;
        }
    }

    internal static class UsageClient
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 };

        public static UsageSnapshot Query(TimeSpan timeout)
        {
            string codexPath = CodexLocator.FindCli();
            if (string.IsNullOrEmpty(codexPath))
                return UsageSnapshot.Failure("Codex CLI was not found.", false);

            Process process = null;
            try
            {
                ProcessStartInfo start = CreateCodexStartInfo(codexPath, "app-server --listen stdio://", true);

                process = Process.Start(start);
                process.ErrorDataReceived += delegate { };
                process.BeginErrorReadLine();

                Send(process, new Dictionary<string, object>
                {
                    { "method", "initialize" },
                    { "id", 1 },
                    { "params", new Dictionary<string, object>
                        {
                            { "clientInfo", new Dictionary<string, object>
                                {
                                    { "name", "codex_usage_pill" },
                                    { "title", "Codex Usage Pill" },
                                    { "version", "1.0.0" }
                                }
                            }
                        }
                    }
                });

                DateTime deadline = DateTime.UtcNow.Add(timeout);
                while (DateTime.UtcNow < deadline)
                {
                    string line = ReadLine(process, deadline);
                    if (line == null) break;

                    Dictionary<string, object> message = DeserializeDictionary(line);
                    if (message == null || !message.ContainsKey("id")) continue;
                    int id = Convert.ToInt32(message["id"], CultureInfo.InvariantCulture);

                    if (id == 1)
                    {
                        if (message.ContainsKey("error"))
                            return UsageSnapshot.Failure(ReadError(message), false);

                        Send(process, new Dictionary<string, object>
                        {
                            { "method", "initialized" },
                            { "params", new Dictionary<string, object>() }
                        });
                        Send(process, new Dictionary<string, object>
                        {
                            { "method", "account/rateLimits/read" },
                            { "id", 2 }
                        });
                    }
                    else if (id == 2)
                    {
                        if (message.ContainsKey("error"))
                        {
                            string error = ReadError(message);
                            bool auth = error.IndexOf("authentication required", StringComparison.OrdinalIgnoreCase) >= 0;
                            return UsageSnapshot.Failure(auth ? "Sign in to Codex CLI first." : error, auth);
                        }

                        return ParseSnapshot(GetDictionary(message, "result"));
                    }
                }

                return UsageSnapshot.Failure("Timed out while reading Codex usage.", false);
            }
            catch (Exception ex)
            {
                return UsageSnapshot.Failure(ex.Message, false);
            }
            finally
            {
                if (process != null)
                {
                    try
                    {
                        if (!process.HasExited) process.Kill();
                    }
                    catch { }
                    process.Dispose();
                }
            }
        }

        private static UsageSnapshot ParseSnapshot(Dictionary<string, object> result)
        {
            Dictionary<string, object> limits = GetDictionary(result, "rateLimits");
            if (limits == null) return UsageSnapshot.Failure("Codex did not return a usage window.", false);

            Dictionary<string, object> primary = GetDictionary(limits, "primary");
            Dictionary<string, object> secondary = GetDictionary(limits, "secondary");
            var snapshot = new UsageSnapshot
            {
                Success = true,
                FetchedAt = DateTime.Now
            };

            int? primaryRemaining;
            long? primaryReset;
            int? primaryMinutes;
            int? secondaryRemaining;
            long? secondaryReset;
            int? secondaryMinutes;
            ReadWindow(primary, out primaryRemaining, out primaryReset, out primaryMinutes);
            ReadWindow(secondary, out secondaryRemaining, out secondaryReset, out secondaryMinutes);
            snapshot.PrimaryRemaining = primaryRemaining;
            snapshot.PrimaryResetsAt = primaryReset;
            snapshot.PrimaryWindowMinutes = primaryMinutes;
            snapshot.SecondaryRemaining = secondaryRemaining;
            snapshot.SecondaryResetsAt = secondaryReset;
            snapshot.SecondaryWindowMinutes = secondaryMinutes;

            if (!snapshot.PrimaryRemaining.HasValue && !snapshot.SecondaryRemaining.HasValue)
                return UsageSnapshot.Failure("Codex returned an empty usage window.", false);
            return snapshot;
        }

        private static void ReadWindow(Dictionary<string, object> window, out int? remaining, out long? reset, out int? minutes)
        {
            remaining = null;
            reset = null;
            minutes = null;
            if (window == null) return;

            object value;
            if (window.TryGetValue("usedPercent", out value) && value != null)
            {
                double used = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                remaining = Math.Max(0, Math.Min(100, (int)Math.Round(100.0 - used, MidpointRounding.AwayFromZero)));
            }
            if (window.TryGetValue("resetsAt", out value) && value != null)
                reset = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            if (window.TryGetValue("windowDurationMins", out value) && value != null)
                minutes = Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static string ReadLine(Process process, DateTime deadline)
        {
            int remainingMs = (int)Math.Max(1, (deadline - DateTime.UtcNow).TotalMilliseconds);
            Task<string> task = process.StandardOutput.ReadLineAsync();
            if (!task.Wait(remainingMs)) throw new TimeoutException();
            return task.Result;
        }

        private static void Send(Process process, object message)
        {
            process.StandardInput.WriteLine(Json.Serialize(message));
            process.StandardInput.Flush();
        }

        private static Dictionary<string, object> DeserializeDictionary(string json)
        {
            try { return Json.DeserializeObject(json) as Dictionary<string, object>; }
            catch { return null; }
        }

        private static Dictionary<string, object> GetDictionary(Dictionary<string, object> parent, string key)
        {
            if (parent == null) return null;
            object value;
            return parent.TryGetValue(key, out value) ? value as Dictionary<string, object> : null;
        }

        private static string ReadError(Dictionary<string, object> message)
        {
            Dictionary<string, object> error = GetDictionary(message, "error");
            if (error == null) return "Codex returned an unknown error.";
            object value;
            return error.TryGetValue("message", out value) && value != null
                ? Convert.ToString(value, CultureInfo.InvariantCulture)
                : "Codex returned an unknown error.";
        }

        public static bool StartInteractiveLogin()
        {
            string codexPath = CodexLocator.FindCli();
            if (string.IsNullOrEmpty(codexPath)) return false;
            try
            {
                ProcessStartInfo start = CreateCodexStartInfo(codexPath, "login", false);
                Process.Start(start);
                return true;
            }
            catch { return false; }
        }

        private static ProcessStartInfo CreateCodexStartInfo(string codexPath, string arguments, bool redirect)
        {
            var start = new ProcessStartInfo
            {
                FileName = codexPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardInput = redirect,
                RedirectStandardOutput = redirect,
                RedirectStandardError = redirect
            };
            start.EnvironmentVariables.Remove("CODEX_INTERNAL_ORIGINATOR_OVERRIDE");
            start.EnvironmentVariables.Remove("CODEX_SHELL");
            start.EnvironmentVariables.Remove("CODEX_THREAD_ID");
            return start;
        }
    }

    internal sealed class UsagePillContext : ApplicationContext
    {
        private readonly PillForm form;
        private readonly NotifyIcon tray;
        private readonly Icon trayIcon;
        private readonly System.Windows.Forms.Timer positionTimer;
        private readonly System.Windows.Forms.Timer refreshTimer;
        private readonly System.Windows.Forms.Timer loginTimer;
        private Point? customOffset;
        private int refreshing;

        public UsagePillContext(bool preview)
        {
            form = new PillForm(preview);
            IntPtr unusedHandle = form.Handle;
            customOffset = PositionStore.Load();
            form.DragCompleted += delegate { SavePosition(); };

            var menu = new ContextMenuStrip();
            menu.Items.Add("Refresh now", null, delegate { RefreshUsage(); });
            menu.Items.Add("Reset position", null, delegate { ResetPosition(); });
            menu.Items.Add("Sign in to Codex…", null, delegate { BeginLogin(); });
            menu.Items.Add("Open usage page", null, delegate { OpenUsagePage(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, delegate { Exit(); });

            trayIcon = CreateTrayIcon();
            tray = new NotifyIcon
            {
                Icon = trayIcon,
                Text = "Codex Usage Pill",
                Visible = true,
                ContextMenuStrip = menu
            };
            tray.DoubleClick += delegate { RefreshUsage(); };

            positionTimer = new System.Windows.Forms.Timer { Interval = 400 };
            positionTimer.Tick += delegate { UpdatePosition(); };
            if (!preview) positionTimer.Start();

            refreshTimer = new System.Windows.Forms.Timer { Interval = 5 * 60 * 1000 };
            refreshTimer.Tick += delegate { RefreshUsage(); };
            if (!preview) refreshTimer.Start();

            loginTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            loginTimer.Tick += delegate { RefreshUsage(); };

            if (preview)
            {
                form.ApplySnapshot(new UsageSnapshot
                {
                    Success = true,
                    PrimaryRemaining = 82,
                    SecondaryRemaining = 69,
                    PrimaryWindowMinutes = 300,
                    SecondaryWindowMinutes = 10080,
                    FetchedAt = DateTime.Now
                });
                form.StartPosition = FormStartPosition.CenterScreen;
                form.Show();
            }
            else
            {
                RefreshUsage();
            }
        }

        private void RefreshUsage()
        {
            if (Interlocked.Exchange(ref refreshing, 1) == 1) return;
            Task.Run(delegate { return UsageClient.Query(TimeSpan.FromSeconds(15)); })
                .ContinueWith(task =>
                {
                    Interlocked.Exchange(ref refreshing, 0);
                    if (form.IsDisposed) return;
                    UsageSnapshot snapshot = task.IsFaulted
                        ? UsageSnapshot.Failure("Could not read Codex usage.", false)
                        : task.Result;
                    form.BeginInvoke((MethodInvoker)delegate
                    {
                        form.ApplySnapshot(snapshot);
                        tray.Text = form.TrayText;
                        if (snapshot.Success) loginTimer.Stop();
                    });
                });
        }

        private void BeginLogin()
        {
            if (!UsageClient.StartInteractiveLogin())
            {
                tray.ShowBalloonTip(4000, "Codex Usage Pill", "Could not start Codex sign-in.", ToolTipIcon.Error);
                return;
            }
            tray.ShowBalloonTip(5000, "Codex Usage Pill", "Finish signing in to Codex in your browser.", ToolTipIcon.Info);
            loginTimer.Start();
        }

        private static void OpenUsagePage()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://chatgpt.com/codex/cloud/settings/usage",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void UpdatePosition()
        {
            IntPtr window = CodexWindow.FindMainWindow();
            if (window == IntPtr.Zero || NativeMethods.IsIconic(window))
            {
                form.Hide();
                return;
            }

            NativeMethods.Rect rect;
            if (!NativeMethods.GetWindowRect(window, out rect))
            {
                form.Hide();
                return;
            }

            int windowWidth = rect.Right - rect.Left;
            int windowHeight = rect.Bottom - rect.Top;
            Point offset = customOffset ?? new Point(
                Math.Min(220, Math.Max(16, windowWidth - form.Width - 42)),
                windowHeight - form.Height - 27);
            int xOffset = Math.Max(8, Math.Min(offset.X, Math.Max(8, windowWidth - form.Width - 8)));
            int yOffset = Math.Max(8, Math.Min(offset.Y, Math.Max(8, windowHeight - form.Height - 8)));
            if (!form.IsDragging)
                form.Location = new Point(rect.Left + xOffset, rect.Top + yOffset);
            if (!form.Visible) form.ShowInactive();
        }

        private void SavePosition()
        {
            IntPtr window = CodexWindow.FindMainWindow();
            NativeMethods.Rect rect;
            if (window == IntPtr.Zero || !NativeMethods.GetWindowRect(window, out rect)) return;

            int windowWidth = rect.Right - rect.Left;
            int windowHeight = rect.Bottom - rect.Top;
            customOffset = new Point(
                Math.Max(8, Math.Min(form.Left - rect.Left, Math.Max(8, windowWidth - form.Width - 8))),
                Math.Max(8, Math.Min(form.Top - rect.Top, Math.Max(8, windowHeight - form.Height - 8))));
            PositionStore.Save(customOffset.Value);
        }

        private void ResetPosition()
        {
            customOffset = null;
            PositionStore.Clear();
            UpdatePosition();
        }

        private void Exit()
        {
            positionTimer.Stop();
            refreshTimer.Stop();
            loginTimer.Stop();
            tray.Visible = false;
            form.Close();
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                positionTimer.Dispose();
                refreshTimer.Dispose();
                loginTimer.Dispose();
                tray.Dispose();
                trayIcon.Dispose();
                form.Dispose();
            }
            base.Dispose(disposing);
        }

        private static Icon CreateTrayIcon()
        {
            using (var bitmap = new Bitmap(32, 32))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var brush = new SolidBrush(Color.FromArgb(68, 181, 91)))
            using (var textBrush = new SolidBrush(Color.White))
            using (var font = new Font("Segoe UI", 17f, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                graphics.FillEllipse(brush, 1, 1, 30, 30);
                var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                graphics.DrawString("C", font, textBrush, new RectangleF(0, 0, 32, 31), format);
                IntPtr handle = bitmap.GetHicon();
                try { return (Icon)Icon.FromHandle(handle).Clone(); }
                finally { NativeMethods.DestroyIcon(handle); }
            }
        }
    }

    internal static class PositionStore
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexUsagePill", "position.txt");

        public static Point? Load()
        {
            try
            {
                string[] parts = File.ReadAllText(FilePath).Split(',');
                int x;
                int y;
                if (parts.Length == 2 &&
                    int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x) &&
                    int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out y))
                    return new Point(x, y);
            }
            catch { }
            return null;
        }

        public static void Save(Point point)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath,
                    point.X.ToString(CultureInfo.InvariantCulture) + "," +
                    point.Y.ToString(CultureInfo.InvariantCulture));
            }
            catch { }
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch { }
        }
    }

    internal sealed class PillForm : Form
    {
        private readonly Label label;
        private readonly ToolTip tooltip;
        private readonly bool standalonePreview;
        private Point dragCursorStart;
        private Point dragFormStart;

        public string TrayText { get; private set; }
        public bool IsDragging { get; private set; }
        public event EventHandler DragCompleted;

        public PillForm(bool standalonePreview)
        {
            this.standalonePreview = standalonePreview;
            FormBorderStyle = FormBorderStyle.None;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(102, 29);
            MinimumSize = ClientSize;
            MaximumSize = ClientSize;
            ShowInTaskbar = standalonePreview;
            StartPosition = FormStartPosition.Manual;
            TopMost = !standalonePreview;
            Text = standalonePreview ? "Codex Usage Pill Preview" : "Codex Usage Pill";
            BackColor = Color.FromArgb(244, 251, 245);
            DoubleBuffered = true;
            Padding = new Padding(1);
            TrayText = "Codex Usage Pill";

            label = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Codex —",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Semibold", 13f, FontStyle.Regular, GraphicsUnit.Pixel),
                ForeColor = Color.FromArgb(68, 181, 91),
                BackColor = Color.Transparent,
                Cursor = Cursors.SizeAll
            };
            label.MouseDown += BeginDrag;
            label.MouseMove += ContinueDrag;
            label.MouseUp += EndDrag;
            Controls.Add(label);

            tooltip = new ToolTip
            {
                AutoPopDelay = 12000,
                InitialDelay = 250,
                ReshowDelay = 100
            };
            tooltip.SetToolTip(label, "Reading Codex usage…");
            UpdateRegion();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                const int WsExToolWindow = 0x00000080;
                const int WsExNoActivate = 0x08000000;
                CreateParams cp = base.CreateParams;
                if (!standalonePreview) cp.ExStyle |= WsExToolWindow | WsExNoActivate;
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = RoundedPath(ClientRectangle, 14))
            using (var pen = new Pen(Color.FromArgb(185, 226, 190), 1.2f))
            {
                e.Graphics.DrawPath(pen, path);
            }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateRegion();
        }

        public void ShowInactive()
        {
            NativeMethods.ShowWindow(Handle, 4);
        }

        private void BeginDrag(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            IsDragging = true;
            dragCursorStart = Cursor.Position;
            dragFormStart = Location;
            label.Capture = true;
        }

        private void ContinueDrag(object sender, MouseEventArgs e)
        {
            if (!IsDragging) return;
            Point cursor = Cursor.Position;
            Location = new Point(
                dragFormStart.X + cursor.X - dragCursorStart.X,
                dragFormStart.Y + cursor.Y - dragCursorStart.Y);
        }

        private void EndDrag(object sender, MouseEventArgs e)
        {
            if (!IsDragging || e.Button != MouseButtons.Left) return;
            IsDragging = false;
            label.Capture = false;
            EventHandler handler = DragCompleted;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public void ApplySnapshot(UsageSnapshot snapshot)
        {
            if (!snapshot.Success)
            {
                label.Text = "Codex —";
                label.ForeColor = Color.FromArgb(111, 117, 116);
                BackColor = Color.FromArgb(246, 247, 247);
                tooltip.SetToolTip(label, snapshot.AuthenticationRequired
                    ? "Sign in to Codex CLI first.\nRight-click the tray icon and choose ‘Sign in to Codex…’."
                    : snapshot.Error);
                TrayText = "Codex Usage: unavailable";
                Invalidate();
                return;
            }

            int display = snapshot.SecondaryRemaining ?? snapshot.PrimaryRemaining ?? 0;
            int warning = new[] { snapshot.PrimaryRemaining, snapshot.SecondaryRemaining }
                .Where(v => v.HasValue)
                .Select(v => v.Value)
                .DefaultIfEmpty(display)
                .Min();

            label.Text = "Codex " + display.ToString(CultureInfo.InvariantCulture) + "%";
            if (warning < 20)
            {
                label.ForeColor = Color.FromArgb(205, 64, 64);
                BackColor = Color.FromArgb(255, 246, 246);
            }
            else if (warning < 50)
            {
                label.ForeColor = Color.FromArgb(191, 133, 31);
                BackColor = Color.FromArgb(255, 250, 238);
            }
            else
            {
                label.ForeColor = Color.FromArgb(68, 181, 91);
                BackColor = Color.FromArgb(244, 251, 245);
            }

            tooltip.SetToolTip(label, BuildTooltip(snapshot));
            TrayText = "Codex weekly remaining: " + display.ToString(CultureInfo.InvariantCulture) + "%";
            Invalidate();
        }

        private static string BuildTooltip(UsageSnapshot snapshot)
        {
            var lines = new List<string>();
            if (snapshot.PrimaryRemaining.HasValue)
                lines.Add(WindowLabel(snapshot.PrimaryWindowMinutes, "5-hour") + ": " + snapshot.PrimaryRemaining.Value + "% remaining" + ResetLabel(snapshot.PrimaryResetsAt));
            if (snapshot.SecondaryRemaining.HasValue)
                lines.Add(WindowLabel(snapshot.SecondaryWindowMinutes, "Weekly") + ": " + snapshot.SecondaryRemaining.Value + "% remaining" + ResetLabel(snapshot.SecondaryResetsAt));
            lines.Add("Updated: " + snapshot.FetchedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture));
            lines.Add("Drag the pill to move it");
            return string.Join(Environment.NewLine, lines.ToArray());
        }

        private static string WindowLabel(int? minutes, string fallback)
        {
            if (!minutes.HasValue) return fallback;
            if (minutes.Value == 300) return "5-hour";
            if (minutes.Value == 10080) return "Weekly";
            return fallback;
        }

        private static string ResetLabel(long? unixSeconds)
        {
            if (!unixSeconds.HasValue) return string.Empty;
            DateTime local = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddSeconds(unixSeconds.Value).ToLocalTime();
            return " (resets " + local.ToString("MMM d HH:mm", CultureInfo.CurrentCulture) + ")";
        }

        private void UpdateRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            using (GraphicsPath path = RoundedPath(ClientRectangle, 14))
                Region = new Region(path);
        }

        private static GraphicsPath RoundedPath(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            Rectangle r = bounds;
            r.Width -= 1;
            r.Height -= 1;
            int diameter = radius * 2;
            path.AddArc(r.Left, r.Top, diameter, diameter, 180, 90);
            path.AddArc(r.Right - diameter, r.Top, diameter, diameter, 270, 90);
            path.AddArc(r.Right - diameter, r.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(r.Left, r.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal static class CodexWindow
    {
        public static IntPtr FindMainWindow()
        {
            foreach (Process process in Process.GetProcessesByName("ChatGPT"))
            {
                try
                {
                    if (process.MainWindowHandle == IntPtr.Zero) continue;
                    string path = process.MainModule.FileName;
                    if (path.IndexOf("OpenAI.Codex", StringComparison.OrdinalIgnoreCase) >= 0)
                        return process.MainWindowHandle;
                }
                catch
                {
                    if (process.MainWindowHandle != IntPtr.Zero &&
                        string.Equals(process.MainWindowTitle, "ChatGPT", StringComparison.OrdinalIgnoreCase))
                        return process.MainWindowHandle;
                }
            }
            return IntPtr.Zero;
        }
    }

    internal static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        internal static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

        [DllImport("user32.dll")]
        internal static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hWnd, int command);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        internal static extern bool DestroyIcon(IntPtr handle);

        internal static void TryEnablePerMonitorDpi()
        {
            try { SetProcessDpiAwarenessContext(new IntPtr(-4)); }
            catch { }
        }
    }
}
