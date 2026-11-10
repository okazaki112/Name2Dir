using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace FileOrganizerApp
{
    // 自绘拖拽落点区：虚线圆角框 + 下箭头托盘图标，拖入悬停时高亮。
    internal sealed class DropPanel : Panel
    {
        private static readonly Color Accent = Color.FromArgb(47, 111, 237);
        private static readonly Color TextPrimary = Color.FromArgb(31, 41, 55);
        private static readonly Color TextSecondary = Color.FromArgb(107, 114, 128);

        private bool isDragging;

        public DropPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            BackColor = Color.White;
        }

        public bool IsDragging
        {
            get { return isDragging; }
            set { isDragging = value; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Color bg = isDragging ? Color.FromArgb(239, 244, 255) : Color.White;
            Color border = isDragging ? Accent : Color.FromArgb(203, 213, 225);
            Color icon = isDragging ? Accent : Color.FromArgb(148, 163, 184);

            using (SolidBrush b = new SolidBrush(bg))
            {
                g.FillRectangle(b, ClientRectangle);
            }

            Rectangle box = new Rectangle(8, 8, ClientSize.Width - 17, ClientSize.Height - 17);
            using (GraphicsPath path = RoundRect(box, 12))
            using (Pen pen = new Pen(border, isDragging ? 2F : 1.5F))
            {
                if (!isDragging)
                {
                    pen.DashStyle = DashStyle.Dash;
                    pen.DashPattern = new float[] { 6F, 4F };
                }
                g.DrawPath(pen, path);
            }

            int cx = ClientSize.Width / 2;
            int iconCy = Math.Max(40, ClientSize.Height / 2 - 34);
            DrawDropIcon(g, cx, iconCy, icon);

            using (StringFormat fmt = new StringFormat())
            {
                fmt.Alignment = StringAlignment.Center;
                fmt.LineAlignment = StringAlignment.Near;
                using (Font mainFont = new Font("Segoe UI Semibold", 12F))
                using (SolidBrush tb = new SolidBrush(TextPrimary))
                {
                    string main = isDragging ? "松开即可归档" : "把文件拖到这里";
                    g.DrawString(main, mainFont, tb, new PointF(cx, iconCy + 26), fmt);
                }
                using (Font subFont = new Font("Segoe UI", 9F))
                using (SolidBrush sb = new SolidBrush(TextSecondary))
                {
                    g.DrawString("支持批量拖入 · 按文件名（去扩展名）建文件夹 · 移入后位置不变",
                        subFont, sb, new PointF(cx, iconCy + 52), fmt);
                }
            }
        }

        private static void DrawDropIcon(Graphics g, int cx, int cy, Color color)
        {
            using (Pen pen = new Pen(color, 2.4F))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                g.DrawLine(pen, cx, cy - 18, cx, cy + 6);
                g.DrawLines(pen, new Point[] {
                    new Point(cx - 9, cy - 3),
                    new Point(cx, cy + 7),
                    new Point(cx + 9, cy - 3)
                });

                g.DrawLines(pen, new Point[] {
                    new Point(cx - 20, cy + 4),
                    new Point(cx - 20, cy + 16),
                    new Point(cx + 20, cy + 16),
                    new Point(cx + 20, cy + 4)
                });
            }
        }

        private static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}