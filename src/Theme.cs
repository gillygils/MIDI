// Dark theme and the custom-drawn controls used by the MIDI Link window.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace MidiLink
{
    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(19, 20, 23);
        public static readonly Color Card = Color.FromArgb(29, 31, 35);
        public static readonly Color CardBorder = Color.FromArgb(44, 47, 53);
        public static readonly Color Input = Color.FromArgb(22, 23, 27);
        public static readonly Color InputBorder = Color.FromArgb(58, 62, 70);
        public static readonly Color InputHover = Color.FromArgb(92, 97, 108);
        public static readonly Color Text = Color.FromArgb(236, 238, 242);
        public static readonly Color Muted = Color.FromArgb(146, 152, 164);
        public static readonly Color Faint = Color.FromArgb(98, 104, 116);
        public static readonly Color Accent = Color.FromArgb(59, 130, 246);
        public static readonly Color AccentHover = Color.FromArgb(96, 156, 250);
        public static readonly Color AccentDown = Color.FromArgb(37, 99, 235);
        public static readonly Color Button = Color.FromArgb(44, 47, 54);
        public static readonly Color ButtonHover = Color.FromArgb(56, 60, 68);
        public static readonly Color ButtonDown = Color.FromArgb(36, 38, 44);
        public static readonly Color Green = Color.FromArgb(34, 197, 94);
        public static readonly Color Amber = Color.FromArgb(245, 158, 11);
        public static readonly Color Orange = Color.FromArgb(249, 115, 22);
        public static readonly Color Red = Color.FromArgb(239, 68, 68);
        public static readonly Color Blue = Color.FromArgb(96, 165, 250);

        public static Font UI(float size) { return new Font("Segoe UI", size, FontStyle.Regular); }
        public static Font Semibold(float size)
        {
            var f = new Font("Segoe UI Semibold", size, FontStyle.Regular);
            return f.Name == "Segoe UI Semibold" ? f : new Font("Segoe UI", size, FontStyle.Bold);
        }

        public static float Scale(Control c) { return c.DeviceDpi / 96f; }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // Fills a rounded box and outlines it, after painting the corners in the parent's color.
        public static void Box(Graphics g, Control c, Color fill, Color border, float radius)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(c.Parent != null ? c.Parent.BackColor : Bg);
            var r = new RectangleF(0.5f, 0.5f, c.Width - 1.5f, c.Height - 1.5f);
            using (var path = Round(r, radius * Scale(c)))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                if (border != Color.Empty) using (var p = new Pen(border, Math.Max(1f, Scale(c)))) g.DrawPath(p, path);
            }
        }

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] public static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);

        public static void DarkTitleBar(IntPtr hwnd)
        {
            int on = 1;
            if (DwmSetWindowAttribute(hwnd, 20, ref on, 4) != 0) DwmSetWindowAttribute(hwnd, 19, ref on, 4); // immersive dark mode
            int caption = Bg.R | (Bg.G << 8) | (Bg.B << 16);
            DwmSetWindowAttribute(hwnd, 35, ref caption, 4); // Windows 11: caption color
            int border = CardBorder.R | (CardBorder.G << 8) | (CardBorder.B << 16);
            DwmSetWindowAttribute(hwnd, 34, ref border, 4); // Windows 11: border color
        }

        public static Label Label(string text, Font font, Color color)
        {
            return new Label { Text = text, Font = font, ForeColor = color, BackColor = Card, AutoSize = true };
        }
    }

    // A rounded panel with a small uppercase heading.
    class Card : Panel
    {
        public string Title;

        public Card(string title)
        {
            Title = title;
            BackColor = Theme.Card;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Box(e.Graphics, this, Theme.Card, Theme.CardBorder, 10);
            if (!string.IsNullOrEmpty(Title))
                using (var f = Theme.Semibold(8.25f))
                    TextRenderer.DrawText(e.Graphics, Title.ToUpperInvariant(), f, new Point((int)(16 * Theme.Scale(this)), (int)(11 * Theme.Scale(this))), Theme.Muted, Theme.Card);
        }
    }

    class ModernButton : Button
    {
        public bool Primary;
        bool hover, down;

        public ModernButton(string text, bool primary)
        {
            Text = text;
            Primary = primary;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Font = Theme.Semibold(9f);
            ForeColor = Theme.Text;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color fill = Primary ? (down ? Theme.AccentDown : hover ? Theme.AccentHover : Theme.Accent)
                                 : (down ? Theme.ButtonDown : hover ? Theme.ButtonHover : Theme.Button);
            Color border = Primary ? Color.Empty : (Focused && ShowFocusCues ? Theme.Accent : Theme.InputBorder);
            Theme.Box(e.Graphics, this, fill, border, 6);
            if (Primary && Focused && ShowFocusCues)
                using (var p = new Pen(Theme.Text, 1))
                using (var path = Theme.Round(new RectangleF(2.5f, 2.5f, Width - 5.5f, Height - 5.5f), 5 * Theme.Scale(this)))
                    e.Graphics.DrawPath(p, path);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Enabled ? Color.White : Theme.Faint, fill,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
    }

    class DarkCombo : ComboBox
    {
        bool hover;
        const int WM_PAINT = 0xF;

        public DarkCombo()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            DrawMode = DrawMode.OwnerDrawFixed;
            FlatStyle = FlatStyle.Flat;
            BackColor = Theme.Input;
            ForeColor = Theme.Text;
            Font = Theme.UI(9.5f);
            ItemHeight = 22;
            MaxDropDownItems = 12;
            IntegralHeight = false;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ItemHeight = (int)(22 * Theme.Scale(this));
            Theme.SetWindowTheme(Handle, "DarkMode_CFD", null);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool editArea = (e.State & DrawItemState.ComboBoxEdit) != 0;
            bool selected = !editArea && (e.State & DrawItemState.Selected) != 0;
            Color bg = selected ? Theme.Accent : Theme.Input;
            using (var b = new SolidBrush(bg)) e.Graphics.FillRectangle(b, e.Bounds);
            var r = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height);
            string text = GetItemText(Items[e.Index]);
            TextRenderer.DrawText(e.Graphics, text, Font, r, text == "(none)" && !selected ? Theme.Muted : Theme.Text, bg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_PAINT) PaintFace();
        }

        void PaintFace()
        {
            using (var g = Graphics.FromHwnd(Handle))
            {
                float s = Theme.Scale(this);
                Color border = DroppedDown || Focused ? Theme.Accent : hover ? Theme.InputHover : Theme.InputBorder;
                Theme.Box(g, this, Theme.Input, border, 6);
                string text = SelectedIndex >= 0 ? GetItemText(SelectedItem) : "";
                var r = new Rectangle((int)(10 * s), 0, Width - (int)(40 * s), Height);
                TextRenderer.DrawText(g, text, Font, r, text == "(none)" ? Theme.Muted : Theme.Text, Theme.Input,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                float cx = Width - 18 * s, cy = Height / 2f;
                using (var p = new Pen(Theme.Muted, 1.6f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLines(p, new[] { new PointF(cx - 4 * s, cy - 2 * s), new PointF(cx, cy + 2 * s), new PointF(cx + 4 * s, cy - 2 * s) });
            }
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnDropDownClosed(EventArgs e) { Invalidate(); base.OnDropDownClosed(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    }

    // A text box with a rounded border and placeholder text.
    class DarkTextBox : Panel
    {
        public readonly TextBox Box;
        bool hover;

        public DarkTextBox(string placeholder)
        {
            BackColor = Theme.Input;
            Cursor = Cursors.IBeam;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Box = new TextBox { BorderStyle = BorderStyle.None, BackColor = Theme.Input, ForeColor = Theme.Text, Font = Theme.UI(9.5f) };
            Controls.Add(Box);
            Box.GotFocus += delegate { Invalidate(); };
            Box.LostFocus += delegate { Invalidate(); };
            Box.MouseEnter += delegate { hover = true; Invalidate(); };
            Box.MouseLeave += delegate { hover = false; Invalidate(); };
            Box.HandleCreated += delegate { if (placeholder != null) Theme.SendMessage(Box.Handle, 0x1501 /* EM_SETCUEBANNER */, new IntPtr(1), placeholder); };
            Click += delegate { Box.Focus(); };
        }

        public override string Text { get { return Box.Text; } set { Box.Text = value; } }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int pad = (int)(10 * Theme.Scale(this));
            Box.Location = new Point(pad, (Height - Box.Height) / 2);
            Box.Width = Width - pad * 2;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Box(e.Graphics, this, Theme.Input, Box.Focused ? Theme.Accent : hover ? Theme.InputHover : Theme.InputBorder, 6);
        }
    }

    // [ - ]  value  [ + ]  number picker.
    class Stepper : Control
    {
        int value, min, max, hoverZone;
        public event EventHandler ValueChanged;

        public Stepper(int min, int max, int value)
        {
            this.min = min; this.max = max; this.value = value;
            Font = Theme.Semibold(9.5f);
            TabStop = true;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        }

        public int Value
        {
            get { return value; }
            set
            {
                int v = Math.Max(min, Math.Min(max, value));
                if (v == this.value) return;
                this.value = v;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        int Zone(int x) { return x < Height ? -1 : x > Width - Height ? 1 : 0; }

        protected override void OnMouseMove(MouseEventArgs e) { int z = Zone(e.X); if (z != hoverZone) { hoverZone = z; Invalidate(); } base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { hoverZone = 0; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { Focus(); Value += Zone(e.X); base.OnMouseDown(e); }
        protected override void OnMouseWheel(MouseEventArgs e) { Value += Math.Sign(e.Delta); base.OnMouseWheel(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override bool IsInputKey(Keys k) { return k == Keys.Up || k == Keys.Down || k == Keys.Left || k == Keys.Right || base.IsInputKey(k); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Right) Value++;
            else if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Left) Value--;
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float s = Theme.Scale(this);
            Theme.Box(g, this, Theme.Input, Focused ? Theme.Accent : Theme.InputBorder, 6);
            for (int side = -1; side <= 1; side += 2)
            {
                var zr = side < 0 ? new RectangleF(3 * s, 3 * s, Height - 6 * s, Height - 6 * s)
                                  : new RectangleF(Width - Height + 3 * s, 3 * s, Height - 6 * s, Height - 6 * s);
                if (hoverZone == side)
                    using (var b = new SolidBrush(Theme.ButtonHover))
                    using (var path = Theme.Round(zr, 4 * s)) g.FillPath(b, path);
                float cx = zr.X + zr.Width / 2, cy = zr.Y + zr.Height / 2, a = 4 * s;
                bool enabled = side < 0 ? value > min : value < max;
                using (var p = new Pen(enabled ? Theme.Text : Theme.Faint, 1.5f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLine(p, cx - a, cy, cx + a, cy);
                    if (side > 0) g.DrawLine(p, cx, cy - a, cx, cy + a);
                }
            }
            TextRenderer.DrawText(g, value.ToString(), Font, ClientRectangle, Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    // On/off switch.
    class Toggle : CheckBox
    {
        bool hover;

        public Toggle(string text)
        {
            Text = text;
            AutoSize = false;
            Cursor = Cursors.Hand;
            Font = Theme.UI(9.5f);
            ForeColor = Theme.Text;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float s = Theme.Scale(this);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Card);
            float w = 40 * s, h = 22 * s, y = (Height - h) / 2;
            var track = new RectangleF(1, y, w, h);
            using (var path = Theme.Round(track, h / 2))
            {
                using (var b = new SolidBrush(Checked ? (hover ? Theme.AccentHover : Theme.Accent) : (hover ? Theme.ButtonHover : Theme.Button))) g.FillPath(b, path);
                if (!Checked || (Focused && ShowFocusCues))
                    using (var p = new Pen(Focused && ShowFocusCues ? Theme.Text : Theme.InputBorder, s)) g.DrawPath(p, path);
            }
            float k = 16 * s, kx = Checked ? track.Right - k - 3 * s : track.X + 3 * s;
            using (var b = new SolidBrush(Checked ? Color.White : Theme.Muted)) g.FillEllipse(b, kx, y + (h - k) / 2, k, k);
            var tr = new Rectangle((int)(w + 12 * s), 0, Width - (int)(w + 12 * s), Height);
            TextRenderer.DrawText(g, Text, Font, tr, Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        }
    }

    // Big status banner at the top of the window.
    class StatusCard : Control
    {
        public Color StateColor = Theme.Amber;
        public string Title = "", Detail = "", Warning = "", Badge = "";
        public bool Pulse;
        readonly Timer anim = new Timer { Interval = 50 };
        double phase;

        public StatusCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            anim.Tick += delegate { if (Pulse) { phase += 0.12; Invalidate(); } };
            anim.Start();
        }

        public void Set(Color color, string title, string detail, string warning, string badge, bool pulse)
        {
            if (color == StateColor && title == Title && detail == Detail && warning == Warning && badge == Badge && pulse == Pulse) return;
            StateColor = color; Title = title; Detail = detail; Warning = warning; Badge = badge; Pulse = pulse;
            Invalidate();
        }

        protected override void Dispose(bool disposing) { if (disposing) anim.Dispose(); base.Dispose(disposing); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float s = Theme.Scale(this);
            Theme.Box(g, this, Theme.Card, Theme.CardBorder, 10);

            // thin colored accent along the left edge
            using (var path = Theme.Round(new RectangleF(0.5f, 0.5f, 6 * s, Height - 1.5f), 3 * s))
            using (var clip = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 10 * s))
            {
                g.SetClip(clip);
                using (var b = new SolidBrush(StateColor)) g.FillRectangle(b, 0, 0, 4 * s, Height);
                g.ResetClip();
            }

            // status dot with a soft (optionally pulsing) halo
            float cx = 32 * s, cy = 31 * s;
            double glow = Pulse ? 0.5 + 0.5 * Math.Sin(phase) : 0.6;
            float halo = (float)(9 + 4 * glow) * s;
            using (var b = new SolidBrush(Color.FromArgb((int)(40 + 50 * glow), StateColor))) g.FillEllipse(b, cx - halo, cy - halo, halo * 2, halo * 2);
            using (var b = new SolidBrush(StateColor)) g.FillEllipse(b, cx - 6 * s, cy - 6 * s, 12 * s, 12 * s);

            int left = (int)(54 * s);
            int badgeW = 0;
            if (!string.IsNullOrEmpty(Badge))
            {
                using (var f = Theme.Semibold(8.5f))
                {
                    var sz = TextRenderer.MeasureText(Badge, f);
                    badgeW = sz.Width + (int)(18 * s);
                    var br = new RectangleF(Width - badgeW - 16 * s, 18 * s, badgeW, 24 * s);
                    using (var path = Theme.Round(br, 12 * s))
                    using (var b = new SolidBrush(Theme.Button)) g.FillPath(b, path);
                    TextRenderer.DrawText(g, Badge, f, Rectangle.Round(br), Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
            using (var f = Theme.Semibold(14f))
                TextRenderer.DrawText(g, Title, f, new Rectangle(left, (int)(14 * s), Width - left - badgeW - (int)(28 * s), (int)(32 * s)), Theme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            using (var f = Theme.UI(9f))
            {
                int y = (int)(48 * s);
                var r = new Rectangle(left, y, Width - left - (int)(16 * s), (int)(20 * s));
                TextRenderer.DrawText(g, Detail, f, r, Theme.Muted, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                if (!string.IsNullOrEmpty(Warning))
                {
                    r.Y += (int)(20 * s);
                    r.Height = Height - r.Y - (int)(4 * s);
                    TextRenderer.DrawText(g, "⚠  " + Warning, f, r, Theme.Amber, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
            }
        }
    }

    enum LogKind { Info, Sent, Received, Good, Warn, Error }

    class LogEntry
    {
        public string Time, Text;
        public LogKind Kind;
        public override string ToString() { return Time + "  " + Text; }
    }

    // Colored, dark activity log. Ctrl+C copies the selected line.
    class LogView : ListBox
    {
        public LogView()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            BorderStyle = BorderStyle.None;
            BackColor = Theme.Card;
            ForeColor = Theme.Text;
            Font = new Font("Consolas", 9.5f);
            IntegralHeight = false;
            ItemHeight = 22;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ItemHeight = (int)(22 * Theme.Scale(this));
            Theme.SetWindowTheme(Handle, "DarkMode_Explorer", null);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.C && SelectedItem != null) Clipboard.SetText(SelectedItem.ToString());
            base.OnKeyDown(e);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) return;
            var item = Items[e.Index] as LogEntry;
            if (item == null) return;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            Color bg = sel ? Theme.ButtonHover : Theme.Card;
            using (var b = new SolidBrush(bg)) e.Graphics.FillRectangle(b, e.Bounds);
            float s = Theme.Scale(this);
            Color c; string mark;
            switch (item.Kind)
            {
                case LogKind.Sent: c = Theme.Blue; mark = "→"; break;
                case LogKind.Received: c = Theme.Green; mark = "←"; break;
                case LogKind.Good: c = Theme.Green; mark = "✓"; break;
                case LogKind.Warn: c = Theme.Amber; mark = "!"; break;
                case LogKind.Error: c = Theme.Red; mark = "✕"; break;
                default: c = Theme.Text; mark = "•"; break;
            }
            var flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            int x = e.Bounds.X + (int)(6 * s);
            TextRenderer.DrawText(e.Graphics, item.Time, Font, new Rectangle(x, e.Bounds.Y, (int)(100 * s), e.Bounds.Height), Theme.Faint, bg, flags);
            x += (int)(104 * s);
            TextRenderer.DrawText(e.Graphics, mark, Font, new Rectangle(x, e.Bounds.Y, (int)(18 * s), e.Bounds.Height), c, bg, flags);
            x += (int)(20 * s);
            TextRenderer.DrawText(e.Graphics, item.Text, Font, new Rectangle(x, e.Bounds.Y, e.Bounds.Right - x, e.Bounds.Height),
                item.Kind == LogKind.Info ? Theme.Text : c, bg, flags | TextFormatFlags.EndEllipsis);
        }
    }

    class DarkMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Theme.Card; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Card; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Card; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Card; } }
        public override Color MenuBorder { get { return Theme.CardBorder; } }
        public override Color MenuItemBorder { get { return Theme.ButtonHover; } }
        public override Color MenuItemSelected { get { return Theme.ButtonHover; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.ButtonHover; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.ButtonHover; } }
        public override Color SeparatorDark { get { return Theme.CardBorder; } }
        public override Color SeparatorLight { get { return Theme.CardBorder; } }
    }

    // Dark replacement for MessageBox yes/no questions.
    static class DarkDialog
    {
        public static bool Confirm(IWin32Window owner, string title, string message, string yes, string no)
        {
            using (var f = new Form())
            {
                f.Text = "MIDI Link";
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.MinimizeBox = f.MaximizeBox = false;
                f.ShowInTaskbar = false;
                f.StartPosition = FormStartPosition.CenterParent;
                f.BackColor = Theme.Bg;
                f.Font = Theme.UI(9.5f);
                f.HandleCreated += delegate { Theme.DarkTitleBar(f.Handle); };

                var lt = new Label { Text = title, Font = Theme.Semibold(12f), ForeColor = Theme.Text, BackColor = Theme.Bg, AutoSize = true, Location = new Point(24, 20) };
                var lm = new Label { Text = message, ForeColor = Theme.Muted, BackColor = Theme.Bg, Location = new Point(24, 56), Size = new Size(372, 70) };
                var bNo = new ModernButton(no, false) { Location = new Point(176, 140), Size = new Size(110, 32), DialogResult = DialogResult.No };
                var bYes = new ModernButton(yes, true) { Location = new Point(296, 140), Size = new Size(110, 32), DialogResult = DialogResult.Yes };
                f.Controls.AddRange(new Control[] { lt, lm, bNo, bYes });
                f.AcceptButton = bNo;
                f.CancelButton = bNo;
                f.ClientSize = new Size(430, 192);
                f.AutoScaleDimensions = new SizeF(96F, 96F);
                f.AutoScaleMode = AutoScaleMode.Dpi;
                f.Shown += delegate { bNo.Focus(); };
                return f.ShowDialog(owner) == DialogResult.Yes;
            }
        }
    }
}
