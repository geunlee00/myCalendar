using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CalendarWidget.Services;

/// <summary>크기를 바꿀 때 움직이는 가장자리. 모서리는 두 가장자리를 함께 씁니다.</summary>
[Flags]
public enum ResizeEdges
{
    None = 0,
    Left = 1,
    Top = 2,
    Right = 4,
    Bottom = 8
}

/// <summary>
/// 바탕화면 아이콘 층(SHELLDLL_DefView) 안에 자식 창을 만들고 위젯 내용을 그 안에 그립니다.
/// 바탕화면의 일부가 되므로 다른 앱 창 위로 올라오지 않고, Win + D(바탕화면 보기)를 눌러도 숨겨지지 않습니다.
/// Windows 11 24H2부터 Progman에 직접 넣은 자식 창은 화면에 합성되지 않으므로 아이콘 층 안에 넣습니다.
/// 아이콘 층을 찾지 못하면 일반 창으로 띄우되 항상 맨 뒤에 고정합니다.
/// </summary>
internal static class DesktopWidgetHost
{
    private static readonly IntPtr HwndTop = IntPtr.Zero;
    private static readonly IntPtr HwndBottom = new(1);
    private const int WmWindowPosChanging = 0x0046;
    private const int WmNcLButtonDown = 0x00A1;
    private const int HtCaption = 2;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const int WsChild = 0x40000000;
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsClipSiblings = 0x04000000;
    private const int WsExToolWindow = 0x00000080;
    private const uint MonitorDefaultToNull = 0;
    private const uint MonitorDefaultToPrimary = 1;
    private const uint MonitorDefaultToNearest = 2;

    private static IntPtr _zOrderTarget = HwndBottom;
    private static IntPtr _parent;
    private static HwndSource? _source;
    private static Size _defaultSize;
    private static Size _minSize;

    public static bool IsDesktopReady => FindDesktopIconLayer() != IntPtr.Zero;

    /// <summary>위젯을 옮기거나 크기를 바꾸고 나면 새 위치와 크기를 알려 줍니다.</summary>
    public static event Action<WidgetBounds>? BoundsChanged;

    public static HwndSource Show(UIElement content, Size defaultSize, Size minSize, WidgetBounds? savedBounds)
    {
        _defaultSize = defaultSize;
        _minSize = minSize;
        _parent = FindDesktopIconLayer();
        var embedded = _parent != IntPtr.Zero;
        _source = new HwndSource(new HwndSourceParameters("CalendarWidget")
        {
            UsesPerPixelTransparency = true,
            ParentWindow = _parent,
            WindowStyle = embedded ? WsChild | WsClipSiblings : WsPopup,
            ExtendedWindowStyle = embedded ? 0 : WsExToolWindow
        })
        {
            // 크기를 지정하지 않고 만든 HwndSource는 내용물 크기에 창을 맞추므로 크기를 직접 정하는 모드로 바꾼다.
            SizeToContent = SizeToContent.Manual,
            RootVisual = content
        };
        _source.AddHook(WndProc);

        // 아이콘 층 안에서는 아이콘 목록(SysListView32)보다 위에 있어야 보이고 클릭할 수 있다.
        _zOrderTarget = embedded ? HwndTop : HwndBottom;

        Place(savedBounds, SwpShowWindow);
        return _source;
    }

    /// <summary>
    /// 마우스 버튼을 누른 채 끌면 위젯을 옮깁니다. 제목 표시줄을 끄는 것과 같은 시스템 이동을 시작하고,
    /// 마우스 버튼을 놓아 이동이 끝나면 돌아옵니다.
    /// </summary>
    public static void BeginDrag()
    {
        if (_source is null) return;

        ReleaseCapture();
        SendMessage(_source.Handle, WmNcLButtonDown, HtCaption, IntPtr.Zero);
        RaiseBoundsChanged();
    }

