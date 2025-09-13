using System;
using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using SharpDX.Mathematics.Interop;
using SharpDX.DXGI;

public class FOverlay : IDisposable
{
    private IntPtr win;
    private WindowRenderTarget target;
    private SharpDX.Direct2D1.Factory d2dFactory;
    private SharpDX.DirectWrite.Factory dwFactory;
    private SolidColorBrush brush;
    private TextFormat textFormat;


    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr FindWindowW(string lpClassName, string lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("dwmapi.dll")]
    static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

    [DllImport("user32.dll")]
    static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    static extern int GetSystemMetrics(int nIndex);

    const int SM_CXSCREEN = 0; // screen width
    const int SM_CYSCREEN = 1; // screen height

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TRANSPARENT = 0x20;

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    private const uint SW_SHOW = 5;
    private const uint LWA_COLORKEY = 0x01;
    private const uint LWA_ALPHA = 0x02;

    [StructLayout(LayoutKind.Sequential)]
    struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    public bool InitWindow()
    {
        //win = FindWindowW("CEF-OSC-WIDGET", "NVIDIA GeForce Overlay");
        win = FindOverlayWindow();
        if (win == IntPtr.Zero) return false;

        SetStyle();
        SetTransparency();
        SetTopMost();
        ShowWindow(win, (int)SW_SHOW);

        return true;
    }

    public IntPtr FindOverlayWindow()
    {
        IntPtr result = IntPtr.Zero;

        EnumWindows((hWnd, lParam) =>
        {
            var sb = new System.Text.StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);
            if (sb.ToString().Contains("NVIDIA"))
            {
                result = hWnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);

        return result;
    }

    private void SetStyle()
    {
        int style = GetWindowLong(win, GWL_EXSTYLE);
        SetWindowLong(win, GWL_EXSTYLE, style | WS_EX_TRANSPARENT | WS_EX_LAYERED);
    }

    private void SetTransparency()
    {
        var margin = new MARGINS() { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
        DwmExtendFrameIntoClientArea(win, ref margin);
        SetLayeredWindowAttributes(win, 0, 255, LWA_ALPHA);
    }

    private void SetTopMost()
    {
        SetWindowPos(win, HWND_TOPMOST, 0, 0, 0, 0, 0x0002 | 0x0001);
    }

    public bool InitD2D()
    {
        d2dFactory = new SharpDX.Direct2D1.Factory();
        dwFactory = new SharpDX.DirectWrite.Factory();

        textFormat = new TextFormat(dwFactory, "Consolas", 13);

        int screenWidth = GetSystemMetrics(SM_CXSCREEN);
        int screenHeight = GetSystemMetrics(SM_CYSCREEN);

        var renderProps = new HwndRenderTargetProperties()
        {
            Hwnd = win,
            PixelSize = new Size2(screenWidth, screenHeight),
            PresentOptions = PresentOptions.None
        };

        target = new WindowRenderTarget(d2dFactory,
            new RenderTargetProperties(new PixelFormat(Format.Unknown, SharpDX.Direct2D1.AlphaMode.Premultiplied)),
            renderProps);

        brush = new SolidColorBrush(target, new RawColor4(1f, 1f, 1f, 1f));

        return true;
    }

    public void Shutdown()
    {
        brush?.Dispose();
        textFormat?.Dispose();
        target?.Dispose();
        d2dFactory?.Dispose();
        dwFactory?.Dispose();
    }

    public void BeginScene() => target.BeginDraw();
    public void EndScene() => target.EndDraw();
    public void ClearScene() => target.Clear(null);

    public void DrawText(int x, int y, string text)
    {
        target.DrawText(text, textFormat, new RawRectangleF(x, y, ScreenWidth, ScreenHeight), brush);
    }

    public void DrawTextOutline(int x, int y, string text, TextFormat txtFormat)
    {
        for (int i = -1; i < 1; i++)
            for (int j = -1; j < 1; j++)
                target.DrawText(text, txtFormat, new RawRectangleF(x + i, y + j, ScreenWidth, ScreenHeight), brush);
        SetColor(1f, 1f, 1f);
        target.DrawText(text, txtFormat, new RawRectangleF(x, y, ScreenWidth, ScreenHeight), brush);
    }

    public void DrawLine(float x1, float y1, float x2, float y2, float thickness = 1f)
    {
        target.DrawLine(new RawVector2(x1, y1), new RawVector2(x2, y2), brush, thickness);
    }

    public void DrawRect(float x, float y, float w, float h, float thickness = 1f)
    {
        target.DrawRectangle(new RawRectangleF(x, y, x + w, y + h), brush, thickness);
    }

    public void FillRect(float x, float y, float w, float h)
    {
        target.FillRectangle(new RawRectangleF(x, y, x + w, y + h), brush);
    }

    public void DrawCircle(float cx, float cy, float r, float thickness = 1f)
    {
        target.DrawEllipse(new Ellipse(new RawVector2(cx, cy), r, r), brush, thickness);
    }

    public void FillCircle(float cx, float cy, float r)
    {
        target.FillEllipse(new Ellipse(new RawVector2(cx, cy), r, r), brush);
    }

    public void SetColor(float r, float g, float b, float a = 1f)
    {
        brush.Color = new RawColor4(r, g, b, a);
    }

    public void ClearScreen()
    {
        BeginScene();
        ClearScene();
        EndScene();
    }

    public float ScreenWidth => target.Size.Width;
    public float ScreenHeight => target.Size.Height;
    public RawVector2 ScreenCenter => new RawVector2(target.Size.Width * 0.5f, target.Size.Height * 0.5f);

    public void Dispose() => Shutdown();
}
