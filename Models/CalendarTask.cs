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
    public DateTime Date { get; init; }

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
