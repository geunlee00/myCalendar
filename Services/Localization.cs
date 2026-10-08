using System.Globalization;

namespace CalendarWidget.Services;

public enum AppLanguage
{
    Korean,
    English
}

/// <summary>
/// 화면 문구를 지금 언어로 꺼내 줍니다. 언어를 바꾸면 Changed가 울려 화면이 다시 그려집니다.
/// 처음에는 Windows 표시 언어를 따라갑니다(한국어가 아니면 영어).
/// </summary>
internal static class Loc
{
    private static readonly CultureInfo KoreanCulture = CultureInfo.GetCultureInfo("ko-KR");
    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");

    public static AppLanguage Current { get; private set; } =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko" ? AppLanguage.Korean : AppLanguage.English;

    /// <summary>날짜·요일 이름을 쓸 때의 문화권.</summary>
    public static CultureInfo Culture => Current == AppLanguage.Korean ? KoreanCulture : EnglishCulture;

    public static event Action? Changed;

    public static void Set(AppLanguage language)
    {
        if (Current == language) return;
        Current = language;
        Changed?.Invoke();
    }

    /// <summary>설정 파일에 저장할 짧은 이름(ko, en).</summary>
    public static string ToCode(AppLanguage language) => language == AppLanguage.Korean ? "ko" : "en";

    public static AppLanguage? FromCode(string? code) => code switch
    {
        "ko" => AppLanguage.Korean,
        "en" => AppLanguage.English,
        _ => null
    };

    public static string T(string key) =>
        (Current == AppLanguage.Korean ? Korean : English).TryGetValue(key, out var text) ? text : key;

    public static string T(string key, params object[] args) => string.Format(Culture, T(key), args);

    /// <summary>날짜를 지금 언어의 형식(Fmt.* 문구)으로 씁니다.</summary>
    public static string Date(DateTime date, string formatKey) => date.ToString(T(formatKey), Culture);

    private static readonly Dictionary<string, string> Korean = new()
    {
        ["Fmt.Year"] = "yyyy년 M월",
        ["Fmt.Day"] = "M월 d일 (ddd)",
        ["Fmt.SearchDate"] = "yyyy. M. d (ddd)",
        ["Weekdays"] = "일,월,화,수,목,금,토",

        ["Header.Today"] = "오늘",
        ["Header.TodayTip"] = "오늘로 돌아가기",
        ["Header.PrevTip"] = "이전 달",
        ["Header.NextTip"] = "다음 달",
        ["Header.SearchTip"] = "검색",
        ["Header.MenuTip"] = "설정",
        ["Header.CloseTip"] = "끝내기",
        ["Header.ResizeTip"] = "끌어서 크기 바꾸기",

        ["Menu.AutoStart"] = "Windows 시작 시 자동 실행",
        ["Menu.ResetBounds"] = "위치·크기 처음으로",
        ["Menu.Holidays"] = "대한민국 공휴일 표시",
        ["Menu.PageFlip"] = "종이 넘기기 효과",
        ["Menu.Language"] = "언어 (Language)",
        ["Menu.Transparency"] = "배경 투명도 {0}%",
        ["Menu.Quit"] = "끝내기",

        ["Update.Badge"] = "업데이트",
        ["Update.BadgeTip"] = "새 버전 {0}이 나왔어요. 눌러서 내려받기",
        ["Update.Check"] = "업데이트 확인 (현재 {0})",
        ["Update.Checking"] = "업데이트 확인하는 중…",
        ["Update.Failed"] = "확인하지 못했어요 · 인터넷 연결을 확인해 주세요",
        ["Update.Latest"] = "최신 버전을 쓰고 있어요 ({0})",
        ["Update.Available"] = "새 버전 {0} 받기 (현재 {1})",

        ["Memo.Empty"] = "할 일이 없어요",
        ["Memo.Hint"] = "✎  할 일을 적고 Enter",
        ["Memo.RangeHint"] = "✎  {0}~{1} 일정을 적고 Enter",
        ["Memo.Days"] = "{0}일",
        ["Memo.Add"] = "추가",

        ["Editor.Title"] = "일정 편집",
        ["Editor.Date"] = "날짜",
        ["Editor.DateHint"] = "달력에서 눌러서 바꾸기",
        ["Editor.Repeat"] = "반복",
        ["Editor.Color"] = "색",
        ["Editor.Dday"] = "D-day 표시",
        ["Editor.Delete"] = "삭제",
        ["Editor.Cancel"] = "취소",
        ["Editor.Save"] = "저장",

        ["Repeat.None"] = "안 함",
        ["Repeat.Weekly"] = "매주",
        ["Repeat.Monthly"] = "매달",
        ["Repeat.Yearly"] = "매년",

        ["Label.Weekly"] = "매주 {0}",
        ["Label.Monthly"] = "매달 {0}일",
        ["Label.Yearly"] = "매년 {0}",
        ["Label.Days"] = "{0}일간",

        ["Search.Title"] = "검색",
        ["Search.Hint"] = "일정 이름으로 찾기",
        ["Search.Empty"] = "찾는 일정이 없어요",

        ["Holiday.NewYear"] = "신정",
        ["Holiday.SeollalHoliday"] = "설날 연휴",
        ["Holiday.Seollal"] = "설날",
        ["Holiday.March1"] = "삼일절",
        ["Holiday.Children"] = "어린이날",
        ["Holiday.Buddha"] = "부처님오신날",
        ["Holiday.Memorial"] = "현충일",
        ["Holiday.Liberation"] = "광복절",
        ["Holiday.ChuseokHoliday"] = "추석 연휴",
        ["Holiday.Chuseok"] = "추석",
        ["Holiday.Foundation"] = "개천절",
        ["Holiday.Hangul"] = "한글날",
        ["Holiday.Christmas"] = "성탄절",
        ["Holiday.Substitute"] = "대체공휴일"
    };

