using System.Runtime.CompilerServices;
using System.Text.Json;
using TLCPlus;
namespace TLCPlus.Desktop;
public static class UiLanguage
{
    public static readonly (string Code,string Name)[] Languages=[("en","English"),("pl","Polski"),("es","Español"),("pt-br","Português (BR)"),("pt-pt","Português (PT)"),("fr","Français"),("de","Deutsch"),("ja","日本語"),("ru","Русский")];
    static readonly Dictionary<string,Dictionary<string,string>> catalogs=[];
    sealed class Source { public string Value=""; }
    static readonly ConditionalWeakTable<object,Source> sources=new();
    static readonly Dictionary<string,string> aliases=new(){
        ["Load Track"]="Load track",["Save Track"]="Save track",["Export TED"]="Export to TED",["Export Polygon (CSV)"]="Export polygon",["Export Screenshot"]="Save screenshot",["Import CSV / TED Path"]="Import polygon",["Import GPX Path"]="Import GPX Path (.gpx)",
        ["Node Number Labels"]="Polygon Vertex ID Labels",["Node Properties"]="Edit Selected Node...",["Node properties"]="Edit Selected Node...",["Preferences…"]="Preferences",["About / Credits"]="Credits",["Custom…"]="Custom...",["DECO"]="DECORATIONS",["Isometric view"]="Draw isometric view"
    };
    public static string Code {get;private set;}="en";
    public static void Use(string code)
    {
        Code=Languages.Any(l=>l.Code==code)?code:"en";
        if(catalogs.ContainsKey(Code))return;
        string folder=Path.Combine(Terrain.AssetsPath,"locales");var values=new Dictionary<string,string>();
        foreach(string file in new[]{Code+".json",Code+".native.json"}){
            string path=Path.Combine(folder,file);if(!File.Exists(path))continue;
            foreach(var pair in JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(path))??[])values[pair.Key]=pair.Value;
        }
        catalogs[Code]=values;
    }
    public static string T(string text)
    {
        if(text=="CREDITS_TEXT"){if(!catalogs.ContainsKey(Code))Use(Code);return catalogs[Code].GetValueOrDefault(text,"eran0004 — Track Layout Creator\ndaydrive7 — TLC+ Python");}
        if(Code=="en"||string.IsNullOrWhiteSpace(text))return text;
        if(!catalogs.TryGetValue(Code,out var values)){Use(Code);values=catalogs[Code];}
        string key=text.Trim(),suffix="";
        if(values.TryGetValue(key,out var exact))return text[..(text.Length-text.TrimStart().Length)]+exact;
        if(key.EndsWith('…')){key=key[..^1];suffix="…";}else if(key.EndsWith("...")){key=key[..^3];suffix="…";}
        if(aliases.TryGetValue(key,out var alias))key=alias;
        string translated=values.GetValueOrDefault(key,key);
        if(translated==key&&key!=text.Trim().TrimEnd('…'))return text;
        return text[..(text.Length-text.TrimStart().Length)]+translated+suffix;
    }
    public static string OriginalText(Control control)=>sources.TryGetValue(control,out var source)?source.Value:control.Text;
    public static string OriginalText(ToolStripItem item)=>sources.TryGetValue(item,out var source)?source.Value:item.Text??"";
    static string Remember(object key,string value)=>sources.GetValue(key,_=>new Source{Value=value}).Value;
    public static void Apply(Control root)
    {
        if(root is not TextBoxBase and not ComboBox and not NumericUpDown and not MainForm&&root.Text.Length>0)root.Text=T(Remember(root,root.Text));
        if(root is ClassicScale scale){scale.Label=T(Remember(scale,scale.Label));scale.AccessibleName=scale.Label;scale.Invalidate();}
        if(root is FlatTabs tabs)tabs.Invalidate();
        if(root is ToolStrip strip)foreach(ToolStripItem item in strip.Items)ApplyItem(item);
        foreach(Control child in root.Controls)Apply(child);
    }
    static void ApplyItem(ToolStripItem item)
    {
        item.Text=T(Remember(item,item.Text??""));
        if(item is ToolStripDropDownItem drop)foreach(ToolStripItem child in drop.DropDownItems)ApplyItem(child);
    }
}
public static class LocalizedMessageBox
{
    public static DialogResult Show(string text,string caption="TLC+",MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.None)
        =>System.Windows.Forms.MessageBox.Show(UiLanguage.T(text),UiLanguage.T(caption),buttons,icon);
    public static DialogResult Show(IWin32Window owner,string text,string caption="TLC+",MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.None)
        =>System.Windows.Forms.MessageBox.Show(owner,UiLanguage.T(text),UiLanguage.T(caption),buttons,icon);
}
public partial class MainForm
{
    readonly Dictionary<string,ToolStripMenuItem> languageItems=[];
    void InitializeLanguage()
    {
        UiLanguage.Use(uiPreferences.Language);
        var menu=new ToolStripMenuItem("Language");var file=(ToolStripMenuItem)MainMenuStrip!.Items[0];int exit=file.DropDownItems.Count-1;file.DropDownItems.Insert(exit,new ToolStripSeparator());file.DropDownItems.Insert(exit+1,menu);file.DropDownItems.Insert(exit+2,new ToolStripSeparator());
        foreach(var language in UiLanguage.Languages){var item=new ToolStripMenuItem(language.Name){Checked=language.Code==UiLanguage.Code};languageItems[language.Code]=item;item.Click+=(_,_)=>ChangeLanguage(language.Code);menu.DropDownItems.Add(item);}
        ApplyLanguage();
    }
    void ChangeLanguage(string code)
    {
        uiPreferences.Language=code;SavePreferences();UiLanguage.Use(code);ApplyLanguage();
    }
    void ApplyLanguage()
    {
        SuspendLayout();UiLanguage.Apply(this);foreach(var item in languageItems)item.Value.Checked=item.Key==UiLanguage.Code;
        foreach(var page in classicPages)ResizeRows(page);layoutDecorationOptions?.Invoke();
        foreach(var pair in iconButtons)hints.SetToolTip(pair.Value,UiLanguage.T(pair.Value.AccessibleName??""));
        if(palette!=null){foreach(var button in palette.Controls.OfType<Button>())if(button.Tag is DecorationArtwork.PaletteItem item)hints.SetToolTip(button,UiLanguage.T(item.Name));UpdatePaletteSelection();}
        RefreshPythonControls();RefreshWindowTitle();ResumeLayout(true);Canvas.Invalidate();elevation.Invalidate();
    }
}
