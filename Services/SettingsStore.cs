namespace CalendarWidget.Services;

/// <summary>
/// 위젯 위치와 크기. X, Y는 화면 기준 실제 픽셀이고 Width, Height는 WPF 단위(DIP)입니다.
/// </summary>
public sealed record WidgetBounds(int X, int Y, double Width, double Height);

public sealed class AppSettings
{
    public WidgetBounds? Bounds { get; set; }

    /// <summary>화면 언어(ko, en). 비어 있으면 Windows 표시 언어를 따릅니다.</summary>
    public string? Language { get; set; }

    /// <summary>대한민국 공휴일 표시. 비어 있으면 처음 언어를 따릅니다(한국어면 켜고, 아니면 끕니다).</summary>
    public bool? ShowKoreanHolidays { get; set; }

    /// <summary>카드 배경 투명도. 0이면 불투명, 1이면 투명합니다.</summary>
    public double BackgroundTransparency { get; set; }

    /// <summary>처음 실행할 때 자동 실행을 한 번 켜 두었는지. 사용자가 끄면 다시 켜지 않습니다.</summary>
    public bool AutoStartInitialized { get; set; }
}

public sealed class SettingsStore
{
    private const string FileName = "settings.json";

    public AppSettings Load() => AppDataFile.Read<AppSettings>(FileName) ?? new AppSettings();

    public void Save(AppSettings settings) => AppDataFile.Write(FileName, settings);
}
