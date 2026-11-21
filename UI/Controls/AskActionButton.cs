using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KerkenezMail.UI.Controls
{
    /// <summary>
    /// Lightweight, double-buffered action button supporting Ask/Send, Generating (spinner),
    /// and Hover-to-Stop (crimson stop icon) states without heavy rendering overhead.
    /// </summary>
    public class AskActionButton : Control
    {
        private readonly System.Windows.Forms.Timer _spinnerTimer;
        private bool _isGenerating;
        private bool _compactIconMode;
        private bool _isHovered;
        private bool _isPressed;
        private float _spinnerAngle;

        private string _idleText = "Ask";
        private string _generatingText = "Generating";
        private string _stopText = "Stop";

        public event EventHandler? AskClicked;
        public event EventHandler? StopClicked;

        public AskActionButton()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable |
                ControlStyles.SupportsTransparentBackColor,
                true);

            this.BackColor = Color.Transparent;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            this.Cursor = Cursors.Hand;
            this.TabStop = true;

            _spinnerTimer = new System.Windows.Forms.Timer { Interval = 45 };
            _spinnerTimer.Tick += (s, e) =>
            {
                _spinnerAngle = (_spinnerAngle + 24f) % 360f;
                if (this.Visible && this.IsHandleCreated)
                {
                    this.Invalidate();
                }
            };
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsGenerating
        {
            get => _isGenerating;
            set
            {
                if (_isGenerating == value) return;
                _isGenerating = value;
                if (_isGenerating)
                {
                    _spinnerAngle = 0f;
                    _spinnerTimer.Start();
                }
                else
                {
                    _spinnerTimer.Stop();
                    _spinnerAngle = 0f;
                }
                UpdateCursor();
                this.Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool CompactIconMode
        {
            get => _compactIconMode;
            set
            {
                if (_compactIconMode == value) return;
                _compactIconMode = value;
                this.Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string IdleText
        {
            get => _idleText;
            set
            {
                _idleText = value ?? string.Empty;
                this.Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string GeneratingText
        {
            get => _generatingText;
            set
            {
                _generatingText = value ?? string.Empty;
                this.Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string StopText
        {
            get => _stopText;
            set
            {
                _stopText = value ?? string.Empty;
                this.Invalidate();
            }
        }

        public bool IsHovered => _isHovered;

        private void UpdateCursor()
        {
            this.Cursor = (_isGenerating || this.Enabled) ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            UpdateCursor();
            this.Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            this.Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            _isPressed = false;
            this.Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && (_isGenerating || this.Enabled))
            {
                _isPressed = true;
                this.Focus();
                this.Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            bool wasPressed = _isPressed;
            _isPressed = false;
            this.Invalidate();

            if (wasPressed && e.Button == MouseButtons.Left && this.ClientRectangle.Contains(e.Location))
            {
                TriggerAction();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                TriggerAction();
            }
        }

        private void TriggerAction()
        {
            if (_isGenerating)
            {
                StopClicked?.Invoke(this, EventArgs.Empty);
            }
            else if (this.Enabled)
            {
                AskClicked?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float scale = this.DeviceDpi / 96f;
            var rect = new RectangleF(0.5f, 0.5f, Math.Max(1f, this.Width - 1.5f), Math.Max(1f, this.Height - 1.5f));
            float radius = _compactIconMode ? Math.Min(8f * scale, rect.Height / 2f) : 6f * scale;

            Color fillColor;
            Color borderColor;
            Color fgColor;

            if (!this.Enabled && !_isGenerating)
            {
                fillColor = Color.FromArgb(233, 238, 245);
                borderColor = Color.FromArgb(212, 220, 230);
                fgColor = Color.FromArgb(148, 160, 176);
            }
            else if (_isGenerating && _isHovered)
            {
                fillColor = _isPressed ? Color.FromArgb(185, 28, 28) : Color.FromArgb(220, 38, 38);
                borderColor = Color.FromArgb(185, 28, 28);
                fgColor = Color.White;
            }
            else if (_isGenerating)
            {
                fillColor = Color.FromArgb(234, 242, 252);
                borderColor = Color.FromArgb(180, 208, 243);
                fgColor = Color.FromArgb(0, 102, 204);
            }
            else
            {
                fillColor = _isPressed
                    ? Color.FromArgb(0, 73, 153)
                    : (_isHovered ? Color.FromArgb(0, 87, 179) : Color.FromArgb(0, 102, 204));
                borderColor = Color.FromArgb(0, 82, 163);
                fgColor = Color.White;
            }

            using (var path = CreateRoundedRect(rect, radius))
            using (var fillBrush = new SolidBrush(fillColor))
            using (var borderPen = new Pen(borderColor, 1f))
            {
                g.FillPath(fillBrush, path);
                g.DrawPath(borderPen, path);
            }

            if (this.Focused && this.ShowFocusCues)
            {
                var focusRect = Rectangle.Inflate(this.ClientRectangle, -3, -3);
                ControlPaint.DrawFocusRectangle(g, focusRect);
            }

            if (_compactIconMode)
            {
                DrawCenteredIcon(g, rect, fgColor, scale);
            }
            else
            {
                DrawIconAndText(g, rect, fgColor, scale);
            }
        }

        private void DrawCenteredIcon(Graphics g, RectangleF rect, Color fgColor, float scale)
        {
            float cx = rect.X + rect.Width / 2f;
            float cy = rect.Y + rect.Height / 2f;

            if (_isGenerating && _isHovered)
            {
                DrawStopSquare(g, cx, cy, 10f * scale, fgColor);
            }
            else if (_isGenerating)
            {
                DrawSpinnerArc(g, cx, cy, 14f * scale, fgColor, 2.2f * scale);
            }
            else
            {
                DrawSendArrow(g, cx, cy, 12f * scale, fgColor, 2f * scale);
            }
        }

        private void DrawIconAndText(Graphics g, RectangleF rect, Color fgColor, float scale)
        {
            string label = _isGenerating
                ? (_isHovered ? _stopText : _generatingText)
                : _idleText;

            Size textSize = TextRenderer.MeasureText(g, label, this.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
            float iconSize = (_isGenerating && !_isHovered) ? 12f * scale : 10f * scale;
            float gap = string.IsNullOrEmpty(label) ? 0f : 6f * scale;
            float totalW = iconSize + gap + textSize.Width;

            float startX = rect.X + Math.Max(6f * scale, (rect.Width - totalW) / 2f);
            float cy = rect.Y + rect.Height / 2f;
            float iconCx = startX + iconSize / 2f;

            if (_isGenerating && _isHovered)
            {
                DrawStopSquare(g, iconCx, cy, 9f * scale, fgColor);
            }
            else if (_isGenerating)
            {
                DrawSpinnerArc(g, iconCx, cy, 12f * scale, fgColor, 2f * scale);
            }
            else
            {
                DrawSendArrow(g, iconCx, cy, 10f * scale, fgColor, 1.8f * scale);
            }

            if (!string.IsNullOrEmpty(label))
            {
                var textRect = new Rectangle(
                    (int)Math.Round(startX + iconSize + gap),
                    (int)Math.Round(rect.Y),
                    Math.Max(1, (int)Math.Round(rect.Right - (startX + iconSize + gap) - 4f * scale)),
                    Math.Max(1, (int)Math.Round(rect.Height)));

                TextRenderer.DrawText(
                    g,
                    label,
                    this.Font,
                    textRect,
                    fgColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
        }

        private void DrawSpinnerArc(Graphics g, float cx, float cy, float diameter, Color color, float strokeWidth)
        {
            using var trackPen = new Pen(Color.FromArgb(55, color), strokeWidth);
            using var arcPen = new Pen(color, strokeWidth)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };

            float r = diameter / 2f;
            var arcRect = new RectangleF(cx - r, cy - r, diameter, diameter);
            g.DrawEllipse(trackPen, arcRect);
            g.DrawArc(arcPen, arcRect, _spinnerAngle, 250f);
        }

        private static void DrawStopSquare(Graphics g, float cx, float cy, float size, Color color)
        {
            float half = size / 2f;
            var sqRect = new RectangleF(cx - half, cy - half, size, size);
            using var path = CreateRoundedRect(sqRect, Math.Max(1.5f, size * 0.22f));
            using var brush = new SolidBrush(color);
            g.FillPath(brush, path);
        }

        private static void DrawSendArrow(Graphics g, float cx, float cy, float size, Color color, float strokeWidth)
        {
            float half = size / 2f;
            using var pen = new Pen(color, strokeWidth)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };

            // Upward arrow matching modern chat send icons
            g.DrawLine(pen, cx, cy + half, cx, cy - half);
            g.DrawLine(pen, cx - half * 0.65f, cy - half * 0.2f, cx, cy - half);
            g.DrawLine(pen, cx + half * 0.65f, cy - half * 0.2f, cx, cy - half);
        }

        private static GraphicsPath CreateRoundedRect(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = Math.Max(1f, Math.Min(radius * 2f, Math.Min(rect.Width, rect.Height)));
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _spinnerTimer.Stop();
                _spinnerTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
