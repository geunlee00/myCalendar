using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace CalendarWidget.Models;

public sealed class CalendarTask : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private bool _isCompleted;
    private bool _isEditing;

    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>일정 날짜. 여러 날 일정이면 시작 날짜입니다.</summary>
    public DateTime Date { get; init; }

    /// <summary>여러 날 일정의 끝 날짜. 하루 일정이면 비어 있습니다.</summary>
    public DateTime? EndDate { get; init; }

    /// <summary>일정의 마지막 날. 하루 일정이면 시작 날짜와 같습니다.</summary>
    [JsonIgnore]
    public DateTime LastDate => EndDate is { } end && end.Date > Date.Date ? end.Date : Date.Date;

    [JsonIgnore]
    public bool IsMultiDay => LastDate > Date.Date;

    /// <summary>목록에 함께 보여 줄 기간(예: 10/8~10/10). 하루 일정이면 비어 있습니다.</summary>
    [JsonIgnore]
    public string? RangeLabel => IsMultiDay ? $"{Date:M'/'d}~{LastDate:M'/'d}" : null;

    public bool Covers(DateTime date) => date >= Date.Date && date <= LastDate;

    public string Title
    {
        get => _title;
        set
        {
            if (_title == value) return;
            _title = value;
            OnPropertyChanged();
        }
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        set
        {
            if (_isCompleted == value) return;
            _isCompleted = value;
            OnPropertyChanged();
        }
    }

    /// <summary>목록에서 이름을 고치는 중인지 나타냅니다. 화면 상태라 저장하지 않습니다.</summary>
    [JsonIgnore]
    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (_isEditing == value) return;
            _isEditing = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
