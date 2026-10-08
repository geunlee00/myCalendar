using Microsoft.Win32;

namespace CalendarWidget.Services;

/// <summary>
/// 현재 사용자의 시작 프로그램(레지스트리 Run 키)에 위젯을 등록하거나 뺍니다.
/// </summary>
internal static class AutoStart
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CalendarWidget";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
