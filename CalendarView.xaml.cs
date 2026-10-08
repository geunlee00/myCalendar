using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
    private static readonly Brush MovePreviewBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xE1, 0xEA, 0xF5)));
    private static readonly Brush DoneChipBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF4, 0xF0, 0xE8)));
    private static readonly Brush DoneTextBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xA8, 0xA0, 0x90)));
    private static readonly Brush GridLineBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xE6, 0xDD, 0xCB)));
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

    /// <summary>메모장 자리에 무엇을 보여 주는지.</summary>
    private enum MemoMode
    {
        List,
        Edit,
        Search
    }

    /// <summary>검색 결과 한 줄. 누르면 Date로 이동합니다.</summary>
    public sealed record SearchResult(DateTime Date, string DateText, string Title, Brush DotBrush);

    private readonly TaskStore _taskStore = new();
    private readonly ObservableCollection<CalendarTask> _tasks = [];
    private readonly ListCollectionView _selectedDayTasks;
    private readonly DispatcherTimer _dayChangeTimer = new() { Interval = TimeSpan.FromMinutes(1) };

    // 켠 뒤 잠시 기다렸다가 한 번, 그 뒤로는 하루에 한 번 새 버전을 확인한다.
    private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private ReleaseInfo? _availableUpdate;
    private bool _checkingUpdate;
    private string? _updateMenuOverride;

    private DateTime _displayMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    // 고른 기간. 하루만 골랐으면 시작과 끝이 같다. 편집 중에는 편집하는 일정의 날짜가 된다.
    private DateTime _selectedDate = DateTime.Today;
    private DateTime _selectedEndDate = DateTime.Today;

    // 끌거나 Shift+클릭으로 기간을 고를 때 기준이 되는 날짜와, 끄는 중인지 여부.
    private DateTime _selectionAnchor = DateTime.Today;
    private bool _isDraggingSelection;

    // 일정을 끌어서 옮기는 중이면 그 회차와, 잡은 날짜, 지금 마우스가 있는 날짜.
    private Occurrence? _moving;
    private DateTime _moveGrabDate;
    private DateTime _moveTargetDate;

    // 편집 중인 일정과 아직 저장하지 않은 반복·색 선택.
    private MemoMode _mode = MemoMode.List;
    private CalendarTask? _editingTask;
    private Recurrence _draftRecurrence;
    private TaskColor _draftColor;

    // 여러 날 일정 회차마다 날짜 칸 안에서 차지하는 줄 번호. 날마다 같은 줄에 그려야 막대가 이어진다.
    private Dictionary<(CalendarTask Task, DateTime Start), int> _lanes = [];
    private List<Occurrence> _visibleOccurrences = [];
    private double _cellWidth = 70;
    private DateTime _renderedToday = DateTime.Today;
    private int _chipsPerDay = 2;
    private double _backgroundTransparency;
    private bool _showHolidays = true;

    /// <summary>배경 투명도(0 = 불투명, 1 = 투명)를 사용자가 바꾸면 알려 줍니다.</summary>
    public event Action<double>? BackgroundTransparencyChanged;

    /// <summary>사용자가 메뉴에서 언어를 바꾸면 알려 줍니다.</summary>
    public event Action<AppLanguage>? LanguageChanged;

    /// <summary>사용자가 메뉴에서 공휴일 표시를 켜거나 끄면 알려 줍니다.</summary>
    public event Action<bool>? ShowHolidaysChanged;

    /// <summary>
    /// 대한민국 공휴일을 빨간 날로 표시할지. 다른 나라 사용자는 끌 수 있습니다.
    /// </summary>
    public bool ShowHolidays
    {
        get => _showHolidays;
        set
        {
            if (_showHolidays == value) return;
            _showHolidays = value;
            RenderCalendar();
            RenderSelectedDate();
        }
    }

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

            // 입력칸 밑줄은 연하게라도 남겨 위치가 보이게 하고, 남색 버튼 글자는 배경이 절반 넘게 남아 있으면 흰색,
            // 그보다 옅으면 남색으로 바꿔 계속 읽히게 한다.
            var underline = new SolidColorBrush(Color.FromArgb((byte)Math.Round(255 * Math.Max(0.45, opacity)), 0xC9, 0xB9, 0x98));
            TaskInput.BorderBrush = underline;
            EditorTitle.BorderBrush = underline;
            SearchInput.BorderBrush = underline;
            foreach (var button in new[] { AddButton, EditorSave })
            {
                button.Background = new SolidColorBrush(Color.FromArgb(backgroundAlpha, InkColor.R, InkColor.G, InkColor.B));
                button.Foreground = opacity >= 0.55 ? Brushes.White : InkBrush;
            }

            // 효과를 켜면 글자가 살짝 흐려지므로 불투명할 때는 아예 끈다.
            TextGlow.Opacity = _backgroundTransparency;
            ContentGrid.Effect = _backgroundTransparency > 0 ? TextGlow : null;
            TransparencyText.Text = Loc.T("Menu.Transparency", Math.Round(_backgroundTransparency * 100));
        }
    }

    public CalendarView()
    {
        InitializeComponent();
        BackgroundTransparency = 0;
        foreach (var task in _taskStore.Load()) AddTracked(task);

        // 목록에는 고른 날짜(기간)에 걸친 일정만, 안 끝낸 일을 먼저 날짜·입력 순서대로 보여 준다.
        _selectedDayTasks = (ListCollectionView)CollectionViewSource.GetDefaultView(_tasks);
        _selectedDayTasks.Filter = item =>
            item is CalendarTask task && task.OccurrencesBetween(_selectedDate, _selectedEndDate).Any();
        _selectedDayTasks.CustomSort = Comparer<object>.Create((a, b) => CompareTasks((CalendarTask)a, (CalendarTask)b));
        TaskList.ItemsSource = _selectedDayTasks;

        ApplyLanguage();
        RenderCalendar();
        RenderSelectedDate();

        // 위젯은 며칠씩 켜 두므로 날짜가 바뀌면 오늘 표시와 D-day를 다시 그린다.
        _dayChangeTimer.Tick += DayChangeTimer_Tick;
        _dayChangeTimer.Start();

        _updateTimer.Tick += async (_, _) =>
        {
            _updateTimer.Interval = TimeSpan.FromDays(1);
            await CheckForUpdateAsync(userRequested: false);
        };
        _updateTimer.Start();

        Loc.Changed += OnLanguageChanged;
        Application.Current.Exit += (_, _) => _taskStore.Save(_tasks);
    }

    #region 언어

    /// <summary>화면에 고정으로 쓰인 문구를 지금 언어로 다시 씁니다.</summary>
    private void ApplyLanguage()
    {
        SearchButton.ToolTip = Loc.T("Header.SearchTip");
        PrevButton.ToolTip = Loc.T("Header.PrevTip");
        NextButton.ToolTip = Loc.T("Header.NextTip");
        TodayButton.Content = Loc.T("Header.Today");
        TodayButton.ToolTip = Loc.T("Header.TodayTip");
        MenuButton.ToolTip = Loc.T("Header.MenuTip");
        CloseButton.ToolTip = Loc.T("Header.CloseTip");
        ResizeGrip.ToolTip = Loc.T("Header.ResizeTip");
        UpdateBadge.Content = Loc.T("Update.Badge");
        if (_availableUpdate is not null) UpdateBadge.ToolTip = Loc.T("Update.BadgeTip", _availableUpdate.Version);

        AutoStartMenuItem.Header = Loc.T("Menu.AutoStart");
        ResetBoundsMenuItem.Header = Loc.T("Menu.ResetBounds");
        HolidaysMenuItem.Header = Loc.T("Menu.Holidays");
        LanguageMenuItem.Header = Loc.T("Menu.Language");
        KoreanMenuItem.IsChecked = Loc.Current == AppLanguage.Korean;
        EnglishMenuItem.IsChecked = Loc.Current == AppLanguage.English;
        QuitMenuItem.Header = Loc.T("Menu.Quit");
        TransparencyText.Text = Loc.T("Menu.Transparency", Math.Round(_backgroundTransparency * 100));
        _updateMenuOverride = null;
        RenderUpdateMenu();

        WeekdayHeader.Children.Clear();
        var weekdays = Loc.T("Weekdays").Split(',');
        for (var day = 0; day < 7; day++)
        {
            WeekdayHeader.Children.Add(new TextBlock
            {
                Text = weekdays[day],
                FontWeight = FontWeights.SemiBold,
                Foreground = day == 0 ? RedBrush : day == 6 ? BlueBrush : InkBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        EmptyListText.Text = Loc.T("Memo.Empty");
        AddButton.Content = Loc.T("Memo.Add");

        EditorDateLabel.Text = Loc.T("Editor.Date");
        EditorDateHint.Text = Loc.T("Editor.DateHint");
        EditorRepeatLabel.Text = Loc.T("Editor.Repeat");
        EditorColorLabel.Text = Loc.T("Editor.Color");
        EditorDday.Content = Loc.T("Editor.Dday");
        EditorDelete.Content = Loc.T("Editor.Delete");
        EditorCancel.Content = Loc.T("Editor.Cancel");
        EditorSave.Content = Loc.T("Editor.Save");
        RenderEditorOptions();

        SearchHint.Text = Loc.T("Search.Hint");
        SearchEmptyText.Text = Loc.T("Search.Empty");
    }

    private void OnLanguageChanged()
    {
        ApplyLanguage();
        foreach (var task in _tasks) task.RefreshLabels();
        RenderCalendar();
        RenderSelectedDate();
        if (_mode == MemoMode.Search) RunSearch();
    }

    private void KoreanMenuItem_Click(object sender, RoutedEventArgs e) => ChangeLanguage(AppLanguage.Korean);
    private void EnglishMenuItem_Click(object sender, RoutedEventArgs e) => ChangeLanguage(AppLanguage.English);

    private void ChangeLanguage(AppLanguage language)
    {
        MenuButton.ContextMenu.IsOpen = false;
        Loc.Set(language);
        KoreanMenuItem.IsChecked = Loc.Current == AppLanguage.Korean;
        EnglishMenuItem.IsChecked = Loc.Current == AppLanguage.English;
        LanguageChanged?.Invoke(language);
    }

    #endregion

    #region 일정 목록

    private int CompareTasks(CalendarTask a, CalendarTask b)
    {
        var byCompletion = a.IsCompleted.CompareTo(b.IsCompleted);
        if (byCompletion != 0) return byCompletion;
        var byStart = a.Date.CompareTo(b.Date);
        return byStart != 0 ? byStart : _tasks.IndexOf(a).CompareTo(_tasks.IndexOf(b));
    }

    private void AddTracked(CalendarTask task)
    {
        task.PropertyChanged += Task_PropertyChanged;
        _tasks.Add(task);
    }

    private void Task_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 설명 문구는 다른 값에서 계산되는 화면용이라 저장할 필요가 없다.
        if (e.PropertyName is nameof(CalendarTask.ScheduleLabel) or nameof(CalendarTask.DdayLabel)) return;

        _taskStore.Save(_tasks);

        // 체크박스 클릭 처리가 끝난 뒤에 목록 순서와 날짜 칸, D-day를 다시 그린다.
        Dispatcher.BeginInvoke(() =>
        {
            RenderCalendar();
            RenderSelectedDate();
        }, DispatcherPriority.Background);
    }

    private void DayChangeTimer_Tick(object? sender, EventArgs e)
    {
        if (DateTime.Today == _renderedToday) return;

        // 어제(이전의 오늘)를 보고 있었다면 새 오늘로 따라간다.
        if (_mode == MemoMode.List && _selectedDate == _renderedToday && _selectedEndDate == _renderedToday)
        {
            _selectedDate = _selectedEndDate = _selectionAnchor = DateTime.Today;
            _displayMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        }
        _renderedToday = DateTime.Today;
        foreach (var task in _tasks) task.RefreshLabels();
        RenderCalendar();
        RenderSelectedDate();
    }

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
        if (sender is Button { Tag: CalendarTask task }) RemoveTask(task);
    }

    private void RemoveTask(CalendarTask task)
    {
        task.PropertyChanged -= Task_PropertyChanged;
        _tasks.Remove(task);
        SaveAndRefresh();
    }

    private void SaveAndRefresh()
    {
        _taskStore.Save(_tasks);
        RenderCalendar();
        RenderSelectedDate();
    }

    private void EditTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CalendarTask task }) OpenEditor(task);
    }

    private void TaskTitle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement { DataContext: CalendarTask task })
        {
            OpenEditor(task);
            e.Handled = true;
        }
    }

    #endregion

    #region 달력 그리기

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

    private void RenderCalendar()
    {
        MonthNumberText.Text = _displayMonth.Month.ToString();
        MonthNameText.Text = string.Join(" ", _displayMonth.ToString("MMMM", English).ToUpperInvariant().ToCharArray());
        YearText.Text = Loc.Date(_displayMonth, "Fmt.Year");
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
        _visibleOccurrences = _tasks.SelectMany(task => task.OccurrencesBetween(gridStart, gridEnd)).ToList();
        AssignLanes();

        for (var cell = 0; cell < DayGrid.Rows * 7; cell++)
        {
            var gridCell = CreateGridCell(CreateDayButton(gridStart.AddDays(cell)));

            // 막대 제목이 오른쪽 칸까지 펼쳐지므로 왼쪽 칸을 위에 그린다.
            Panel.SetZIndex(gridCell, 6 - cell % 7);
            DayGrid.Children.Add(gridCell);
        }
    }

    /// <summary>
    /// 보이는 여러 날 일정 회차에, 서로 겹치지 않는 가장 위쪽 줄 번호를 매깁니다.
    /// 먼저 시작하고 긴 일정부터 위쪽 줄을 차지합니다.
    /// </summary>
    private void AssignLanes()
    {
        _lanes = [];
        var laneEnds = new List<DateTime>();
        var multiDay = _visibleOccurrences
            .Where(o => o.IsMultiDay)
            .OrderBy(o => o.Start)
            .ThenByDescending(o => o.End)
            .ThenBy(o => _tasks.IndexOf(o.Task));
        foreach (var occurrence in multiDay)
        {
            var lane = laneEnds.FindIndex(end => end < occurrence.Start);
            if (lane < 0)
            {
                lane = laneEnds.Count;
                laneEnds.Add(occurrence.End);
            }
            else
            {
                laneEnds[lane] = occurrence.End;
            }
            _lanes[(occurrence.Task, occurrence.Start)] = lane;
        }
    }

    /// <summary>공휴일 표시를 켰을 때만 그날의 공휴일 이름을 돌려줍니다.</summary>
    private string? HolidayName(DateTime date) => _showHolidays ? KoreanHolidays.GetName(date) : null;

    private Button CreateDayButton(DateTime date)
    {
        var holiday = HolidayName(date);
        var occurrences = _visibleOccurrences
            .Where(o => o.Start <= date && o.End >= date)
            .OrderBy(o => o.Task.IsCompleted)
            .ThenBy(o => _tasks.IndexOf(o.Task))
            .ToList();
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
        var rows = new FrameworkElement?[_chipsPerDay];
        var hidden = 0;
        foreach (var occurrence in occurrences.Where(o => o.IsMultiDay))
        {
            var lane = _lanes.GetValueOrDefault((occurrence.Task, occurrence.Start), int.MaxValue);
            if (lane < rows.Length) rows[lane] = CreateRangeSegment(occurrence, date);
            else hidden++;
        }
        foreach (var occurrence in occurrences.Where(o => !o.IsMultiDay))
        {
            var free = Array.IndexOf(rows, null);
            if (free >= 0) rows[free] = CreateChip(occurrence);
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
        if (IsSingleSelection && date == _selectedDate && !IsMovePreview(date))
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
        tooltipLines.AddRange(occurrences.Select(o =>
            (o.Task.IsRecurring ? "↻ " : o.Task.IsCompleted ? "✓ " : "• ") + o.Task.Title +
            (o.Task.ScheduleLabel is { } label ? $" ({label})" : string.Empty)));

        return new Button
        {
            Content = content,
            Style = (Style)FindResource("DayButtonStyle"),
            Tag = date,
            Background = IsMovePreview(date) ? MovePreviewBrush : IsSelected(date) ? SelectedBrush : Brushes.Transparent,
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

    /// <summary>일정을 끌어서 옮기는 중일 때, 놓으면 일정이 들어갈 날짜인지.</summary>
    private bool IsMovePreview(DateTime date)
    {
        if (_moving is not { } moving) return false;
        var offset = (_moveTargetDate - _moveGrabDate).Days;
        return date >= moving.Start.AddDays(offset) && date <= moving.End.AddDays(offset);
    }

    /// <summary>
    /// 여러 날 일정 막대 중 하루치 조각을 만듭니다. 같은 주 안에서 앞뒤 날짜로 이어지는 쪽은
    /// 모서리를 펴고 칸 끝까지 늘려 막대가 끊기지 않게 보이게 합니다.
    /// </summary>
    private FrameworkElement CreateRangeSegment(Occurrence occurrence, DateTime date)
    {
        var task = occurrence.Task;
        var done = task.IsCompleted && !task.IsRecurring;
        var fromLeft = date > occurrence.Start && date.DayOfWeek != DayOfWeek.Sunday;
        var toRight = date < occurrence.End && date.DayOfWeek != DayOfWeek.Saturday;
        var bar = new Border
        {
            Tag = occurrence,
            Background = done ? DoneChipBrush : TaskPalette.BarBackground(task.Color),
            Height = ChipHeight - 1,
            // 칸 안쪽 여백(5)과 버튼 여백(2), 오른쪽 격자선(1)까지 덮는다.
            Margin = new Thickness(fromLeft ? -7 : 0, 1, toRight ? -8 : 0, 0),
            CornerRadius = new CornerRadius(fromLeft ? 0 : 4, toRight ? 0 : 4, toRight ? 0 : 4, fromLeft ? 0 : 4),
            Cursor = Cursors.SizeAll
        };

        // 제목은 막대가 시작하는 날(또는 주가 바뀐 첫날)에만 쓰고, 이번 주 안에서 막대가 이어지는 길이만큼 펼친다.
        if (!fromLeft)
        {
            var weekEnd = date.AddDays(6 - (int)date.DayOfWeek);
            var spanDays = ((occurrence.End < weekEnd ? occurrence.End : weekEnd) - date).Days + 1;
            var title = new TextBlock
            {
                Text = task.Title,
                FontSize = 10.5,
                Foreground = done ? DoneTextBrush : TaskPalette.BarText(task.Color),
                TextDecorations = done ? TextDecorations.Strikethrough : null,
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

    private static Border CreateChip(Occurrence occurrence)
    {
        var task = occurrence.Task;
        var done = task.IsCompleted && !task.IsRecurring;
        return new Border
        {
            Tag = occurrence,
            Background = done ? DoneChipBrush : TaskPalette.ChipBackground(task.Color),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4, 0, 4, 0),
            Margin = new Thickness(0, 1, 0, 0),
            Height = ChipHeight - 1,
            Cursor = Cursors.SizeAll,
            Child = new TextBlock
            {
                Text = task.Title,
                FontSize = 10.5,
                Foreground = done ? DoneTextBrush : TaskPalette.ChipText(task.Color),
                TextDecorations = done ? TextDecorations.Strikethrough : null,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    private static Brush GetDayColor(DateTime date, string? holiday)
    {
        if (holiday is not null || date.DayOfWeek == DayOfWeek.Sunday) return RedBrush;
        return date.DayOfWeek == DayOfWeek.Saturday ? BlueBrush : InkBrush;
    }

    /// <summary>메모장 머리(고른 날짜·편집·검색 제목, D-day)와 메모장 내용을 지금 상태에 맞게 씁니다.</summary>
    private void RenderSelectedDate()
    {
        SelectedHolidayText.Text = string.Empty;
        switch (_mode)
        {
            case MemoMode.Edit:
                SelectedDateText.Text = Loc.T("Editor.Title");
                EditorDateText.Text = DescribeSelection();
                break;
            case MemoMode.Search:
                SelectedDateText.Text = Loc.T("Search.Title");
                break;
            default:
                SelectedDateText.Text = DescribeSelection();
                if (IsSingleSelection)
                {
                    SelectedHolidayText.Text = HolidayName(_selectedDate) ?? string.Empty;
                    SelectedHolidayText.Foreground = RedBrush;
                    InputHint.Text = Loc.T("Memo.Hint");
                }
                else
                {
                    SelectedHolidayText.Text = Loc.T("Memo.Days", (_selectedEndDate - _selectedDate).Days + 1);
                    SelectedHolidayText.Foreground = SubInkBrush;
                    InputHint.Text = Loc.T("Memo.RangeHint", _selectedDate.ToString("M'/'d"), _selectedEndDate.ToString("M'/'d"));
                }
                break;
        }

        _selectedDayTasks.Refresh();
        EmptyListText.Visibility = _selectedDayTasks.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        RenderDdays();
    }

    private string DescribeSelection() => IsSingleSelection
        ? Loc.Date(_selectedDate, "Fmt.Day")
        : $"{Loc.Date(_selectedDate, "Fmt.Day")} ~ {Loc.Date(_selectedEndDate, "Fmt.Day")}";

    /// <summary>D-day로 표시한 일정 중 가장 가까운 두 개를 메모장 머리 오른쪽에 보여 줍니다.</summary>
    private void RenderDdays()
    {
        var upcoming = _tasks
            .Where(t => t.IsDday)
            .Select(t => (Task: t, Next: t.NextOccurrence(DateTime.Today)))
            .Where(x => x.Next is not null)
            .OrderBy(x => x.Next!.Value.Start)
            .Take(2)
            .Select(x => $"{x.Task.DdayLabel} {x.Task.Title}");
        DdayText.Text = string.Join("  ·  ", upcoming);
    }

    #endregion

    #region 달력 마우스: 고르기, 기간 고르기, 일정 옮기기

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
        var source = e.OriginalSource as DependencyObject;
        if (FindDayButton(source) is not { Tag: DateTime date }) return;
        e.Handled = true;

        // 검색 중에 날짜를 누르면 검색을 닫고 목록으로 돌아간다.
        if (_mode == MemoMode.Search) ShowMemoMode(MemoMode.List);

        var occurrence = FindOccurrence(source);
        if (e.ClickCount == 2)
        {
            if (_mode == MemoMode.Edit) return;

            // 일정을 더블클릭하면 편집하고, 빈 곳을 더블클릭하면 그 날짜에 바로 입력할 수 있게 입력칸으로 간다.
            if (occurrence is { } clicked)
            {
                OpenEditor(clicked.Task);
                return;
            }
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

        // 편집 중이 아니면 일정을 누른 채 끌어 다른 날짜로 옮길 수 있다. 놓을 때까지 마우스를 붙잡는다.
        if (occurrence is { } grabbed && _mode != MemoMode.Edit)
        {
            _moving = grabbed;
            _moveGrabDate = _moveTargetDate = date;
            DayGrid.CaptureMouse();
            return;
        }

        // 빈 곳을 누른 채로 끌면 기간을 고른다.
        _selectionAnchor = date;
        _isDraggingSelection = true;
        SelectRange(date, date);
        DayGrid.CaptureMouse();
    }

    private void DayGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (_moving is null && !_isDraggingSelection) return;

        var hit = DayGrid.InputHitTest(e.GetPosition(DayGrid)) as DependencyObject;
        if (FindDayButton(hit) is not { Tag: DateTime date }) return;

        if (_moving is not null)
        {
            if (date == _moveTargetDate) return;
            _moveTargetDate = date;
            RenderCalendar();
            return;
        }
        SelectRange(_selectionAnchor, date);
    }

    private void DayGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_moving is { } moving)
        {
            _moving = null;
            DayGrid.ReleaseMouseCapture();
            var offset = (_moveTargetDate - _moveGrabDate).Days;
            if (offset == 0)
            {
                // 끌지 않았으면 그냥 날짜를 누른 것과 같다.
                SelectDate(_moveGrabDate);
                return;
            }

            // 옮긴 일정의 첫날을 고른 채로 둔다. 보고 있는 달은 그대로 둔다.
            moving.Task.MoveBy(offset);
            _selectedDate = _selectedEndDate = _selectionAnchor = moving.Start.AddDays(offset);
            RenderCalendar();
            RenderSelectedDate();
            return;
        }

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

    /// <summary>누른 곳이 날짜 칸 안의 일정 칩이나 막대면 그 회차를 돌려줍니다.</summary>
    private Occurrence? FindOccurrence(DependencyObject? element)
    {
        while (element is not null && element != DayGrid)
        {
            if (element is FrameworkElement { Tag: Occurrence occurrence }) return occurrence;
            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
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

    #endregion

    #region 메모장 모드: 목록 / 편집 / 검색

    private void ShowMemoMode(MemoMode mode)
    {
        _mode = mode;
        ListPanel.Visibility = mode == MemoMode.List ? Visibility.Visible : Visibility.Collapsed;
        EditorPanel.Visibility = mode == MemoMode.Edit ? Visibility.Visible : Visibility.Collapsed;
        SearchPanel.Visibility = mode == MemoMode.Search ? Visibility.Visible : Visibility.Collapsed;
        if (mode != MemoMode.Edit) _editingTask = null;
        RenderSelectedDate();
    }

    /// <summary>
    /// 일정 편집을 엽니다. 편집하는 동안 달력의 선택이 그 일정의 날짜가 되어, 달력을 누르거나 끌면 날짜가 바뀝니다.
    /// </summary>
    private void OpenEditor(CalendarTask task)
    {
        _editingTask = task;
        _draftRecurrence = task.Recurrence;
        _draftColor = task.Color;
        EditorTitle.Text = task.Title;
        EditorDday.IsChecked = task.IsDday;

        _selectedDate = _selectionAnchor = task.Date;
        _selectedEndDate = task.LastDate;
        _displayMonth = new DateTime(task.Date.Year, task.Date.Month, 1);

        ShowMemoMode(MemoMode.Edit);
        _editingTask = task;
        RenderEditorOptions();
        RenderCalendar();
        Dispatcher.BeginInvoke(() =>
        {
            EditorTitle.Focus();
            EditorTitle.SelectAll();
        }, DispatcherPriority.Input);
    }

    /// <summary>반복 선택 알약과 색 동그라미를 지금 고른 값에 맞게 다시 그립니다.</summary>
    private void RenderEditorOptions()
    {
        RepeatOptions.Children.Clear();
        foreach (var recurrence in Enum.GetValues<Recurrence>())
        {
            var selected = recurrence == _draftRecurrence;
            var pill = new Border
            {
                Background = selected ? InkBrush : Brushes.Transparent,
                BorderBrush = selected ? InkBrush : SelectedEdgeBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(9, 1, 9, 1),
                Margin = new Thickness(0, 0, 5, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = Loc.T("Repeat." + recurrence),
                    FontSize = 11.5,
                    Foreground = selected ? Brushes.White : InkBrush
                }
            };
            pill.MouseLeftButtonUp += (_, _) =>
            {
                _draftRecurrence = recurrence;
                RenderEditorOptions();
            };
            RepeatOptions.Children.Add(pill);
        }

        ColorOptions.Children.Clear();
        foreach (var color in Enum.GetValues<TaskColor>())
        {
            var selected = color == _draftColor;
            var swatch = new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(9),
                Background = TaskPalette.Dot(color),
                BorderBrush = InkBrush,
                BorderThickness = new Thickness(selected ? 2 : 0),
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            swatch.MouseLeftButtonUp += (_, _) =>
            {
                _draftColor = color;
                RenderEditorOptions();
            };
            ColorOptions.Children.Add(swatch);
        }
    }

    private void SaveEditor()
    {
        if (_editingTask is not { } task) return;

        // 제목을 비우면 원래 제목을 그대로 둔다.
        var title = EditorTitle.Text.Trim();
        task.Update(
            title.Length > 0 ? title : task.Title,
            _selectedDate,
            _selectedEndDate,
            _draftRecurrence,
            _draftColor,
            EditorDday.IsChecked == true);
        CloseEditor();
    }

    private void CloseEditor()
    {
        ShowMemoMode(MemoMode.List);
        RenderCalendar();
    }

    private void EditorSave_Click(object sender, RoutedEventArgs e) => SaveEditor();
    private void EditorCancel_Click(object sender, RoutedEventArgs e) => CloseEditor();

    private void EditorDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_editingTask is { } task)
        {
            ShowMemoMode(MemoMode.List);
            RemoveTask(task);
        }
    }

    private void EditorTitle_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SaveEditor();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseEditor();
            e.Handled = true;
        }
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        if (_mode == MemoMode.Search)
        {
            ShowMemoMode(MemoMode.List);
            return;
        }

        ShowMemoMode(MemoMode.Search);
        SearchInput.Clear();
        RunSearch();
        Dispatcher.BeginInvoke(() =>
        {
            SearchInput.Focus();
            Keyboard.Focus(SearchInput);
        }, DispatcherPriority.Input);
    }

    private void SearchInput_TextChanged(object sender, TextChangedEventArgs e) => RunSearch();

    private void SearchInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ShowMemoMode(MemoMode.List);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && SearchResults.Items.Count > 0)
        {
            GoToSearchResult((SearchResult)SearchResults.Items[0]);
            e.Handled = true;
        }
    }

    /// <summary>
    /// 이름에 검색어가 들어간 일정을 찾습니다. 앞으로 올 일정을 가까운 순서로 먼저, 지난 일정을 최근 순서로 뒤에 보여 줍니다.
    /// 반복 일정은 다음 회차 날짜를 보여 줍니다.
    /// </summary>
    private void RunSearch()
    {
        var query = SearchInput.Text.Trim();
        var today = DateTime.Today;
        var results = query.Length == 0
            ? []
            : _tasks
                .Where(t => t.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                .Select(t => t.NextOccurrence(today) ?? new Occurrence(t, t.Date, t.LastDate))
                .OrderBy(o => o.End < today)
                .ThenBy(o => o.End < today ? -o.Start.Ticks : o.Start.Ticks)
                .Select(o => new SearchResult(
                    o.Start,
                    Loc.Date(o.Start, "Fmt.SearchDate"),
                    o.Task.Title,
                    TaskPalette.Dot(o.Task.Color)))
                .ToList();
        SearchResults.ItemsSource = results;
        SearchEmptyText.Visibility = query.Length > 0 && results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SearchResults_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var element = e.OriginalSource as DependencyObject;
        while (element is not null && element is not ListBoxItem) element = VisualTreeHelper.GetParent(element);
        if (element is ListBoxItem { DataContext: SearchResult result }) GoToSearchResult(result);
    }

    private void GoToSearchResult(SearchResult result)
    {
        ShowMemoMode(MemoMode.List);
        SelectDate(result.Date);
    }

    #endregion

    #region 위젯 창: 옮기기, 크기, 메뉴, 업데이트

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
        HolidaysMenuItem.IsChecked = _showHolidays;
        TransparencySlider.Value = Math.Round(_backgroundTransparency * 100);
        _updateMenuOverride = null;
        RenderUpdateMenu();
        MenuButton.ContextMenu.PlacementTarget = MenuButton;
        MenuButton.ContextMenu.Placement = PlacementMode.Bottom;
        MenuButton.ContextMenu.IsOpen = true;
    }

    private void AutoStartMenuItem_Click(object sender, RoutedEventArgs e) =>
        AutoStart.SetEnabled(AutoStartMenuItem.IsChecked);

    private void ResetBounds_Click(object sender, RoutedEventArgs e) => DesktopWidgetHost.ResetBounds();

    private void HolidaysMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ShowHolidays = HolidaysMenuItem.IsChecked;
        ShowHolidaysChanged?.Invoke(ShowHolidays);
    }

    /// <summary>메뉴의 업데이트 항목 문구를 씁니다. 직접 확인한 결과가 있으면 그 결과를 보여 줍니다.</summary>
    private void RenderUpdateMenu()
    {
        UpdateMenuItem.Header = _updateMenuOverride ?? (_availableUpdate is not null
            ? Loc.T("Update.Available", _availableUpdate.Version, UpdateChecker.CurrentVersion)
            : Loc.T("Update.Check", UpdateChecker.CurrentVersion));
    }

    /// <summary>
    /// GitHub에 최신 버전을 물어보고, 새 버전이 있으면 머리 부분에 "업데이트" 표시를 띄웁니다.
    /// 사용자가 메뉴에서 직접 확인한 경우에만 "최신 버전" 또는 "확인 실패" 결과를 메뉴에 보여 줍니다.
    /// </summary>
    private async Task CheckForUpdateAsync(bool userRequested)
    {
        if (_checkingUpdate) return;
        _checkingUpdate = true;
        if (userRequested)
        {
            _updateMenuOverride = Loc.T("Update.Checking");
            RenderUpdateMenu();
        }

        var latest = await UpdateChecker.GetLatestReleaseAsync();
        _checkingUpdate = false;

        if (latest is not null && latest.Version > UpdateChecker.CurrentVersion)
        {
            _availableUpdate = latest;
            _updateMenuOverride = null;
            UpdateBadge.Visibility = Visibility.Visible;
            UpdateBadge.ToolTip = Loc.T("Update.BadgeTip", latest.Version);
        }
        else if (userRequested)
        {
            _updateMenuOverride = latest is null
                ? Loc.T("Update.Failed")
                : Loc.T("Update.Latest", UpdateChecker.CurrentVersion);
        }
        RenderUpdateMenu();
    }

    private async void UpdateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_availableUpdate is not null)
        {
            MenuButton.ContextMenu.IsOpen = false;
            OpenReleasePage();
            return;
        }
        await CheckForUpdateAsync(userRequested: true);
    }

    private void UpdateBadge_Click(object sender, RoutedEventArgs e) => OpenReleasePage();

    private void OpenReleasePage()
    {
        if (_availableUpdate is null) return;
        Process.Start(new ProcessStartInfo(_availableUpdate.Url) { UseShellExecute = true });
    }

    private void TransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // 메뉴를 열 때 현재 값을 맞추는 경우에는 저장하지 않는다.
        var transparency = Math.Round(e.NewValue) / 100;
        if (transparency == _backgroundTransparency) return;

        BackgroundTransparency = transparency;
        BackgroundTransparencyChanged?.Invoke(transparency);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    #endregion
}
