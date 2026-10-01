using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace CodexUsagePill
{
    internal static class LayoutTests
    {
        [STAThread]
        private static int Main(string[] args)
        {
            NativeMethods.TryEnablePerMonitorDpi();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                using (var form = new PillForm(true))
                {
                    Check((float)typeof(PillForm).GetField("LetterGap", BindingFlags.Static | BindingFlags.NonPublic)
                        .GetRawConstantValue() == 3f, "three-pixel letter spacing");
                    UsageSnapshot weekly = Parse("{\"rateLimits\":{\"primary\":{\"usedPercent\":31,\"windowDurationMins\":10080},\"secondary\":null}}");
                    form.ApplySnapshot(weekly);
                    Check(form.TrayText == "Codex remaining: 69%", "weekly-only percentage");
                    Check(form.Width == 36 && form.Height < 90, "compact weekly-only layout");
                    int singleHeight = form.Height;

                    UsageSnapshot plus = Parse("{\"rateLimits\":{\"primary\":{\"usedPercent\":90,\"windowDurationMins\":300},\"secondary\":{\"usedPercent\":31,\"windowDurationMins\":10080}}}");
                    form.ApplySnapshot(plus);
                    Check(form.TrayText == "Codex 5h: 10% | Week: 69%", "both allowances visible");
                    Check(form.Height > singleHeight && form.Height < 125, "stacked layout");
                    Check(Field<int?>(form, "shortRemaining") == 10 && Field<int?>(form, "weeklyRemaining") == 69, "independent allowances");
                    using (Bitmap bitmap = Render(form))
                    {
                        Rectangle main = Field<Rectangle>(form, "mainBounds");
                        Check(IsRed(bitmap.GetPixel(3, 20)), "short-window background turns red");
                        Check(IsGreen(bitmap.GetPixel(3, main.Top + 20)), "weekly background stays green");
                        Check(IsRed(bitmap.GetPixel(18, 0)), "short-window border turns red");
                        Check(IsGreen(bitmap.GetPixel(18, main.Top)), "weekly border stays green");
                    }

                    UsageSnapshot reversed = Parse("{\"rateLimits\":{\"primary\":{\"usedPercent\":31,\"windowDurationMins\":10080},\"secondary\":{\"usedPercent\":68,\"windowDurationMins\":300}}}");
                    form.ApplySnapshot(reversed);
                    Check(form.TrayText == "Codex 5h: 32% | Week: 69%", "window order does not matter");
                    using (Bitmap bitmap = Render(form))
                        Check(IsAmber(bitmap.GetPixel(18, 0)), "amber border follows its own allowance");
                    if (args.Length > 0) SavePreview(form, weekly, args[0]);

                    form.ApplySnapshot(weekly);
                    Check(form.Height == singleHeight && form.TrayText == "Codex remaining: 69%", "upgrade removes five-hour cell");
                    form.ApplySnapshot(plus);
                    Check(form.Height > singleHeight, "downgrade restores both cells");

                    UsageSnapshot bucket = Parse("{\"rateLimits\":{\"primary\":{\"usedPercent\":99,\"windowDurationMins\":10080}},\"rateLimitsByLimitId\":{\"codex\":{\"primary\":{\"usedPercent\":31,\"windowDurationMins\":10080}}}}");
                    form.ApplySnapshot(bucket);
                    Check(form.TrayText == "Codex remaining: 69%", "named Codex bucket wins over legacy data");
                    form.ApplySnapshot(Parse("{\"rateLimits\":{\"primary\":{\"usedPercent\":0,\"windowDurationMins\":300}}}"));
                    Check(form.TrayText == "Codex 5h: 100%" && form.Height == 40, "five-hour-only response has no invented weekly allowance");
                    form.ApplySnapshot(UsageSnapshot.Failure("Test outage", false));
                    Check(form.TrayText == "Codex Usage: unavailable" && form.Height == singleHeight, "failure does not show stale percentages");
                }
                Console.WriteLine("PASS: layouts, window mapping, upgrade/downgrade, independent colors, and unavailable state.");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error);
                return 1;
            }
        }

        private static UsageSnapshot Parse(string json)
        {
            var payload = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
            return (UsageSnapshot)typeof(UsageClient).GetMethod("ParseSnapshot",
                BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { payload });
        }

        private static T Field<T>(object instance, string name)
        {
            return (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
        }

        private static Bitmap Render(PillForm form)
        {
            var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            return bitmap;
        }

        private static bool IsRed(Color color) { return color.R > color.G + 5 && Math.Abs(color.G - color.B) < 5; }
        private static bool IsGreen(Color color) { return color.G > color.R + 3 && color.G > color.B + 3; }
        private static bool IsAmber(Color color) { return color.R > color.G && color.G > color.B + 10; }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("FAIL: " + message);
        }

        private static void SavePreview(PillForm form, UsageSnapshot weekly, string directory)
        {
            Directory.CreateDirectory(directory);
            using (Bitmap dual = Render(form))
            {
                form.ApplySnapshot(weekly);
                using (Bitmap single = Render(form))
                using (var preview = new Bitmap(300, 180))
                using (Graphics graphics = Graphics.FromImage(preview))
                using (var font = new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    graphics.Clear(Color.FromArgb(249, 251, 253));
                    graphics.DrawString("Weekly only", font, Brushes.Black, 20, 10);
                    graphics.DrawString("5-hour + weekly", font, Brushes.Black, 155, 10);
                    using (var rail = new SolidBrush(Color.FromArgb(237, 242, 246)))
                    {
                        graphics.FillRectangle(rail, 32, 35, 42, 138);
                        graphics.FillRectangle(rail, 177, 35, 42, 138);
                    }
                    graphics.DrawImageUnscaled(single, 35, 42);
                    graphics.DrawImageUnscaled(dual, 180, 42);
                    preview.Save(Path.Combine(directory, "screenshot.png"), ImageFormat.Png);
                }
            }
        }
    }
}
