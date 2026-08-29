using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EmailSummarizer.UI.Controls
{
    public class SidebarNav : Panel
    {
        public const int ExpandedWidth = 195;
        public const int CollapsedWidth = 60;

        public event EventHandler<int>? TabChanged;
        public event EventHandler<bool>? CollapsedChanged;

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
        private bool _isToggleHovered = false;
        private bool _isCollapsed = false;

        private readonly System.Windows.Forms.Timer _animTimer;
        private int _startWidth;
        private int _targetWidth;
        private int _animFrame = 0;
        private const int TotalAnimFrames = 6; // ~90ms fast micro-animation

        private readonly ToolTip _toolTip;
        private string _currentToolTipText = "";

        private readonly Color _bgColor = Color.FromArgb(240, 242, 245);
        private readonly Color _activeBgColor = Color.FromArgb(255, 255, 255);
        private readonly Color _hoverBgColor = Color.FromArgb(230, 233, 238);
        private readonly Color _btnHoverBgColor = Color.FromArgb(220, 224, 230);
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

        public bool IsCollapsed
        {
            get => _isCollapsed;
            set
            {
                if (_isCollapsed != value)
                {
                    _isCollapsed = value;
                    StartAnimation();
                    CollapsedChanged?.Invoke(this, _isCollapsed);
                }
            }
        }

        public SidebarNav()
        {
            this.DoubleBuffered = true;
            this.Dock = DockStyle.Left;
            this.BackColor = _bgColor;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.Cursor = Cursors.Default;

            _animTimer = new System.Windows.Forms.Timer { Interval = 15 };
            _animTimer.Tick += OnAnimTimerTick;

            _toolTip = new ToolTip
            {
                InitialDelay = 200,
                ReshowDelay = 100,
                AutoPopDelay = 3000,
                ShowAlways = true
            };

            float scale = this.DeviceDpi / 96f;
            this.Width = _isCollapsed ? (int)(CollapsedWidth * scale) : (int)(ExpandedWidth * scale);
        }

        public void ToggleCollapsed()
        {
            IsCollapsed = !IsCollapsed;
        }

        private void StartAnimation()
        {
            float scale = this.DeviceDpi / 96f;
            _targetWidth = _isCollapsed ? (int)(CollapsedWidth * scale) : (int)(ExpandedWidth * scale);

            if (!this.IsHandleCreated || !this.Visible)
            {
                this.Width = _targetWidth;
                Invalidate();
                return;
            }

            _startWidth = this.Width;
            _animFrame = 0;
            _animTimer.Stop();
            _animTimer.Start();
        }

        private void OnAnimTimerTick(object? sender, EventArgs e)
        {
            _animFrame++;
            float t = (float)_animFrame / TotalAnimFrames;
            // Ease-out cubic: 1 - (1-t)^3
            float ease = 1f - (float)Math.Pow(1f - t, 3);
            int currentW = (int)Math.Round(_startWidth + (_targetWidth - _startWidth) * ease);

            if (_animFrame >= TotalAnimFrames || currentW == _targetWidth)
            {
                _animTimer.Stop();
                this.Width = _targetWidth;
            }
            else
            {
                this.Width = currentW;
            }

            Invalidate();
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            float scale = this.DeviceDpi / 96f;
            this.Width = _isCollapsed ? (int)(CollapsedWidth * scale) : (int)(ExpandedWidth * scale);
            Invalidate();
        }

        private Rectangle GetToggleButtonBounds(float scale)
        {
            bool isWide = this.Width >= (int)(130 * scale);
            if (!isWide)
            {
                int btnW = (int)(32 * scale);
                int btnH = (int)(28 * scale);
                return new Rectangle((this.Width - btnW) / 2, (int)(18 * scale), btnW, btnH);
            }
            else
            {
                int btnSize = (int)(24 * scale);
                return new Rectangle(this.Width - (int)(30 * scale), (int)(18 * scale), btnSize, btnSize);
            }
        }

        private void UpdateToolTip(string text, Point pt)
        {
            if (_currentToolTipText != text)
            {
                _currentToolTipText = text;
                if (string.IsNullOrEmpty(text))
                {
                    _toolTip.Hide(this);
                }
                else
                {
                    _toolTip.Show(text, this, pt.X + 16, pt.Y + 8, 3000);
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            float scale = this.DeviceDpi / 96f;
            int headerH = (int)(72 * scale);
            int itemH = (int)(46 * scale);

            bool prevToggleHover = _isToggleHovered;
            int prevHover = _hoveredIndex;

            var toggleRect = GetToggleButtonBounds(scale);

            if (toggleRect.Contains(e.Location))
            {
                _isToggleHovered = true;
                _hoveredIndex = -1;
                this.Cursor = Cursors.Hand;
                string toggleTip = _isCollapsed ? "Expand sidebar (Ctrl+B)" : "Collapse sidebar (Ctrl+B)";
                UpdateToolTip(toggleTip, e.Location);
            }
            else if (e.Y >= headerH && e.Y < headerH + (_tabTitles.Length * itemH))
            {
                _isToggleHovered = false;
                _hoveredIndex = (e.Y - headerH) / itemH;
                this.Cursor = Cursors.Hand;

                if (_isCollapsed && _hoveredIndex >= 0 && _hoveredIndex < _tabTitles.Length)
                {
                    UpdateToolTip(_tabTitles[_hoveredIndex], e.Location);
                }
                else
                {
                    UpdateToolTip("", Point.Empty);
                }
            }
            else
            {
                _isToggleHovered = false;
                _hoveredIndex = -1;
                this.Cursor = Cursors.Default;
                UpdateToolTip("", Point.Empty);
            }

            if (prevToggleHover != _isToggleHovered || prevHover != _hoveredIndex)
            {
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isToggleHovered = false;
            _hoveredIndex = -1;
            this.Cursor = Cursors.Default;
            UpdateToolTip("", Point.Empty);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                if (_isToggleHovered)
                {
                    ToggleCollapsed();
                    return;
                }

                if (_hoveredIndex >= 0 && _hoveredIndex < _tabTitles.Length)
                {
                    SelectedIndex = _hoveredIndex;
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float scale = this.DeviceDpi / 96f;
            int headerH = (int)(72 * scale);
            int itemH = (int)(46 * scale);
            bool isWide = this.Width >= (int)(130 * scale);

            // Background
            using (var bgBrush = new SolidBrush(_bgColor))
            {
                g.FillRectangle(bgBrush, this.ClientRectangle);
            }

            // Right border line
            using (var borderPen = new Pen(_borderColor, 1))
            {
                g.DrawLine(borderPen, this.Width - 1, 0, this.Width - 1, this.Height);
            }

            // Header Section
            var toggleRect = GetToggleButtonBounds(scale);

            if (isWide)
            {
                // App Brand Title & Subtitle
                using (var titleFont = new Font("Segoe UI", 10.75F, FontStyle.Bold))
                using (var subFont = new Font("Segoe UI", 8.25F, FontStyle.Regular))
                using (var titleBrush = new SolidBrush(Color.FromArgb(20, 20, 20)))
                using (var subBrush = new SolidBrush(Color.FromArgb(110, 110, 110)))
                {
                    var textFormat = new StringFormat
                    {
                        Trimming = StringTrimming.EllipsisCharacter,
                        FormatFlags = StringFormatFlags.NoWrap
                    };

                    int titleMaxWidth = toggleRect.Left - (int)(14 * scale);
                    if (titleMaxWidth > 20)
                    {
                        var titleRect = new RectangleF(12 * scale, 14 * scale, titleMaxWidth, 22 * scale);
                        var subRect = new RectangleF(12 * scale, 38 * scale, titleMaxWidth, 18 * scale);
                        g.DrawString("Email Summarizer", titleFont, titleBrush, titleRect, textFormat);
                        g.DrawString("Win32 AI Assistant", subFont, subBrush, subRect, textFormat);
                    }
                }

                // Collapse Toggle Button ("«")
                if (_isToggleHovered)
                {
                    using var btnHoverBrush = new SolidBrush(_btnHoverBgColor);
                    using var btnBorderPen = new Pen(_borderColor);
                    FillRoundedRectangle(g, btnHoverBrush, toggleRect, 4);
                    DrawRoundedRectangle(g, btnBorderPen, toggleRect, 4);
                }

                using (var btnFont = new Font("Segoe UI", 10.5F, FontStyle.Bold))
                using (var btnBrush = new SolidBrush(_isToggleHovered ? _activeTextColor : Color.FromArgb(100, 105, 115)))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("«", btnFont, btnBrush, toggleRect, sf);
                }

                // Header separator line
                using (var sepPen = new Pen(_borderColor, 1))
                {
                    g.DrawLine(sepPen, 12 * scale, headerH - 8, this.Width - (12 * scale), headerH - 8);
                }
            }
            else
            {
                // Collapsed Mode Toggle Button ("»")
                if (_isToggleHovered)
                {
                    using var btnHoverBrush = new SolidBrush(_btnHoverBgColor);
                    using var btnBorderPen = new Pen(_borderColor);
                    FillRoundedRectangle(g, btnHoverBrush, toggleRect, 4);
                    DrawRoundedRectangle(g, btnBorderPen, toggleRect, 4);
                }
                else
                {
                    using var btnBgBrush = new SolidBrush(Color.FromArgb(248, 249, 250));
                    using var btnBorderPen = new Pen(_borderColor);
                    FillRoundedRectangle(g, btnBgBrush, toggleRect, 4);
                    DrawRoundedRectangle(g, btnBorderPen, toggleRect, 4);
                }

                using (var btnFont = new Font("Segoe UI", 10.5F, FontStyle.Bold))
                using (var btnBrush = new SolidBrush(_isToggleHovered ? _activeTextColor : Color.FromArgb(80, 85, 95)))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("»", btnFont, btnBrush, toggleRect, sf);
                }

                // Header separator line
                using (var sepPen = new Pen(_borderColor, 1))
                {
                    g.DrawLine(sepPen, 8 * scale, headerH - 8, this.Width - (8 * scale), headerH - 8);
                }
            }

            // Draw Navigation Tab Items
            for (int i = 0; i < _tabTitles.Length; i++)
            {
                int itemY = headerH + (i * itemH);
                bool isSelected = (i == _selectedIndex);
                bool isHovered = (i == _hoveredIndex && !isSelected);

                var itemRect = isWide
                    ? new Rectangle((int)(8 * scale), itemY, this.Width - (int)(16 * scale), itemH - (int)(4 * scale))
                    : new Rectangle((int)(8 * scale), itemY, this.Width - (int)(16 * scale), itemH - (int)(4 * scale));

                // Tab Item Background
                if (isSelected)
                {
                    using var activeBrush = new SolidBrush(_activeBgColor);
                    using var activeBorderPen = new Pen(_borderColor);
                    FillRoundedRectangle(g, activeBrush, itemRect, 5);
                    DrawRoundedRectangle(g, activeBorderPen, itemRect, 5);

                    // Left Accent Indicator
                    using var accentBrush = new SolidBrush(_accentColor);
                    g.FillRectangle(accentBrush, new Rectangle(itemRect.Left + 2, itemRect.Top + 6, isWide ? 4 : 3, itemRect.Height - 12));
                }
                else if (isHovered)
                {
                    using var hoverBrush = new SolidBrush(_hoverBgColor);
                    FillRoundedRectangle(g, hoverBrush, itemRect, 5);
                }

                // Tab Icon & Text Colors
                var textColor = isSelected ? _activeTextColor : _textColor;
                var fontStyle = isSelected ? FontStyle.Bold : FontStyle.Regular;
                using var itemFont = new Font("Segoe UI", 9.25F, fontStyle);
                using var textBrush = new SolidBrush(textColor);
                using var iconFont = new Font("Segoe UI Symbol", 11F, FontStyle.Regular);

                if (isWide)
                {
                    var stringFormat = new StringFormat
                    {
                        Alignment = StringAlignment.Near,
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter,
                        FormatFlags = StringFormatFlags.NoWrap
                    };

                    // Draw icon
                    int iconLeft = itemRect.Left + (int)(8 * scale);
                    int iconWidth = (int)(22 * scale);
                    g.DrawString(_tabIcons[i], iconFont, textBrush, new Rectangle(iconLeft, itemRect.Top, iconWidth, itemRect.Height), stringFormat);

                    // Draw label text
                    int textLeft = iconLeft + iconWidth + (int)(6 * scale);
                    int textWidth = itemRect.Width - (textLeft - itemRect.Left) - 2;
                    if (textWidth > 0)
                    {
                        var textRect = new Rectangle(textLeft, itemRect.Top, textWidth, itemRect.Height);
                        g.DrawString(_tabTitles[i], itemFont, textBrush, textRect, stringFormat);
                    }
                }
                else
                {
                    // Centered icon in collapsed rail
                    var centerFormat = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    g.DrawString(_tabIcons[i], iconFont, textBrush, itemRect, centerFormat);
                }
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

        private static GraphicsPath GetRoundedPath(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animTimer?.Stop();
                _animTimer?.Dispose();
                _toolTip?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
