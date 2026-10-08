using System.Net.Http;
using System.Text.Json;

namespace CalendarWidget.Services;

/// <summary>GitHub 릴리스의 최신 버전과 그 페이지 주소입니다.</summary>
internal sealed record ReleaseInfo(Version Version, string Url);

/// <summary>
/// GitHub에 최신 릴리스 버전을 물어봅니다. 버전 정보만 받아 오고 사용자 정보는 보내지 않습니다.
/// </summary>
internal static class UpdateChecker
{
    private const string LatestReleaseApi = "https://api.github.com/repos/geunlee00/myCalendar/releases/latest";

    /// <summary>지금 실행 중인 버전(예: 1.1.0). 릴리스에서는 태그 버전이 들어갑니다.</summary>
    public static Version CurrentVersion { get; } =
        Normalize(typeof(UpdateChecker).Assembly.GetName().Version ?? new Version(0, 0, 0));

    // 정적 값은 적힌 순서대로 만들어지므로, 버전을 쓰는 HttpClient는 버전 다음에 만든다.
    private static readonly HttpClient Http = CreateClient();

    /// <summary>최신 릴리스를 가져옵니다. 인터넷 연결이 없거나 응답을 읽지 못하면 null입니다.</summary>
    public static async Task<ReleaseInfo?> GetLatestReleaseAsync()
    {
        try
        {
            using var stream = await Http.GetStreamAsync(LatestReleaseApi);
            using var json = await JsonDocument.ParseAsync(stream);
            var tag = json.RootElement.GetProperty("tag_name").GetString();
            var url = json.RootElement.GetProperty("html_url").GetString();
            if (tag is null || url is null || !Version.TryParse(tag.TrimStart('v', 'V'), out var version)) return null;

            return new ReleaseInfo(Normalize(version), url);
        }
        catch
        {
            return null;
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        // GitHub API는 User-Agent가 없는 요청을 거절한다.
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"CalendarWidget/{CurrentVersion}");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    /// <summary>1.1 과 1.1.0.0 처럼 자리 수가 달라도 같은 버전으로 비교되게 세 자리로 맞춥니다.</summary>
    private static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(0, version.Build));
}
