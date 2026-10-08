using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using CalendarWidget.Models;

namespace CalendarWidget;

/// <summary>
/// 일정 색깔별로 달력 칩·막대의 배경과 글자색, 목록의 점 색을 정합니다.
/// 기본 색은 하루 일정은 베이지 칩, 여러 날 일정은 파란 막대로 그립니다.
/// </summary>
internal static class TaskPalette
{
    private sealed record Swatch(Brush Background, Brush Text, Brush Dot);

    private static readonly Dictionary<TaskColor, Swatch> Swatches = new()
    {
        [TaskColor.Default] = Make(0xEFE6D2, 0x4A4234, 0xC9B998),
        [TaskColor.Red] = Make(0xF6DCD8, 0x8E2C22, 0xC8483D),
        [TaskColor.Orange] = Make(0xF8E3CC, 0x8A4B12, 0xE08A2E),
        [TaskColor.Green] = Make(0xDCEBD9, 0x2F5E2A, 0x5A9A4F),
        [TaskColor.Blue] = Make(0xD9E4EF, 0x2E4A6B, 0x3F6FB5),
        [TaskColor.Purple] = Make(0xE6DDF0, 0x54367A, 0x8C64B8)
    };

    public static Brush ChipBackground(TaskColor color) => Swatches[color].Background;
    public static Brush ChipText(TaskColor color) => Swatches[color].Text;

    // 기본 색의 여러 날 일정은 하루 일정과 구분되도록 파란 막대로 그린다.
    public static Brush BarBackground(TaskColor color) => Swatches[color == TaskColor.Default ? TaskColor.Blue : color].Background;
    public static Brush BarText(TaskColor color) => Swatches[color == TaskColor.Default ? TaskColor.Blue : color].Text;

    public static Brush Dot(TaskColor color) => Swatches[color].Dot;

    private static Swatch Make(int background, int text, int dot) => new(Solid(background), Solid(text), Solid(dot));

    private static Brush Solid(int rgb)
    {
        var brush = new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
        brush.Freeze();
        return brush;
    }
}

/// <summary>목록에서 일정 색깔을 점 색으로 바꿉니다.</summary>
public sealed class TaskColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        TaskPalette.Dot(value is TaskColor color ? color : TaskColor.Default);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
