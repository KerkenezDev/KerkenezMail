using System;
using System.Drawing;
using System.Windows.Forms;

namespace EmailSummarizer.UI.Controls
{
    public class SidebarNav : Panel
    {
        public event EventHandler<int>? TabChanged;

        private readonly string[] _tabTitles = new[]
        {
            "Summaries",
            "Accounts",
            "Settings",
            "Live Logs"
        };

        private readonly string[] _tabIcons = new[]
        {
            "✉",
            "👥",
            "⚙",
            "≡"
        };

        private int _selectedIndex = 0;
        private int _hoveredIndex = -1;

        private readonly Color _bgColor = Color.FromArgb(240, 242, 245);
        private readonly Color _activeBgColor = Color.FromArgb(255, 255, 255);
        private readonly Color _hoverBgColor = Color.FromArgb(230, 233, 238);
        private readonly Color _textColor = Color.FromArgb(50, 54, 62);
        private readonly Color _activeTextColor = Color.FromArgb(0, 102, 204);
        private readonly Color _accentColor = Color.FromArgb(0, 120, 215);
        private readonly Color _borderColor = Color.FromArgb(218, 222, 228);

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex != value && value >= 0 && value < _tabTitles.Length)
                {
                    _selectedIndex = value;
                    Invalidate();
                    TabChanged?.Invoke(this, _selectedIndex);
                }
            }
        }

        public SidebarNav()
        {
            this.DoubleBuffered = true;
            this.Width = 230; // Scalable generous width
            this.Dock = DockStyle.Left;
            this.BackColor = _bgColor;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.Cursor = Cursors.Hand;
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            float scale = this.DeviceDpi / 96f;
            this.Width = (int)(230 * scale);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            float scale = this.DeviceDpi / 96f;
            int headerH = (int)(72 * scale);
            int itemH = (int)(46 * scale);

            int prevHover = _hoveredIndex;

            if (e.Y >= headerH && e.Y < headerH + (_tabTitles.Length * itemH))
            {
                _hoveredIndex = (e.Y - headerH) / itemH;
            }
            else
            {
                _hoveredIndex = -1;
            }

            if (prevHover != _hoveredIndex)
            {
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredIndex != -1)
            {
                _hoveredIndex = -1;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && _hoveredIndex >= 0 && _hoveredIndex < _tabTitles.Length)
            {
                SelectedIndex = _hoveredIndex;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float scale = this.DeviceDpi / 96f;
            int headerH = (int)(72 * scale);
            int itemH = (int)(46 * scale);

            // Background
            using (var bgBrush = new SolidBrush(_bgColor))
            {
                g.FillRectangle(bgBrush, this.ClientRectangle);
            }

            // Right border
            using (var borderPen = new Pen(_borderColor, 1))
            {
                g.DrawLine(borderPen, this.Width - 1, 0, this.Width - 1, this.Height);
            }

            // App Brand Header
            using (var titleFont = new Font("Segoe UI", 11.5F, FontStyle.Bold))
            using (var subFont = new Font("Segoe UI", 8.5F, FontStyle.Regular))
            using (var titleBrush = new SolidBrush(Color.FromArgb(20, 20, 20)))
            using (var subBrush = new SolidBrush(Color.FromArgb(110, 110, 110)))
            {
                g.DrawString("Email Summarizer", titleFont, titleBrush, new PointF(16 * scale, 14 * scale));
                g.DrawString("Win32 AI Assistant", subFont, subBrush, new PointF(16 * scale, 40 * scale));
            }

            // Header separator line
            using (var sepPen = new Pen(_borderColor, 1))
            {
                g.DrawLine(sepPen, 14 * scale, headerH - 8, this.Width - (14 * scale), headerH - 8);
            }

            // Draw Tabs
            for (int i = 0; i < _tabTitles.Length; i++)
            {
                int itemY = headerH + (i * itemH);
                var itemRect = new Rectangle((int)(10 * scale), itemY, this.Width - (int)(20 * scale), itemH - (int)(4 * scale));
                bool isSelected = (i == _selectedIndex);
                bool isHovered = (i == _hoveredIndex && !isSelected);

                // Tab Item Background
                if (isSelected)
                {
                    using var activeBrush = new SolidBrush(_activeBgColor);
                    using var activeBorderPen = new Pen(_borderColor);
                    FillRoundedRectangle(g, activeBrush, itemRect, 5);
                    DrawRoundedRectangle(g, activeBorderPen, itemRect, 5);

                    // Left Accent Indicator
                    using var accentBrush = new SolidBrush(_accentColor);
                    g.FillRectangle(accentBrush, new Rectangle(itemRect.Left + 2, itemRect.Top + 6, 4, itemRect.Height - 12));
                }
                else if (isHovered)
                {
                    using var hoverBrush = new SolidBrush(_hoverBgColor);
                    FillRoundedRectangle(g, hoverBrush, itemRect, 5);
                }

                // Tab Icon & Text
                var textColor = isSelected ? _activeTextColor : _textColor;
                var fontStyle = isSelected ? FontStyle.Bold : FontStyle.Regular;
                using var itemFont = new Font("Segoe UI", 9.75F, fontStyle);
                using var textBrush = new SolidBrush(textColor);

                var stringFormat = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center
                };

                // Draw clean icon
                using var iconFont = new Font("Segoe UI Symbol", 11F, FontStyle.Regular);
                int iconLeft = itemRect.Left + (int)(10 * scale);
                int iconWidth = (int)(24 * scale);
                g.DrawString(_tabIcons[i], iconFont, textBrush, new Rectangle(iconLeft, itemRect.Top, iconWidth, itemRect.Height), stringFormat);

                // Draw label text
                int textLeft = iconLeft + iconWidth + (int)(6 * scale);
                var textRect = new Rectangle(textLeft, itemRect.Top, itemRect.Width - (textLeft - itemRect.Left) - 4, itemRect.Height);
                g.DrawString(_tabTitles[i], itemFont, textBrush, textRect, stringFormat);
            }
        }

        private static void FillRoundedRectangle(Graphics g, Brush brush, Rectangle bounds, int cornerRadius)
        {
            using var path = GetRoundedPath(bounds, cornerRadius);
            g.FillPath(brush, path);
        }

        private static void DrawRoundedRectangle(Graphics g, Pen pen, Rectangle bounds, int cornerRadius)
        {
            using var path = GetRoundedPath(bounds, cornerRadius);
            g.DrawPath(pen, path);
        }

        private static System.Drawing.Drawing2D.GraphicsPath GetRoundedPath(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
