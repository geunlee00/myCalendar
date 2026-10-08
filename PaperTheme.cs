using System.Collections.Concurrent;
using System.Windows.Media;

namespace CalendarWidget;

/// <summary>
/// 종이 색 테마. 종이·글자·격자·버튼·그림자 색을 한꺼번에 정합니다.
/// 스프링 고리는 금속이라 테마와 상관없이 그대로 둡니다.
/// </summary>
internal sealed class PaperTheme
{
    public required string Key { get; init; }

    // 종이와 아래에 겹쳐 쌓인 두 장
    public required Color Paper { get; init; }
    public required Color PaperEdge { get; init; }
    public required Color Page1 { get; init; }
    public required Color Page1Edge { get; init; }
    public required Color Page2 { get; init; }
    public required Color Page2Edge { get; init; }

    // 글자: 본문, 보조, 강조(일요일·공휴일·오늘), 토요일, 아이콘 버튼
    public required Color Ink { get; init; }
    public required Color SubInk { get; init; }
    public required Color Accent { get; init; }
    public required Color Saturday { get; init; }
    public required Color FlatText { get; init; }
    public required Color Hint { get; init; }

    // 마우스를 올리거나 누른 칸, 고른 칸, 끌어 옮길 자리
    public required Color Hover { get; init; }
    public required Color Pressed { get; init; }
    public required Color Selected { get; init; }
    public required Color SelectedEdge { get; init; }
    public required Color MovePreview { get; init; }
    public required Color ListHover { get; init; }

    // 선: 밑줄·메모장 줄·격자
    public required Color Line { get; init; }
    public required Color MemoLine { get; init; }
    public required Color GridLine { get; init; }

    // 일정 칩: 기본 색, 끝낸 일, 목록의 기간 꼬리표
    public required Color DefaultChip { get; init; }
    public required Color DefaultChipText { get; init; }
    public required Color DoneChip { get; init; }
    public required Color DoneText { get; init; }
    public required Color ScheduleTag { get; init; }
    public required Color ScheduleTagText { get; init; }

    // 남색 알약 버튼(추가·저장), 그림자, 투명할 때 글자 둘레 빛
    public required Color ButtonBack { get; init; }
    public required Color ButtonText { get; init; }
    public required Color Shadow { get; init; }
    public required Color Glow { get; init; }

    public static readonly PaperTheme Ivory = new()
    {
        Key = "ivory",
        Paper = H(0xFFFCF5), PaperEdge = H(0xE6DECD), Page1 = H(0xF5EFE3), Page1Edge = H(0xE2D9C7), Page2 = H(0xEDE6D6), Page2Edge = H(0xDCD2BE),
        Ink = H(0x2E3440), SubInk = H(0x8A8172), Accent = H(0xC8483D), Saturday = H(0x3F6FB5), FlatText = H(0x5C5548), Hint = H(0xB0A796),
        Hover = H(0xF3ECDF), Pressed = H(0xEBE1CC), Selected = H(0xF1E8D6), SelectedEdge = H(0xC9B998), MovePreview = H(0xE1EAF5), ListHover = A(0x40, 0xE8DCC4),
        Line = H(0xC9B998), MemoLine = H(0xC9B998), GridLine = H(0xE6DDCB),
        DefaultChip = H(0xEFE6D2), DefaultChipText = H(0x4A4234), DoneChip = H(0xF4F0E8), DoneText = H(0xA8A090), ScheduleTag = H(0xE6EAF0), ScheduleTagText = H(0x2E4A6B),
        ButtonBack = H(0x2E3440), ButtonText = Colors.White, Shadow = H(0x5A4E3A), Glow = Colors.White
    };

    public static readonly PaperTheme Kraft = new()
    {
        Key = "kraft",
        Paper = H(0xD9C3A0), PaperEdge = H(0xB89C74), Page1 = H(0xCFB792), Page1Edge = H(0xB39570), Page2 = H(0xC4AB84), Page2Edge = H(0xA88A65),
        Ink = H(0x3A2A1C), SubInk = H(0x6B5640), Accent = H(0xA83A2C), Saturday = H(0x2E5A86), FlatText = H(0x4A3826), Hint = H(0x8C7658),
        Hover = H(0xE2CFAF), Pressed = H(0xCDB48D), Selected = H(0xE6D5B8), SelectedEdge = H(0x8E7553), MovePreview = H(0xC9D3D0), ListHover = A(0x50, 0xE6D5B8),
        Line = H(0x8E7553), MemoLine = H(0x9C8462), GridLine = H(0xBFA37C),
        DefaultChip = H(0xEADBC0), DefaultChipText = H(0x3A2A1C), DoneChip = H(0xCDB894), DoneText = H(0x8C7658), ScheduleTag = H(0xE6DCC8), ScheduleTagText = H(0x3A2A1C),
        ButtonBack = H(0x3A2A1C), ButtonText = H(0xF3E6CF), Shadow = H(0x3E2E1E), Glow = H(0xF3E6CF)
    };

