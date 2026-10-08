using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CalendarWidget.Models;
using CalendarWidget.Services;

namespace CalendarWidget;

public partial class CalendarView : UserControl
{
    private static readonly CultureInfo Korean = CultureInfo.GetCultureInfo("ko-KR");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
    private static readonly FontFamily PrintedFont = new("Georgia");

    // 탁상 달력 색: 미색 종이, 남색 글자, 빨간 강조
    private static readonly Color PaperColor = Color.FromRgb(0xFF, 0xFC, 0xF5);
    private static readonly Color PaperEdgeColor = Color.FromRgb(0xE6, 0xDE, 0xCD);
    private static readonly Color InkColor = Color.FromRgb(0x2E, 0x34, 0x40);
    private static readonly Brush InkBrush = Freeze(new SolidColorBrush(InkColor));
    private static readonly Brush RedBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xC8, 0x48, 0x3D)));
    private static readonly Brush BlueBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x3F, 0x6F, 0xB5)));
    private static readonly Brush SelectedBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF1, 0xE8, 0xD6)));
    private static readonly Brush SelectedEdgeBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xC9, 0xB9, 0x98)));
    private static readonly Brush ChipBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xEF, 0xE6, 0xD2)));
    private static readonly Brush ChipTextBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x4A, 0x42, 0x34)));
    private static readonly Brush DoneChipBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF4, 0xF0, 0xE8)));
    private static readonly Brush DoneTextBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xA8, 0xA0, 0x90)));
    private static readonly Brush GridLineBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xE6, 0xDD, 0xCB)));
    private static readonly Brush RangeBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xD9, 0xE4, 0xEF)));
    private static readonly Brush RangeTextBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x2E, 0x4A, 0x6B)));
    private static readonly Brush DoneRangeBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xEC, 0xEF, 0xF3)));
    private static readonly Brush SubInkBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x8A, 0x81, 0x72)));
    private static readonly Brush HoleBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x99, 0x3A, 0x34, 0x2C)));
    private static readonly Brush WireEdgeBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x6E, 0x74, 0x7C)));
    private static readonly Brush WireBrush = Freeze(new LinearGradientBrush(
        [
            new GradientStop(Color.FromRgb(0x8E, 0x94, 0x9C), 0),
            new GradientStop(Color.FromRgb(0xF2, 0xF4, 0xF6), 0.45),
            new GradientStop(Color.FromRgb(0x6E, 0x74, 0x7C), 1)
        ],
        0));

    // 날짜 칸 안의 일정 한 줄 높이와, 날짜 숫자 줄이 차지하는 높이(WPF 단위).
    private const double ChipHeight = 16;
    private const double DayHeaderHeight = 28;

    // 메모장 가로줄 간격. 할 일 한 줄 높이(TaskItemStyle)와 같다.
    private const double MemoLineSpacing = 28;

    // 앞뒤 달 날짜를 흐리게 보여 줄 때의 불투명도.
    private const double OtherMonthOpacity = 0.35;

    private readonly TaskStore _taskStore = new();
    private readonly ObservableCollection<CalendarTask> _tasks = [];
    private readonly ListCollectionView _selectedDayTasks;
    private readonly DispatcherTimer _dayChangeTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private DateTime _displayMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    // 고른 기간. 하루만 골랐으면 시작과 끝이 같다.
    private DateTime _selectedDate = DateTime.Today;
    private DateTime _selectedEndDate = DateTime.Today;

    // 끌거나 Shift+클릭으로 기간을 고를 때 기준이 되는 날짜와, 끄는 중인지 여부.
    private DateTime _selectionAnchor = DateTime.Today;
    private bool _isDraggingSelection;

    // 여러 날 일정마다 날짜 칸 안에서 차지하는 줄 번호. 날마다 같은 줄에 그려야 막대가 이어진다.
    private Dictionary<CalendarTask, int> _lanes = [];
    private double _cellWidth = 70;
    private DateTime _renderedToday = DateTime.Today;
    private int _chipsPerDay = 2;
    private double _backgroundTransparency;

    /// <summary>배경 투명도(0 = 불투명, 1 = 투명)를 사용자가 바꾸면 알려 줍니다.</summary>
    public event Action<double>? BackgroundTransparencyChanged;

    /// <summary>
    /// 카드 배경의 투명도입니다(0 = 불투명, 1 = 투명). 글자와 일정 표시는 그대로 또렷하게 둡니다.
    /// </summary>
    public double BackgroundTransparency
    {
        get => _backgroundTransparency;
        set
        {
            _backgroundTransparency = Math.Clamp(value, 0, 1);
            var opacity = 1 - _backgroundTransparency;

            // 완전히 투명한 픽셀은 클릭이 바탕화면으로 통과하므로, 눈에 안 보일 만큼은 배경을 남긴다.
            var backgroundAlpha = (byte)Math.Max(3, Math.Round(255 * opacity));
            var paperAlpha = (byte)Math.Round(255 * opacity);
            Card.Background = new SolidColorBrush(Color.FromArgb(backgroundAlpha, PaperColor.R, PaperColor.G, PaperColor.B));
            Card.BorderBrush = new SolidColorBrush(Color.FromArgb(paperAlpha, PaperEdgeColor.R, PaperEdgeColor.G, PaperEdgeColor.B));
            CardShadow.Opacity = 0.22 * opacity;

            // 아래에 쌓인 종이와 스프링 고리, 메모장 줄도 종이와 함께 옅어진다.
            PageLayer1.Opacity = opacity;
            PageLayer2.Opacity = opacity;
            RingsLayer.Opacity = opacity;
            MemoLines.Background = CreateMemoLinesBrush((byte)Math.Round(0x70 * opacity));

            // 입력칸 밑줄은 연하게라도 남겨 위치가 보이게 하고, 추가 버튼 글자는 배경이 절반 넘게 남아 있으면 흰색,
            // 그보다 옅으면 남색으로 바꿔 계속 읽히게 한다.
            TaskInput.BorderBrush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(255 * Math.Max(0.45, opacity)), 0xC9, 0xB9, 0x98));
            AddButton.Background = new SolidColorBrush(Color.FromArgb(backgroundAlpha, InkColor.R, InkColor.G, InkColor.B));
            AddButton.Foreground = opacity >= 0.55 ? Brushes.White : InkBrush;

            // 효과를 켜면 글자가 살짝 흐려지므로 불투명할 때는 아예 끈다.
            TextGlow.Opacity = _backgroundTransparency;
            ContentGrid.Effect = _backgroundTransparency > 0 ? TextGlow : null;
            TransparencyText.Text = $"배경 투명도 {Math.Round(_backgroundTransparency * 100)}%";
        }
    }

    public CalendarView()
    {
        InitializeComponent();
        BackgroundTransparency = 0;
        foreach (var task in _taskStore.Load()) AddTracked(task);

        // 목록에는 선택한 날짜의 할 일만, 안 끝낸 일을 먼저 입력한 순서대로 보여 준다.
        _selectedDayTasks = (ListCollectionView)CollectionViewSource.GetDefaultView(_tasks);
        _selectedDayTasks.Filter = item =>
            item is CalendarTask task && task.Date.Date <= _selectedEndDate && task.LastDate >= _selectedDate;
        _selectedDayTasks.CustomSort = Comparer<object>.Create((a, b) => CompareTasks((CalendarTask)a, (CalendarTask)b));
        TaskList.ItemsSource = _selectedDayTasks;

        RenderCalendar();
        RenderSelectedDate();

        // 위젯은 며칠씩 켜 두므로 날짜가 바뀌면 오늘 표시를 다시 그린다.
        _dayChangeTimer.Tick += DayChangeTimer_Tick;
        _dayChangeTimer.Start();
        Application.Current.Exit += (_, _) => _taskStore.Save(_tasks);
    }

    /// <summary>메모장처럼 일정한 간격으로 가로줄이 그어진 배경을 만듭니다.</summary>
    private static Brush CreateMemoLinesBrush(byte alpha)
    {
        var line = new GeometryDrawing(
            null,
            new Pen(new SolidColorBrush(Color.FromArgb(alpha, 0xC9, 0xB9, 0x98)), 1),
            new LineGeometry(new Point(0, MemoLineSpacing - 0.5), new Point(10, MemoLineSpacing - 0.5)));
        var brush = new DrawingBrush(line)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 10, MemoLineSpacing),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, 10, MemoLineSpacing),
            ViewboxUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None
        };
        brush.Freeze();
        return brush;
    }

    /// <summary>종이 위쪽 가장자리에 두 가닥 철사 스프링 고리를 폭에 맞춰 고르게 그립니다.</summary>
    private void RingsLayer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RingsLayer.Children.Clear();

        const double pitch = 28;
        var count = Math.Max(2, (int)((e.NewSize.Width - 60) / pitch));
        var start = (e.NewSize.Width - count * pitch) / 2 + 7;
        for (var i = 0; i < count; i++)
        {
            var x = start + i * pitch;

            // 종이에 뚫린 구멍(종이는 위에서 12 아래부터 시작한다)
            var hole = new Border { Width = 14, Height = 7, CornerRadius = new CornerRadius(3.5), Background = HoleBrush };
            Canvas.SetLeft(hole, x);
            Canvas.SetTop(hole, 20);
            RingsLayer.Children.Add(hole);

            foreach (var offset in new[] { 2.5, 7.5 })
            {
                var wire = new Border
                {
                    Width = 4,
                    Height = 25,
                    CornerRadius = new CornerRadius(2),
                    Background = WireBrush,
                    BorderBrush = WireEdgeBrush,
                    BorderThickness = new Thickness(0.5)
                };
                Canvas.SetLeft(wire, x + offset);
                Canvas.SetTop(wire, 0);
                RingsLayer.Children.Add(wire);
            }
        }
    }

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }

    private int CompareTasks(CalendarTask a, CalendarTask b)
    {
        var byCompletion = a.IsCompleted.CompareTo(b.IsCompleted);
        if (byCompletion != 0) return byCompletion;
        var byStart = a.Date.Date.CompareTo(b.Date.Date);
        return byStart != 0 ? byStart : _tasks.IndexOf(a).CompareTo(_tasks.IndexOf(b));
    }

    private IEnumerable<CalendarTask> TasksOn(DateTime date) =>
        _tasks.Where(t => t.Covers(date)).OrderBy(t => t.IsCompleted).ThenBy(_tasks.IndexOf);

    private void AddTracked(CalendarTask task)
    {
        task.PropertyChanged += Task_PropertyChanged;
        _tasks.Add(task);
    }

    private void Task_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 이름 고치는 중인지는 화면 상태일 뿐이라 저장하지 않는다.
        if (e.PropertyName == nameof(CalendarTask.IsEditing)) return;

        _taskStore.Save(_tasks);

        // 체크박스 클릭 처리가 끝난 뒤에 목록 순서와 날짜 칸을 다시 그린다.
        Dispatcher.BeginInvoke(() =>
        {
            _selectedDayTasks.Refresh();
            RenderCalendar();
        }, DispatcherPriority.Background);
    }

    private void DayChangeTimer_Tick(object? sender, EventArgs e)
    {
        if (DateTime.Today == _renderedToday) return;

        // 어제(이전의 오늘)를 보고 있었다면 새 오늘로 따라간다.
        if (_selectedDate == _renderedToday && _selectedEndDate == _renderedToday)
        {
            _selectedDate = _selectedEndDate = _selectionAnchor = DateTime.Today;
            _displayMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        }
        _renderedToday = DateTime.Today;
        RenderCalendar();
        RenderSelectedDate();
    }

    private void RenderCalendar()
    {
        MonthNumberText.Text = _displayMonth.Month.ToString();
        MonthNameText.Text = string.Join("\u2009", _displayMonth.ToString("MMMM", English).ToUpperInvariant().ToCharArray());
        YearText.Text = _displayMonth.ToString("yyyy년 M월", Korean);
        DayGrid.Children.Clear();

        var firstCell = (int)_displayMonth.DayOfWeek;
        var daysInMonth = DateTime.DaysInMonth(_displayMonth.Year, _displayMonth.Month);

        // 달마다 필요한 주 수(4~6주)만큼만 줄을 만들어 빈 줄 없이 칸을 크게 쓴다.
        DayGrid.Rows = (firstCell + daysInMonth + 6) / 7;
        UpdateChipsPerDay();

        // 앞뒤 빈칸에는 지난달 끝 날짜와 다음 달 첫 날짜를 흐리게 채운다.
        var gridStart = _displayMonth.AddDays(-firstCell);
        var gridEnd = gridStart.AddDays(DayGrid.Rows * 7 - 1);
        if (DayGrid.ActualWidth > 0) _cellWidth = DayGrid.ActualWidth / 7;
        AssignLanes(gridStart, gridEnd);

        for (var cell = 0; cell < DayGrid.Rows * 7; cell++)
        {
            var gridCell = CreateGridCell(CreateDayButton(gridStart.AddDays(cell)));

            // 막대 제목이 오른쪽 칸까지 펼쳐지므로 왼쪽 칸을 위에 그린다.
            Panel.SetZIndex(gridCell, 6 - cell % 7);
            DayGrid.Children.Add(gridCell);
        }
    }

    /// <summary>
    /// 보이는 기간과 겹치는 여러 날 일정에, 서로 겹치지 않는 가장 위쪽 줄 번호를 매깁니다.
    /// 먼저 시작하고 긴 일정부터 위쪽 줄을 차지합니다.
    /// </summary>
    private void AssignLanes(DateTime gridStart, DateTime gridEnd)
    {
        _lanes = [];
        var laneEnds = new List<DateTime>();
        var visible = _tasks
            .Where(t => t.IsMultiDay && t.LastDate >= gridStart && t.Date.Date <= gridEnd)
            .OrderBy(t => t.Date.Date)
            .ThenByDescending(t => t.LastDate)
            .ThenBy(_tasks.IndexOf);
        foreach (var task in visible)
        {
            var lane = laneEnds.FindIndex(end => end < task.Date.Date);
            if (lane < 0)
            {
                lane = laneEnds.Count;
                laneEnds.Add(task.LastDate);
            }
            else
            {
                laneEnds[lane] = task.LastDate;
            }
            _lanes[task] = lane;
        }
    }

    private Button CreateDayButton(DateTime date)
    {
        var holiday = KoreanHolidays.GetName(date);
        var tasks = TasksOn(date).ToList();
        var isToday = date == DateTime.Today;

        var content = new Grid
        {
            Margin = new Thickness(5, 3, 5, 3),
            Opacity = date.Month == _displayMonth.Month ? 1 : OtherMonthOpacity
        };
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(DayHeaderHeight - 6) });
        content.RowDefinitions.Add(new RowDefinition());

        var header = new DockPanel { LastChildFill = true };
        var number = new TextBlock
        {
            Text = date.Day.ToString(),
            FontFamily = PrintedFont,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = isToday ? Brushes.White : GetDayColor(date, holiday),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        // Georgia 기본 숫자는 3·4·5·7·9가 아래로 처지므로 높이가 고른 숫자를 쓴다.
        Typography.SetNumeralStyle(number, FontNumeralStyle.Lining);

        // 오늘은 날짜 숫자에 빨간 동그라미를 친다.
        header.Children.Add(new Border
        {
            MinWidth = 22,
            Height = 22,
            Padding = new Thickness(isToday ? 4 : 0, 0, isToday ? 4 : 0, 0),
            CornerRadius = new CornerRadius(11),
            Background = isToday ? RedBrush : null,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = number
        });
        content.Children.Add(header);

        // 여러 날 일정은 정해진 줄에, 하루 일정은 남은 빈 줄에 넣는다.
        // 공휴일 이름과 "+N"은 숫자 옆 머리 줄에 써서, 아래 줄은 모두 일정에 쓰고 막대 줄이 어긋나지 않게 한다.
        var slots = _chipsPerDay;
        var rows = new FrameworkElement?[slots];
        var hidden = 0;
        foreach (var task in tasks.Where(t => t.IsMultiDay))
        {
            var lane = _lanes.GetValueOrDefault(task, int.MaxValue);
            if (lane < slots) rows[lane] = CreateRangeSegment(task, date);
            else hidden++;
        }
        foreach (var task in tasks.Where(t => !t.IsMultiDay))
        {
            var free = Array.IndexOf(rows, null);
            if (free >= 0) rows[free] = CreateChip(task);
            else hidden++;
        }
        if (hidden > 0)
        {
            var more = new TextBlock
            {
                Text = $"+{hidden}",
                FontSize = 10,
                Foreground = SubInkBrush,
                Margin = new Thickness(3, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(more, Dock.Right);
            header.Children.Add(more);
        }
        if (holiday is not null)
        {
            header.Children.Add(new TextBlock
            {
                Text = holiday,
                FontSize = 9.5,
                Foreground = RedBrush,
                Margin = new Thickness(3, 1, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }

        // 빈 줄도 자리를 지켜야 아래쪽 막대가 다른 날과 같은 높이에 그려진다.
        var chips = new UnclippedStackPanel();
        Grid.SetRow(chips, 1);
        var lastUsed = Array.FindLastIndex(rows, row => row is not null);
        for (var i = 0; i <= lastUsed; i++) chips.Children.Add(rows[i] ?? new Border { Height = ChipHeight });
        content.Children.Add(chips);

        // 하루만 고른 날짜에는 테두리를 두른다. 버튼 자체에 테두리를 주면 WPF가 안쪽 내용을 테두리 안으로 잘라
        // 여러 날 일정 막대가 옆 칸으로 이어지지 못하므로, 테두리만 따로 위에 얹는다.
        if (IsSingleSelection && date == _selectedDate)
        {
            var outline = new Border
            {
                BorderBrush = SelectedEdgeBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(-content.Margin.Left, -content.Margin.Top, -content.Margin.Right, -content.Margin.Bottom),
                IsHitTestVisible = false
            };
            Grid.SetRowSpan(outline, 2);
            content.Children.Add(outline);
        }

        var tooltipLines = new List<string>();
        if (holiday is not null) tooltipLines.Add(holiday);
        tooltipLines.AddRange(tasks.Select(t =>
            (t.IsCompleted ? "✓ " : "• ") + t.Title + (t.IsMultiDay ? $" ({t.RangeLabel})" : string.Empty)));

        return new Button
        {
            Content = content,
            Style = (Style)FindResource("DayButtonStyle"),
            Tag = date,
            Background = IsSelected(date) ? SelectedBrush : Brushes.Transparent,
            ToolTip = tooltipLines.Count > 0 ? string.Join(Environment.NewLine, tooltipLines) : null
        };
    }

    /// <summary>날짜 칸을 오른쪽·아래쪽 격자선으로 감쌉니다.</summary>
    private static Border CreateGridCell(UIElement child) => new()
    {
        BorderBrush = GridLineBrush,
        BorderThickness = new Thickness(0, 0, 1, 1),
        Child = child
    };

    /// <summary>
    /// 자기 영역 밖으로 그리는 것을 자르지 않는 StackPanel. 여러 날 일정 막대가 칸 끝까지 뻗어
    /// 옆 칸과 이어 보이려면 필요하다. (StackPanel은 기본으로 영역 밖을 잘라 낸다)
    /// </summary>
    private sealed class UnclippedStackPanel : StackPanel
    {
        protected override Geometry? GetLayoutClip(Size layoutSlotSize) => null;
    }

    private bool IsSingleSelection => _selectedEndDate == _selectedDate;

    private bool IsSelected(DateTime date) => date >= _selectedDate && date <= _selectedEndDate;

    /// <summary>
    /// 여러 날 일정 막대 중 하루치 조각을 만듭니다. 같은 주 안에서 앞뒤 날짜로 이어지는 쪽은
    /// 모서리를 펴고 칸 끝까지 늘려 막대가 끊기지 않게 보이게 합니다.
    /// </summary>
    private FrameworkElement CreateRangeSegment(CalendarTask task, DateTime date)
    {
        var fromLeft = date > task.Date.Date && date.DayOfWeek != DayOfWeek.Sunday;
        var toRight = date < task.LastDate && date.DayOfWeek != DayOfWeek.Saturday;
        var bar = new Border
        {
            Background = task.IsCompleted ? DoneRangeBrush : RangeBrush,
            Height = ChipHeight - 1,
            // 칸 안쪽 여백(5)과 버튼 여백(2), 오른쪽 격자선(1)까지 덮는다.
            Margin = new Thickness(fromLeft ? -7 : 0, 1, toRight ? -8 : 0, 0),
            CornerRadius = new CornerRadius(fromLeft ? 0 : 4, toRight ? 0 : 4, toRight ? 0 : 4, fromLeft ? 0 : 4)
        };

        // 제목은 막대가 시작하는 날(또는 주가 바뀐 첫날)에만 쓰고, 이번 주 안에서 막대가 이어지는 길이만큼 펼친다.
        if (!fromLeft)
        {
            var weekEnd = date.AddDays(6 - (int)date.DayOfWeek);
            var spanDays = ((task.LastDate < weekEnd ? task.LastDate : weekEnd) - date).Days + 1;
            var title = new TextBlock
            {
                Text = task.Title,
                FontSize = 10.5,
                Foreground = task.IsCompleted ? DoneTextBrush : RangeTextBrush,
                TextDecorations = task.IsCompleted ? TextDecorations.Strikethrough : null,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Width = Math.Max(12, spanDays * _cellWidth - 18)
            };
            Canvas.SetLeft(title, 4);
            Canvas.SetTop(title, 0.5);

            // Canvas는 크기를 차지하지 않아 제목이 칸 밖(오른쪽 칸들)까지 그려질 수 있다.
            bar.Child = new Canvas { Children = { title } };
        }
        return bar;
    }

    private static Border CreateChip(CalendarTask task) => new()
    {
        Background = task.IsCompleted ? DoneChipBrush : ChipBrush,
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(4, 0, 4, 0),
        Margin = new Thickness(0, 1, 0, 0),
        Height = ChipHeight - 1,
        Child = new TextBlock
        {
            Text = task.Title,
            FontSize = 10.5,
            Foreground = task.IsCompleted ? DoneTextBrush : ChipTextBrush,
            TextDecorations = task.IsCompleted ? TextDecorations.Strikethrough : null,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        }
    };

    private static Brush GetDayColor(DateTime date, string? holiday)
    {
        if (holiday is not null || date.DayOfWeek == DayOfWeek.Sunday) return RedBrush;
        return date.DayOfWeek == DayOfWeek.Saturday ? BlueBrush : InkBrush;
    }

    private void RenderSelectedDate()
    {
        if (IsSingleSelection)
        {
            SelectedDateText.Text = _selectedDate.ToString("M월 d일 (ddd)", Korean);
            SelectedHolidayText.Text = KoreanHolidays.GetName(_selectedDate) ?? string.Empty;
            SelectedHolidayText.Foreground = RedBrush;
            InputHint.Text = "✎  할 일을 적고 Enter";
        }
        else
        {
            SelectedDateText.Text =
                $"{_selectedDate.ToString("M월 d일 (ddd)", Korean)} ~ {_selectedEndDate.ToString("M월 d일 (ddd)", Korean)}";
            SelectedHolidayText.Text = $"{(_selectedEndDate - _selectedDate).Days + 1}일";
            SelectedHolidayText.Foreground = SubInkBrush;
            InputHint.Text = $"✎  {_selectedDate:M'/'d}~{_selectedEndDate:M'/'d} 일정을 적고 Enter";
        }
        _selectedDayTasks.Refresh();
        EmptyListText.Visibility = _selectedDayTasks.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SelectDate(DateTime date)
    {
        _selectedDate = _selectedEndDate = _selectionAnchor = date.Date;
        _displayMonth = new DateTime(date.Year, date.Month, 1);
        RenderCalendar();
        RenderSelectedDate();
    }

    /// <summary>두 날짜 사이를 기간으로 고릅니다. 순서는 상관없습니다. 보고 있는 달은 바꾸지 않습니다.</summary>
    private void SelectRange(DateTime from, DateTime to)
    {
        var start = from < to ? from : to;
        var end = from < to ? to : from;
        if (start == _selectedDate && end == _selectedEndDate) return;

        _selectedDate = start;
        _selectedEndDate = end;
        RenderCalendar();
        RenderSelectedDate();
    }

    private void ShowMonth(int offset)
    {
        _displayMonth = _displayMonth.AddMonths(offset);
        RenderCalendar();
    }

    private void DayGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindDayButton(e.OriginalSource as DependencyObject) is not { Tag: DateTime date }) return;
        e.Handled = true;

        if (e.ClickCount == 2)
        {
            // 더블클릭하면 첫 클릭으로 고른 날짜에 바로 입력할 수 있게 입력칸으로 옮겨 간다.
            Dispatcher.BeginInvoke(() =>
            {
                TaskInput.Focus();
                Keyboard.Focus(TaskInput);
            }, DispatcherPriority.Input);
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            // Shift+클릭: 먼저 고른 날짜부터 누른 날짜까지를 기간으로 고른다.
            SelectRange(_selectionAnchor, date);
            return;
        }

        // 누른 채로 끌면 기간을 고른다. 놓을 때까지 마우스를 붙잡아 칸 밖으로 나가도 따라간다.
        _selectionAnchor = date;
        _isDraggingSelection = true;
        SelectRange(date, date);
        DayGrid.CaptureMouse();
    }

    private void DayGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingSelection) return;

        var hit = DayGrid.InputHitTest(e.GetPosition(DayGrid)) as DependencyObject;
        if (FindDayButton(hit) is { Tag: DateTime date }) SelectRange(_selectionAnchor, date);
    }

    private void DayGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingSelection) return;

        _isDraggingSelection = false;
        DayGrid.ReleaseMouseCapture();

        // 끌지 않고 흐린 앞뒤 달 날짜를 눌렀으면 그 달로 넘어간다.
        if (IsSingleSelection && (_selectedDate.Year != _displayMonth.Year || _selectedDate.Month != _displayMonth.Month))
            SelectDate(_selectedDate);
    }

    private Button? FindDayButton(DependencyObject? element)
    {
        while (element is not null && element != DayGrid)
        {
            if (element is Button button) return button;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    private void DayGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // 칸 너비가 바뀌면 막대 제목을 펼칠 길이도 바뀌므로 다시 그린다.
        var widthChanged = Math.Abs(e.NewSize.Width - e.PreviousSize.Width) > 0.5;
        if (UpdateChipsPerDay() | widthChanged) RenderCalendar();
    }

    /// <summary>칸 높이에 맞춰 날짜 칸 하나에 보여 줄 일정 줄 수를 정합니다. 바뀌었으면 true입니다.</summary>
    private bool UpdateChipsPerDay()
    {
        if (DayGrid.ActualHeight <= 0) return false;

        var cellHeight = DayGrid.ActualHeight / DayGrid.Rows - 4;
        var chips = Math.Max(1, (int)((cellHeight - DayHeaderHeight) / ChipHeight));
        if (chips == _chipsPerDay) return false;

        _chipsPerDay = chips;
        return true;
    }

    private void Calendar_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        ShowMonth(e.Delta > 0 ? -1 : 1);
        e.Handled = true;
    }

    private void PreviousMonth_Click(object sender, RoutedEventArgs e) => ShowMonth(-1);
    private void NextMonth_Click(object sender, RoutedEventArgs e) => ShowMonth(1);
    private void Today_Click(object sender, RoutedEventArgs e) => SelectDate(DateTime.Today);

    private void AddTask_Click(object sender, RoutedEventArgs e) => AddTask();

    private void TaskInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) AddTask();
    }

    private void AddTask()
    {
        var title = TaskInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(title)) return;

        AddTracked(new CalendarTask
        {
            Date = _selectedDate,
            EndDate = IsSingleSelection ? null : _selectedEndDate,
            Title = title
        });
        TaskInput.Clear();
        SaveAndRefresh();
    }

    private void DeleteTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CalendarTask task })
        {
            task.PropertyChanged -= Task_PropertyChanged;
            _tasks.Remove(task);
            SaveAndRefresh();
        }
    }

    private void SaveAndRefresh()
    {
        _taskStore.Save(_tasks);
        RenderCalendar();
        RenderSelectedDate();
    }

    private void TaskTitle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement { DataContext: CalendarTask task })
        {
            task.IsEditing = true;
            e.Handled = true;
        }
    }

    private void TitleEditor_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox { IsVisible: true } editor)
        {
            editor.Text = (editor.DataContext as CalendarTask)?.Title ?? string.Empty;
            Dispatcher.BeginInvoke(() =>
            {
                editor.Focus();
                editor.SelectAll();
            }, DispatcherPriority.Input);
        }
    }

    private void TitleEditor_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: CalendarTask task } editor) return;

        if (e.Key == Key.Enter)
        {
            CommitTitle(editor, task);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            task.IsEditing = false;
            e.Handled = true;
        }
    }

    private void TitleEditor_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox { DataContext: CalendarTask { IsEditing: true } task } editor) CommitTitle(editor, task);
    }

    private static void CommitTitle(TextBox editor, CalendarTask task)
    {
        // 비워 두면 원래 이름을 그대로 둔다.
        var title = editor.Text.Trim();
        if (title.Length > 0) task.Title = title;
        task.IsEditing = false;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DesktopWidgetHost.BeginDrag();
    }

    private void ResizeGrip_DragDelta(object sender, DragDeltaEventArgs e) =>
        DesktopWidgetHost.ResizeBy(e.HorizontalChange, e.VerticalChange);

    private void ResizeGrip_DragCompleted(object sender, DragCompletedEventArgs e) => DesktopWidgetHost.EndResize();

    private void MenuButton_Click(object sender, RoutedEventArgs e)
    {
        AutoStartMenuItem.IsChecked = AutoStart.IsEnabled;
        TransparencySlider.Value = Math.Round(_backgroundTransparency * 100);
        MenuButton.ContextMenu.PlacementTarget = MenuButton;
        MenuButton.ContextMenu.Placement = PlacementMode.Bottom;
        MenuButton.ContextMenu.IsOpen = true;
    }

    private void AutoStartMenuItem_Click(object sender, RoutedEventArgs e) =>
        AutoStart.SetEnabled(AutoStartMenuItem.IsChecked);

    private void ResetBounds_Click(object sender, RoutedEventArgs e) => DesktopWidgetHost.ResetBounds();

    private void TransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // 메뉴를 열 때 현재 값을 맞추는 경우에는 저장하지 않는다.
        var transparency = Math.Round(e.NewValue) / 100;
        if (transparency == _backgroundTransparency) return;

        BackgroundTransparency = transparency;
        BackgroundTransparencyChanged?.Invoke(transparency);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}