    /// <summary>
    /// 끄는 가장자리를 마우스가 움직인 만큼(WPF 단위) 옮겨 크기를 바꿉니다. 왼쪽·위쪽 가장자리를 끌면 위치도 함께 바뀝니다.
    /// 최소 크기보다 작아지면 움직이는 가장자리 쪽을 멈춥니다.
    /// </summary>
    public static void ResizeBy(ResizeEdges edges, double deltaX, double deltaY)
    {
        if (_source is null || edges == ResizeEdges.None) return;

        var scale = Scale;
        GetWindowRect(_source.Handle, out var rect);
        double left = rect.Left, top = rect.Top, right = rect.Right, bottom = rect.Bottom;
        if (edges.HasFlag(ResizeEdges.Left)) left += deltaX * scale;
        if (edges.HasFlag(ResizeEdges.Right)) right += deltaX * scale;
        if (edges.HasFlag(ResizeEdges.Top)) top += deltaY * scale;
        if (edges.HasFlag(ResizeEdges.Bottom)) bottom += deltaY * scale;

        var minWidth = _minSize.Width * scale;
        var minHeight = _minSize.Height * scale;
        if (right - left < minWidth)
        {
            if (edges.HasFlag(ResizeEdges.Left)) left = right - minWidth;
            else right = left + minWidth;
        }
        if (bottom - top < minHeight)
        {
            if (edges.HasFlag(ResizeEdges.Top)) top = bottom - minHeight;
            else bottom = top + minHeight;
        }

        SetScreenBounds(new Rect32
        {
            Left = (int)Math.Round(left),
            Top = (int)Math.Round(top),
            Right = (int)Math.Round(right),
            Bottom = (int)Math.Round(bottom)
        }, 0);
    }

    public static void EndResize() => RaiseBoundsChanged();

    /// <summary>
    /// 미니 모드와 큰 달력을 오갈 때 창 크기를 바꿉니다. 그 모드에서 쓰던 위치·크기가 있으면 그대로 쓰고,
    /// 처음이면 지금 위젯이 붙어 있는 화면 모서리 쪽에 그 모드의 기본 크기로 붙입니다.
    /// </summary>
    public static void ChangeMode(WidgetBounds? savedBounds, Size defaultSize, Size minSize)
    {
        if (_source is null) return;

        _defaultSize = defaultSize;
        _minSize = minSize;
        if (savedBounds is not null)
        {
            Place(savedBounds, 0);
            RaiseBoundsChanged();
            return;
        }

        var scale = Scale;
        GetWindowRect(_source.Handle, out var rect);
        var workArea = GetWorkAreaNear(rect);
        var width = (int)Math.Round(defaultSize.Width * scale);
        var height = (int)Math.Round(defaultSize.Height * scale);
        var anchorRight = workArea.Right - rect.Right < rect.Left - workArea.Left;
        var anchorBottom = workArea.Bottom - rect.Bottom < rect.Top - workArea.Top;
        var left = anchorRight ? rect.Right - width : rect.Left;
        var top = anchorBottom ? rect.Bottom - height : rect.Top;
        SetScreenBounds(FitIntoWorkArea(new Rect32 { Left = left, Top = top, Right = left + width, Bottom = top + height }), 0);
        RaiseBoundsChanged();
    }

    /// <summary>주 모니터 오른쪽 아래의 처음 위치와 크기로 되돌립니다.</summary>
    public static void ResetBounds()
    {
        Place(null, 0);
        RaiseBoundsChanged();
    }

    private static double Scale => _source?.CompositionTarget.TransformToDevice.M11 ?? 1.0;

    /// <summary>
    /// 저장된 위치가 지금 연결된 모니터 안에 있으면 그곳에, 아니면 주 모니터 작업 영역의 오른쪽 아래에 놓습니다.
    /// 어느 쪽이든 놓일 모니터의 작업 영역보다 크면 줄이고, 밖으로 나간 부분은 안으로 당겨
    /// 작은 화면에서도 위젯 전체(크기 조절 가장자리 포함)가 보이게 합니다. Win32 좌표는 DPI 배율을 곱한 실제 픽셀입니다.
    /// </summary>
    private static void Place(WidgetBounds? bounds, uint extraFlags)
    {
        if (_source is null) return;

        var scale = Scale;
        var target = default(Rect32);
        if (bounds is not null)
        {
            var width = (int)Math.Round(Math.Max(_minSize.Width, bounds.Width) * scale);
            var height = (int)Math.Round(Math.Max(_minSize.Height, bounds.Height) * scale);
            target = new Rect32 { Left = bounds.X, Top = bounds.Y, Right = bounds.X + width, Bottom = bounds.Y + height };
        }
        if (bounds is null || !IsOnAnyMonitor(target))
        {
            var margin = (int)Math.Round(24 * scale);
            var workArea = GetPrimaryWorkArea();
            var width = Math.Min((int)Math.Round(_defaultSize.Width * scale), workArea.Right - workArea.Left - 2 * margin);
            var height = Math.Min((int)Math.Round(_defaultSize.Height * scale), workArea.Bottom - workArea.Top - 2 * margin);
            target = new Rect32
            {
                Left = workArea.Right - width - margin,
                Top = workArea.Bottom - height - margin,
                Right = workArea.Right - margin,
                Bottom = workArea.Bottom - margin
            };
        }

        SetScreenBounds(FitIntoWorkArea(target), extraFlags);
    }

