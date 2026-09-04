using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;

namespace KerkenezMail.Services
{
    public static class TrayIconHelper
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private static Icon? _cachedNormalIcon;
        private static Icon? _cachedUnreadIcon;
        private static readonly object _syncLock = new object();

        public static Icon GetNormalIcon()
        {
            lock (_syncLock)
            {
                if (_cachedNormalIcon != null) return _cachedNormalIcon;

                _cachedNormalIcon = LoadBaseAppIcon();
                return _cachedNormalIcon;
            }
        }

        public static Icon GetUnreadIcon()
        {
            lock (_syncLock)
            {
                if (_cachedUnreadIcon != null) return _cachedUnreadIcon;

                Icon baseIcon = GetNormalIcon();
                _cachedUnreadIcon = CreateRedDotBadgeIcon(baseIcon);
                return _cachedUnreadIcon;
            }
        }

        private static Icon LoadBaseAppIcon()
        {
            try
            {
                // Try from embedded resource
                using var stream = typeof(TrayIconHelper).Assembly.GetManifestResourceStream("KerkenezMail.app.ico");
                if (stream != null)
                {
                    return new Icon(stream, 32, 32);
                }

                // Try from relative file
                string localIco = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(localIco))
                {
                    return new Icon(localIco, 32, 32);
                }
            }
            catch
            {
                // Fallback to rendering vector envelope icon
            }

            return GenerateVectorMailIcon(false);
        }

        public static Icon CreateRedDotBadgeIcon(Icon baseIcon)
        {
            int size = 32;
            using var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                // Draw base icon
                g.DrawIcon(baseIcon, new Rectangle(0, 0, size, size));

                // Draw Red Dot badge on top-right corner
                int dotDiameter = 11;
                int dotX = size - dotDiameter - 1;
                int dotY = 1;

                // White/Light-gray border for contrast on dark/light taskbars
                using var borderPen = new Pen(Color.FromArgb(255, 255, 255), 1.5f);
                using var redBrush = new SolidBrush(Color.FromArgb(235, 35, 35));
                using var highlightBrush = new SolidBrush(Color.FromArgb(255, 100, 100));

                g.FillEllipse(redBrush, dotX, dotY, dotDiameter, dotDiameter);
                g.DrawEllipse(borderPen, dotX, dotY, dotDiameter, dotDiameter);

                // Subtle 3D gloss highlight
                g.FillEllipse(highlightBrush, dotX + 2, dotY + 2, 3, 3);
            }

            IntPtr hIcon = bmp.GetHicon();
            try
            {
                using var temp = Icon.FromHandle(hIcon);
                return (Icon)temp.Clone();
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }

        private static Icon GenerateVectorMailIcon(bool withRedDot)
        {
            int size = 32;
            using var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.Clear(Color.Transparent);

                // Envelope body
                int pad = 4;
                var envelopeRect = new Rectangle(pad, pad + 4, size - (pad * 2), size - (pad * 2) - 6);
                
                using var bgBrush = new SolidBrush(Color.FromArgb(52, 120, 246));
                using var strokePen = new Pen(Color.FromArgb(30, 80, 180), 1.5f);
                g.FillRectangle(bgBrush, envelopeRect);
                g.DrawRectangle(strokePen, envelopeRect);

                // Envelope flap
                var p1 = new Point(envelopeRect.Left, envelopeRect.Top);
                var p2 = new Point(envelopeRect.Left + envelopeRect.Width / 2, envelopeRect.Top + envelopeRect.Height / 2);
                var p3 = new Point(envelopeRect.Right, envelopeRect.Top);

                using var flapPen = new Pen(Color.FromArgb(230, 240, 255), 1.5f);
                g.DrawLine(flapPen, p1, p2);
                g.DrawLine(flapPen, p2, p3);

                if (withRedDot)
                {
                    int dotDiameter = 10;
                    int dotX = size - dotDiameter - 1;
                    int dotY = 1;
                    using var redBrush = new SolidBrush(Color.FromArgb(235, 35, 35));
                    using var borderPen = new Pen(Color.White, 1.5f);
                    g.FillEllipse(redBrush, dotX, dotY, dotDiameter, dotDiameter);
                    g.DrawEllipse(borderPen, dotX, dotY, dotDiameter, dotDiameter);
                }
            }

            IntPtr hIcon = bmp.GetHicon();
            try
            {
                using var temp = Icon.FromHandle(hIcon);
                return (Icon)temp.Clone();
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }
    }
}
