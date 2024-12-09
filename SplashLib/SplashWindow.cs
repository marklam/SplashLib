using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SplashLib
{
    // ReSharper disable IdentifierTypo
    // ReSharper disable InconsistentNaming

    [SupportedOSPlatform("windows")]
    public class SplashWindow : MarshalByRefObject
    {
        private const string ThreadName = "SplashThread";
        private const string WindowClassName = "SplashWindow";

        private static SplashWindow? _current;
        private Image? _image;
        private int _width;
        private int _height;
        private Color _transparencyKey;
        private bool _showShadow;
        private SplashScreenCustomizerEventHandler? _customizer;
        private IntPtr _hwnd;
        private int _minimumDuration;
        private bool _minimumDurationComplete;
        private bool _waitingForTimer;
        private int _timer;

        private SplashWindow()
        {
        }

        public static SplashWindow Current => _current ??= new SplashWindow();

        public Image? Image
        {
            get => _image;

            set
            {
                _image = value;
                if (_image != null)
                {
                    _width = _image.Width;
                    _height = _image.Height;
                }
            }
        }

        public int MinimumDuration
        {
            get => _minimumDuration;

            set
            {
                ArgumentOutOfRangeException.ThrowIfNegative(value);

                if (_hwnd != IntPtr.Zero)
                {
                    throw new InvalidOperationException();
                }

                _minimumDuration = value;
            }
        }

        public bool ShowShadow
        {
            get => _showShadow;

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
            get => _transparencyKey;

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
            var result = false;

            const int style = WS_VISIBLE | WS_POPUP;
            var exStyle = WS_EX_TOOLWINDOW | WS_EX_TOPMOST;

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

            var left = Math.Max(screenRect.X, screenRect.X + (screenRect.Width - _width) / 2);
            var top = Math.Max(screenRect.Y, screenRect.Y + (screenRect.Height - _height) / 2);

            _hwnd = CreateWindowEx(exStyle, WindowClassName, "", style, left, top, _width, _height, IntPtr.Zero, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);
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

        private bool RegisterWindowClass()
        {
            var result = false;

            var wc = new WNDCLASS
            {
                style = 0,
                lpfnWndProc = this.WndProc,
                hInstance = GetModuleHandle(null),
                hbrBackground = (COLOR_WINDOW + 1),
                lpszClassName = WindowClassName,
                cbClsExtra = 0,
                cbWndExtra = 0,
                hIcon = IntPtr.Zero,
                hCursor = IntPtr.Zero,
                lpszMenuName = null
            };

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
                var thread = new Thread(SplashWindow.ThreadFunction)
                {
                    Name = ThreadName
                };
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.IsBackground = true;
            }
        }

        private static void ThreadFunction()
        {
            if (_current != null)
            {
                var result = _current.RegisterWindowClass();
                if (result)
                {
                    result = _current.CreateNativeWindow();
                }

                if (result)
                {
                    var msg = new MSG();
                    while (GetMessage(ref msg, IntPtr.Zero, 0, 0))
                    {
                        TranslateMessage(ref msg);
                        _ = DispatchMessage(ref msg);
                    }

                    _current._hwnd = IntPtr.Zero;
                }
            }
        }

        protected virtual int WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case WM_CREATE:
                    if ((_transparencyKey.IsEmpty == false) && IsLayeringSupported())
                    {
                        _ = SetLayeredWindowAttributes(hwnd, ColorTranslator.ToWin32(_transparencyKey), 0, LWA_COLORKEY);
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
                        var ps = new PAINTSTRUCT();
                        var hdc = BeginPaint(hwnd, ref ps);

                        if (hdc != IntPtr.Zero)
                        {
                            using var g = Graphics.FromHdcInternal(hdc);
                            if (_image != null)
                            {
                                g.DrawImage(_image, 0, 0, _width, _height);
                            }

                            if (_customizer != null)
                            {
                                _customizer(
                                    new SplashScreenSurface(g, new Rectangle(0, 0, _width - 1, _height - 1)));
                            }
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
                        _ = PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
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
            MONITOR_DEFAULTTOPRIMARY = 0x00000001
        }

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        private static extern IntPtr BeginPaint(IntPtr hWnd, [In, Out] ref PAINTSTRUCT lpPaint);

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

        [DllImport("user32.dll", EntryPoint = "CreateWindowEx", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(int dwExStyle, string lpszClassName, string lpszWindowName, int style, int x, int y, int width, int height, IntPtr hWndParent, IntPtr hMenu, IntPtr hInst, [MarshalAs(UnmanagedType.IUnknown)] object pvParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int DefWindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int DispatchMessage(ref MSG msg);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GetMessage(ref MSG msg, IntPtr hwnd, int minFilter, int maxFilter);

        [DllImport("kernel32", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string? modName);

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        private static extern bool KillTimer(IntPtr hwnd, int idEvent);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        private static extern void PostQuitMessage(int nExitCode);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr RegisterClass(WNDCLASS wc);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SetLayeredWindowAttributes(IntPtr hwnd, int color, byte alpha, int flags);

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
            public WNDPROC? lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string? lpszMenuName;
            public string? lpszClassName;
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

#pragma warning disable CS0414 // Field is assigned but its value is never used
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 4)]
        private class MONITORINFOEX
        {
            // ReSharper disable FieldCanBeMadeReadOnly.Local
            // ReSharper disable UnusedMember.Local
            public int cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));

            public RECT rcMonitor = new();
            public RECT rcWork = new();
            public int dwFlags = 0;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public char[] szDevice = new char[32];
        }
#pragma warning restore CS0414 // Field is assigned but its value is never used

        private delegate int WNDPROC(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}