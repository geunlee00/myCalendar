using System.Globalization;

namespace CalendarWidget.Services;

/// <summary>
/// 대한민국 법정 공휴일과 대체공휴일을 계산합니다. 설날·추석·부처님오신날은 음력으로 계산합니다.
/// 선거일 같은 임시공휴일은 미리 알 수 없어서 포함하지 않습니다.
/// 이름은 언어를 바꿔도 맞게 보이도록 번역 열쇠(Holiday.*)로 계산해 두고, 꺼낼 때 지금 언어로 바꿉니다.
/// </summary>
internal static class KoreanHolidays
{
    private static readonly KoreanLunisolarCalendar Lunar = new();
    private static readonly Dictionary<int, Dictionary<DateTime, List<string>>> Cache = [];

    public static string? GetName(DateTime date)
    {
        if (date < Lunar.MinSupportedDateTime || date > Lunar.MaxSupportedDateTime) return null;
        if (!Cache.TryGetValue(date.Year, out var holidays))
        {
            holidays = Build(date.Year);
            Cache[date.Year] = holidays;
        }
        return holidays.TryGetValue(date.Date, out var keys)
            ? string.Join(" · ", keys.Select(key => Loc.T("Holiday." + key)))
            : null;
    }

    private static Dictionary<DateTime, List<string>> Build(int year)
    {
        var holidays = new Dictionary<DateTime, List<string>>();
        void Add(DateTime date, string key)
        {
            if (!holidays.TryGetValue(date, out var keys)) holidays[date] = keys = [];
            keys.Add(key);
        }

        var seollal = FromLunar(year, 1, 1);
        var chuseok = FromLunar(year, 8, 15);
        var buddha = FromLunar(year, 4, 8);

        // 대체공휴일 판단 전에 모든 공휴일을 먼저 넣어야 겹침을 알 수 있다.
        Add(new DateTime(year, 1, 1), "NewYear");
        Add(seollal.AddDays(-1), "SeollalHoliday");
        Add(seollal, "Seollal");
        Add(seollal.AddDays(1), "SeollalHoliday");
        Add(new DateTime(year, 3, 1), "March1");
        Add(new DateTime(year, 5, 5), "Children");
        Add(buddha, "Buddha");
        Add(new DateTime(year, 6, 6), "Memorial");
        Add(new DateTime(year, 8, 15), "Liberation");
        Add(chuseok.AddDays(-1), "ChuseokHoliday");
        Add(chuseok, "Chuseok");
        Add(chuseok.AddDays(1), "ChuseokHoliday");
        Add(new DateTime(year, 10, 3), "Foundation");
        Add(new DateTime(year, 10, 9), "Hangul");
        Add(new DateTime(year, 12, 25), "Christmas");

        // 설날·추석 연휴: 일요일이나 다른 공휴일과 겹치면 연휴 다음 첫 평일이 대체공휴일이다.
        foreach (var center in new[] { seollal, chuseok })
        {
            var days = new[] { center.AddDays(-1), center, center.AddDays(1) };
            var overlapsSunday = days.Any(d => d.DayOfWeek == DayOfWeek.Sunday);
            var overlapsOther = days.Any(d => IsFixedHoliday(d) || d == buddha);
            if (overlapsSunday || overlapsOther) Add(NextWorkday(center.AddDays(1), holidays), "Substitute");
        }

        // 나머지 대체공휴일 대상: 토요일이나 일요일과 겹치면 다음 첫 평일이 대체공휴일이다.
        // 설날·추석 연휴와 겹친 경우는 위에서 하루만 더했으므로 다시 더하지 않는다.
        var weekendRule = new[]
        {
            new DateTime(year, 3, 1), new DateTime(year, 5, 5), buddha, new DateTime(year, 8, 15),
            new DateTime(year, 10, 3), new DateTime(year, 10, 9), new DateTime(year, 12, 25)
        };
        foreach (var day in weekendRule)
        {
            var onWeekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var buddhaOnChildrensDay = day == buddha && buddha == new DateTime(year, 5, 5);
            if (onWeekend || buddhaOnChildrensDay) Add(NextWorkday(day, holidays), "Substitute");
        }

        return holidays;
    }

    private static bool IsFixedHoliday(DateTime date) => (date.Month, date.Day) is
        (1, 1) or (3, 1) or (5, 5) or (6, 6) or (8, 15) or (10, 3) or (10, 9) or (12, 25);

    private static DateTime NextWorkday(DateTime after, Dictionary<DateTime, List<string>> holidays)
    {
        var day = after.AddDays(1);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || holidays.ContainsKey(day))
            day = day.AddDays(1);
        return day;
    }

    /// <summary>음력 날짜를 양력으로 바꿉니다. 윤달이 있는 해는 윤달 뒤의 달 번호가 하나씩 밀립니다.</summary>
    private static DateTime FromLunar(int year, int month, int day)
    {
        var leapMonth = Lunar.GetLeapMonth(year);
        var index = leapMonth > 0 && month >= leapMonth ? month + 1 : month;
        return Lunar.ToDateTime(year, index, day, 0, 0, 0, 0);
    }
}
