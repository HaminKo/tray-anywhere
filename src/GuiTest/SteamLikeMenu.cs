using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;

namespace GuiTest;

// A window like Steam's tray menu as the app knows it: class SDL_app, titled "Menu",
// always on top, shown without taking the focus. The test can't make Steam open its
// menu, so its own tray icon opens this one where Steam would on a scaled monitor (far
// from the click), and the test checks that the app moves it to the click. Only the GUI
// test's copy of the app moves it (--gui-test), so the user's copy leaves it alone.
internal sealed class SteamLikeMenu : IDisposable
{
    private const string ClassName = "SDL_app";
    private const string Title = "Menu";

    // The class's window procedure does what Windows does by default. In a static field,
    // so the garbage collector keeps it while Windows may call it.
    private static readonly WndProc DefaultProc = DefWindowProc;

    // Registers the window class once, before the first window: class names count per
    // program, so this one doesn't clash with Steam's.
    static SteamLikeMenu()
    {
        var windowClass = new WndClass
        {
            WndProcPointer = Marshal.GetFunctionPointerForDelegate(DefaultProc),
            Instance = GetModuleHandle(null),
            ClassName = ClassName,
        };
        if (RegisterClass(ref windowClass) == 0)
        {
            throw new Win32Exception();
        }
    }

    // Opens the window at this place and size, in physical pixels.
    public SteamLikeMenu(Rectangle place)
    {
        handle = CreateWindowEx(WsExTopmost | WsExToolWindow | WsExNoActivate, ClassName, Title, WsPopup,
            place.X, place.Y, place.Width, place.Height, IntPtr.Zero, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);
        if (handle == IntPtr.Zero)
        {
            throw new Win32Exception();
        }
        ShowWindow(handle, SwShowNoActivate);
    }

    private readonly IntPtr handle;

    public Rectangle Bounds
    {
        get
        {
            Win32.GetWindowRect(handle, out var r);
            return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        }
    }

    public void Dispose() => DestroyWindow(handle);

    private const uint WsPopup = 0x80000000;
    private const uint WsExTopmost = 0x8;
    private const uint WsExToolWindow = 0x80;
    private const uint WsExNoActivate = 0x08000000;
    private const int SwShowNoActivate = 4;

    private delegate IntPtr WndProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    // WNDCLASSW: the fields not set stay zero (no icon, cursor, background or menu).
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClass
    {
        public uint Style;
        public IntPtr WndProcPointer;
        public int ClassExtra;
        public int WindowExtra;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr Background;
        public string? MenuName;
        public string ClassName;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClass(ref WndClass windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(uint exStyle, string className, string title, uint style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
    private static extern IntPtr DefWindowProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr window);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
