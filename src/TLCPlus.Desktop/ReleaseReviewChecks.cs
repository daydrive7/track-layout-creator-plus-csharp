using System.Net;
using System.Reflection;
using TLCPlus;
namespace TLCPlus.Desktop;
static class ReleaseReviewChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    sealed class Stub(HttpStatusCode code,string json):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {if(request.RequestUri!.AbsoluteUri!=$"https://api.github.com/repos/{ReleaseUpdates.Repository}/releases/latest")throw new Exception("Wrong C# update endpoint");return Task.FromResult(new HttpResponseMessage(code){Content=new StringContent(json)});}
    }
    public static void Run(MainForm form,string root,List<string> checks)
    {
        CheckPreferences();
        checks.Add("Preferences tolerate malformed JSON, null/invalid accents and clamp tracing accuracy");
        var menu=form.MainMenuStrip!;var last=menu.Items[^1];var blank=new Point(last.Bounds.Right+3,menu.Height/2);
        if(!form.IsMenuDragPoint(blank)||form.IsMenuDragPoint(new Point(last.Bounds.Left+5,last.Bounds.Top+5)))throw new Exception("Unused area beside update menu is not distinguished from menu actions");
        if(form.Controls.Cast<Control>().SelectMany(c=>c.Controls.Cast<Control>()).OfType<Label>().Any(c=>c.Text=="TLC+ C# 1.0.0"))throw new Exception("Centered title remains in the bar");
        if(!MainForm.CombinedCredits().Contains("eran0004")||!MainForm.CombinedCredits().Contains("daydrive7")||MainForm.CombinedCredits().Contains("CREDITS_TEXT"))throw new Exception("Combined credits omit Python acknowledgments");
        checks.Add("Empty update-menu area is draggable; centered title removed; combined Python/C# credits retain original acknowledgments");
        string Json(string version)=>"{\"tag_name\":\""+version+"\",\"html_url\":\"https://github.com/"+ReleaseUpdates.Repository+"/releases/tag/"+version+"\"}";
        if(!ReleaseUpdates.Parse(Json("v1.0.2")).Newer||ReleaseUpdates.Parse(Json("v"+ReleaseUpdates.Version)).Newer||ReleaseUpdates.Parse(Json("v0.9.0")).Newer)throw new Exception("Native release comparison failed");
        using(var client=new HttpClient(new Stub(HttpStatusCode.NotFound,""))){if(ReleaseUpdates.Check(client).GetAwaiter().GetResult().Message!="No public C# release is available yet.")throw new Exception("Missing releases are incorrectly reported as up to date");}
        using(var client=new HttpClient(new Stub(HttpStatusCode.OK,Json("v1.2.0")))){if(!ReleaseUpdates.Check(client).GetAwaiter().GetResult().Newer)throw new Exception("C# release endpoint did not work");}
        checks.Add("C# 1.0.0 updater: correct repository, newer/equal/older versions and no-public-release response");
        var preferences=(AppPreferences)typeof(MainForm).GetField("uiPreferences",Private)!.GetValue(form)!;
        var original=(preferences.Theme,preferences.Accent,preferences.CanvasColor,preferences.PanelColor,preferences.ShowTitle);int scene=form.Project.Settings.Scenery;
        try{
            typeof(MainForm).GetMethod("ShowPage",Private)!.Invoke(form,[3]);preferences.ShowTitle=true;preferences.Accent="#30D158";
            foreach(int scenery in new[]{1,2,3,5}){
                form.Project.Settings.Scenery=scenery;form.Commit();Application.DoEvents();
                var palette=(Panel)typeof(MainForm).GetField("palette",Private)!.GetValue(form)!;palette.PerformLayout();
                int bottom=palette.Controls.Cast<Control>().Max(c=>c.Bottom+c.Margin.Bottom);
                if(palette.AutoScroll||palette.Height-bottom>8||bottom>palette.Height)throw new Exception($"Palette spacing differs for scenery {scenery}: {palette.Height}/{bottom}");
            }
            form.Project.Settings.Scenery=3;form.Project.Decorations.Clear();form.Commit();
            foreach(string theme in new[]{"Dark","Classic","Custom"}){
                preferences.Theme=theme=="Classic"?"Classic":"Dark";preferences.PanelColor=theme=="Custom"?"#252B35":null;preferences.CanvasColor=theme=="Custom"?"#161D28":null;
                typeof(MainForm).GetMethod("ApplyPreferences",Private)!.Invoke(form,null);Application.DoEvents();
                using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(root,$"analysis/review-andalusia-{theme.ToLowerInvariant()}.png"));
            }
            preferences.Theme="Dark";preferences.CanvasColor=preferences.PanelColor=null;typeof(MainForm).GetMethod("ApplyPreferences",Private)!.Invoke(form,null);
            typeof(MainForm).GetMethod("ChangeLanguage",Private)!.Invoke(form,["pl"]);
            using var timer=new System.Windows.Forms.Timer{Interval=80};timer.Tick+=(_,_)=>{var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f!=form&&f.Text==UiLanguage.T("About / Credits"));if(dialog==null)return;timer.Stop();using var bitmap=new Bitmap(dialog.Width,dialog.Height);dialog.DrawToBitmap(bitmap,new(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(root,"analysis/review-about-pl.png"));dialog.Close();};timer.Start();typeof(MainForm).GetMethod("ShowAbout",Private)!.Invoke(form,null);
            checks.Add("Palette spacing in all four scenery sets; dark/light/custom UI and Polish About previews");
        }finally{
            (preferences.Theme,preferences.Accent,preferences.CanvasColor,preferences.PanelColor,preferences.ShowTitle)=original;
            typeof(MainForm).GetMethod("ChangeLanguage",Private)!.Invoke(form,["en"]);typeof(MainForm).GetMethod("ApplyPreferences",Private)!.Invoke(form,null);form.Project.Settings.Scenery=scene;form.Commit();
        }
    }

    static void CheckPreferences()
    {
        string? original=File.Exists(AppPreferences.FilePath)?File.ReadAllText(AppPreferences.FilePath):null;
        try
        {
            foreach(string json in new[]{"{","null","{\"accent_color\":null}","{\"accent_color\":\"#GGGGGG\"}"})
            {
                File.WriteAllText(AppPreferences.FilePath,json);
                if(AppPreferences.Load().Accent!="#0A84FF")throw new Exception("Invalid preferences did not use the default accent");
            }
            File.WriteAllText(AppPreferences.FilePath,"{\"accent_color\":\"#30D158\",\"theme\":\"Classic\",\"trace_accuracy\":999}");
            var preferences=AppPreferences.Load();
            if(preferences.Accent!="#30D158"||preferences.Theme!="Classic"||preferences.TraceAccuracy!=100)
                throw new Exception("Valid preferences or upper accuracy limit were not retained");
            File.WriteAllText(AppPreferences.FilePath,"{\"trace_accuracy\":-1}");
            if(AppPreferences.Load().TraceAccuracy!=50)throw new Exception("Lower accuracy limit was not applied");
        }
        finally
        {
            if(original==null)File.Delete(AppPreferences.FilePath);
            else File.WriteAllText(AppPreferences.FilePath,original);
        }
    }
}