    private static readonly Dictionary<string, string> English = new()
    {
        ["Fmt.Year"] = "MMMM yyyy",
        ["Fmt.Day"] = "ddd, MMM d",
        ["Fmt.SearchDate"] = "MMM d, yyyy (ddd)",
        ["Weekdays"] = "Sun,Mon,Tue,Wed,Thu,Fri,Sat",

        ["Header.Today"] = "Today",
        ["Header.TodayTip"] = "Go to today",
        ["Header.PrevTip"] = "Previous month",
        ["Header.NextTip"] = "Next month",
        ["Header.SearchTip"] = "Search",
        ["Header.MenuTip"] = "Settings",
        ["Header.CloseTip"] = "Quit",
        ["Header.ResizeTip"] = "Drag to resize",

        ["Menu.AutoStart"] = "Start with Windows",
        ["Menu.ResetBounds"] = "Reset position and size",
        ["Menu.Holidays"] = "Show Korean public holidays",
        ["Menu.PageFlip"] = "Page-turn animation",
        ["Menu.Language"] = "Language (언어)",
        ["Menu.Transparency"] = "Background transparency {0}%",
        ["Menu.Quit"] = "Quit",

        ["Update.Badge"] = "Update",
        ["Update.BadgeTip"] = "Version {0} is available. Click to download",
        ["Update.Check"] = "Check for updates (current {0})",
        ["Update.Checking"] = "Checking for updates…",
        ["Update.Failed"] = "Couldn't check · please check your internet connection",
        ["Update.Latest"] = "You're on the latest version ({0})",
        ["Update.Available"] = "Get version {0} (current {1})",

        ["Memo.Empty"] = "Nothing planned",
        ["Memo.Hint"] = "✎  Type a task and press Enter",
        ["Memo.RangeHint"] = "✎  Add an event for {0}–{1}",
        ["Memo.Days"] = "{0} days",
        ["Memo.Add"] = "Add",

        ["Editor.Title"] = "Edit event",
        ["Editor.Date"] = "Date",
        ["Editor.DateHint"] = "click the calendar to change",
        ["Editor.Repeat"] = "Repeat",
        ["Editor.Color"] = "Color",
        ["Editor.Dday"] = "Show D-day",
        ["Editor.Delete"] = "Delete",
        ["Editor.Cancel"] = "Cancel",
        ["Editor.Save"] = "Save",

        ["Repeat.None"] = "None",
        ["Repeat.Weekly"] = "Weekly",
        ["Repeat.Monthly"] = "Monthly",
        ["Repeat.Yearly"] = "Yearly",

        ["Label.Weekly"] = "Every {0}",
        ["Label.Monthly"] = "Monthly on day {0}",
        ["Label.Yearly"] = "Yearly on {0}",
        ["Label.Days"] = "{0} days",

        ["Search.Title"] = "Search",
        ["Search.Hint"] = "Search by name",
        ["Search.Empty"] = "No matching events",

        ["Holiday.NewYear"] = "New Year's Day",
        ["Holiday.SeollalHoliday"] = "Seollal Holiday",
        ["Holiday.Seollal"] = "Seollal",
        ["Holiday.March1"] = "Independence Movement Day",
        ["Holiday.Children"] = "Children's Day",
        ["Holiday.Buddha"] = "Buddha's Birthday",
        ["Holiday.Memorial"] = "Memorial Day",
        ["Holiday.Liberation"] = "Liberation Day",
        ["Holiday.ChuseokHoliday"] = "Chuseok Holiday",
        ["Holiday.Chuseok"] = "Chuseok",
        ["Holiday.Foundation"] = "National Foundation Day",
        ["Holiday.Hangul"] = "Hangul Day",
        ["Holiday.Christmas"] = "Christmas",
        ["Holiday.Substitute"] = "Substitute Holiday"
    };
}