    /// <summary>사각형이 걸친 모니터의 작업 영역보다 크면 줄이고, 밖으로 나간 부분은 안으로 당깁니다.</summary>
    private static Rect32 FitIntoWorkArea(Rect32 rect)
    {
        var workArea = GetWorkAreaNear(rect);
        var width = Math.Min(rect.Right - rect.Left, workArea.Right - workArea.Left);
        var height = Math.Min(rect.Bottom - rect.Top, workArea.Bottom - workArea.Top);
        var left = Math.Clamp(rect.Left, workArea.Left, workArea.Right - width);
        var top = Math.Clamp(rect.Top, workArea.Top, workArea.Bottom - height);
        return new Rect32 { Left = left, Top = top, Right = left + width, Bottom = top + height };
    }

    /// <summary>화면 좌표(실제 픽셀) 사각형으로 위젯을 옮기고 크기를 바꿉니다. 바탕화면 안에 있으면 바탕화면 기준 좌표로 바꿉니다.</summary>
    private static void SetScreenBounds(Rect32 rect, uint extraFlags)
    {
        if (_source is null) return;

        var position = new Point32 { X = rect.Left, Y = rect.Top };
        if (_parent != IntPtr.Zero) ScreenToClient(_parent, ref position);
        SetWindowPos(_source.Handle, _zOrderTarget, position.X, position.Y,
            rect.Right - rect.Left, rect.Bottom - rect.Top, SwpNoActivate | extraFlags);
    }

    private static void RaiseBoundsChanged()
    {
        if (_source is null) return;

        var scale = Scale;
        GetWindowRect(_source.Handle, out var rect);
        BoundsChanged?.Invoke(new WidgetBounds(
            rect.Left,
            rect.Top,
            Math.Round((rect.Right - rect.Left) / scale),
            Math.Round((rect.Bottom - rect.Top) / scale)));
    }

    /// <summary>
    /// 바탕화면 아이콘 층(SHELLDLL_DefView)을 찾습니다.
    /// Windows 버전과 배경화면 설정에 따라 Progman 또는 WorkerW 아래에 있습니다.
    /// </summary>
    private static IntPtr FindDesktopIconLayer()
    {
        var progman = FindWindow("Progman", null);
        var iconLayer = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (iconLayer != IntPtr.Zero) return iconLayer;

        EnumWindows((window, _) =>
        {
            iconLayer = FindWindowEx(window, IntPtr.Zero, "SHELLDLL_DefView", null);
            return iconLayer == IntPtr.Zero;
        }, IntPtr.Zero);
        return iconLayer;
    }

    private static bool IsOnAnyMonitor(Rect32 rect) => MonitorFromRect(ref rect, MonitorDefaultToNull) != IntPtr.Zero;

    private static Rect32 GetPrimaryWorkArea() => GetWorkArea(MonitorFromPoint(new Point32(), MonitorDefaultToPrimary));

    private static Rect32 GetWorkAreaNear(Rect32 rect) => GetWorkArea(MonitorFromRect(ref rect, MonitorDefaultToNearest));

    private static Rect32 GetWorkArea(IntPtr monitor)
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        GetMonitorInfo(monitor, ref info);
        return info.WorkArea;
    }

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmWindowPosChanging)
        {
            var pos = Marshal.PtrToStructure<WindowPos>(lParam);
            pos.InsertAfter = _zOrderTarget;
            pos.Flags &= ~SwpNoZOrder;
            Marshal.StructureToPtr(pos, lParam, false);
        }

        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPos
    {
        public IntPtr Hwnd;
        public IntPtr InsertAfter;
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect32
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point32
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect32 Monitor;
        public Rect32 WorkArea;
        public uint Flags;
    }

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string? windowName);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr windowHandle, out Rect32 rect);

    [DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr windowHandle, ref Point32 point);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(Point32 point, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromRect(ref Rect32 rect, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr windowHandle, int message, IntPtr wParam, IntPtr lParam);
}
