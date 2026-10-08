using System.Globalization;

namespace CalendarWidget.Services;

/// <summary>음력 날짜. Month는 1~12, 윤달이면 IsLeap이 true입니다(예: 윤4월 3일 → 4, 3, true).</summary>
public readonly record struct LunarDate(int Year, int Month, int Day, bool IsLeap);

/// <summary>
/// 한국 음력(KoreanLunisolarCalendar)과 양력을 서로 바꿉니다. 지원 범위(대략 918~2050년)를 벗어나면 null입니다.
/// </summary>
internal static class KoreanLunar
{
    private static readonly KoreanLunisolarCalendar Calendar = new();

    public static LunarDate? FromSolar(DateTime date)
    {
        if (date < Calendar.MinSupportedDateTime || date > Calendar.MaxSupportedDateTime) return null;

        var year = Calendar.GetYear(date);
        var index = Calendar.GetMonth(date);
        var leapMonth = Calendar.GetLeapMonth(year);

        // KoreanLunisolarCalendar는 윤달을 끼워 넣어 달 번호를 매기므로(예: 윤4월이면 4월=4, 윤4월=5, 5월=6) 되돌린다.
        var isLeap = leapMonth > 0 && index == leapMonth;
        var month = leapMonth > 0 && index >= leapMonth ? index - 1 : index;
        return new LunarDate(year, month, Calendar.GetDayOfMonth(date), isLeap);
    }

    /// <summary>
    /// 음력 날짜를 양력으로 바꿉니다. 그 해에 해당 윤달이 없으면 평달로, 그 달에 그 날이 없으면(30일) 그 달 마지막 날로 바꿉니다.
    /// </summary>
    public static DateTime? ToSolar(int year, int month, int day, bool isLeap)
    {
        try
        {
            var leapMonth = Calendar.GetLeapMonth(year);
            var index = leapMonth > 0 && month >= leapMonth ? month + 1 : month;
            if (isLeap && leapMonth == month + 1) index = leapMonth;

            var lastDay = Calendar.GetDaysInMonth(year, index);
            return Calendar.ToDateTime(year, index, Math.Min(day, lastDay), 0, 0, 0, 0);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>날짜 칸에 쓰는 짧은 음력 표시(예: 8.27, 윤4.3).</summary>
    public static string Short(LunarDate date) => $"{(date.IsLeap ? Loc.T("Lunar.Leap") : string.Empty)}{date.Month}.{date.Day}";
}
