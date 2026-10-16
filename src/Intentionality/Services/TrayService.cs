using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Windows.Threading;

namespace Intentionality.Services;

public sealed class TrayService : IDisposable
{
    private NotifyIcon? _icon;
    private ToolStripMenuItem? _endSessionItem;
    private readonly Dispatcher _dispatcher;

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
        var icon = !string.IsNullOrEmpty(exe) ? Icon.ExtractAssociatedIcon(exe) : null;

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
            Icon = icon ?? SystemIcons.Application,
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

    public void UpdateRemaining(TimeSpan? remaining)
    {
        if (_icon is null)
            return;

        string text;
        if (remaining is null)
            text = "Intentionality";
        else if (remaining.Value <= TimeSpan.Zero)
            text = "Zeit ist um";
        else
        {
            var r = remaining.Value;
            text = r.TotalHours >= 1
                ? $"Noch {r.Hours:D2}:{r.Minutes:D2}:{r.Seconds:D2}"
                : $"Noch {r.Minutes:D2}:{r.Seconds:D2}";
        }

        if (text.Length > 63)
            text = text[..63];

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
    }

    public void Dispose() => Hide();

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
