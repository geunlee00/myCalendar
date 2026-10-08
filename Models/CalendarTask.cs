using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using CalendarWidget.Services;

namespace CalendarWidget.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Recurrence
{
    None,
    Weekly,
    Monthly,
    Yearly,

    /// <summary>매년 음력으로 같은 달·날(음력 생신, 제사 등)</summary>
    LunarYearly
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskColor
{
    Default,
    Red,
    Orange,
    Green,
    Blue,
    Purple
}

/// <summary>반복 일정이 실제로 놓이는 한 번(시작·끝 날짜). 반복하지 않으면 일정 하나에 한 번뿐입니다.</summary>
public readonly record struct Occurrence(CalendarTask Task, DateTime Start, DateTime End)
{
    public bool IsMultiDay => End > Start;
}

public sealed class CalendarTask : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private DateTime _date;
    private DateTime? _endDate;
    private bool _isCompleted;
    private Recurrence _recurrence;
    private TaskColor _color;
    private bool _isDday;

    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>일정 날짜. 여러 날 일정이면 시작 날짜, 반복 일정이면 첫 번째 날짜입니다.</summary>
    public DateTime Date
    {
        get => _date;
        set => Set(ref _date, value.Date);
    }

    /// <summary>여러 날 일정의 끝 날짜. 하루 일정이면 비어 있습니다.</summary>
    public DateTime? EndDate
    {
        get => _endDate;
        set => Set(ref _endDate, value?.Date);
    }

    public string Title
    {
        get => _title;
        set => Set(ref _title, value);
    }

    /// <summary>완료 여부. 반복 일정은 날마다 따로 기억하지 않으므로 쓰지 않습니다.</summary>
    public bool IsCompleted
    {
        get => _isCompleted;
        set => Set(ref _isCompleted, value);
    }

    public Recurrence Recurrence
    {
        get => _recurrence;
        set => Set(ref _recurrence, value);
    }

    public TaskColor Color
    {
        get => _color;
        set => Set(ref _color, value);
    }

    /// <summary>메모장 머리와 목록에 남은 날(D-12)을 보여 줄지.</summary>
    public bool IsDday
    {
        get => _isDday;
        set => Set(ref _isDday, value);
    }

    /// <summary>일정의 마지막 날(반복 일정은 첫 번째 회차의 마지막 날). 하루 일정이면 시작 날짜와 같습니다.</summary>
    [JsonIgnore]
    public DateTime LastDate => EndDate is { } end && end > Date ? end : Date;

    [JsonIgnore]
    public bool IsMultiDay => LastDate > Date;

    [JsonIgnore]
    public bool IsRecurring => Recurrence != Recurrence.None;

    /// <summary>목록에 함께 보여 줄 일정 설명(예: 10/8~10/10, 매주 화, 매년 3/15). 하루 일정이면 비어 있습니다.</summary>
    [JsonIgnore]
    public string? ScheduleLabel
    {
        get
        {
            var parts = new List<string>();
            switch (Recurrence)
            {
                case Recurrence.Weekly:
                    parts.Add(Loc.T("Label.Weekly", Loc.Culture.DateTimeFormat.GetAbbreviatedDayName(Date.DayOfWeek)));
                    break;
                case Recurrence.Monthly:
                    parts.Add(Loc.T("Label.Monthly", Date.Day));
                    break;
                case Recurrence.Yearly:
                    parts.Add(Loc.T("Label.Yearly", Date.ToString("M'/'d")));
                    break;
                case Recurrence.LunarYearly when KoreanLunar.FromSolar(Date) is { } lunar:
                    parts.Add(Loc.T("Label.LunarYearly", KoreanLunar.Short(lunar).Replace('.', '/')));
                    break;
            }
            if (IsMultiDay)
                parts.Add(IsRecurring ? Loc.T("Label.Days", (LastDate - Date).Days + 1) : $"{Date:M'/'d}~{LastDate:M'/'d}");
            return parts.Count > 0 ? string.Join(" · ", parts) : null;
        }
    }

