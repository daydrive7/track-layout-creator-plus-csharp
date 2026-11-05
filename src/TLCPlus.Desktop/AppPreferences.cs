using System.Text.Json;
using System.Text.Json.Serialization;
namespace TLCPlus.Desktop;
public sealed class AppPreferences
{
    [JsonPropertyName("language")] public string Language {get;set;}="en";
    [JsonPropertyName("theme")] public string Theme {get;set;}="Dark";
    [JsonPropertyName("accent_color")] public string Accent {get;set;}="#0A84FF";
    public string? CanvasColor {get;set;}
    public string? PanelColor {get;set;}
    [JsonPropertyName("show_proc_gen")] public bool ShowProcedural {get;set;}=true;
    [JsonPropertyName("show_img_vec")] public bool ShowTracer {get;set;}
    [JsonPropertyName("show_ext_path")] public bool ShowImports {get;set;}=true;
    [JsonPropertyName("allow_rally_circuit")] public bool AllowRallyCircuit {get;set;}
    [JsonPropertyName("remind_updates_on_startup")] public bool RemindUpdates {get;set;}=true;
    [JsonPropertyName("show_title")] public bool ShowTitle {get;set;}=true;
    [JsonPropertyName("show_elevation")] public bool ShowElevation {get;set;}=true;
    [JsonPropertyName("performance_draft")] public bool PerformanceDraft {get;set;}=true;
    [JsonPropertyName("show_node_numbers")] public bool ShowNodeNumbers {get;set;}=true;
    [JsonPropertyName("show_node_properties")] public bool ShowNodeProperties {get;set;}=true;
    [JsonPropertyName("trace_accuracy")] public int TraceAccuracy {get;set;}=50;
    [JsonPropertyName("last_notified_version")] public string LastNotifiedVersion {get;set;}="";
    public static string FilePath=>Path.Combine(AppContext.BaseDirectory,"config.json");
    public static AppPreferences Load()
    {
        AppPreferences result = new();
        try
        {
            if (File.Exists(FilePath))
                result = JsonSerializer.Deserialize<AppPreferences>(File.ReadAllText(FilePath)) ?? new();
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            // Use defaults if the saved preferences cannot be read.
        }

        result.Theme = result.Theme == "Classic" ? "Classic" : "Dark";
        if (result.Accent is not { Length: 7 } || result.Accent[0] != '#' ||
            !int.TryParse(result.Accent.AsSpan(1), System.Globalization.NumberStyles.AllowHexSpecifier,
                System.Globalization.CultureInfo.InvariantCulture, out _))
            result.Accent = "#0A84FF";
        result.TraceAccuracy = Math.Clamp(result.TraceAccuracy, 50, 100);
        return result;
    }

    public void Save()
    {
        string temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, FilePath, true);
    }
}
/// <summary>Live palette shared by native controls and renderers.</summary>
public static class UiTheme
{
    public static bool Classic {get;private set;}
    public static Color Accent {get;private set;}=Color.FromArgb(10,132,255);
    public static Color AccentLight {get;private set;}=Color.FromArgb(96,175,255);
    public static Color AccentDark {get;private set;}=Color.FromArgb(7,92,178);
    static Color? customPanel,customCanvas;
    public static Color Panel=>customPanel??(Classic?Color.FromArgb(240,240,240):Color.FromArgb(30,30,30));
    public static Color Button=>Classic?Color.FromArgb(226,229,234):Color.FromArgb(45,45,45);
    public static Color Text=>Contrast(Panel);
    public static Color Muted=>Classic?Color.FromArgb(114,115,115):Color.FromArgb(133,133,133);
    public static Color Canvas=>customCanvas??(Classic?Color.FromArgb(248,249,251):Color.FromArgb(37,37,38));
    public static Color Contrast(Color color)=>color.GetBrightness()>.55?Color.FromArgb(33,33,33):Color.FromArgb(230,230,230);
    public static Color CanvasText=>Contrast(Canvas);
    public static Color Chrome=>Classic?Color.FromArgb(242,243,245):Color.FromArgb(23,25,29);
    public static Color Border=>Classic?Color.FromArgb(201,201,206):Color.FromArgb(60,60,60);
    public static Color Road=>CanvasText;
    public static Color RoadBorder=>Classic?Color.FromArgb(201,201,206):Color.FromArgb(133,133,133);
    public static Color Polygon=>Classic?AccentDark:AccentLight;
    public static Color NavBackground=>Panel;
    public static Color NavLine=>Classic?AccentDark:AccentLight;
    public static Color NavSlope=>Classic?Color.FromArgb(199,144,7):Color.FromArgb(255,159,10);
    public static Color NavGrid=>Classic?Color.FromArgb(216,220,226):Color.FromArgb(64,64,64);
    public static Color NavCursor=>Classic?Color.FromArgb(247,247,247):Color.FromArgb(255,69,58);
    public static Color Arrow=>Classic?Color.FromArgb(33,33,33):Color.FromArgb(255,214,10);
    public static Color Header=>Classic?AccentDark:AccentLight;
    public static void Apply(AppPreferences preferences){Classic=preferences.Theme=="Classic";customPanel=Parse(preferences.PanelColor);customCanvas=Parse(preferences.CanvasColor);Accent=ColorTranslator.FromHtml(preferences.Accent);AccentLight=Mix(Accent,Color.White,.35);AccentDark=Mix(Accent,Color.Black,.30);}
    static Color? Parse(string? value){try{return string.IsNullOrWhiteSpace(value)?null:ColorTranslator.FromHtml(value);}catch{return null;}}
    static Color Mix(Color a,Color b,double amount)=>Color.FromArgb((int)(a.R+(b.R-a.R)*amount),(int)(a.G+(b.G-a.G)*amount),(int)(a.B+(b.B-a.B)*amount));
}
