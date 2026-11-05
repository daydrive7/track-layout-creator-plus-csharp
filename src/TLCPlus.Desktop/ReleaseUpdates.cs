using System.Net;
using System.Text.Json;
namespace TLCPlus.Desktop;
public static class ReleaseUpdates
{
    public const string Version="1.0.0", PythonVersion="1.3.0";
    public const string Repository="daydrive7/track-layout-creator-plus-csharp";
    public record Result(bool Newer,string Tag,string? Url,string Message);
    public static async Task<Result> Check(HttpClient? supplied=null)
    {
        using var owned=supplied==null?new HttpClient{Timeout=TimeSpan.FromSeconds(12)}:null;var client=supplied??owned!;
        using var request=new HttpRequestMessage(HttpMethod.Get,$"https://api.github.com/repos/{Repository}/releases/latest");request.Headers.UserAgent.ParseAdd("TLCPlus-CSharp/"+Version);request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response=await client.SendAsync(request);
        if(response.StatusCode==HttpStatusCode.NotFound)return new(false,"",null,"No public C# release is available yet.");
        response.EnsureSuccessStatusCode();return Parse(await response.Content.ReadAsStringAsync());
    }
    internal static Result Parse(string json)
    {
        using var doc=JsonDocument.Parse(json);var data=doc.RootElement;
        if(data.TryGetProperty("draft",out var draft)&&draft.GetBoolean()||data.TryGetProperty("prerelease",out var pre)&&pre.GetBoolean())return new(false,"",null,"No public C# release is available yet.");
        string tag=data.GetProperty("tag_name").GetString()??"";
        if(!System.Version.TryParse(tag.TrimStart('v','V'),out var version))throw new JsonException("Invalid C# release version.");
        string expected=$"https://github.com/{Repository}/releases/";string? url=data.GetProperty("html_url").GetString();
        if(url==null||!url.StartsWith(expected,StringComparison.OrdinalIgnoreCase))throw new JsonException("Unexpected release page.");
        return new(version>new System.Version(Version),tag,url,"You are using the latest C# version.");
    }
}
