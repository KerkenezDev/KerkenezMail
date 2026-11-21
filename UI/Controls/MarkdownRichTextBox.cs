using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using KerkenezMail.Models;
using KerkenezMail.Services;

namespace KerkenezMail.UI.Controls
{
    /// <summary>
    /// Shared read-only RichTextBox for rendered Markdown responses with regular-mail
    /// hyperlink tooltips, click-to-open links, flicker-free streaming updates, and
    /// optional parent scroll-wheel forwarding for auto-sized chat bubbles.
    /// </summary>
    public class MarkdownRichTextBox : RichTextBox
    {
        private const int WM_SETREDRAW = 0x000B;
        private const int WM_MOUSEWHEEL = 0x020A;

        private readonly List<(int Start, int Length, string Url)> _linkSpans = new List<(int Start, int Length, string Url)>();
        private string? _lastHoverLinkUrl;
        private Point _mouseDownPoint;
        private DateTime _lastLinkOpenedUtc = DateTime.MinValue;
        private int _measuredContentHeight;
        private bool _heightSyncQueued;

        public event Action<MarkdownRichTextBox, string>? LinkHoverRequested;
        public event Action<MarkdownRichTextBox>? LinkHoverCleared;
        public event Action<string>? LinkOpenFailed;
        public event EventHandler? AutoHeightChanged;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ForwardWheelToParent { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool AutoSizeHeight { get; set; }

        public int MeasuredContentHeight => _measuredContentHeight;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IReadOnlyList<(int Start, int Length, string Url)> LinkSpans => _linkSpans;

        public MarkdownRichTextBox()
        {
            this.ReadOnly = true;
            this.DetectUrls = true;
            this.WordWrap = true;
            this.BorderStyle = BorderStyle.None;
            this.BackColor = Color.White;
            this.Font = new Font("Segoe UI", 9.75F);
            this.TabStop = false;
            this.ShortcutsEnabled = true;

            this.LinkClicked += OnInternalLinkClicked;
            this.MouseMove += OnInternalMouseMove;
            this.MouseLeave += (s, e) => ClearHover();
            this.MouseDown += (s, e) =>
            {
                _mouseDownPoint = e.Location;
                ClearHover();
            };
            this.MouseClick += OnInternalMouseClick;
        }

        public void SetMarkdown(string? markdown, IEnumerable<EmailLink>? knownLinks, bool scrollToEnd = false)
        {
            if (this.IsDisposed) return;

            var rendered = MarkdownRtfRenderer.ConvertToRtf(markdown ?? string.Empty, knownLinks);
            bool suppressRedraw = this.IsHandleCreated && this.Visible;
            if (suppressRedraw)
            {
                NativeMethods.SendMessage(this.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            }

            try
            {
                _measuredContentHeight = 0;
                try
                {
                    this.Rtf = rendered.Rtf;
                }
                catch
                {
                    this.Text = markdown ?? string.Empty;
                }

                PopulateLinkSpans(rendered.Links);

                if (scrollToEnd && this.TextLength > 0)
                {
                    this.SelectionStart = this.TextLength;
                    this.SelectionLength = 0;
                    if (this.IsHandleCreated)
                    {
                        this.ScrollToCaret();
                    }
                }
                else
                {
                    this.SelectionStart = 0;
                    this.SelectionLength = 0;
                    if (this.IsHandleCreated)
                    {
                        this.ScrollToCaret();
                    }
                }
            }
            finally
            {
                if (suppressRedraw)
                {
                    NativeMethods.SendMessage(this.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                    this.Invalidate();
                }
            }
        }

        public void SetStyledStatusText(string text, Color color, Font font)
        {
            if (this.IsDisposed) return;

            _linkSpans.Clear();
            ClearHover();
            _measuredContentHeight = 0;
            this.Rtf = string.Empty;
            this.Text = text ?? string.Empty;
            this.SelectAll();
            this.SelectionColor = color;
            this.SelectionFont = font;
            this.SelectionLength = 0;
        }

        public bool ApplyMeasuredHeight(int minHeight, int verticalPadding)
        {
            if (_measuredContentHeight <= 0) return false;

            int desiredH = Math.Max(minHeight, _measuredContentHeight + verticalPadding);
            if (Math.Abs(this.Height - desiredH) > 1)
            {
                this.Height = desiredH;
                return true;
            }
            return false;
        }

        protected override void OnContentsResized(ContentsResizedEventArgs e)
        {
            base.OnContentsResized(e);
            _measuredContentHeight = e.NewRectangle.Height;

            // Defer height sync via BeginInvoke so we never mutate Height synchronously inside SetBoundsCore
            if (AutoSizeHeight && !_heightSyncQueued && this.IsHandleCreated && !this.IsDisposed)
            {
                _heightSyncQueued = true;
                try
                {
                    this.BeginInvoke((Action)ApplyPendingAutoHeight);
                }
                catch
                {
                    _heightSyncQueued = false;
                }
            }
        }

        private void ApplyPendingAutoHeight()
        {
            _heightSyncQueued = false;
            if (this.IsDisposed || !AutoSizeHeight) return;

            float scale = this.DeviceDpi / 96f;
            if (ApplyMeasuredHeight((int)(24 * scale), (int)(8 * scale)))
            {
                AutoHeightChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (ForwardWheelToParent && m.Msg == WM_MOUSEWHEEL && this.Parent != null && this.Parent.IsHandleCreated)
            {
                NativeMethods.SendMessage(this.Parent.Handle, m.Msg, m.WParam, m.LParam);
                return;
            }
            base.WndProc(ref m);
        }

        private void PopulateLinkSpans(List<RenderedLinkSpan> renderedLinks)
        {
            _linkSpans.Clear();
            ClearHover();

            string visibleText = this.Text;
            if (string.IsNullOrEmpty(visibleText)) return;

            int sequentialFloor = 0;
            foreach (var link in renderedLinks)
            {
                if (string.IsNullOrWhiteSpace(link.Text) || string.IsNullOrWhiteSpace(link.Url)) continue;

                int found = -1;
                string fieldPrefix = $"HYPERLINK \"{link.Url}\"";
                int fieldIdx = visibleText.IndexOf(fieldPrefix, sequentialFloor, StringComparison.Ordinal);
                if (fieldIdx < 0 && sequentialFloor > 0)
                {
                    fieldIdx = visibleText.IndexOf(fieldPrefix, 0, StringComparison.Ordinal);
                }

                if (fieldIdx >= 0)
                {
                    int afterPrefix = fieldIdx + fieldPrefix.Length;
                    if (afterPrefix + link.Text.Length <= visibleText.Length &&
                        string.Compare(visibleText, afterPrefix, link.Text, 0, link.Text.Length, StringComparison.Ordinal) == 0 &&
                        !OverlapsExistingSpan(afterPrefix, link.Text.Length))
                    {
                        found = afterPrefix;
                    }
                }

                if (found < 0)
                {
                    int hintStart = Math.Clamp(link.HintIndex - 24, sequentialFloor, Math.Max(0, visibleText.Length - 1));
                    found = visibleText.IndexOf(link.Text, hintStart, StringComparison.Ordinal);

                    if (found < 0 && hintStart > sequentialFloor)
                    {
                        found = visibleText.IndexOf(link.Text, sequentialFloor, StringComparison.Ordinal);
                    }

                    if (found < 0)
                    {
                        found = FindNonOverlappingMatch(visibleText, link.Text);
                    }
                }

                if (found >= 0)
                {
                    _linkSpans.Add((found, link.Text.Length, link.Url));
                    sequentialFloor = Math.Max(sequentialFloor, found + link.Text.Length);
                }
            }

            // Supplement with any raw http/https URLs not inside hidden HYPERLINK field instructions
            var urlMatches = Regex.Matches(visibleText, @"https?://[^\s<>""'{}|\^\[\]`]+", RegexOptions.IgnoreCase);
            foreach (Match m in urlMatches)
            {
                const string hiddenFieldToken = "HYPERLINK \"";
                if (m.Index >= hiddenFieldToken.Length &&
                    string.Compare(visibleText, m.Index - hiddenFieldToken.Length, hiddenFieldToken, 0, hiddenFieldToken.Length, StringComparison.Ordinal) == 0)
                {
                    continue;
                }

                if (!OverlapsExistingSpan(m.Index, m.Length))
                {
                    _linkSpans.Add((m.Index, m.Length, m.Value));
                }
            }
        }

        private int FindNonOverlappingMatch(string visibleText, string target)
        {
            int pos = 0;
            while (pos < visibleText.Length)
            {
                int idx = visibleText.IndexOf(target, pos, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) return -1;
                if (!OverlapsExistingSpan(idx, target.Length))
                {
                    return idx;
                }
                pos = idx + 1;
            }
            return -1;
        }

        private bool OverlapsExistingSpan(int start, int length)
        {
            int end = start + length;
            return _linkSpans.Any(s => start < s.Start + s.Length && end > s.Start);
        }

        public bool TryGetLinkSpanAtPoint(Point pt, out (int Start, int Length, string Url) span)
        {
            span = default;
            if (_linkSpans.Count == 0 || string.IsNullOrEmpty(this.Text) || !this.IsHandleCreated)
            {
                return false;
            }

            int charIndex = this.GetCharIndexFromPosition(pt);
            if (charIndex < 0 || charIndex >= this.TextLength)
            {
                return false;
            }

            Point charPt = this.GetPositionFromCharIndex(charIndex);
            if (Math.Abs(pt.Y - charPt.Y) > 24 || pt.X < charPt.X - 15)
            {
                return false;
            }

            if (charIndex + 1 < this.TextLength)
            {
                Point nextPt = this.GetPositionFromCharIndex(charIndex + 1);
                if (nextPt.Y == charPt.Y && nextPt.X > charPt.X && pt.X > nextPt.X + 12)
                {
                    return false;
                }
                if (nextPt.Y != charPt.Y && pt.X > charPt.X + 24)
                {
                    return false;
                }
            }

            var matched = _linkSpans.FirstOrDefault(s => charIndex >= s.Start && charIndex < s.Start + s.Length);
            if (matched.Length > 0 && !string.IsNullOrWhiteSpace(matched.Url))
            {
                span = matched;
                return true;
            }

            return false;
        }

        private void OnInternalMouseMove(object? sender, MouseEventArgs e)
        {
            if (TryGetLinkSpanAtPoint(e.Location, out var span))
            {
                this.Cursor = Cursors.Hand;
                if (!string.Equals(span.Url, _lastHoverLinkUrl, StringComparison.Ordinal))
                {
                    _lastHoverLinkUrl = span.Url;
                    LinkHoverRequested?.Invoke(this, span.Url);
                }
            }
            else
            {
                this.Cursor = Cursors.IBeam;
                ClearHover();
            }
        }

        public void ClearHover()
        {
            if (_lastHoverLinkUrl != null)
            {
                _lastHoverLinkUrl = null;
                LinkHoverCleared?.Invoke(this);
            }
        }

        private void OnInternalLinkClicked(object? sender, LinkClickedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.LinkText)) return;

            string candidate = MarkdownRtfRenderer.ExtractUrlFromRichTextLinkText(e.LinkText);
            if (!MarkdownRtfRenderer.TryNormalizeWebOrMailtoUrl(candidate, out _))
            {
                Point mousePos = this.PointToClient(Cursor.Position);
                if (TryGetLinkSpanAtPoint(mousePos, out var span))
                {
                    candidate = span.Url;
                }
                else
                {
                    int charIdx = this.GetCharIndexFromPosition(mousePos);
                    var nearSpan = _linkSpans.FirstOrDefault(s => charIdx >= s.Start - 2 && charIdx <= s.Start + s.Length + 2);
                    if (!string.IsNullOrEmpty(nearSpan.Url))
                    {
                        candidate = nearSpan.Url;
                    }
                    else if (!string.IsNullOrEmpty(_lastHoverLinkUrl))
                    {
                        candidate = _lastHoverLinkUrl;
                    }
                }
            }

            OpenUrl(candidate);
        }

        private void OnInternalMouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if ((DateTime.UtcNow - _lastLinkOpenedUtc).TotalMilliseconds < 350) return;
            if (this.SelectionLength > 0) return;

            int dx = Math.Abs(e.X - _mouseDownPoint.X);
            int dy = Math.Abs(e.Y - _mouseDownPoint.Y);
            if (dx > 4 || dy > 4) return;

            if (TryGetLinkSpanAtPoint(e.Location, out var span))
            {
                OpenUrl(span.Url);
            }
        }

        private void OpenUrl(string url)
        {
            if (MarkdownRtfRenderer.TryOpenExternalUrl(url, out string? error))
            {
                _lastLinkOpenedUtc = DateTime.UtcNow;
            }
            else if (!string.IsNullOrWhiteSpace(error))
            {
                LinkOpenFailed?.Invoke(error);
            }
        }
    }
}
