using System.Reflection;
using System.Text.Json;
namespace TLCPlus.Desktop;

static class ToolbarChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(MainForm form,string root,List<string> checks)
    {
        var tools=(Dictionary<string,Button>)typeof(MainForm).GetField("iconButtons",Private)!.GetValue(form)!;
        var pages=(FlowLayoutPanel[])typeof(MainForm).GetField("classicPages",Private)!.GetValue(form)!;
        var split=(SplitContainer)typeof(MainForm).GetField("mainSplit",Private)!.GetValue(form)!;
        var choice=(ComboBox)typeof(MainForm).GetField("roadTypeChoice",Private)!.GetValue(form)!;
        var preferences=(AppPreferences)typeof(MainForm).GetField("uiPreferences",Private)!.GetValue(form)!;
        string theme=preferences.Theme,accent=preferences.Accent,tool=form.Canvas.Tool;
        int page=(int)typeof(MainForm).GetField("activePage",Private)!.GetValue(form)!;
        bool collapsed=split.Panel1Collapsed;
        try{
            if(choice.Parent!=pages[3]||pages[0].Controls.Contains(choice))throw new Exception("Road types are not in Decorations");
            split.Panel1Collapsed=true;tools["road"].PerformClick();Application.DoEvents();
            if(form.Canvas.Tool!="road"||split.Panel1Collapsed||!pages[3].Visible||!pages[3].ClientRectangle.Contains(choice.Bounds)||!pages[3].ClientRectangle.Contains(pages[3].Controls[^1].Bounds)||tools["road"].Image==null)throw new Exception($"Road toolbar shortcut does not reveal the road controls: viewport {pages[3].ClientRectangle}, choice {choice.Bounds}, footer {pages[3].Controls[^1].Bounds}, scroll {pages[3].AutoScrollPosition}");
            var toolbar=tools["road"].Parent!;
            foreach(string mode in new[]{"select","decorate","road","pan"}){
                tools[mode].PerformClick();
                foreach(var pair in tools)if(pair.Value.BackColor!=(pair.Key==mode?UiTheme.Button:UiTheme.Panel))throw new Exception("Toolbar selection highlight differs from the active tool");
                foreach(var button in toolbar.Controls.OfType<Button>().Where(b=>b.AccessibleName is "Zoom in" or "Zoom out" or "Reverse direction" or "Save screenshot"))
                    if(button.BackColor!=UiTheme.Panel)throw new Exception("Inactive toolbar action remains highlighted");
            }
            tools["road"].PerformClick();
            pages[3].Controls.OfType<Button>().Single(b=>UiLanguage.OriginalText(b)=="Remove road type").PerformClick();
            if(form.Canvas.Tool!="road-erase"||tools["road"].BackColor!=UiTheme.Button)throw new Exception("Road erase mode loses the road-tool highlight");
            foreach(string value in new[]{"Classic","Dark"}){
                preferences.Theme=value;typeof(MainForm).GetMethod("ApplyPreferences",Private)!.Invoke(form,null);
                foreach(var button in toolbar.Controls.OfType<Button>().Where(b=>b.AccessibleName is "Zoom in" or "Zoom out" or "Reverse direction" or "Save screenshot"))
                    if(button.BackColor!=UiTheme.Panel)throw new Exception("Theme change highlights inactive toolbar actions");
            }
            using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new(0,0,image.Width,image.Height));image.Save(Path.Combine(root,"analysis/v1.0.3-road-toolbar.png"));}
            string? saved=File.Exists(AppPreferences.FilePath)?File.ReadAllText(AppPreferences.FilePath):null;
            try{
                File.WriteAllText(AppPreferences.FilePath,"{\"CanvasColor\":\"#FF0000\",\"PanelColor\":\"#00FF00\",\"accent_color\":\"#BF5AF2\"}");
                var loaded=AppPreferences.Load();UiTheme.Apply(loaded);
                if(UiTheme.Canvas!=Color.FromArgb(37,37,38)||UiTheme.Panel!=Color.FromArgb(30,30,30)||UiTheme.Accent!=Color.FromArgb(191,90,242))throw new Exception("Legacy custom colors remain active or the accent was lost");
                string json=JsonSerializer.Serialize(loaded);if(json.Contains("CanvasColor")||json.Contains("PanelColor"))throw new Exception("Removed custom colors remain in new preferences");
            }finally{if(saved==null)File.Delete(AppPreferences.FilePath);else File.WriteAllText(AppPreferences.FilePath,saved);}
            checks.Add("Road toolbar icon opens Decorations at Road Types; road erase stays selected; inactive toolbar actions remain neutral in both themes");
            checks.Add("Legacy panel/canvas color overrides are ignored and dropped while accent colors remain supported");
        }finally{
            preferences.Theme=theme;preferences.Accent=accent;typeof(MainForm).GetMethod("ApplyPreferences",Private)!.Invoke(form,null);
            typeof(MainForm).GetMethod("ClassicTool",Private)!.Invoke(form,[tool]);typeof(MainForm).GetMethod("ShowPage",Private)!.Invoke(form,[page]);split.Panel1Collapsed=collapsed;
        }
    }
}
