using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using EmailSummarizer.Models;

namespace EmailSummarizer.UI.Controls
{
    public class SidebarNav : Panel
    {
        public const int ExpandedWidth = 195;
        public const int CollapsedWidth = 60;

        public event EventHandler<int>? TabChanged;
        public event EventHandler<MailFolderType>? MailFolderSelected;
        public event EventHandler<bool>? CollapsedChanged;

        private readonly string[] _tabTitles = new[]
        {
            "Inbox",
            "Send Mail",
            "Accounts",
            "Settings",
            "Live Logs"
        };

        private static readonly (MailFolderType Folder, string Title, string Icon)[] SubFolders = new[]
        {
            (MailFolderType.Sent, "Sent", "\uE89C"),
            (MailFolderType.Archive, "Archived", "\uE7B8"),
            (MailFolderType.Spam, "Spam", "\uE7BA"),
            (MailFolderType.Trash, "Trash", "\uE74D")
        };

        private readonly string[] _tabIcons = new[]
        {
            "\uE715", // Mail (Inbox)
            "\uE724", // Paper plane (Send Mail)
            "\uE716", // People (Accounts)
            "\uE713", // Settings gear
            "\uE700"  // Menu (Live Logs)
        };

        private static string? _iconFontFamily;
        private static string GetIconFontFamily()
        {
            if (_iconFontFamily != null) return _iconFontFamily;

            try
            {
                using var installedFonts = new System.Drawing.Text.InstalledFontCollection();
                var set = new HashSet<string>(installedFonts.Families.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
                if (set.Contains("Segoe Fluent Icons")) return _iconFontFamily = "Segoe Fluent Icons";
                if (set.Contains("Segoe MDL2 Assets")) return _iconFontFamily = "Segoe MDL2 Assets";
            }
            catch { }

            return _iconFontFamily = "Segoe UI Symbol";
        }

        private int _selectedIndex = 0;
        private int _hoveredIndex = -1;
        private bool _isToggleHovered = false;
        private bool _isCollapsed = false;

        private bool _isInboxExpanded = false;
        private MailFolderType _selectedFolder = MailFolderType.Inbox;
        private MailFolderType? _hoveredFolder = null;
        private bool _isChevronHovered = false;

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

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public MailFolderType SelectedFolder
        {
            get => _selectedFolder;
            set
            {
                if (_selectedFolder != value)
                {
                    _selectedFolder = value;
                    Invalidate();
                }
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsInboxExpanded
        {
            get => _isInboxExpanded;
            set
            {
                if (_isInboxExpanded != value)
                {
                    _isInboxExpanded = value;
                    Invalidate();
                }
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsCollapsed
        {
            get => _isCollapsed;
            set
            {
                if (_isCollapsed != value)
                {
                    _isCollapsed = value;
                    if (_isCollapsed)
                    {
                        // Always collapse inbox folder tree when sidebar collapses, but keep selected menu/folder active
                        _isInboxExpanded = false;
                    }
                    StartAnimation();
                    CollapsedChanged?.Invoke(this, _isCollapsed);
                }
            }
        }

        private float CurrentScale => (this.DeviceDpi > 0 ? this.DeviceDpi : 96f) / 96f;

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

            float scale = CurrentScale;
            this.Width = _isCollapsed ? (int)(CollapsedWidth * scale) : (int)(ExpandedWidth * scale);
        }

        public void ToggleCollapsed()
        {
            IsCollapsed = !IsCollapsed;
        }

        private void StartAnimation()
        {
            float scale = CurrentScale;
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
            float scale = CurrentScale;
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

        public Rectangle GetChevronBounds(float scale)
        {
            var item0Rect = GetItemBounds(0, scale);
            int chevronSize = (int)(22 * scale);
            int top = item0Rect.Top + (item0Rect.Height - chevronSize) / 2;
            int left = item0Rect.Right - chevronSize - (int)(5 * scale);
            return new Rectangle(left, top, chevronSize, chevronSize);
        }

        public Rectangle GetSubFolderBounds(int k, float scale)
        {
            int headerH = (int)(72 * scale);
            int itemH = (int)(46 * scale);
            int subH = (int)(32 * scale);
            int subSpacing = (int)(2 * scale);
            int top = headerH + itemH + k * (subH + subSpacing);
            int left = (int)(18 * scale);
            int width = this.Width - (int)(26 * scale);
            return new Rectangle(left, top, width, subH);
        }

        public Rectangle GetItemBounds(int index, float scale)
        {
            int headerH = (int)(72 * scale);
            int itemH = (int)(46 * scale);
            bool isWide = this.Width >= (int)(130 * scale);
            int subH = (_isInboxExpanded && isWide) ? (SubFolders.Length * (int)(34 * scale) + (int)(2 * scale)) : 0;

            int itemY = (index == 0)
                ? headerH
                : headerH + index * itemH + subH;

            return new Rectangle((int)(8 * scale), itemY, this.Width - (int)(16 * scale), itemH - (int)(4 * scale));
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

        private void CheckOtherTabs(Point loc, float scale)
        {
            int matchedIdx = -1;
            for (int i = 1; i < _tabTitles.Length; i++)
            {
                if (GetItemBounds(i, scale).Contains(loc))
                {
                    matchedIdx = i;
                    break;
                }
            }
            _hoveredIndex = matchedIdx;

            if (matchedIdx >= 0)
            {
                this.Cursor = Cursors.Hand;
                // No tooltip for Send Mail (tab 1); only show tooltip for other tabs when collapsed
                if (_isCollapsed && matchedIdx != 1)
                {
                    UpdateToolTip(_tabTitles[matchedIdx], loc);
                }
                else
                {
                    UpdateToolTip("", Point.Empty);
                }
            }
            else
            {
                this.Cursor = Cursors.Default;
                UpdateToolTip("", Point.Empty);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            float scale = CurrentScale;
            bool isWide = this.Width >= (int)(130 * scale);

            bool prevToggleHover = _isToggleHovered;
            bool prevChevronHover = _isChevronHovered;
            int prevHover = _hoveredIndex;
            MailFolderType? prevFolderHover = _hoveredFolder;

            var toggleRect = GetToggleButtonBounds(scale);
            var item0Rect = GetItemBounds(0, scale);
            var chevronRect = isWide ? GetChevronBounds(scale) : Rectangle.Empty;

            if (toggleRect.Contains(e.Location))
            {
                _isToggleHovered = true;
                _hoveredIndex = -1;
                _isChevronHovered = false;
                _hoveredFolder = null;
                this.Cursor = Cursors.Hand;
                string toggleTip = _isCollapsed ? "Expand sidebar (Ctrl+B)" : "Collapse sidebar (Ctrl+B)";
                UpdateToolTip(toggleTip, e.Location);
            }
            else if (isWide && chevronRect.Contains(e.Location))
            {
                _isToggleHovered = false;
                _hoveredIndex = 0;
                _isChevronHovered = true;
                _hoveredFolder = null;
                this.Cursor = Cursors.Hand;
                UpdateToolTip(_isInboxExpanded ? "Collapse mail folders" : "Expand mail folders (Sent, Archive, Trash...)", e.Location);
            }
            else if (item0Rect.Contains(e.Location))
            {
                _isToggleHovered = false;
                _hoveredIndex = 0;
                _isChevronHovered = false;
                _hoveredFolder = null;
                this.Cursor = Cursors.Hand;
                if (_isCollapsed)
                {
                    string tip = _selectedFolder == MailFolderType.Inbox ? "Inbox" : $"Inbox ({_selectedFolder.GetDisplayName()})";
                    UpdateToolTip(tip, e.Location);
                }
                else
                {
                    UpdateToolTip("", Point.Empty);
                }
            }
            else if (isWide && _isInboxExpanded)
            {
                MailFolderType? matched = null;
                for (int k = 0; k < SubFolders.Length; k++)
                {
                    if (GetSubFolderBounds(k, scale).Contains(e.Location))
                    {
                        matched = SubFolders[k].Folder;
                        break;
                    }
                }

                if (matched.HasValue)
                {
                    _isToggleHovered = false;
                    _hoveredIndex = -1;
                    _isChevronHovered = false;
                    _hoveredFolder = matched;
                    this.Cursor = Cursors.Hand;
                    UpdateToolTip("", Point.Empty);
                }
                else
                {
                    _hoveredFolder = null;
                    _isChevronHovered = false;
                    CheckOtherTabs(e.Location, scale);
                }
            }
            else
            {
                _hoveredFolder = null;
                _isChevronHovered = false;
                CheckOtherTabs(e.Location, scale);
            }

            if (prevToggleHover != _isToggleHovered ||
                prevChevronHover != _isChevronHovered ||
                prevHover != _hoveredIndex ||
                prevFolderHover != _hoveredFolder)
            {
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isToggleHovered = false;
            _isChevronHovered = false;
            _hoveredIndex = -1;
            _hoveredFolder = null;
            this.Cursor = Cursors.Default;
            UpdateToolTip("", Point.Empty);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            float scale = CurrentScale;
            bool isWide = this.Width >= (int)(130 * scale);

            if (e.Button == MouseButtons.Left)
            {
                var toggleRect = GetToggleButtonBounds(scale);
                var item0Rect = GetItemBounds(0, scale);
                var chevronRect = isWide ? GetChevronBounds(scale) : Rectangle.Empty;

                if (toggleRect.Contains(e.Location) || _isToggleHovered)
                {
                    ToggleCollapsed();
                    return;
                }

                if (isWide && (chevronRect.Contains(e.Location) || _isChevronHovered))
                {
                    IsInboxExpanded = !IsInboxExpanded;
                    return;
                }

                if (isWide && _isInboxExpanded)
                {
                    for (int k = 0; k < SubFolders.Length; k++)
                    {
                        if (GetSubFolderBounds(k, scale).Contains(e.Location) || (_hoveredFolder == SubFolders[k].Folder))
                        {
                            _selectedFolder = SubFolders[k].Folder;
                            SelectedIndex = 0;
                            Invalidate();
                            MailFolderSelected?.Invoke(this, SubFolders[k].Folder);
                            return;
                        }
                    }
                }

                if (item0Rect.Contains(e.Location) || _hoveredIndex == 0)
                {
                    _selectedFolder = MailFolderType.Inbox;
                    SelectedIndex = 0;
                    Invalidate();
                    MailFolderSelected?.Invoke(this, MailFolderType.Inbox);
                    return;
                }

                for (int i = 1; i < _tabTitles.Length; i++)
                {
                    if (GetItemBounds(i, scale).Contains(e.Location) || _hoveredIndex == i)
                    {
                        SelectedIndex = i;
                        return;
                    }
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float scale = CurrentScale;
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

            // 1. Draw Tab Item 0 (Inbox)
            DrawTabItem(g, 0, scale, isWide);

            // 1b. Draw SubFolders if expanded
            if (isWide && _isInboxExpanded)
            {
                for (int k = 0; k < SubFolders.Length; k++)
                {
                    var sub = SubFolders[k];
                    var subRect = GetSubFolderBounds(k, scale);
                    bool isSubSelected = (_selectedIndex == 0 && _selectedFolder == sub.Folder);
                    bool isSubHovered = (_hoveredFolder == sub.Folder && !isSubSelected);

                    if (isSubSelected)
                    {
                        using var subActiveBrush = new SolidBrush(_activeBgColor);
                        using var subActivePen = new Pen(_borderColor);
                        FillRoundedRectangle(g, subActiveBrush, subRect, 4);
                        DrawRoundedRectangle(g, subActivePen, subRect, 4);

                        using var accentBrush = new SolidBrush(_accentColor);
                        g.FillRectangle(accentBrush, new Rectangle(subRect.Left + 2, subRect.Top + 5, 3, subRect.Height - 10));
                    }
                    else if (isSubHovered)
                    {
                        using var subHoverBrush = new SolidBrush(_hoverBgColor);
                        FillRoundedRectangle(g, subHoverBrush, subRect, 4);
                    }

                    var subTextColor = isSubSelected ? _activeTextColor : _textColor;
                    using var subFont = new Font("Segoe UI", 8.75F, isSubSelected ? FontStyle.Bold : FontStyle.Regular);
                    using var subTextBrush = new SolidBrush(subTextColor);
                    using var subIconFont = new Font(GetIconFontFamily(), 9.5F, FontStyle.Regular);

                    var sfSub = new StringFormat
                    {
                        Alignment = StringAlignment.Near,
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter,
                        FormatFlags = StringFormatFlags.NoWrap
                    };

                    int sIconLeft = subRect.Left + (int)(8 * scale);
                    int sIconWidth = (int)(18 * scale);
                    g.DrawString(sub.Icon, subIconFont, subTextBrush, new Rectangle(sIconLeft, subRect.Top, sIconWidth, subRect.Height), sfSub);

                    int sTextLeft = sIconLeft + sIconWidth + (int)(6 * scale);
                    int sTextWidth = subRect.Width - (sTextLeft - subRect.Left) - 2;
                    if (sTextWidth > 0)
                    {
                        g.DrawString(sub.Title, subFont, subTextBrush, new Rectangle(sTextLeft, subRect.Top, sTextWidth, subRect.Height), sfSub);
                    }
                }
            }

            // 2. Draw Tab Items (Send Mail, Accounts, Settings, Live Logs)
            for (int i = 1; i < _tabTitles.Length; i++)
            {
                DrawTabItem(g, i, scale, isWide);
            }
        }

        private void DrawTabItem(Graphics g, int i, float scale, bool isWide)
        {
            var itemRect = GetItemBounds(i, scale);
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
            using var iconFont = new Font(GetIconFontFamily(), 11F, FontStyle.Regular);

            var chevronRect = (i == 0 && isWide) ? GetChevronBounds(scale) : Rectangle.Empty;

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

                // Draw label text (reserve space on right for chevron on Item 0)
                int textLeft = iconLeft + iconWidth + (int)(6 * scale);
                int rightBound = (i == 0) ? chevronRect.Left - (int)(2 * scale) : itemRect.Right - 2;
                int textWidth = rightBound - textLeft;
                if (textWidth > 0)
                {
                    var textRect = new Rectangle(textLeft, itemRect.Top, textWidth, itemRect.Height);
                    g.DrawString(_tabTitles[i], itemFont, textBrush, textRect, stringFormat);
                }

                // Draw chevron on Item 0
                if (i == 0)
                {
                    if (_isChevronHovered)
                    {
                        using var chHoverBrush = new SolidBrush(Color.FromArgb(25, 0, 102, 204));
                        FillRoundedRectangle(g, chHoverBrush, chevronRect, 4);
                    }

                    using var chevronBrush = new SolidBrush(_isChevronHovered ? _activeTextColor : Color.FromArgb(120, 125, 135));
                    using var chevronFont = new Font(GetIconFontFamily(), 9F, FontStyle.Bold);
                    var sfChevron = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    string chevronGlyph = _isInboxExpanded ? "\uE70E" : "\uE70D";
                    g.DrawString(chevronGlyph, chevronFont, chevronBrush, chevronRect, sfChevron);
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
