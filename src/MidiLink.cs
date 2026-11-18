// MIDI Link - send MIDI between two Windows PCs over a network or a direct Ethernet cable.
// Builds with the C# compiler that ships with Windows (.NET Framework 4.8), so the
// resulting MidiLink.exe runs on any Windows 10/11 PC with nothing to install.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("MIDI Link")]
[assembly: System.Reflection.AssemblyProduct("MIDI Link")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]

namespace MidiLink
{
    // ------------------------------------------------------------------ Windows MIDI (WinMM)
    static class WinMM
    {
        public delegate void MidiInProc(IntPtr hMidiIn, uint wMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MIDIINCAPS
        {
            public ushort wMid; public ushort wPid; public uint vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szPname;
            public uint dwSupport;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MIDIOUTCAPS
        {
            public ushort wMid; public ushort wPid; public uint vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szPname;
            public ushort wTechnology; public ushort wVoices; public ushort wNotes; public ushort wChannelMask;
            public uint dwSupport;
        }

        [DllImport("winmm.dll")] public static extern uint midiInGetNumDevs();
        [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "midiInGetDevCapsW")]
        public static extern int midiInGetDevCaps(UIntPtr id, out MIDIINCAPS caps, uint size);
        [DllImport("winmm.dll")] public static extern uint midiOutGetNumDevs();
        [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "midiOutGetDevCapsW")]
        public static extern int midiOutGetDevCaps(UIntPtr id, out MIDIOUTCAPS caps, uint size);
        [DllImport("winmm.dll")] public static extern int midiInOpen(out IntPtr h, uint id, MidiInProc cb, IntPtr inst, uint flags);
        [DllImport("winmm.dll")] public static extern int midiInStart(IntPtr h);
        [DllImport("winmm.dll")] public static extern int midiInStop(IntPtr h);
        [DllImport("winmm.dll")] public static extern int midiInReset(IntPtr h);
        [DllImport("winmm.dll")] public static extern int midiInClose(IntPtr h);
        [DllImport("winmm.dll")] public static extern int midiOutOpen(out IntPtr h, uint id, IntPtr cb, IntPtr inst, uint flags);
        [DllImport("winmm.dll")] public static extern int midiOutShortMsg(IntPtr h, uint msg);
        [DllImport("winmm.dll")] public static extern int midiOutClose(IntPtr h);

        public const uint CALLBACK_FUNCTION = 0x30000;
        public const uint MIM_DATA = 0x3C3;

        public static List<string> InputNames()
        {
            var list = new List<string>();
            uint n = midiInGetNumDevs();
            for (uint i = 0; i < n; i++)
            {
                MIDIINCAPS c;
                if (midiInGetDevCaps(new UIntPtr(i), out c, (uint)Marshal.SizeOf(typeof(MIDIINCAPS))) == 0) list.Add(c.szPname);
                else list.Add("MIDI In " + i);
            }
            return list;
        }

        public static List<string> OutputNames()
        {
            var list = new List<string>();
            uint n = midiOutGetNumDevs();
            for (uint i = 0; i < n; i++)
            {
                MIDIOUTCAPS c;
                if (midiOutGetDevCaps(new UIntPtr(i), out c, (uint)Marshal.SizeOf(typeof(MIDIOUTCAPS))) == 0) list.Add(c.szPname);
                else list.Add("MIDI Out " + i);
            }
            return list;
        }

        public static string ErrorText(int code)
        {
            switch (code)
            {
                case 4: return "the port is already in use by another program";
                case 2: return "the port no longer exists";
                case 6: return "the MIDI driver is not responding";
                case 7: return "out of memory";
                default: return "Windows MIDI error " + code;
            }
        }
    }

    class MidiIn : IDisposable
    {
        IntPtr handle;
        WinMM.MidiInProc callback; // kept in a field so the garbage collector cannot free it
        Action<uint> onMessage;
        public string Name;

