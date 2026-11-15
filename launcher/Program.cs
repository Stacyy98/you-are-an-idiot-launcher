// You are an Idiot! (dot org) -- native host
//
// Reconstruction of the youdontknowwhoiam.org / youareanidiot.org behaviour
// on a modern Windows box.  The original page was a Flash movie inside popup
// windows that you.js moved around with window.moveTo() and re-spawned six
// times on unload.  Browsers cannot do any of that anymore, so this host
// drives the *original* youare.swf with the *original* Flash standalone
// player, and re-implements you.js 1:1.
//
// you.js, verbatim:
//     var xOff = 5; var yOff = 5; var xPos = 400; var yPos = -100;
//     function newXlt(){ xOff = Math.ceil( 0 - 6 * Math.random()) * 5 - 10 ; window.focus() }
//     function newXrt(){ xOff = Math.ceil(7 * Math.random())  * 5 - 10 ; }
//     function newYup(){ yOff = Math.ceil( 0 - 6 * Math.random())  * 5 - 10 ; }
//     function newYdn(){ yOff = Math.ceil( 7 * Math.random())  * 5 - 10  ; }
//     function playBall(){
//         xPos += xOff; yPos += yOff;
//         if (xPos > screen.width-175){ newXlt(); }
//         if (xPos < 0){ newXrt(); }
//         if (yPos > screen.height-100){ newYup(); }
//         if (yPos < 0){ newYdn(); }
//         window.moveTo(xPos,yPos);
//         setTimeout('playBall()',1);
//     }

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace YouAreAnIdiot
{
    internal static class Program
    {
        // openWindow(..., 'width=180,height=175')
        // 180x175 was sized for a 1024x768 monitor.  Scale it up for modern
        // screens; override with a plain number in size.txt next to the exe.
        internal static int PopupW = 320;
        internal static int PopupH = 311;
        internal static double Scale = 320.0 / 180.0;
        internal static double MarginX = 175 * (320.0 / 180.0);
        internal static double MarginY = 100 * (320.0 / 180.0);

        // setTimeout('playBall()',1) -- browsers clamped it to roughly this.
        internal const int TickMs = 15;

        // you.js moved xOff pixels per tick, and a tick was ~1/60s, so
        // "5" means ~300 px/s.  Half that keeps the same feel at 1920 wide:
        // movement is time-based now, so a busy machine cannot make it race.
        internal const double PxPerSecondPerUnit = 30.0;

        internal static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
        private static long lastTick;

        // Hard safety stop.  The original had no cap; 6^n popups ends a machine.
        internal const int MaxWindows = 200;

        internal static readonly List<IdiotWindow> Windows = new List<IdiotWindow>();
        internal static string SwfPath;
        internal static string FlashExe;
        internal static bool Respawn = true;
        internal static bool ShuttingDown;
        internal static NotifyIcon Tray;
        internal static ToolStripMenuItem TrayHeader;
        internal static ToolStripMenuItem TrayRespawn;
        internal static Timer Watchdog;
        internal static Timer Tick;

        [STAThread]
        private static void Main(string[] args)
        {
            // "YouAreAnIdiot.exe /stop" -- kill everything, no respawn.  Used by
            // the STOP shortcut.  Hosts die first so nothing is left to spawn.
            if (args.Length > 0 && (args[0] == "/stop" || args[0] == "-stop"))
            {
                StopEverything();
                return;
            }

            string dir = AppDomain.CurrentDomain.BaseDirectory;
            SwfPath = Path.Combine(dir, "youare.swf");
            FlashExe = LocateFlash();

            // Internal: render one window off-screen and save a PNG of it.
            // Used to pull the smiley out of the .swf for the program icon.
            if (args.Length >= 2 && args[0] == "/capture")
            {
                CaptureFrame(args[1], args.Length > 2 ? int.Parse(args[2]) : 1600);
                return;
            }

            if (FlashExe == null || !File.Exists(SwfPath))
            {
                MessageBox.Show(
                    "The idiot needs two things in " + dir + ":\r\n\r\n" +
                    "  - youare.swf\r\n" +
                    "  - the Adobe Flash standalone player (flashplayer_sa.exe)\r\n\r\n" +
                    "One of them is missing.",
                    "You are an Idiot! (dot org)",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            LoadSize();
            BuildTray();

            Watchdog = new Timer { Interval = 20 };
            Watchdog.Tick += delegate { TryAttachPending(); };
            Watchdog.Start();

            // one clock for every window: they step together, so no async
            // stampede of per-window timers
            lastTick = Clock.ElapsedMilliseconds;
            Tick = new Timer { Interval = TickMs };
            Tick.Tick += delegate { StepAll(); };
            Tick.Start();

            // The landing "page": the movie, normal size, does not bounce.
            Spawn(1, false);

            Application.Run(new HotkeyForm());
        }

        // ------------------------------------------------------------------
        // assets
        // ------------------------------------------------------------------

        private static string LocateFlash()
        {
            string[] candidates =
            {
                @"C:\Program Files (x86)\Flash Player\flashplayer_sa.exe",
                @"C:\Program Files\Flash Player\flashplayer_sa.exe",
            };
            foreach (string c in candidates)
                if (File.Exists(c)) return c;
            return null;
        }

        // Popup size: original was 180x175 on a 1024x768 screen.  Default here
        // is 320 wide; put a single number in size.txt to change it.
        private static void LoadSize()
        {
            int w = 320;
            try
            {
                string f = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "size.txt");
                if (File.Exists(f))
                {
                    int t;
                    if (int.TryParse(File.ReadAllText(f).Trim(), out t)) w = t;
                }
            }
            catch { }

            w = Math.Max(140, Math.Min(900, w));
            PopupW = w;
            PopupH = (int)Math.Round(w * 175.0 / 180.0);
            Scale = w / 180.0;

            // you.js hard-coded margins: screen.width-175 and screen.height-100
            MarginX = 175 * Scale;
            MarginY = 100 * Scale;
        }

        // ------------------------------------------------------------------
        // spawning  (procreate(): openWindow('open.html') x6)
        // ------------------------------------------------------------------

        internal static void Spawn(int count, bool bounce)
        {
            if (ShuttingDown) return;

            for (int i = 0; i < count; i++)
            {
                if (Windows.Count >= MaxWindows) return;

                var w = new IdiotWindow(bounce, i);
                Windows.Add(w);
                UpdateTray();
            }
        }

        // 6 more, exactly like onUnLoad="procreate()"
        internal static void Procreate()
        {
            if (ShuttingDown || !Respawn) return;
            Spawn(6, true);
        }

        // ------------------------------------------------------------------
        // window plumbing
        // ------------------------------------------------------------------

        // One step for all windows on one clock.
        private static void StepAll()
        {
            long now = Clock.ElapsedMilliseconds;
            double dt = (now - lastTick) / 1000.0;
            lastTick = now;
            if (dt <= 0) return;
            if (dt > 0.10) dt = 0.10;      // after a stall, don't teleport

            foreach (IdiotWindow w in Windows.ToArray())
            {
                try { w.Step(dt); }
                catch { }
            }
        }

        private static void TryAttachPending()
        {
            foreach (IdiotWindow w in Windows.ToArray())
            {
                if (w.Closed) continue;
                try
                {
                    if (w.Hwnd == IntPtr.Zero)
                    {
                        if (w.Proc.HasExited)
                        {
                            w.MarkClosed();
                            continue;
                        }
                        IntPtr h = FindTopLevelWindowForPid(w.Proc.Id);
                        if (h != IntPtr.Zero) w.Attach(h);
                    }
                    else if (w.Proc.HasExited)
                    {
                        w.MarkClosed();
                    }
                }
                catch { }
            }
        }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumProc cb, IntPtr lp);

        private delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder s, int max);

        private static IntPtr FindTopLevelWindowForPid(int pid)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows(delegate (IntPtr h, IntPtr l)
            {
                uint p;
                GetWindowThreadProcessId(h, out p);
                if ((int)p != pid) return true;
                if (!IsWindowVisible(h)) return true;
                if (GetWindow(h, 4 /* GW_OWNER */) != IntPtr.Zero) return true;
                var sb = new StringBuilder(256);
                if (GetWindowTextW(h, sb, 256) <= 0) return true;
                found = h;
                return false;
            }, IntPtr.Zero);
            return found;
        }

        // ------------------------------------------------------------------
        // tray icon  (the mercy button the original never had)
        // ------------------------------------------------------------------

        private static void BuildTray()
        {
            Icon icon = null;
            try { icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }
            if (icon == null)
            {
                try { icon = Icon.ExtractAssociatedIcon(FlashExe); }
                catch { }
            }
            if (icon == null) icon = SystemIcons.Warning;

            var menu = new ContextMenuStrip();
            TrayHeader = new ToolStripMenuItem("0 windows") { Enabled = false };
            var more = new ToolStripMenuItem("Open 6 more");
            TrayRespawn = new ToolStripMenuItem("Respawn on close") { Checked = true, CheckOnClick = true };
            var stop = new ToolStripMenuItem("Close everything");
            var exit = new ToolStripMenuItem("Exit");

            more.Click += delegate { Spawn(6, true); };
            TrayRespawn.CheckedChanged += delegate (object s, EventArgs e)
            {
                Respawn = TrayRespawn.Checked;
            };
            stop.Click += delegate { KillAll(); };
            exit.Click += delegate { KillAll(); Application.Exit(); };

            menu.Items.Add(TrayHeader);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(more);
            menu.Items.Add(TrayRespawn);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(stop);
            menu.Items.Add(exit);

            Tray = new NotifyIcon
            {
                Icon = icon,
                Text = "You are an Idiot! (dot org)  |  Ctrl+Alt+Shift+X = stop",
                ContextMenuStrip = menu,
                Visible = true
            };
        }

        internal static void UpdateTray()
        {
            if (TrayHeader != null)
                TrayHeader.Text = Windows.Count + (Windows.Count == 1 ? " window" : " windows");
        }

        internal static void KillAll()
        {
            ShuttingDown = true;
            foreach (IdiotWindow w in Windows.ToArray())
                w.SilentClose();
            Windows.Clear();
            UpdateTray();
            ShuttingDown = false;
        }

        // "/capture <png> [width]": render one window completely off-screen and
        // save it with PrintWindow.  Silent -- nothing appears on the desktop.
        private static void CaptureFrame(string pngPath, int width)
        {
            int cw = Math.Max(200, width);
            int ch = cw * 3 / 4;          // the stage is 800x600

            var psi = new ProcessStartInfo
            {
                FileName = FlashExe,
                Arguments = "\"" + SwfPath + "\"",
                WorkingDirectory = Path.GetDirectoryName(SwfPath),
                UseShellExecute = false
            };
            Process proc = Process.Start(psi);

            IntPtr hwnd = IntPtr.Zero;
            for (int i = 0; i < 200 && hwnd == IntPtr.Zero; i++)
            {
                System.Threading.Thread.Sleep(50);
                hwnd = FindTopLevelWindowForPid(proc.Id);
            }

            if (hwnd != IntPtr.Zero)
            {
                Win.SetWindowText(hwnd, "You are an Idiot!");
                IntPtr menu = Win.GetMenu(hwnd);
                if (menu != IntPtr.Zero) { Win.SetMenu(hwnd, IntPtr.Zero); Win.DrawMenuBar(hwnd); }

                // size the *client* area to exactly 4:3 so there is no letterbox
                var rc = new Win.RECT { L = 0, T = 0, R = cw, B = ch };
                int style = Win.GetWindowLong(hwnd, -16);
                int exStyle = Win.GetWindowLong(hwnd, -20);
                Win.AdjustWindowRectEx(ref rc, style, false, exStyle);

                Win.ShowWindow(hwnd, 0);
                Win.SetWindowPos(hwnd, IntPtr.Zero, -8000, -8000, rc.R - rc.L, rc.B - rc.T,
                    0x0004 | 0x0010 | 0x0020);
                Win.ShowWindow(hwnd, 4);
                System.Threading.Thread.Sleep(2500);

                Win.RECT cr;
                Win.GetClientRect(hwnd, out cr);

                using (var bmp = new Bitmap(cr.R, cr.B))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        IntPtr hdc = g.GetHdc();
                        Win.PrintWindow(hwnd, hdc, 3);   // CLIENTONLY | RENDERFULLCONTENT
                        g.ReleaseHdc(hdc);
                    }
                    bmp.Save(pngPath, System.Drawing.Imaging.ImageFormat.Png);
                }
            }

            try { proc.Kill(); }
            catch { }
        }

        // "/stop": hosts first, so nothing is left alive to spawn 6 more,
        private static void StopEverything()
        {
            string hostName = Process.GetCurrentProcess().ProcessName;
            int self = Process.GetCurrentProcess().Id;

            foreach (Process p in Process.GetProcessesByName(hostName))
            {
                try { if (p.Id != self) p.Kill(); }
                catch { }
            }

            // let the hosts actually die before reaping their children
            System.Threading.Thread.Sleep(500);

            foreach (Process p in Process.GetProcessesByName("flashplayer_sa"))
            {
                try
                {
                    if (p.MainWindowTitle == "You are an Idiot!")
                        p.Kill();
                }
                catch { }
            }
        }
    }

    // ----------------------------------------------------------------------
    // one projector window == one open.html popup
    // ----------------------------------------------------------------------
    internal sealed class IdiotWindow
    {
        private static readonly Random Rng = new Random();

        internal Process Proc;
        internal IntPtr Hwnd;
        internal bool Closed;
        internal readonly bool Bounce;
        private readonly int Index;

        internal static double MarginX { get { return Program.MarginX; } }
        internal static double MarginY { get { return Program.MarginY; } }

        // you.js globals, per window
        private double xOff = 5;
        private double yOff = 5;
        private double xPos = 400;
        private double yPos = -100;

        private static readonly Rectangle ScreenBounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;

        internal IdiotWindow(bool bounce, int index)
        {
            Bounce = bounce;
            Index = index;

            var psi = new ProcessStartInfo
            {
                FileName = Program.FlashExe,
                Arguments = "\"" + Program.SwfPath + "\"",
                WorkingDirectory = Path.GetDirectoryName(Program.SwfPath),
                UseShellExecute = false
            };

            Proc = Process.Start(psi);
        }

        // --------------------------------------------------------------
        internal void Attach(IntPtr hwnd)
        {
            Hwnd = hwnd;

            Win.SetWindowText(Hwnd, "You are an Idiot!");

            // no File/View/Control/Help strip, no resize grips
            IntPtr menu = Win.GetMenu(Hwnd);
            if (menu != IntPtr.Zero)
            {
                Win.SetMenu(Hwnd, IntPtr.Zero);
                Win.DrawMenuBar(Hwnd);
            }
            int style = Win.GetWindowLong(Hwnd, -16);
            style &= ~(0x00040000 /*THICKFRAME*/ | 0x00010000 /*MAXIMIZEBOX*/ | 0x00020000 /*MINIMIZEBOX*/);
            Win.SetWindowLong(Hwnd, -16, style);

            // clicks must not steal focus back from whatever you are doing
            int ex = Win.GetWindowLong(Hwnd, -20);
            Win.SetWindowLong(Hwnd, -20, ex | 0x08000000 /*WS_EX_NOACTIVATE*/);

            Win.ShowWindow(Hwnd, 0 /*HIDE*/);

            Rectangle bounds;
            if (Bounce)
            {
                // cascade like the OS used to stack the popups, then they scatter
                xPos = 400 + (Index % 6) * (int)(26 * Program.Scale);
                yPos = -100 + (Index % 6) * (int)(26 * Program.Scale);
                bounds = new Rectangle((int)xPos, (int)yPos, Program.PopupW, Program.PopupH);
            }
            else
            {
                Size sz = new Size(560, 420);
                Rectangle wa = ScreenBounds;
                bounds = new Rectangle(
                    wa.Left + (wa.Width - sz.Width) / 2,
                    wa.Top + (wa.Height - sz.Height) / 2,
                    sz.Width, sz.Height);

                // the landing page never bounces -- keep the step math here so a
                // stray Step() can't fling it off to the old 400,-100 start point
                xPos = bounds.X;
                yPos = bounds.Y;
            }

            // topmost once -- never again per frame, or they fight over the
            // top of the z-order and flicker
            Win.SetWindowPos(Hwnd, new IntPtr(-1) /*HWND_TOPMOST*/, bounds.X, bounds.Y, bounds.Width, bounds.Height,
                0x0010 /*NOACTIVATE*/ | 0x0020 /*FRAMECHANGED*/);
            Win.ShowWindow(Hwnd, 4 /*SHOWNOACTIVATE*/);
        }

        internal void MarkClosed()
        {
            if (Closed) return;
            Closed = true;

            Program.Windows.Remove(this);
            Program.UpdateTray();

            // onUnLoad="procreate()"
            Program.Procreate();
        }

        // used by the tray / hotkey: die quietly, no respawn
        internal void SilentClose()
        {
            if (Closed) return;
            Closed = true;
            try { if (Proc != null && !Proc.HasExited) Proc.Kill(); }
            catch { }
        }

        // --------------------------------------------------------------
        // you.js, one playBall() per tick, but scaled by real elapsed time
        // --------------------------------------------------------------
        internal void Step(double dt)
        {
            if (Closed || Hwnd == IntPtr.Zero) return;
            if (!Bounce) return;      // the landing page sits still, like the original

            xPos += xOff * Program.PxPerSecondPerUnit * dt;
            yPos += yOff * Program.PxPerSecondPerUnit * dt;

            if (xPos > ScreenBounds.Width - IdiotWindow.MarginX) NewXlt();
            if (xPos < 0) NewXrt();
            if (yPos > ScreenBounds.Height - IdiotWindow.MarginY) NewYup();
            if (yPos < 0) NewYdn();

            // move only: no z-order change, no activation, no resize
            Win.SetWindowPos(Hwnd, IntPtr.Zero, (int)xPos, (int)yPos, 0, 0,
                0x0001 /*NOSIZE*/ | 0x0004 /*NOZORDER*/ | 0x0010 /*NOACTIVATE*/);
        }

        // Math.ceil(0 - 6 * Math.random()) * 5 - 10
        private void NewXlt() { xOff = (int)Math.Ceiling(0 - 6 * Rng.NextDouble()) * 5 - 10; }
        // Math.ceil(7 * Math.random()) * 5 - 10
        private void NewXrt() { xOff = (int)Math.Ceiling(7 * Rng.NextDouble()) * 5 - 10; }
        private void NewYup() { yOff = (int)Math.Ceiling(0 - 6 * Rng.NextDouble()) * 5 - 10; }
        private void NewYdn() { yOff = (int)Math.Ceiling(7 * Rng.NextDouble()) * 5 - 10; }
    }

    // ----------------------------------------------------------------------
    // invisible window that owns the global stop hotkey
    // ----------------------------------------------------------------------
    internal sealed class HotkeyForm : Form
    {
        private const int WmHotkey = 0x0312;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public HotkeyForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-32000, -32000);
            Size = new Size(1, 1);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Ctrl+Alt+Shift+X
            RegisterHotKey(Handle, 1, 0x0002 | 0x0001 | 0x0004, 0x58);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey && m.WParam.ToInt32() == 1)
                Program.KillAll();
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            UnregisterHotKey(Handle, 1);
            base.OnFormClosing(e);
        }
    }

    // ----------------------------------------------------------------------
    internal static class Win
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern bool SetWindowText(IntPtr h, string s);
        [DllImport("user32.dll")]
        internal static extern IntPtr GetMenu(IntPtr h);
        [DllImport("user32.dll")]
        internal static extern bool SetMenu(IntPtr h, IntPtr m);
        [DllImport("user32.dll")]
        internal static extern bool DrawMenuBar(IntPtr h);
        [DllImport("user32.dll")]
        internal static extern int GetWindowLong(IntPtr h, int idx);
        [DllImport("user32.dll")]
        internal static extern int SetWindowLong(IntPtr h, int idx, int val);
        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")]
        internal static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")]
        internal static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
        [DllImport("user32.dll")]
        internal static extern bool GetClientRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")]
        internal static extern bool AdjustWindowRectEx(ref RECT r, int style, bool menu, int exStyle);

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT { public int L, T, R, B; }
    }
}

