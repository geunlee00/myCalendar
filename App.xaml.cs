using System.Windows;
using System.Windows.Threading;
using CalendarWidget.Services;

namespace CalendarWidget;

public partial class App : Application
{
    private static readonly Size DefaultWidgetSize = new(540, 740);
    private static readonly Size MinWidgetSize = new(380, 540);

    private readonly DispatcherTimer _attachTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly SettingsStore _settingsStore = new();
    private AppSettings _settings = new();
    private Mutex? _singleInstance;
    private CalendarView? _view;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 이미 실행 중이면 달력이 두 개 겹치지 않도록 바로 끝낸다.
        _singleInstance = new Mutex(true, @"Local\CalendarWidget", out var isFirstInstance);
        if (!isFirstInstance)
        {
            Shutdown();
            return;
        }

        _settings = _settingsStore.Load();
        if (Loc.FromCode(_settings.Language) is { } language) Loc.Set(language);
        if (!_settings.AutoStartInitialized)
        {
            // 처음 실행할 때 한 번만 자동 실행을 켠다. 이후에는 메뉴에서 사용자가 정한 대로 둔다.
            AutoStart.SetEnabled(true);
            _settings.AutoStartInitialized = true;
            _settingsStore.Save(_settings);
        }

        DesktopWidgetHost.BoundsChanged += bounds =>
        {
            _settings.Bounds = bounds;
            _settingsStore.Save(_settings);
        };

        _view = new CalendarView { BackgroundTransparency = _settings.BackgroundTransparency };
        _view.BackgroundTransparencyChanged += transparency =>
        {
            _settings.BackgroundTransparency = transparency;
            _settingsStore.Save(_settings);
        };
        _view.ShowHolidays = _settings.ShowKoreanHolidays ?? Loc.Current == AppLanguage.Korean;
        _view.ShowHolidaysChanged += show =>
        {
            _settings.ShowKoreanHolidays = show;
            _settingsStore.Save(_settings);
        };
        _view.PageFlipEnabled = _settings.PageFlipAnimation ?? true;
        _view.PageFlipEnabledChanged += enabled =>
        {
            _settings.PageFlipAnimation = enabled;
            _settingsStore.Save(_settings);
        };
        _view.LanguageChanged += language =>
        {
            _settings.Language = Loc.ToCode(language);
            _settingsStore.Save(_settings);
        };
        _attachTimer.Tick += (_, _) => TryShowWidget();
        TryShowWidget();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _exiting = true;
        base.OnExit(e);
    }

    /// <summary>
    /// 바탕화면이 준비돼 있으면 위젯을 붙이고, 아니면 준비될 때까지 2초마다 다시 시도합니다.
    /// 탐색기가 다시 시작되면 바탕화면과 함께 위젯 창도 사라지므로 같은 방법으로 다시 붙입니다.
    /// </summary>
    private void TryShowWidget()
    {
        if (!DesktopWidgetHost.IsDesktopReady)
        {
            _attachTimer.Start();
            return;
        }

        _attachTimer.Stop();
        var source = DesktopWidgetHost.Show(_view!, DefaultWidgetSize, MinWidgetSize, _settings.Bounds);
        source.Disposed += (_, _) =>
        {
            if (!_exiting) _attachTimer.Start();
        };
    }
}
