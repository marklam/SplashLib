using System.Drawing;
using System.Runtime.InteropServices;

namespace SplashLib
{
    public class SplashWindow : MarshalByRefObject
    {
        private const string ThreadName = "SplashThread";
        private const string WindowClassName = "SplashWindow";

        private static SplashWindow? current;
        private Image _image;
        private int _width;
        private int _height;
        private Color _transparencyKey;
        private bool _showShadow;
        private SplashScreenCustomizerEventHandler _customizer;
        private IntPtr _hwnd;
        private int _minimumDuration;
        private bool _minimumDurationComplete;
        private bool _waitingForTimer;
        private int _timer;

        private SplashWindow()
        {
        }

        public static SplashWindow Current
        {
            get
            {
                if (current == null)
                {
                    current = new SplashWindow();
                }
                return current;
            }
        }

        public Image Image
        {
            get
            {
                return _image;
            }

            set
            {
                _image = value;
                _width = _image.Width;
                _height = _image.Height;
            }
        }

        public int MinimumDuration
        {
            get
            {
                return _minimumDuration;
            }

            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException();
                }

                if (_hwnd != IntPtr.Zero)
                {
                    throw new InvalidOperationException();
                }
                _minimumDuration = value;
            }
        }

        public bool ShowShadow
        {
            get
            {
                return _showShadow;
            }

            set
            {
                if (_hwnd != IntPtr.Zero)
                {
                    throw new InvalidOperationException();
                }
                _showShadow = value;
            }
        }

        public Color TransparencyKey
        {
            get
            {
                return _transparencyKey;
            }

            set
            {
                if (_hwnd != IntPtr.Zero)
                {
                    throw new InvalidOperationException();
                }
                _transparencyKey = value;
            }
        }

        private bool CreateNativeWindow()
        {
            bool result = false;

            int style = WS_VISIBLE | WS_POPUP;
            int exStyle = WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
            int left;
            int top;

            if ((_transparencyKey.IsEmpty == false) && IsLayeringSupported())
            {
                exStyle |= WS_EX_LAYERED;
            }

            GetCursorPos(out var cursor);
            var monitor = MonitorFromPoint(cursor, MonitorDefault.MONITOR_DEFAULTTOPRIMARY);

            var info = new MONITORINFOEX();
            GetMonitorInfo(new HandleRef(null, monitor), info);

            var screenRect = new Rectangle(info.rcMonitor.left, info.rcMonitor.top,
                info.rcMonitor.right - info.rcMonitor.left, info.rcMonitor.bottom - info.rcMonitor.top);

            left = Math.Max(screenRect.X, screenRect.X + (screenRect.Width - _width) / 2);
            top = Math.Max(screenRect.Y, screenRect.Y + (screenRect.Height - _height) / 2);

            _hwnd = CreateWindowEx(exStyle, WindowClassName, "", style, left, top, _width, _height, IntPtr.Zero, IntPtr.Zero, GetModuleHandle(null), null);
            if (_hwnd != IntPtr.Zero)
            {
                ShowWindow(_hwnd, SW_SHOWNORMAL);
                UpdateWindow(_hwnd);
                result = true;
            }

            return result;
        }

        public void Hide()
        {
            if (_minimumDuration > 0)
            {
                _waitingForTimer = true;
                if (_minimumDurationComplete == false)
                {
                    return;
                }
            }
            if (_hwnd != IntPtr.Zero)
            {
                PostMessage(_hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
        }

        private static WNDPROC WindowProcedure;

        private bool RegisterWindowClass()
        {
            bool result = false;

            WNDCLASS wc = new WNDCLASS();
            wc.style = 0;
            wc.lpfnWndProc = WindowProcedure = this.WndProc;
            wc.hInstance = GetModuleHandle(null);
            wc.hbrBackground = (IntPtr)(COLOR_WINDOW + 1);
            wc.lpszClassName = WindowClassName;
            wc.cbClsExtra = 0;
            wc.cbWndExtra = 0;
            wc.hIcon = IntPtr.Zero;
            wc.hCursor = IntPtr.Zero;
            wc.lpszMenuName = null;

            if (_showShadow && IsDropShadowSupported())
            {
                wc.style |= CS_DROPSHADOW;
            }

            if (RegisterClass(wc) != IntPtr.Zero)
            {
                result = true;
            }

            return result;
        }

        public void SetCustomizer(SplashScreenCustomizerEventHandler customizer)
        {
            _customizer = customizer;
        }

        public void Show()
        {
            if (_hwnd == IntPtr.Zero)
            {
                Thread thread = new Thread(SplashWindow.ThreadFunction);
                thread.Name = ThreadName;
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.IsBackground = true;
            }
        }

        private static void ThreadFunction()
        {
            bool result = current.RegisterWindowClass();
            if (result)
            {
                result = current.CreateNativeWindow();
            }

            if (result)
            {
                MSG msg = new MSG();
                while (GetMessage(ref msg, IntPtr.Zero, 0, 0))
                {
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }

                current._hwnd = IntPtr.Zero;
            }
        }

        protected virtual int WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case WM_CREATE:
                    if ((_transparencyKey.IsEmpty == false) && IsLayeringSupported())
                    {
                        SetLayeredWindowAttributes(hwnd, ColorTranslator.ToWin32(_transparencyKey), 0, LWA_COLORKEY);
                    }

                    if (_minimumDuration > 0)
                    {
                        _timer = SetTimer(hwnd, 1, _minimumDuration, IntPtr.Zero);
                    }
                    break;
                case WM_DESTROY:
                    PostQuitMessage(0);
                    break;
                case WM_ERASEBKGND:
                    return 1;
                case WM_PAINT:
                    {
                        PAINTSTRUCT ps = new PAINTSTRUCT();
                        IntPtr hdc = BeginPaint(hwnd, ref ps);

                        if (hdc != IntPtr.Zero)
                        {
                            Graphics g = Graphics.FromHdcInternal(hdc);
                            g.DrawImage(_image, 0, 0, _width, _height);
                            if (_customizer != null)
                            {
                                _customizer(new SplashScreenSurface(g, new Rectangle(0, 0, _width - 1, _height - 1)));
                            }
                            g.Dispose();
                        }

                        EndPaint(hwnd, ref ps);
                    }
                    return 0;
                case WM_TIMER:
                    KillTimer(hwnd, _timer);
                    _timer = 0;
                    _minimumDurationComplete = true;

                    if (_waitingForTimer)
                    {
                        PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    }
                    return 0;
            }
            return DefWindowProc(hwnd, msg, wParam, lParam);
        }

        private static bool IsDropShadowSupported()
        {
            return (Environment.OSVersion.Version.CompareTo(new Version(5, 1, 0, 0)) >= 0);
        }

        private static bool IsLayeringSupported()
        {
            return (Environment.OSVersion.Version.CompareTo(new Version(5, 0, 0, 0)) >= 0);
        }

        private const int COLOR_WINDOW = 5;
        private const int CS_DROPSHADOW = 0x00020000;
        private const int WS_POPUP = (unchecked((int)0x80000000));
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_VISIBLE = 0x10000000;
        private const int WM_DESTROY = 0x0002;
        private const int WM_ERASEBKGND = 0x0014;
        private const int WM_PAINT = 0x000F;
        private const int WM_CREATE = 0x0001;
        private const int WM_CLOSE = 0x0010;
        private const int WM_TIMER = 0x0113;
        private const int SW_SHOWNORMAL = 1;
        private const int LWA_COLORKEY = 0x00000001;

        public enum MonitorDefault
        {
            MONITOR_DEFAULTTONEAREST = 0x00000002,
            MONITOR_DEFAULTTONULL = 0x00000000,
            MONITOR_DEFAULTTOPRIMARY = 0x00000001
        }

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        private static extern IntPtr BeginPaint(IntPtr hWnd, [In, Out] ref PAINTSTRUCT lpPaint);

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

        [DllImport("user32.dll", EntryPoint = "CreateWindowEx", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(int dwExStyle, string lpszClassName, string lpszWindowName, int style, int x, int y, int width, int height, IntPtr hWndParent, IntPtr hMenu, IntPtr hInst, [MarshalAs(UnmanagedType.AsAny)] object pvParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int DefWindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int DispatchMessage(ref MSG msg);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GetMessage(ref MSG msg, IntPtr hwnd, int minFilter, int maxFilter);

        [DllImport("kernel32", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string modName);

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        private static extern bool KillTimer(IntPtr hwnd, int idEvent);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool PeekMessage([In, Out] ref MSG msg, IntPtr hwnd, int msgMin, int msgMax, int remove);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        private static extern void PostQuitMessage(int nExitCode);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr RegisterClass(WNDCLASS wc);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SetLayeredWindowAttributes(IntPtr hwnd, int color, byte alpha, int flags);

        [DllImport("user32.dll")]
        private static extern int SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        private static extern int SetTimer(IntPtr hWnd, int nIDEvent, int uElapse, IntPtr lpTimerFunc);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        private static extern bool TranslateMessage(ref MSG msg);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr MonitorFromPoint(POINT pt, MonitorDefault flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(HandleRef hmonitor, [In][Out] MONITORINFOEX info);

        [ComVisible(true), StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public int message;
            public IntPtr wParam;
            public IntPtr lParam;
            public int time;
            public int pt_x;
            public int pt_y;
        }

        [ComVisible(false), StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class WNDCLASS
        {
            public int style;
            public WNDPROC lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string lpszMenuName;
            public string lpszClassName;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;

            public static implicit operator Point(POINT point)
            {
                return new Point(point.X, point.Y);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;

            internal RECT(int left, int top, int right, int bottom)
            {
                this.left = left;
                this.top = top;
                this.right = right;
                this.bottom = bottom;
            }

            internal static RECT FromXYWH(int x, int y, int width, int height)
            {
                return new RECT(x, y, x + width, y + height);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PAINTSTRUCT
        {
            public IntPtr hdc;
            public bool fErase;
            public int rcPaint_left;
            public int rcPaint_top;
            public int rcPaint_right;
            public int rcPaint_bottom;
            public bool fRestore;
            public bool fIncUpdate;
            public int reserved1;
            public int reserved2;
            public int reserved3;
            public int reserved4;
            public int reserved5;
            public int reserved6;
            public int reserved7;
            public int reserved8;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 4)]
        private class MONITORINFOEX
        {
            internal int cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));

            internal RECT rcMonitor = new RECT();
            internal RECT rcWork = new RECT();
            internal int dwFlags = 0;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            internal char[] szDevice = new char[32];
        }

        private delegate int WNDPROC(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}