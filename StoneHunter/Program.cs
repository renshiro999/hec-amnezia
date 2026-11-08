using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    private const uint LEFTDOWN = 0x0002, LEFTUP = 0x0004;

    private readonly Button start = new() { Text = "START", Width = 110, Height = 38 };
    private readonly Button stop = new() { Text = "STOP", Width = 110, Height = 38, Enabled = false };
    private readonly Button choose = new() { Text = "Alege imagine...", Width = 140, Height = 32 };
    private readonly TrackBar sensitivity = new() { Minimum = 55, Maximum = 95, Value = 78, TickFrequency = 5, Width = 230 };
    private readonly NumericUpDown delay = new() { Minimum = 100, Maximum = 3000, Increment = 100, Value = 500, Width = 90 };
    private readonly Label status = new() { AutoSize = true, Text = "Pregătit. Deschide jocul și apasă START." };
    private readonly Label templateLabel = new() { AutoSize = true };
    private string templatePath = Path.Combine(AppContext.BaseDirectory, "piatra.png");
    private CancellationTokenSource? cancel;
    private volatile bool running;

    public MainForm()
    {
        Text = "StoneHunter";
        Width = 510; Height = 275;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        layout.Controls.Add(new Label { Text = "StoneHunter — detectare și click automat", AutoSize = true, Font = new Font("Segoe UI", 12, FontStyle.Bold) });
        layout.Controls.Add(new Label { Text = "Imagine de referință:", AutoSize = true });
        layout.Controls.Add(choose);
        layout.Controls.Add(templateLabel);
        layout.Controls.Add(new Label { Text = "Sensibilitate (mai mică = detectează mai ușor):", AutoSize = true });
        layout.Controls.Add(sensitivity);
        var delayPanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        delayPanel.Controls.Add(new Label { Text = "Pauză între click-uri (ms):", AutoSize = true, Padding = new Padding(0, 7, 4, 0) });
        delayPanel.Controls.Add(delay);
        layout.Controls.Add(delayPanel);
        var buttons = new FlowLayoutPanel { AutoSize = true };
        buttons.Controls.Add(start); buttons.Controls.Add(stop);
        layout.Controls.Add(buttons);
        layout.Controls.Add(status);
        Controls.Add(layout);

        choose.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Filter = "Imagini|*.png;*.jpg;*.jpeg;*.bmp" };
            if (dlg.ShowDialog() == DialogResult.OK) { templatePath = dlg.FileName; UpdateTemplateLabel(); }
        };
        start.Click += (_, _) => StartBot();
        stop.Click += (_, _) => StopBot();
        FormClosing += (_, _) => StopBot();
        UpdateTemplateLabel();
    }

    private void UpdateTemplateLabel() =>
        templateLabel.Text = File.Exists(templatePath) ? Path.GetFileName(templatePath) : "Nu există piatra.png — alege o imagine.";

    private void StartBot()
    {
        if (running) return;
        if (!File.Exists(templatePath)) { MessageBox.Show("Alege o imagine de referință."); return; }
        running = true; cancel = new CancellationTokenSource();
        start.Enabled = false; stop.Enabled = true;
        status.Text = "Rulează. Mută jocul în prim-plan; STOP oprește click-urile.";
        var token = cancel.Token;
        Task.Run(() => ScanLoop(token), token);
    }

    private void StopBot()
    {
        running = false;
        cancel?.Cancel();
        if (IsHandleCreated && !IsDisposed)
            BeginInvoke(new Action(() => { start.Enabled = true; stop.Enabled = false; status.Text = "Oprit."; }));
    }

    private void ScanLoop(CancellationToken token)
    {
        try
        {
            using var source = new Bitmap(templatePath);
            var variants = BuildVariants(source);
            while (!token.IsCancellationRequested)
            {
                var bounds = Screen.PrimaryScreen!.Bounds;
                using var screen = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(screen)) g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                var hits = FindHits(screen, variants, sensitivity.Value / 100.0);
                foreach (var hit in hits.Take(8))
                {
                    if (token.IsCancellationRequested) break;
                    SetCursorPos(bounds.Left + hit.X, bounds.Top + hit.Y);
                    mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                    mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero);
                    Thread.Sleep((int)delay.Value);
                }
                Thread.Sleep(120);
            }
        }
        catch (Exception ex)
        {
            if (!IsDisposed && IsHandleCreated)
                BeginInvoke(new Action(() => { status.Text = "Eroare: " + ex.Message; start.Enabled = true; stop.Enabled = false; }));
        }
    }

    private sealed record SampleTemplate(int Width, int Height, (int X, int Y, double V)[] Samples);
    private sealed record Hit(int X, int Y, double Score);

    private static List<SampleTemplate> BuildVariants(Bitmap original)
    {
        var result = new List<SampleTemplate>();
        foreach (double scale in new[] { 0.75, 0.9, 1.0, 1.1, 1.25 })
        foreach (float angle in new[] { -35f, -20f, 0f, 20f, 35f })
        {
            int w = Math.Max(30, (int)(original.Width * scale));
            int h = Math.Max(30, (int)(original.Height * scale));
            using var transformed = new Bitmap(w, h);
            using (var g = Graphics.FromImage(transformed))
            {
                g.Clear(Color.Black);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.TranslateTransform(w / 2f, h / 2f);
                g.RotateTransform(angle);
                g.DrawImage(original, -w / 2f, -h / 2f, w, h);
            }
            var samples = ReadGray(transformed);
            var points = new List<(int, int, double)>();
            for (int y = 4; y < h - 4; y += 8)
                for (int x = 4; x < w - 4; x += 8)
                    points.Add((x, y, samples[y * w + x]));
            if (points.Count > 20) result.Add(new SampleTemplate(w, h, points.ToArray()));
        }
        return result;
    }

    private static byte[] ReadGray(Bitmap bmp)
    {
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            int stride = Math.Abs(data.Stride);
            byte[] raw = new byte[stride * bmp.Height];
            Marshal.Copy(data.Scan0, raw, 0, raw.Length);
            byte[] gray = new byte[bmp.Width * bmp.Height];
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                {
                    int i = y * stride + x * 3;
                    gray[y * bmp.Width + x] = (byte)((raw[i] * 29 + raw[i + 1] * 150 + raw[i + 2] * 77) >> 8);
                }
            return gray;
        }
        finally { bmp.UnlockBits(data); }
    }

    private static List<Hit> FindHits(Bitmap screen, List<SampleTemplate> variants, double threshold)
    {
        var pixels = ReadGray(screen);
        int sw = screen.Width, sh = screen.Height;
        var hits = new List<Hit>();
        foreach (var t in variants)
        {
            if (t.Width >= sw || t.Height >= sh) continue;
            int stride = 8;
            for (int y = 0; y <= sh - t.Height; y += stride)
            for (int x = 0; x <= sw - t.Width; x += stride)
            {
                double sumA = 0, sumB = 0, sumAA = 0, sumBB = 0, sumAB = 0;
                int n = t.Samples.Length;
                foreach (var p in t.Samples)
                {
                    double a = p.V;
                    double b = pixels[(y + p.Y) * sw + x + p.X];
                    sumA += a; sumB += b; sumAA += a * a; sumBB += b * b; sumAB += a * b;
                }
                double numerator = sumAB - sumA * sumB / n;
                double denomA = sumAA - sumA * sumA / n;
                double denomB = sumBB - sumB * sumB / n;
                double score = denomA <= 0 || denomB <= 0 ? 0 : numerator / Math.Sqrt(denomA * denomB);
                if (score >= threshold)
                {
                    int cx = x + t.Width / 2, cy = y + t.Height / 2;
                    if (!hits.Any(h => Math.Abs(h.X - cx) < t.Width / 2 && Math.Abs(h.Y - cy) < t.Height / 2))
                        hits.Add(new Hit(cx, cy, score));
                }
            }
        }
        return hits.OrderByDescending(h => h.Score).ToList();
    }
}