        public static MidiIn Open(string name, Action<uint> onMessage, out string error)
        {
            error = null;
            int idx = WinMM.InputNames().IndexOf(name);
            if (idx < 0) { error = "the port was not found"; return null; }
            var m = new MidiIn();
            m.Name = name;
            m.onMessage = onMessage;
            m.callback = m.Callback;
            int r = WinMM.midiInOpen(out m.handle, (uint)idx, m.callback, IntPtr.Zero, WinMM.CALLBACK_FUNCTION);
            if (r != 0) { error = WinMM.ErrorText(r); return null; }
            WinMM.midiInStart(m.handle);
            return m;
        }

        void Callback(IntPtr h, uint msg, IntPtr inst, IntPtr p1, IntPtr p2)
        {
            if (msg == WinMM.MIM_DATA && onMessage != null)
            {
                try { onMessage((uint)(p1.ToInt64() & 0xFFFFFFFF)); } catch { }
            }
        }

        public void Dispose()
        {
            if (handle == IntPtr.Zero) return;
            onMessage = null;
            WinMM.midiInStop(handle);
            WinMM.midiInReset(handle);
            WinMM.midiInClose(handle);
            handle = IntPtr.Zero;
        }
    }

    class MidiOut : IDisposable
    {
        IntPtr handle;
        public string Name;

        public static MidiOut Open(string name, out string error)
        {
            error = null;
            int idx = WinMM.OutputNames().IndexOf(name);
            if (idx < 0) { error = "the port was not found"; return null; }
            var m = new MidiOut();
            m.Name = name;
            int r = WinMM.midiOutOpen(out m.handle, (uint)idx, IntPtr.Zero, IntPtr.Zero, 0);
            if (r != 0) { error = WinMM.ErrorText(r); return null; }
            return m;
        }

        public int Send(uint msg) { return handle == IntPtr.Zero ? -1 : WinMM.midiOutShortMsg(handle, msg); }

        public void Dispose()
        {
            if (handle == IntPtr.Zero) return;
            WinMM.midiOutClose(handle);
            handle = IntPtr.Zero;
        }
    }

    static class Midi
    {
        public static string Describe(uint m)
        {
            int st = (int)(m & 0xFF), d1 = (int)((m >> 8) & 0x7F), d2 = (int)((m >> 16) & 0x7F);
            int ch = (st & 0x0F) + 1;
            switch (st & 0xF0)
            {
                case 0x80: return "Note Off  ch " + ch + "  note " + d1;
                case 0x90: return d2 == 0 ? "Note Off  ch " + ch + "  note " + d1 : "Note On   ch " + ch + "  note " + d1 + "  velocity " + d2;
                case 0xA0: return "Aftertouch  ch " + ch + "  note " + d1 + "  value " + d2;
                case 0xB0: return "Control Change  ch " + ch + "  CC " + d1 + "  value " + d2;
                case 0xC0: return "Program Change  ch " + ch + "  program " + d1;
                case 0xD0: return "Channel Pressure  ch " + ch + "  value " + d1;
                case 0xE0: return "Pitch Bend  ch " + ch + "  value " + ((d2 << 7) | d1);
                default: return "System message 0x" + st.ToString("X2");
            }
        }
    }

    // ------------------------------------------------------------------ Network link
    class Peer
    {
        public IPEndPoint EP;
        public string Name = "?";
        public string Session;
        public string Flags = "";
        public DateTime LastSeen;
    }

    // Protocol: UDP text packets "ML1|KIND|linkName|sessionId|...".
    //  HELLO|machine|flags   - sent every second (broadcast, or to the fixed IP) so the computers find each other
    //  MIDI|seq|hexmsg       - a MIDI message; resent every 50 ms until ACKed (gives up after 3 s)
    //  ACK|seq               - receipt; duplicates are filtered with the sequence number
    //  PING|t / PONG|t       - round-trip time
    class NetLink : IDisposable
    {
        public const int DefaultPort = 21928;
        const int ConnectedSeconds = 4;

