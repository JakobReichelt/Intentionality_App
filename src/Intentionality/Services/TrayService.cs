using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Threading;

namespace Intentionality.Services;

public sealed class TrayService : IDisposable
{
    private const int TooltipMaxLength = 127;

    private NotifyIcon? _icon;
    private ToolStripMenuItem? _endSessionItem;
    private readonly Dispatcher _dispatcher;
    private Icon? _appIcon;
    private Icon? _countdownIcon;
    private string? _lastIconLabel;

    public event Action? EndSessionRequested;

    public TrayService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void Show()
    {
        if (_icon is not null)
            return;

        var exe = Environment.ProcessPath;
        _appIcon = !string.IsNullOrEmpty(exe) ? Icon.ExtractAssociatedIcon(exe) : null;

        var menu = new TipMenu();
        _endSessionItem = new ToolStripMenuItem("Sitzung beenden")
        {
            Enabled = false,
            ForeColor = Color.White,
            BackColor = TipMenu.BackgroundColor,
            Padding = new Padding(4, 2, 4, 2)
        };
        _endSessionItem.Click += (_, _) =>
            _dispatcher.BeginInvoke(() => EndSessionRequested?.Invoke());
        menu.Items.Add(_endSessionItem);

        _icon = new NotifyIcon
        {
            Icon = _appIcon ?? SystemIcons.Application,
            Visible = true,
            Text = "Intentionality",
            ContextMenuStrip = menu
        };
    }

    public void SetSessionActive(bool active)
    {
        void Apply()
        {
            if (_endSessionItem is not null)
                _endSessionItem.Enabled = active;
        }

        if (_dispatcher.CheckAccess())
            Apply();
        else
            _dispatcher.Invoke(Apply);
    }

    public void UpdateRemaining(TimeSpan? remaining, string? goal = null)
    {
        if (_icon is null)
            return;

        string text;
        if (remaining is null)
        {
            text = "Intentionality";
            SetAppIcon();
        }
        else if (remaining.Value <= TimeSpan.Zero)
        {
            text = BuildTooltip("Zeit ist um", goal);
            SetCountdownIcon("0");
        }
        else
        {
            var r = remaining.Value;
            var clock = r.TotalHours >= 1
                ? $"Noch {r.Hours:D2}:{r.Minutes:D2}:{r.Seconds:D2}"
                : $"Noch {r.Minutes:D2}:{r.Seconds:D2}";
            text = BuildTooltip(clock, goal);
            SetCountdownIcon(FormatIconLabel(r));
        }

        if (text.Length > TooltipMaxLength)
            text = text[..(TooltipMaxLength - 1)] + "…";

        void Apply()
        {
            if (_icon is not null)
                _icon.Text = text;
        }

        if (_dispatcher.CheckAccess())
            Apply();
        else
            _dispatcher.Invoke(Apply);
    }

    public void Hide()
    {
        if (_icon is null)
            return;
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
        _icon = null;
        _endSessionItem = null;
        DisposeCountdownIcon();
        _appIcon?.Dispose();
        _appIcon = null;
        _lastIconLabel = null;
    }

    public void Dispose() => Hide();

    private static string BuildTooltip(string clock, string? goal)
    {
        if (string.IsNullOrWhiteSpace(goal))
            return clock;

        var trimmed = goal.Trim();
        return $"{clock} – {trimmed}";
    }

    private static string FormatIconLabel(TimeSpan remaining)
    {
        if (remaining.TotalHours >= 1)
        {
            var hours = (int)Math.Ceiling(remaining.TotalHours);
            return hours >= 10 ? "9h+" : $"{hours}h";
        }

        var minutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
        return minutes.ToString();
    }

    private void SetAppIcon()
    {
        if (_lastIconLabel is null && _icon?.Icon == (_appIcon ?? SystemIcons.Application))
            return;

        _lastIconLabel = null;
        void Apply()
        {
            if (_icon is null)
                return;
            _icon.Icon = _appIcon ?? SystemIcons.Application;
            DisposeCountdownIcon();
        }

        if (_dispatcher.CheckAccess())
            Apply();
        else
            _dispatcher.Invoke(Apply);
    }

    private void SetCountdownIcon(string label)
    {
        if (_lastIconLabel == label)
            return;

        _lastIconLabel = label;
        var drawn = DrawCountdownIcon(label);

        void Apply()
        {
            if (_icon is null)
            {
                drawn.Dispose();
                return;
            }

            var previous = _countdownIcon;
            _countdownIcon = drawn;
            _icon.Icon = drawn;
            previous?.Dispose();
        }

        if (_dispatcher.CheckAccess())
            Apply();
        else
            _dispatcher.Invoke(Apply);
    }

    private void DisposeCountdownIcon()
    {
        _countdownIcon?.Dispose();
        _countdownIcon = null;
    }

    private static Icon DrawCountdownIcon(string label)
    {
        const int size = 16;
        using var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            using var bg = new SolidBrush(Color.FromArgb(45, 45, 45));
            g.FillEllipse(bg, 0, 0, size - 1, size - 1);

            var fontSize = label.Length >= 3 ? 5.5f : label.Length == 2 ? 7f : 9f;
            using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(Color.White);
            var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(label, font, brush, new RectangleF(0, 0.5f, size, size), format);
        }

        var hIcon = bitmap.GetHicon();
        // Clone so we own a managed Icon; DestroyIcon frees the temporary GetHicon handle.
        using var temp = Icon.FromHandle(hIcon);
        var clone = (Icon)temp.Clone();
        DestroyIcon(hIcon);
        return clone;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private sealed class TipMenu : ContextMenuStrip
    {
        public static readonly Color BackgroundColor = Color.FromArgb(45, 45, 45);

        public TipMenu()
        {
            ShowImageMargin = false;
            ShowCheckMargin = false;
            Font = new Font("Segoe UI", 9f);
            ForeColor = Color.White;
            BackColor = BackgroundColor;
            Padding = new Padding(2, 2, 2, 2);
            Renderer = new TipRenderer();
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            using var path = Round(ClientRectangle, 8);
            Region = new Region(path);
        }

        private static GraphicsPath Round(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    private sealed class TipRenderer : ToolStripProfessionalRenderer
    {
        static readonly Color Hover = Color.FromArgb(90, 90, 90);

        public TipRenderer() : base(new TipColors())
        {
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.Clear(TipMenu.BackgroundColor);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var color = e.Item.Selected || e.Item.Pressed ? Hover : TipMenu.BackgroundColor;
            using var brush = new SolidBrush(color);
            e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            TextRenderer.DrawText(
                e.Graphics,
                e.Text,
                e.Item.Font,
                e.TextRectangle,
                Color.White,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }

    private sealed class TipColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => TipMenu.BackgroundColor;
        public override Color MenuBorder => TipMenu.BackgroundColor;
        public override Color MenuItemBorder => TipMenu.BackgroundColor;
        public override Color MenuItemSelected => Color.FromArgb(68, 68, 68);
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(68, 68, 68);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(68, 68, 68);
        public override Color ImageMarginGradientBegin => TipMenu.BackgroundColor;
        public override Color ImageMarginGradientMiddle => TipMenu.BackgroundColor;
        public override Color ImageMarginGradientEnd => TipMenu.BackgroundColor;
    }
}
