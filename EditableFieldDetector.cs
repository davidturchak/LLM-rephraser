using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LlmRephraser;

/// <summary>
/// Detects whether the currently focused control in another application
/// is an editable text field (textbox, input, rich edit) vs non-editable.
/// </summary>
public static class EditableFieldDetector
{
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    {
        public int cbSize;
        public uint flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public RECT rcCaret;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    private static readonly string[] EditableClassNames =
    [
        "Edit",
        "RichEdit20W",
        "RichEdit50W",
        "RICHEDIT60W",
        "TextBox",
        "_WwG",                          // Microsoft Word document area
        "Scintilla",                      // Notepad++, SciTE
        "ConsoleWindowClass",
    ];

    private static readonly string[] BrowserClassNames =
    [
        "Chrome_RenderWidgetHostHWND",    // Chrome, Edge, Electron apps
        "MozillaWindowClass",             // Firefox
    ];

    /// <summary>
    /// Checks if the focused control in the foreground window is an editable text field.
    /// Uses a fast Win32 heuristic first, then falls back to UI Automation.
    /// </summary>
    public static async Task<bool> IsEditableAsync(IntPtr foregroundWindow)
    {
        // Stage 1: fast Win32 check
        var threadId = GetWindowThreadProcessId(foregroundWindow, out _);
        var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };

        if (GetGUIThreadInfo(threadId, ref info))
        {
            // If there's a caret, likely an editable field
            if (info.hwndCaret != IntPtr.Zero)
                return true;

            // Check focused control class name
            var hwndFocus = info.hwndFocus;
            if (hwndFocus != IntPtr.Zero)
            {
                var className = GetWindowClassName(hwndFocus);

                foreach (var name in EditableClassNames)
                {
                    if (className.Equals(name, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                // Browsers: assume editable (we can't distinguish input vs page body
                // without UIA, but users typically select from editable fields in browsers)
                foreach (var name in BrowserClassNames)
                {
                    if (className.Equals(name, StringComparison.OrdinalIgnoreCase))
                        return await CheckUiaEditableAsync(hwndFocus);
                }
            }
        }

        // Stage 2: UI Automation fallback for WPF, UWP, etc.
        return await CheckUiaEditableAsync(foregroundWindow);
    }

    private static string GetWindowClassName(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hwnd, sb, 256);
        return sb.ToString();
    }

    private static Task<bool> CheckUiaEditableAsync(IntPtr hwnd)
    {
        return Task.Run(() =>
        {
            try
            {
                var element = System.Windows.Automation.AutomationElement.FocusedElement;
                if (element == null) return false;

                // Check if it supports ValuePattern (text inputs)
                if (element.TryGetCurrentPattern(
                        System.Windows.Automation.ValuePattern.Pattern, out var pattern))
                {
                    var vp = (System.Windows.Automation.ValuePattern)pattern;
                    return !vp.Current.IsReadOnly;
                }

                // Check ControlType
                var controlType = element.Current.ControlType;
                if (controlType == System.Windows.Automation.ControlType.Edit ||
                    controlType == System.Windows.Automation.ControlType.Document)
                    return true;

                return false;
            }
            catch
            {
                return false;
            }
        });
    }
}
