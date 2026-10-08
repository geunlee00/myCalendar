using System.IO;
using System.Text.Json;

namespace CalendarWidget.Services;

/// <summary>
/// %LOCALAPPDATA%\CalendarWidget 폴더의 JSON 파일을 읽고 씁니다.
/// </summary>
internal static class AppDataFile
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CalendarWidget");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static T? Read<T>(string fileName)
    {
        var path = Path.Combine(Folder, fileName);
        if (!File.Exists(path)) return default;

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
        }
        catch
        {
            // 읽지 못한 파일을 다음 저장 때 덮어쓰지 않도록 따로 보관한다.
            try
            {
                File.Move(path, $"{path}.broken-{DateTime.Now:yyyyMMdd-HHmmss}");
            }
            catch
            {
            }
            return default;
        }
    }

    public static void Write<T>(string fileName, T value)
    {
        Directory.CreateDirectory(Folder);
        var path = Path.Combine(Folder, fileName);

        // 임시 파일에 다 쓴 뒤 바꿔치기해서, 저장 도중 꺼져도 기존 파일이 깨지지 않게 한다.
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(value, JsonOptions));
        File.Move(tempPath, path, overwrite: true);
    }
}
