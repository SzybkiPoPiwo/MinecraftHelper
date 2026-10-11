using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace MinecraftHelper.Services;

/// <summary>
/// Tracks only physical LPM/PPM transitions while a combination clicker is active.
/// Injected clicks are ignored, and mouse-move messages return immediately without
/// marshalling or logging so the hook stays out of the hot cursor path.
/// </summary>
internal sealed class PhysicalMouseStateTracker : IDisposable
{
    private const int WhMouseLowLevel = 14;
    private const int WmQuit = 0x0012;
    private const int WmLeftButtonDown = 0x0201;
    private const int WmLeftButtonUp = 0x0202;
    private const int WmRightButtonDown = 0x0204;
    private const int WmRightButtonUp = 0x0205;
    private const int VkLeftButton = 0x01;
    private const int VkRightButton = 0x02;
    private const uint InjectedMouseFlags = 0x00000003;

    private readonly object _sync = new();
    private readonly ManualResetEventSlim _ready = new(false);
    private Thread? _thread;
    private MouseHookProc? _hookProc;
    private IntPtr _hookHandle;
    private volatile uint _threadId;
    private volatile bool _monitoring;
    private volatile bool _leftButtonDown;
    private volatile bool _rightButtonDown;
    private bool _disposed;

    public bool IsMonitoring => _monitoring;

    public int LastError { get; private set; }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseHookData
    {
        public Point Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public IntPtr Window;
        public uint Id;
        public UIntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public Point Point;
        public uint Private;
    }

    private delegate IntPtr MouseHookProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int hookId,
        MouseHookProc callback,
        IntPtr module,
        uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hook,
        int code,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetMessage(out Message message, IntPtr window, uint minimum, uint maximum);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessage(
        out Message message,
        IntPtr window,
        uint minimum,
        uint maximum,
        uint removeMessage);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint threadId, uint message, UIntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    public bool Start()
    {
        lock (_sync)
        {
            if (_disposed)
                return false;
            if (_thread?.IsAlive == true)
                return _monitoring;

            LastError = 0;
            _ready.Reset();
            _hookProc = HookCallback;
            _thread = new Thread(ThreadMain)
            {
                IsBackground = true,
                Name = "Auto Clicker physical mouse tracker"
            };
            _thread.Start();
        }

        return _ready.Wait(TimeSpan.FromSeconds(1)) && _monitoring;
    }

    public void Stop()
    {
        Thread? thread;
        lock (_sync)
        {
            thread = _thread;
            if (thread == null)
                return;
        }

        uint threadId = _threadId;
        if (threadId == 0)
        {
            _ = _ready.Wait(TimeSpan.FromMilliseconds(250));
            threadId = _threadId;
        }
        if (threadId != 0)
            _ = PostThreadMessage(threadId, WmQuit, UIntPtr.Zero, IntPtr.Zero);
        if (thread != Thread.CurrentThread)
            _ = thread.Join(TimeSpan.FromSeconds(1));

        lock (_sync)
        {
            if (ReferenceEquals(_thread, thread) && !thread.IsAlive)
            {
                _thread = null;
                _hookProc = null;
            }
        }
    }

    public bool IsButtonDown(int virtualKey)
    {
        if (!_monitoring)
            return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

        return virtualKey switch
        {
            VkLeftButton => _leftButtonDown,
            VkRightButton => _rightButtonDown,
            _ => (GetAsyncKeyState(virtualKey) & 0x8000) != 0
        };
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
        }
        Stop();
    }

    private void ThreadMain()
    {
        try
        {
            _threadId = GetCurrentThreadId();
            _ = PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
            _leftButtonDown = (GetAsyncKeyState(VkLeftButton) & 0x8000) != 0;
            _rightButtonDown = (GetAsyncKeyState(VkRightButton) & 0x8000) != 0;
            _hookHandle = SetWindowsHookEx(
                WhMouseLowLevel,
                _hookProc!,
                GetModuleHandle(null),
                0);
            if (_hookHandle == IntPtr.Zero)
            {
                LastError = Marshal.GetLastWin32Error();
                return;
            }

            _monitoring = true;
            _ready.Set();
            while (GetMessage(out _, IntPtr.Zero, 0, 0) > 0)
            {
            }
        }
        finally
        {
            _ready.Set();
            _monitoring = false;
            if (_hookHandle != IntPtr.Zero)
            {
                _ = UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }
            _leftButtonDown = false;
            _rightButtonDown = false;
            _threadId = 0;
        }
    }

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            int message = unchecked((int)wParam.ToInt64());
            if (message is WmLeftButtonDown or WmLeftButtonUp or WmRightButtonDown or WmRightButtonUp)
            {
                MouseHookData data = Marshal.PtrToStructure<MouseHookData>(lParam);
                if ((data.Flags & InjectedMouseFlags) == 0)
                {
                    if (message == WmLeftButtonDown)
                        _leftButtonDown = true;
                    else if (message == WmLeftButtonUp)
                        _leftButtonDown = false;
                    else if (message == WmRightButtonDown)
                        _rightButtonDown = true;
                    else if (message == WmRightButtonUp)
                        _rightButtonDown = false;
                }
            }
        }

        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }
}
