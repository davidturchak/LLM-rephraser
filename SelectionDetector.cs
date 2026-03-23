using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LlmRephraser;

/// <summary>
/// Detects probable text selection gestures (mouse drag) system-wide
/// and fires an event with the mouse-up screen position.
/// </summary>
public sealed class SelectionDetector : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int MinDragDistance = 20; // pixels — ignore short clicks/jitters

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// Fired when a mouse drag (probable text selection) ends.
    /// Parameter is the screen position of the mouse-up.
    /// </summary>
    public event Action<Point>? SelectionDetected;

    private IntPtr _hookId = IntPtr.Zero;
    private readonly LowLevelMouseProc _proc;
    private Point _downPos;
    private bool _isDown;

    public SelectionDetector()
    {
        _proc = HookCallback;
    }

    public void Install()
    {
        if (_hookId != IntPtr.Zero) return;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookId = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
    }

    public void Uninstall()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

            if (wParam == (IntPtr)WM_LBUTTONDOWN)
            {
                _downPos = new Point(info.pt.x, info.pt.y);
                _isDown = true;
            }
            else if (wParam == (IntPtr)WM_LBUTTONUP && _isDown)
            {
                _isDown = false;
                var upPos = new Point(info.pt.x, info.pt.y);
                double dist = Math.Sqrt(
                    Math.Pow(upPos.X - _downPos.X, 2) +
                    Math.Pow(upPos.Y - _downPos.Y, 2));

                if (dist >= MinDragDistance)
                {
                    // Likely a text selection drag — fire after a brief delay
                    // to let the OS finalize the selection
                    var timer = new System.Windows.Forms.Timer { Interval = 250 };
                    timer.Tick += (_, _) =>
                    {
                        timer.Stop();
                        timer.Dispose();
                        SelectionDetected?.Invoke(upPos);
                    };
                    timer.Start();
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Uninstall();
    }
}