    public static readonly PaperTheme Dark = new()
    {
        Key = "dark",
        Paper = H(0x2B2D33), PaperEdge = H(0x40434B), Page1 = H(0x33363D), Page1Edge = H(0x45484F), Page2 = H(0x2E3036), Page2Edge = H(0x3D4048),
        Ink = H(0xE8E6E1), SubInk = H(0xA3A09A), Accent = H(0xF07A6A), Saturday = H(0x7FA7E8), FlatText = H(0xC9C6C0), Hint = H(0x7E7B76),
        Hover = H(0x3A3D45), Pressed = H(0x444852), Selected = H(0x3D4049), SelectedEdge = H(0x7A7D86), MovePreview = H(0x34405A), ListHover = A(0x40, 0x5A5D66),
        Line = H(0x6A6D76), MemoLine = H(0x4E5159), GridLine = H(0x3E4149),
        DefaultChip = H(0x4A4538), DefaultChipText = H(0xEDE3CF), DoneChip = H(0x35373D), DoneText = H(0x7E7B76), ScheduleTag = H(0x3A4558), ScheduleTagText = H(0xC9D8F0),
        ButtonBack = H(0xE8E6E1), ButtonText = H(0x2B2D33), Shadow = Colors.Black, Glow = Colors.Black
    };

    public static readonly PaperTheme Sky = new()
    {
        Key = "sky",
        Paper = H(0xF4F8FC), PaperEdge = H(0xD5E0EC), Page1 = H(0xE9F0F7), Page1Edge = H(0xCCD9E7), Page2 = H(0xDFE8F2), Page2Edge = H(0xC2D1E2),
        Ink = H(0x26364A), SubInk = H(0x6F8096), Accent = H(0xD2555A), Saturday = H(0x3A6FB8), FlatText = H(0x4A5B70), Hint = H(0x9AA9BC),
        Hover = H(0xE3ECF6), Pressed = H(0xD3E0EE), Selected = H(0xE0EAF5), SelectedEdge = H(0x9DB2CB), MovePreview = H(0xD5E6F7), ListHover = A(0x40, 0xC9D8EA),
        Line = H(0x9DB2CB), MemoLine = H(0xA9BBD1), GridLine = H(0xD8E2EE),
        DefaultChip = H(0xE3EAF3), DefaultChipText = H(0x3A4A60), DoneChip = H(0xEAF0F6), DoneText = H(0x9AA9BC), ScheduleTag = H(0xE0E8F2), ScheduleTagText = H(0x2E4A6B),
        ButtonBack = H(0x26364A), ButtonText = Colors.White, Shadow = H(0x3A4A60), Glow = Colors.White
    };

    public static readonly PaperTheme Blossom = new()
    {
        Key = "blossom",
        Paper = H(0xFFF6F6), PaperEdge = H(0xF0D9DA), Page1 = H(0xFBEAEB), Page1Edge = H(0xE9CDCF), Page2 = H(0xF5E0E2), Page2Edge = H(0xE0C2C5),
        Ink = H(0x3D2C30), SubInk = H(0x957B80), Accent = H(0xD0505F), Saturday = H(0x5A72B5), FlatText = H(0x6A5257), Hint = H(0xB39CA0),
        Hover = H(0xFBE6E8), Pressed = H(0xF3D6D9), Selected = H(0xFCE3E6), SelectedEdge = H(0xD8AEB3), MovePreview = H(0xE8E4F5), ListHover = A(0x40, 0xF0CFD3),
        Line = H(0xD8AEB3), MemoLine = H(0xE0BCC0), GridLine = H(0xF1DADC),
        DefaultChip = H(0xF6DDE1), DefaultChipText = H(0x5A3A42), DoneChip = H(0xF8ECEE), DoneText = H(0xB39CA0), ScheduleTag = H(0xEFE3EC), ScheduleTagText = H(0x5A3A52),
        ButtonBack = H(0x3D2C30), ButtonText = Colors.White, Shadow = H(0x6A4A50), Glow = Colors.White
    };

    public static readonly IReadOnlyList<PaperTheme> All = [Ivory, Kraft, Dark, Sky, Blossom];

    public static PaperTheme FromKey(string? key) => All.FirstOrDefault(theme => theme.Key == key) ?? Ivory;

    private static readonly ConcurrentDictionary<Color, Brush> BrushCache = new();

    /// <summary>색에 맞는 고정(Freeze)된 브러시. 같은 색이면 같은 브러시를 다시 씁니다.</summary>
    public static Brush Solid(Color color) => BrushCache.GetOrAdd(color, c =>
    {
        var brush = new SolidColorBrush(c);
        brush.Freeze();
        return brush;
    });

    private static Color H(int rgb) => Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    private static Color A(byte alpha, int rgb) => Color.FromArgb(alpha, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
}
