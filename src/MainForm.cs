// The MIDI Link window (always dark).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MidiLink
{
    class MainForm : Form
    {
        const string NONE = "(none)";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "MIDI Link";

        readonly Settings settings;
        readonly int listenPort, remotePort;
        readonly bool startMinimized;
        NetLink net;
        string netError;
        MidiIn midiIn;
        MidiOut midiOut;
        string wantIn, wantOut;
        bool suppress, reallyClose, logNextPing;
        int healthTick;

        StatusCard status;
        DarkCombo cbIn, cbOut;
        DarkTextBox tbLink, tbIp;
        Toggle chkStartup;
        Stepper stepCh, stepNote;
        LogView log;
        NotifyIcon tray;
        Timer uiTimer;
        Icon appIcon;

        public MainForm(Settings settings, int listenPort, int remotePort, bool startMinimized)
        {
            this.settings = settings;
            this.listenPort = listenPort;
            this.remotePort = remotePort;
            this.startMinimized = startMinimized;
            BuildUi();
            wantIn = NullIfNone(settings.Get("input", ""));
            wantOut = NullIfNone(settings.Get("output", ""));
            tbLink.Text = settings.Get("link", "Church");
            tbIp.Text = settings.Get("ip", "");
            suppress = true;
            chkStartup.Checked = IsStartupEnabled();
            suppress = false;
            RefreshPortLists();
            OpenInput();
            OpenOutput();
            StartNet();
            uiTimer = new Timer { Interval = 500 };
            uiTimer.Tick += delegate { UiTick(); };
            uiTimer.Start();
            UiTick();
        }

        static string NullIfNone(string s) { return string.IsNullOrEmpty(s) || s == NONE ? null : s; }

        // ---------------------------------------------------------------- layout
        void BuildUi()
        {
            SuspendLayout();
            Text = "MIDI Link";
            Font = Theme.UI(9.5f);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            StartPosition = FormStartPosition.CenterScreen;
            appIcon = MakeIcon();
            Icon = appIcon;

            var top = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            var topRight = AnchorStyles.Top | AnchorStyles.Right;
            Font label = Theme.UI(9f);

            status = new StatusCard { Location = new Point(16, 16), Size = new Size(608, 96), Anchor = top };
            Controls.Add(status);

            // MIDI ports
            var ports = new Card("MIDI ports") { Location = new Point(16, 122), Size = new Size(608, 148), Anchor = top };
            ports.Controls.Add(Theme.Label("Send to the other computer from", label, Theme.Muted));
            ports.Controls[0].Location = new Point(16, 34);
            cbIn = new DarkCombo { Location = new Point(16, 54), Width = 452, Anchor = top };
            ports.Controls.Add(cbIn);
            var btnRefresh = new ModernButton("Refresh", false) { Location = new Point(480, 53), Size = new Size(112, 30), Anchor = topRight };
            btnRefresh.Click += delegate { RefreshPortLists(); OpenInput(); OpenOutput(); Log("Port list refreshed."); };
            ports.Controls.Add(btnRefresh);
            var l2 = Theme.Label("Play MIDI received from the other computer into", label, Theme.Muted);
            l2.Location = new Point(16, 90);
            ports.Controls.Add(l2);
            cbOut = new DarkCombo { Location = new Point(16, 110), Width = 452, Anchor = top };
            ports.Controls.Add(cbOut);
            Controls.Add(ports);
            cbIn.SelectedIndexChanged += delegate { if (suppress) return; wantIn = NullIfNone((string)cbIn.SelectedItem); settings.Set("input", wantIn ?? ""); OpenInput(); };
            cbOut.SelectedIndexChanged += delegate { if (suppress) return; wantOut = NullIfNone((string)cbOut.SelectedItem); settings.Set("output", wantOut ?? ""); OpenOutput(); };

            // Connection
            var conn = new Card("Connection") { Location = new Point(16, 280), Size = new Size(608, 138), Anchor = top };
            var l3 = Theme.Label("Link name (same on both PCs)", label, Theme.Muted); l3.Location = new Point(16, 34); conn.Controls.Add(l3);
            tbLink = new DarkTextBox("Church") { Location = new Point(16, 54), Size = new Size(200, 30) };
            conn.Controls.Add(tbLink);
            var l4 = Theme.Label("Other computer's IP (optional)", label, Theme.Muted); l4.Location = new Point(228, 34); conn.Controls.Add(l4);
            tbIp = new DarkTextBox("Find automatically") { Location = new Point(228, 54), Size = new Size(240, 30), Anchor = top };
            conn.Controls.Add(tbIp);
            var btnApply = new ModernButton("Apply", true) { Location = new Point(480, 54), Size = new Size(112, 30), Anchor = topRight };
            btnApply.Click += delegate { ApplyConnection(); };
            conn.Controls.Add(btnApply);
            tbLink.Box.KeyDown += ApplyOnEnter;
            tbIp.Box.KeyDown += ApplyOnEnter;
            chkStartup = new Toggle("Start with Windows") { Location = new Point(16, 96), Size = new Size(260, 30) };
            chkStartup.CheckedChanged += delegate { if (!suppress) SetStartup(chkStartup.Checked); };
            conn.Controls.Add(chkStartup);
            var btnFw = new ModernButton("Allow through firewall", false) { Location = new Point(400, 96), Size = new Size(192, 30), Anchor = topRight };
            btnFw.Click += delegate { AllowFirewall(); };
            conn.Controls.Add(btnFw);
            Controls.Add(conn);

            // Test
            var test = new Card("Test") { Location = new Point(16, 428), Size = new Size(608, 80), Anchor = top };
            var btnPing = new ModernButton("Test connection", false) { Location = new Point(16, 36), Size = new Size(140, 30) };
            btnPing.Click += delegate { TestConnection(); };
            test.Controls.Add(btnPing);
            var l5 = Theme.Label("Channel", label, Theme.Muted); l5.Location = new Point(176, 43); test.Controls.Add(l5);
            stepCh = new Stepper(1, 16, 16) { Location = new Point(234, 36), Size = new Size(100, 30) };
            test.Controls.Add(stepCh);
            var l6 = Theme.Label("Note", label, Theme.Muted); l6.Location = new Point(348, 43); test.Controls.Add(l6);
            stepNote = new Stepper(0, 127, 0) { Location = new Point(386, 36), Size = new Size(100, 30) };
            test.Controls.Add(stepNote);
            var btnNote = new ModernButton("Send note", true) { Location = new Point(500, 36), Size = new Size(92, 30), Anchor = topRight };
            btnNote.Click += delegate { SendTestNote(); };
            test.Controls.Add(btnNote);
            Controls.Add(test);

            // Activity
            var act = new Card("Activity") { Location = new Point(16, 518), Size = new Size(608, 226), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            var btnClear = new ModernButton("Clear", false) { Location = new Point(524, 8), Size = new Size(68, 24), Anchor = topRight, Font = Theme.UI(8.5f) };
            btnClear.Click += delegate { log.Items.Clear(); };
            act.Controls.Add(btnClear);
            log = new LogView { Location = new Point(10, 38), Size = new Size(588, 178), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            act.Controls.Add(log);
            Controls.Add(act);

            ClientSize = new Size(640, 760);
            MinimumSize = new Size(560, 640);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;

            tray = new NotifyIcon { Icon = appIcon, Text = "MIDI Link", Visible = true };
            var menu = new ContextMenuStrip { Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors()), BackColor = Theme.Card, ForeColor = Theme.Text, ShowImageMargin = false, Font = Theme.UI(9.5f) };
            menu.Items.Add("Open MIDI Link", null, delegate { ShowFromTray(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, delegate { Close(); });
            foreach (ToolStripItem it in menu.Items) it.ForeColor = Theme.Text;
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ShowFromTray(); };

            ResumeLayout(false);
        }

        void ApplyOnEnter(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ApplyConnection(); }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.DarkTitleBar(Handle);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // Keep the window on screen on small or high-DPI displays.
            var wa = Screen.FromControl(this).WorkingArea;
            if (Height > wa.Height) { Height = wa.Height; Top = wa.Top; }
        }

        static Icon MakeIcon()
        {
            using (var bmp = new Bitmap(32, 32))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    g.Clear(Color.Transparent);
                    using (var path = Theme.Round(new RectangleF(1, 1, 30, 30), 8))
                    using (var b = new SolidBrush(Theme.Accent)) g.FillPath(b, path);
                    using (var f = new Font("Segoe UI", 16F, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString("M", f, Brushes.White, new RectangleF(0, 1, 32, 32), sf);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        // ---------------------------------------------------------------- logging
        void Log(string text) { Log(text, LogKind.Info); }

        void Log(string text, LogKind kind)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { try { BeginInvoke(new Action<string, LogKind>(Log), text, kind); } catch { } return; }
            log.Items.Insert(0, new LogEntry { Time = DateTime.Now.ToString("HH:mm:ss.fff"), Text = text, Kind = kind });
            while (log.Items.Count > 400) log.Items.RemoveAt(log.Items.Count - 1);
        }

        // ---------------------------------------------------------------- MIDI ports
        void RefreshPortLists()
        {
            suppress = true;
            FillCombo(cbIn, WinMM.InputNames(), wantIn);
            FillCombo(cbOut, WinMM.OutputNames(), wantOut);
            suppress = false;
        }

        static void FillCombo(ComboBox cb, List<string> names, string want)
        {
            cb.BeginUpdate();
            cb.Items.Clear();
            cb.Items.Add(NONE);
            foreach (var n in names) if (!cb.Items.Contains(n)) cb.Items.Add(n);
            if (want != null && !cb.Items.Contains(want)) cb.Items.Add(want); // remembered but currently missing
            cb.SelectedItem = want ?? NONE;
            cb.EndUpdate();
        }

        void OpenInput()
        {
            if (midiIn != null) { midiIn.Dispose(); midiIn = null; }
            if (wantIn == null) return;
            string err;
            midiIn = MidiIn.Open(wantIn, OnLocalMidi, out err);
            if (midiIn != null) Log("Listening for MIDI on \"" + wantIn + "\".", LogKind.Good);
            else Log("Could not open \"" + wantIn + "\": " + err + ".", LogKind.Error);
        }

        void OpenOutput()
        {
            if (midiOut != null) { midiOut.Dispose(); midiOut = null; }
            if (wantOut == null) return;
            string err;
            midiOut = MidiOut.Open(wantOut, out err);
            if (midiOut != null) Log("Ready to play MIDI into \"" + wantOut + "\".", LogKind.Good);
            else Log("Could not open \"" + wantOut + "\": " + err + ".", LogKind.Error);
        }

        // Called on a Windows MIDI thread.
        void OnLocalMidi(uint msg)
        {
            int st = (int)(msg & 0xFF);
            if (st == 0xF8 || st == 0xFE || st < 0x80) return; // skip clock / active-sensing noise
            NetLink n = net;
            if (n == null) { Log("Not sent (network not running): " + Midi.Describe(msg), LogKind.Error); return; }
            n.Send(msg);
            if (n.ConnectedPeer == null) Log("Waiting to send: " + Midi.Describe(msg), LogKind.Warn);
            else Log("Sent  " + Midi.Describe(msg), LogKind.Sent);
        }

        // Called on the network thread.
        void OnRemoteMidi(uint msg)
        {
            MidiOut o = midiOut;
            if (o == null) { Log("Received " + Midi.Describe(msg) + "  (NOT played: no output port chosen)", LogKind.Warn); return; }
            int r = o.Send(msg);
            if (r == 0) Log("Received  " + Midi.Describe(msg), LogKind.Received);
            else Log("Received " + Midi.Describe(msg) + "  (playback error " + r + ")", LogKind.Error);
        }

        void HealthCheck()
        {
            var ins = WinMM.InputNames();
            var outs = WinMM.OutputNames();
            if (midiIn != null && !ins.Contains(midiIn.Name)) { Log("Port \"" + midiIn.Name + "\" disappeared.", LogKind.Error); midiIn.Dispose(); midiIn = null; }
            if (midiOut != null && !outs.Contains(midiOut.Name)) { Log("Port \"" + midiOut.Name + "\" disappeared.", LogKind.Error); midiOut.Dispose(); midiOut = null; }
            if (!HasAll(cbIn, ins) || !HasAll(cbOut, outs)) RefreshPortLists();
            if (midiIn == null && wantIn != null && ins.Contains(wantIn)) OpenInput();
            if (midiOut == null && wantOut != null && outs.Contains(wantOut)) OpenOutput();
        }

        // True when every real port is listed (a remembered-but-missing port may also be listed).
        static bool HasAll(ComboBox cb, List<string> names)
        {
            foreach (var n in names) if (!cb.Items.Contains(n)) return false;
            return true;
        }

        // ---------------------------------------------------------------- network
        void StartNet()
        {
            if (net != null) { net.Dispose(); net = null; }
            netError = null;
            IPAddress ip = null;
            string ipText = tbIp.Text.Trim();
            if (ipText.Length > 0 && !IPAddress.TryParse(ipText, out ip))
            {
                netError = "\"" + ipText + "\" is not a valid IP address (example: 192.168.77.2)";
                Log(netError, LogKind.Error);
                return;
            }
            try
            {
                net = new NetLink(listenPort, remotePort, LinkName(), ip);
                net.GetFlags = delegate { return (midiIn != null ? "I" : "") + (midiOut != null ? "O" : ""); };
                net.MidiReceived += OnRemoteMidi;
                net.MidiLost += delegate(uint m) { Log("NOT DELIVERED (other computer did not confirm): " + Midi.Describe(m), LogKind.Error); };
                net.Info += delegate(string s) { Log(s, LogKind.Good); };
                net.PingResult += delegate(double ms) { if (logNextPing) { logNextPing = false; Log("Connection OK, round trip " + ms.ToString("0.0") + " ms.", LogKind.Good); } };
                Log("Network ready (UDP port " + listenPort + ", link name \"" + LinkName() + "\"" + (ip != null ? ", only talking to " + ip : "") + ").");
            }
            catch (SocketException ex)
            {
                netError = ex.SocketErrorCode == SocketError.AddressAlreadyInUse
                    ? "UDP port " + listenPort + " is already in use (is MIDI Link already open?)"
                    : "Network error: " + ex.Message;
                Log(netError, LogKind.Error);
            }
        }

        string LinkName()
        {
            string n = NetLink.Clean(tbLink.Text);
            return n.Length == 0 ? "Church" : n;
        }

        void ApplyConnection()
        {
            settings.Set("link", LinkName());
            settings.Set("ip", tbIp.Text.Trim());
            StartNet();
        }

        void TestConnection()
        {
            if (net == null || net.ConnectedPeer == null) { Log("Test: the other computer has not been found yet.", LogKind.Warn); return; }
            logNextPing = true;
            net.PingNow();
        }

        void SendTestNote()
        {
            if (net == null) { Log("Test note not sent: network not running.", LogKind.Error); return; }
            uint ch = (uint)stepCh.Value - 1, note = (uint)stepNote.Value;
            uint on = 0x90u | ch | (note << 8) | (127u << 16);
            uint off = 0x80u | ch | (note << 8);
            net.Send(on);
            Log("Sent test  " + Midi.Describe(on), LogKind.Sent);
            var t = new Timer { Interval = 250 };
            t.Tick += delegate { t.Stop(); t.Dispose(); if (net != null) net.Send(off); };
            t.Start();
        }

        // ---------------------------------------------------------------- status
        void UiTick()
        {
            if (++healthTick >= 6) { healthTick = 0; HealthCheck(); }

            var problems = new List<string>();
            if (wantIn == null && wantOut == null) problems.Add("Choose a MIDI port below.");
            if (wantIn != null && midiIn == null) problems.Add("Send port \"" + wantIn + "\" is not available.");
            if (wantOut != null && midiOut == null) problems.Add("Output port \"" + wantOut + "\" is not available.");

            Color color; string title, detail, badge = "";
            bool pulse = false;
            Peer p = net == null ? null : net.ConnectedPeer;
            if (net == null)
            {
                color = Theme.Red; title = "Not running";
                detail = netError ?? "Network not started.";
            }
            else if (p == null)
            {
                color = Theme.Amber; title = "Looking for the other computer…"; pulse = true;
                detail = "Open MIDI Link on the other computer with link name “" + LinkName() + "”.";
            }
            else
            {
                color = problems.Count > 0 ? Theme.Orange : Theme.Green;
                title = "Connected to " + p.Name;
                if (net.LastPingMs >= 0) badge = net.LastPingMs.ToString("0.0") + " ms";
                detail = p.EP.Address + "   ·   " + p.Name + " sends: " + (p.Flags.Contains("I") ? "yes" : "no") +
                         "   ·   plays received MIDI: " + (p.Flags.Contains("O") ? "yes" : "no");
                if (wantIn != null && !p.Flags.Contains("O")) problems.Add(p.Name + " has no output port chosen, so cues sent from here won't be played there.");
            }
            if (net != null && net.OtherComputerWarning != null && (DateTime.UtcNow - net.OtherComputerSeen).TotalSeconds < 5)
                problems.Add(net.OtherComputerWarning + ". Give each pair its own link name.");

            status.Set(color, title, detail, string.Join("  ", problems.ToArray()), badge, pulse);
            string tip = "MIDI Link - " + title;
            tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
        }

        // ---------------------------------------------------------------- startup, firewall, tray
        static bool IsStartupEnabled()
        {
            try { using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue(RunName) != null; }
            catch { return false; }
        }

        void SetStartup(bool on)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (on) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\" /minimized");
                    else k.DeleteValue(RunName, false);
                }
                Log(on ? "MIDI Link will start (in the tray) when you sign in to Windows." : "MIDI Link will no longer start automatically.", LogKind.Good);
            }
            catch (Exception ex) { Log("Could not change startup setting: " + ex.Message, LogKind.Error); }
        }

        void AllowFirewall()
        {
            string rule = "name=\"MIDI Link\"";
            string args = "/c netsh advfirewall firewall delete rule " + rule + " >nul & " +
                          "netsh advfirewall firewall add rule " + rule + " dir=in action=allow protocol=UDP localport=" + listenPort + " profile=any";
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", args) { Verb = "runas", UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden };
                var proc = Process.Start(psi);
                proc.WaitForExit(15000);
                if (proc.ExitCode == 0) Log("Firewall rule added: UDP " + listenPort + " allowed on all network types.", LogKind.Good);
                else Log("Firewall command returned " + proc.ExitCode + ".", LogKind.Warn);
            }
            catch (Exception ex) { Log("Firewall change cancelled or failed: " + ex.Message, LogKind.Warn); }
        }

        void ShowFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (startMinimized) WindowState = FormWindowState.Minimized;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized && tray != null)
            {
                Hide();
                if (!startMinimized || settings.Get("trayTipShown", "") != "1")
                {
                    tray.ShowBalloonTip(3000, "MIDI Link is still running", "Double-click this icon to open it again.", ToolTipIcon.Info);
                    settings.Set("trayTipShown", "1");
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!reallyClose && e.CloseReason == CloseReason.UserClosing)
            {
                if (!Visible) ShowFromTray();
                bool yes = DarkDialog.Confirm(this, "Close MIDI Link?",
                    "MIDI cues will stop passing between the computers.\n\nTip: minimize the window instead to keep it running in the tray.",
                    "Close", "Keep running");
                if (!yes) { e.Cancel = true; return; }
            }
            reallyClose = true;
            uiTimer.Stop();
            if (net != null) net.Dispose();
            if (midiIn != null) midiIn.Dispose();
            if (midiOut != null) midiOut.Dispose();
            tray.Visible = false;
            tray.Dispose();
            base.OnFormClosing(e);
        }
    }
}
