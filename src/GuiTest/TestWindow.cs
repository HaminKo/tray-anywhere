using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GuiTest;

// A window of the test's own, for the app to react to as it would to another program's
// window. Made with Windows' own calls, not WinForms, so it gets exactly the class,
// title, styles and place given: WinForms could size a window again on a monitor with
// other scaling. Shown without taking the focus, so the user can keep working.
internal sealed class TestWindow : IDisposable
{
    // A window like Steam's tray menu as the app knows it: class SDL_app, titled "Menu",
    // always on top. The test can't make Steam open its menu, so its own tray icon opens
    // this one where Steam would on a scaled monitor (far from the click), and the test
    // checks that the app moves it to the click. Only the GUI test's copy of the app moves
    // it (--gui-test), so the user's copy leaves it alone.
    public static TestWindow SteamLikeMenu(Rectangle place) =>
        new("SDL_app", "Menu", WsExTopmost | WsExToolWindow | WsExNoActivate, place, alpha: null);

    // A window over the whole monitor, like a full-screen video or game, without the
    // focus. Always on top, as many games are: a normal window that never had the focus
    // stayed behind the user's windows there (measured: once shown near the back, once
    // found in front and once not; docs/research/full-screen.md). Almost see-through
    // (1 %), so it hides nothing of the user's screen; the app doesn't look at that. A
    // click on that monitor while it is open lands on it. The user's copy of the app hides
    // its button there too meanwhile.
    public static TestWindow FullScreen(Screen monitor) =>
        new("tray-anywhere-gui-test", "tray-anywhere GUI test: full screen", WsExTopmost | WsExToolWindow | WsExNoActivate | WsExLayered, monitor.Bounds, alpha: 3);

    // The classes' window procedure does what Windows does by default. In a static field,
    // so the garbage collector keeps it while Windows may call it.
    private static readonly WndProc DefaultProc = DefWindowProc;

    private readonly IntPtr handle;

    // Opens the window at this place and size, in physical pixels. alpha: for a layered
    // window, how opaque it is, 0 to 255.
    private TestWindow(string className, string title, uint exStyle, Rectangle place, byte? alpha)
    {
        Register(className);
        handle = CreateWindowEx(exStyle, className, title, WsPopup,
            place.X, place.Y, place.Width, place.Height, IntPtr.Zero, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);
        if (handle == IntPtr.Zero)
        {
            throw new Win32Exception();
        }
        // Before it shows: Windows draws a layered window only once it has this.
        if (alpha is byte opacity)
        {
            SetLayeredWindowAttributes(handle, 0, opacity, LwaAlpha);
        }
        ShowWindow(handle, SwShowNoActivate);
    }

    // Registers the window class before its first window: class names count per program,
    // so this SDL_app doesn't clash with Steam's. Registered once; the second window of a
    // class finds it there already.
    private static void Register(string className)
    {
        var windowClass = new WndClass
        {
            WndProcPointer = Marshal.GetFunctionPointerForDelegate(DefaultProc),
            Instance = GetModuleHandle(null),
            ClassName = className,
        };
        if (RegisterClass(ref windowClass) == 0 && Marshal.GetLastWin32Error() != ErrorClassAlreadyExists)
        {
            throw new Win32Exception();
        }
    }

    public Rectangle Bounds => Win32.Bounds(handle);

    public void Dispose() => DestroyWindow(handle);

    private const uint WsPopup = 0x80000000;
    private const uint WsExTopmost = 0x8;
    private const uint WsExToolWindow = 0x80;
    private const uint WsExLayered = 0x80000;
    private const uint WsExNoActivate = 0x08000000;
    private const int SwShowNoActivate = 4;
    private const uint LwaAlpha = 0x2;
    private const int ErrorClassAlreadyExists = 1410;

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
    private static extern bool SetLayeredWindowAttributes(IntPtr window, uint colorKey, byte alpha, uint flags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr window);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