    /// <summary>D-day로 표시한 일정의 남은 날(D-12, 진행 중이면 D-DAY). 표시하지 않거나 다 지났으면 비어 있습니다.</summary>
    [JsonIgnore]
    public string? DdayLabel
    {
        get
        {
            if (!IsDday || NextOccurrence(DateTime.Today) is not { } next) return null;
            var days = (next.Start - DateTime.Today).Days;
            return days <= 0 ? "D-DAY" : $"D-{days}";
        }
    }

    /// <summary>
    /// 주어진 기간과 겹치는 회차를 앞에서부터 돌려줍니다. 매달·매년 반복은 그 달에 없는 날(31일, 2월 29일)이면
    /// 그 달의 마지막 날로 옮깁니다.
    /// </summary>
    public IEnumerable<Occurrence> OccurrencesBetween(DateTime from, DateTime to)
    {
        var span = (LastDate - Date).Days;
        if (!IsRecurring)
        {
            if (Date <= to && LastDate >= from) yield return new Occurrence(this, Date, LastDate);
            yield break;
        }

        if (Recurrence == Recurrence.LunarYearly)
        {
            // 처음 날짜의 음력 달·날을 해마다 양력으로 바꾼다. 음력 해는 양력 해와 한 해쯤 어긋날 수 있어 앞뒤로 넉넉히 본다.
            if (KoreanLunar.FromSolar(Date) is not { } origin) yield break;
            var firstYear = Math.Max(origin.Year, from.AddDays(-span).Year - 1);
            for (var year = firstYear; year <= to.Year + 1; year++)
            {
                if (KoreanLunar.ToSolar(year, origin.Month, origin.Day, origin.IsLeap) is not { } start || start < Date) continue;
                var end = start.AddDays(span);
                if (start <= to && end >= from) yield return new Occurrence(this, start, end);
            }
            yield break;
        }

        // 기간과 겹칠 수 있는 가장 이른 회차부터 센다.
        var earliestStart = from.AddDays(-span);
        var index = Recurrence switch
        {
            Recurrence.Weekly => Math.Max(0, (earliestStart - Date).Days / 7),
            Recurrence.Monthly => Math.Max(0, (earliestStart.Year - Date.Year) * 12 + earliestStart.Month - Date.Month - 1),
            _ => Math.Max(0, earliestStart.Year - Date.Year - 1)
        };
        for (; ; index++)
        {
            var start = Recurrence switch
            {
                Recurrence.Weekly => Date.AddDays(7 * index),
                Recurrence.Monthly => Date.AddMonths(index),
                _ => Date.AddYears(index)
            };
            if (start > to) yield break;

            var end = start.AddDays(span);
            if (end >= from) yield return new Occurrence(this, start, end);
        }
    }

    /// <summary>주어진 날짜에 진행 중이거나 그 뒤에 처음 오는 회차. 없으면 null입니다.</summary>
    public Occurrence? NextOccurrence(DateTime from)
    {
        foreach (var occurrence in OccurrencesBetween(from, from.AddYears(5))) return occurrence;
        return null;
    }

    /// <summary>편집 화면에서 고친 내용을 한꺼번에 반영합니다. 변경 알림은 한 번만 보냅니다.</summary>
    public void Update(string title, DateTime start, DateTime end, Recurrence recurrence, TaskColor color, bool isDday)
    {
        _title = title;
        _date = start.Date;
        _endDate = end.Date > start.Date ? end.Date : null;
        _recurrence = recurrence;
        _color = color;
        _isDday = isDday;
        if (recurrence != Recurrence.None) _isCompleted = false;
        OnPropertyChanged(string.Empty);
    }

    /// <summary>일정을 며칠 앞뒤로 옮깁니다. 반복 일정은 모든 회차가 함께 옮겨집니다.</summary>
    public void MoveBy(int days)
    {
        _date = _date.AddDays(days);
        _endDate = _endDate?.AddDays(days);
        OnPropertyChanged(string.Empty);
    }

    /// <summary>언어나 날짜가 바뀌어 화면에 보이는 설명 문구만 다시 계산해야 할 때 부릅니다.</summary>
    public void RefreshLabels()
    {
        OnPropertyChanged(nameof(ScheduleLabel));
        OnPropertyChanged(nameof(DdayLabel));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