        class Pending { public int Seq; public uint Msg; public DateTime First; public DateTime LastSent; }

        readonly UdpClient udp;
        readonly string link;
        readonly string session = Guid.NewGuid().ToString("N").Substring(0, 10);
        readonly int remotePort;
        readonly IPAddress fixedIp;
        readonly object lk = new object();
        readonly Dictionary<int, Pending> pending = new Dictionary<int, Pending>();
        readonly HashSet<int> seen = new HashSet<int>();
        readonly Queue<int> seenOrder = new Queue<int>();
        readonly BlockingCollection<uint> outQueue = new BlockingCollection<uint>();
        readonly Thread rxThread, txThread;
        readonly System.Threading.Timer timer;
        volatile bool running = true;
        volatile Peer peer;
        string seenSession;
        int seq, timerBusy;
        DateTime lastHello = DateTime.MinValue, lastPing = DateTime.MinValue, lastBcastRefresh = DateTime.MinValue;
        List<IPEndPoint> helloTargets = new List<IPEndPoint>();

        public Func<string> GetFlags = delegate { return ""; };
        public event Action<uint> MidiReceived;
        public event Action<uint> MidiLost;
        public event Action<string> Info;
        public event Action<double> PingResult;
        public double LastPingMs = -1;
        public string OtherComputerWarning; public DateTime OtherComputerSeen;
        public int DropNextMidi, DropNextAcks; // used only by the self-test

        public NetLink(int listenPort, int remotePort, string link, IPAddress fixedIp)
        {
            this.link = Clean(link);
            this.remotePort = remotePort;
            this.fixedIp = fixedIp;
            udp = new UdpClient(AddressFamily.InterNetwork);
            udp.Client.ExclusiveAddressUse = true;
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, listenPort));
            udp.EnableBroadcast = true;
            try { udp.Client.IOControl(-1744830452 /* SIO_UDP_CONNRESET */, new byte[4], null); } catch { }
            rxThread = new Thread(RxLoop) { IsBackground = true, Name = "MidiLink RX" };
            txThread = new Thread(TxLoop) { IsBackground = true, Name = "MidiLink TX", Priority = ThreadPriority.AboveNormal };
            rxThread.Start();
            txThread.Start();
            timer = new System.Threading.Timer(Tick, null, 0, 20);
        }

        public static string Clean(string s) { return (s ?? "").Replace("|", "").Trim(); }

        string Hdr(string kind) { return "ML1|" + kind + "|" + link + "|" + session + "|"; }

        void SendRaw(string s, IPEndPoint ep)
        {
            try { byte[] b = Encoding.UTF8.GetBytes(s); udp.Send(b, b.Length, ep); } catch { }
        }

        public Peer ConnectedPeer
        {
            get
            {
                Peer p = peer;
                if (p == null || (DateTime.UtcNow - p.LastSeen).TotalSeconds > ConnectedSeconds) return null;
                return p;
            }
        }

        public void Send(uint msg) { if (running) outQueue.Add(msg); }

        public void PingNow() { lastPing = DateTime.MinValue; }

        void TxLoop()
        {
            try
            {
                foreach (uint m in outQueue.GetConsumingEnumerable())
                {
                    Pending p;
                    lock (lk)
                    {
                        seq++;
                        p = new Pending { Seq = seq, Msg = m, First = DateTime.UtcNow, LastSent = DateTime.UtcNow };
                        pending[seq] = p;
                    }
                    Peer peerNow = ConnectedPeer;
                    if (peerNow != null) SendRaw(Hdr("MIDI") + p.Seq + "|" + p.Msg.ToString("X"), peerNow.EP);
                }
            }
            catch { }
        }

        void Tick(object state)
        {
            if (Interlocked.Exchange(ref timerBusy, 1) == 1) return;
            try
            {
                if (!running) return;
                DateTime now = DateTime.UtcNow;
                if ((now - lastHello).TotalMilliseconds >= 1000)
                {
                    lastHello = now;
                    if ((now - lastBcastRefresh).TotalSeconds >= 5) { lastBcastRefresh = now; helloTargets = HelloTargets(); }
                    string hello = Hdr("HELLO") + Clean(Environment.MachineName) + "|" + GetFlags();
                    foreach (var ep in helloTargets) SendRaw(hello, ep);
                }
                Peer p = ConnectedPeer;
                if (p != null && (now - lastPing).TotalMilliseconds >= 2000)
                {
                    lastPing = now;
                    SendRaw(Hdr("PING") + Stopwatch.GetTimestamp(), p.EP);
                }
                var resend = new List<Pending>();
                var lost = new List<Pending>();
                lock (lk)
                {
                    foreach (var q in pending.Values)
                    {
                        if ((now - q.First).TotalMilliseconds > 3000) lost.Add(q);
                        else if ((now - q.LastSent).TotalMilliseconds >= 50) { q.LastSent = now; resend.Add(q); }
                    }
                    foreach (var q in lost) pending.Remove(q.Seq);
                }
                if (p != null) foreach (var q in resend) SendRaw(Hdr("MIDI") + q.Seq + "|" + q.Msg.ToString("X"), p.EP);
                var lostHandler = MidiLost;
                if (lostHandler != null) foreach (var q in lost) lostHandler(q.Msg);
            }
            catch { }
            finally { Interlocked.Exchange(ref timerBusy, 0); }
        }

        List<IPEndPoint> HelloTargets()
        {
            var list = new List<IPEndPoint>();
            if (fixedIp != null) { list.Add(new IPEndPoint(fixedIp, remotePort)); return list; }
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork || ua.IPv4Mask == null) continue;
                        byte[] ip = ua.Address.GetAddressBytes(), mask = ua.IPv4Mask.GetAddressBytes(), b = new byte[4];
                        for (int i = 0; i < 4; i++) b[i] = (byte)(ip[i] | ~mask[i]);
                        var ep = new IPEndPoint(new IPAddress(b), remotePort);
                        if (!list.Contains(ep)) list.Add(ep);
                    }
                }
            }
            catch { }
            list.Add(new IPEndPoint(IPAddress.Broadcast, remotePort));
            return list;
        }

        void RxLoop()
        {
            while (running)
            {
                try
                {
                    var from = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = udp.Receive(ref from);
                    Handle(Encoding.UTF8.GetString(data), from);
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception) { if (!running) break; }
            }
        }

        void Handle(string s, IPEndPoint from)
        {
            string[] f = s.Split('|');
            if (f.Length < 4 || f[0] != "ML1" || f[2] != link || f[3] == session) return;
            if (fixedIp != null && !from.Address.Equals(fixedIp)) return;
            string kind = f[1], their = f[3];

            if (!TouchPeer(their, from, kind == "HELLO" ? f : null) && kind != "MIDI" && kind != "PING") return;

            switch (kind)
            {
                case "MIDI":
                    {
                        if (f.Length < 6) return;
                        int sq = int.Parse(f[4]);
                        uint m = Convert.ToUInt32(f[5], 16);
                        if (DropNextMidi > 0) { DropNextMidi--; return; }
                        SendRaw(Hdr("ACK") + sq, from);
                        bool isNew;
                        lock (lk)
                        {
                            if (seenSession != their) { seenSession = their; seen.Clear(); seenOrder.Clear(); }
                            isNew = seen.Add(sq);
                            if (isNew) { seenOrder.Enqueue(sq); if (seenOrder.Count > 5000) seen.Remove(seenOrder.Dequeue()); }
                        }
                        var h = MidiReceived;
                        if (isNew && h != null) h(m);
                        break;
                    }
                case "ACK":
                    {
                        if (DropNextAcks > 0) { DropNextAcks--; return; }
                        int a = int.Parse(f[4]);
                        lock (lk) pending.Remove(a);
                        break;
                    }
                case "PING":
                    SendRaw(Hdr("PONG") + f[4], from);
                    break;
                case "PONG":
                    {
                        long t = long.Parse(f[4]);
                        double ms = (Stopwatch.GetTimestamp() - t) * 1000.0 / Stopwatch.Frequency;
                        LastPingMs = ms;
                        var h = PingResult;
                        if (h != null) h(ms);
                        break;
                    }
            }
        }

        // Returns false if the packet came from a third computer while we already have a live partner.
        bool TouchPeer(string their, IPEndPoint from, string[] hello)
        {
            DateTime now = DateTime.UtcNow;
            Peer p = peer;
            if (p == null || p.Session != their)
            {
                if (p != null && (now - p.LastSeen).TotalSeconds < ConnectedSeconds)
                {
                    string nm = hello != null && hello.Length > 4 ? hello[4] : "another computer";
                    OtherComputerWarning = nm + " (" + from.Address + ") is also using this link name";
                    OtherComputerSeen = now;
                    return false;
                }
                bool sameMachine = p != null && p.EP.Address.Equals(from.Address);
                p = new Peer { Session = their, EP = from, LastSeen = now };
                peer = p;
                var h = Info;
                if (h != null) h(sameMachine ? "MIDI Link restarted on the other computer (" + from.Address + ")" : "Found the other computer at " + from.Address);
            }
            p.LastSeen = now;
            p.EP = from;
            if (hello != null && hello.Length >= 6) { p.Name = hello[4]; p.Flags = hello[5]; }
            return true;
        }

        public void Dispose()
        {
            running = false;
            try { timer.Dispose(); } catch { }
            try { outQueue.CompleteAdding(); } catch { }
            try { udp.Close(); } catch { }
            rxThread.Join(500);
            txThread.Join(500);
        }
    }

    // ------------------------------------------------------------------ Settings
    class Settings
    {
        readonly string path;
        readonly Dictionary<string, string> d = new Dictionary<string, string>();

        public Settings(string path)
        {
            this.path = path;
            try
            {
                if (File.Exists(path))
                    foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
                    {
                        int i = line.IndexOf('=');
                        if (i > 0) d[line.Substring(0, i)] = line.Substring(i + 1);
                    }
            }
            catch { }
        }

        public string Get(string k, string def) { string v; return d.TryGetValue(k, out v) ? v : def; }

        public void Set(string k, string v)
        {
            d[k] = v ?? "";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var sb = new StringBuilder();
                foreach (var kv in d) sb.Append(kv.Key).Append('=').Append(kv.Value.Replace("\r", "").Replace("\n", "")).Append("\r\n");
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }
    }


    // ------------------------------------------------------------------ Entry point + self-test
    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        [STAThread]
        static int Main(string[] argv)
        {
            var a = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in argv)
            {
                string t = s.TrimStart('/', '-');
                int i = t.IndexOf(':');
                if (i > 0) a[t.Substring(0, i)] = t.Substring(i + 1); else a[t] = "";
            }
            if (a.ContainsKey("selftest")) return SelfTest.Run(a["selftest"]);

            int port = a.ContainsKey("port") ? int.Parse(a["port"]) : NetLink.DefaultPort;
            int remote = a.ContainsKey("remoteport") ? int.Parse(a["remoteport"]) : port;
            string settingsPath = a.ContainsKey("settings") ? a["settings"]
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MidiLink", "settings.txt");

            try { SetProcessDPIAware(); } catch { }
            bool created;
            using (var mutex = new Mutex(true, "Local\\MidiLink_" + port, out created))
            {
                if (!created)
                {
                    MessageBox.Show("MIDI Link is already running.\n\nLook for its blue \"M\" icon in the system tray (near the clock).", "MIDI Link", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm(new Settings(settingsPath), port, remote, a.ContainsKey("minimized")));
            }
            return 0;
        }
    }

    static class SelfTest
    {
        public static int Run(string outFile)
        {
            var sb = new StringBuilder();
            int failures = 0;
            Action<bool, string> check = delegate(bool ok, string what) { sb.AppendLine((ok ? "PASS  " : "FAIL  ") + what); if (!ok) failures++; };

            sb.AppendLine("MIDI inputs : " + string.Join(", ", WinMM.InputNames().ToArray()));
            sb.AppendLine("MIDI outputs: " + string.Join(", ", WinMM.OutputNames().ToArray()));

            var lo = IPAddress.Loopback;
            var gotA = new List<uint>(); var gotB = new List<uint>(); var lostA = new List<uint>();
            using (var a = new NetLink(21950, 21951, "Test", lo))
            using (var b = new NetLink(21951, 21950, "Test", lo))
            using (var c = new NetLink(21952, 21950, "Other", lo)) // different link name must be ignored
            {
                a.MidiReceived += delegate(uint m) { lock (gotA) gotA.Add(m); };
                b.MidiReceived += delegate(uint m) { lock (gotB) gotB.Add(m); };
                a.MidiLost += delegate(uint m) { lock (lostA) lostA.Add(m); };

                var sw = Stopwatch.StartNew();
                while ((a.ConnectedPeer == null || b.ConnectedPeer == null) && sw.ElapsedMilliseconds < 4000) Thread.Sleep(20);
                check(a.ConnectedPeer != null && b.ConnectedPeer != null, "computers find each other (" + sw.ElapsedMilliseconds + " ms)");
                check(a.ConnectedPeer != null && a.ConnectedPeer.EP.Port == 21951, "A paired with B, not with the different link name");

                a.Send(0x7F3C90); Thread.Sleep(300);
                lock (gotB) check(gotB.Count == 1 && gotB[0] == 0x7F3C90, "A -> B note arrives exactly once");

                b.Send(0x0040B5); Thread.Sleep(300);
                lock (gotA) check(gotA.Count == 1 && gotA[0] == 0x0040B5, "B -> A control change arrives exactly once");

                b.DropNextMidi = 3; // simulate 3 lost packets
                a.Send(0x643E90); Thread.Sleep(600);
                lock (gotB) check(gotB.Count == 2 && gotB[1] == 0x643E90, "lost packets are resent and delivered once");

                a.DropNextAcks = 4; // simulate lost receipts -> duplicates must be filtered
                a.Send(0x004080); Thread.Sleep(600);
                lock (gotB) check(gotB.Count == 3 && gotB[2] == 0x004080, "duplicate resends are filtered");

                for (uint i = 0; i < 200; i++) a.Send(0x7F0090u | (i % 128) << 8);
                Thread.Sleep(1000);
                lock (gotB) check(gotB.Count == 203, "burst of 200 messages all delivered (" + (gotB.Count - 3) + ")");

                Thread.Sleep(2500);
                check(a.LastPingMs >= 0 && a.LastPingMs < 50, "round-trip time measured: " + a.LastPingMs.ToString("0.00") + " ms");
                lock (lostA) check(lostA.Count == 0, "nothing reported as lost");
            }
            // Partner disappears -> sender reports loss
            using (var a = new NetLink(21960, 21961, "Test", lo))
            using (var b = new NetLink(21961, 21960, "Test", lo))
            {
                var lost = new List<uint>();
                a.MidiLost += delegate(uint m) { lock (lost) lost.Add(m); };
                var sw = Stopwatch.StartNew();
                while (a.ConnectedPeer == null && sw.ElapsedMilliseconds < 4000) Thread.Sleep(20);
                b.Dispose();
                a.Send(0x7F3C90);
                Thread.Sleep(3500);
                lock (lost) check(lost.Count == 1, "undeliverable message is reported, not silently dropped");
            }

            sb.AppendLine(failures == 0 ? "ALL TESTS PASSED" : failures + " TEST(S) FAILED");
            File.WriteAllText(outFile, sb.ToString());
            return failures;
        }
    }
}
