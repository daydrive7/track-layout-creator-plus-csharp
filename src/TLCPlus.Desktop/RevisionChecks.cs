using System.Globalization;
using System.Reflection;
using TLCPlus;
namespace TLCPlus.Desktop;
static class RevisionChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static IEnumerable<Control> All(Control root){yield return root;foreach(Control child in root.Controls)foreach(var descendant in All(child))yield return descendant;}
    public static void Run(MainForm form,string root,List<string> checks)
    {
        var type=typeof(MainForm);type.GetMethod("ShowPage",Private)!.Invoke(form,[3]);
        form.Canvas.CursorDistance=null;
        var hover=typeof(TrackCanvas).GetMethod("OnMouseMove",Private)!;var nodePoint=Point.Round(form.Canvas.Screen(form.Project.Nodes[0].XY));
        hover.Invoke(form.Canvas,[new MouseEventArgs(MouseButtons.None,0,nodePoint.X,nodePoint.Y,0)]);
        if(form.Canvas.CursorDistance!=null)throw new Exception("Canvas hover activates the elevation marker");
        var graph=(ElevationView)type.GetField("elevation",Private)!.GetValue(form)!;
        using(var image=new Bitmap(graph.Width,graph.Height))graph.DrawToBitmap(image,new(0,0,image.Width,image.Height));
        typeof(ElevationView).GetMethod("OnMouseMove",Private)!.Invoke(graph,[new MouseEventArgs(MouseButtons.None,0,graph.Width/2,graph.Height/2,0)]);
        if(form.Canvas.CursorDistance==null)throw new Exception("Elevation graph does not activate the track marker");
        typeof(ElevationView).GetMethod("OnMouseLeave",Private)!.Invoke(graph,[EventArgs.Empty]);
        if(form.Canvas.CursorDistance!=null||graph.CursorDistance!=null)throw new Exception("Elevation marker remains after leaving the graph");
        checks.Add("Red track marker activates only from the elevation graph and clears on leaving it");
        var palette=(Panel)type.GetField("palette",Private)!.GetValue(form)!;
        var buttons=palette.Controls.OfType<Button>().ToArray();int builds=form.FullPreviewBuilds;
        for(int i=0;i<300;i++)buttons[i%buttons.Length].PerformClick();
        if(buttons.Any(b=>b.IsDisposed)||!buttons.SequenceEqual(palette.Controls.OfType<Button>())||form.FullPreviewBuilds!=builds)throw new Exception("Palette clicks rebuild controls or the track");
        checks.Add("300 rapid decoration palette clicks retain the same controls and do not rebuild the track");
        var variant=Catalog.Variants(form.Project.Settings.Scenery).First(v=>v.GetProperty("type").GetInt32()==1);
        form.Project.Decorations.Add(new(){Type=1,Uuid=variant.GetProperty("uuid").GetUInt64(),Vpos=form.Preview!.PlanLength*.5});form.Commit();form.Canvas.Tool="decorate";
        using(var bitmap=form.Canvas.Render()){}
        var positions=(Dictionary<int,PointF>)typeof(TrackCanvas).GetField("decorationPositions",Private)!.GetValue(form.Canvas)!;
        var location=Point.Round(positions[0]);builds=form.FullPreviewBuilds;
        var down=typeof(TrackCanvas).GetMethod("OnMouseDown",Private)!;var up=typeof(TrackCanvas).GetMethod("OnMouseUp",Private)!;
        for(int i=0;i<30;i++){down.Invoke(form.Canvas,[new MouseEventArgs(MouseButtons.Left,1,location.X,location.Y,0)]);up.Invoke(form.Canvas,[new MouseEventArgs(MouseButtons.Left,1,location.X,location.Y,0)]);}
        if(form.FullPreviewBuilds!=builds||form.Canvas.SelectedDecoration!=0)throw new Exception("Clicking a placed decoration rebuilds the track");
        form.Project.Decorations.Clear();form.Commit();checks.Add("Repeated selection of placed decorations performs no full refresh or undo edit");
        var plan=Exporter.BuildPlanPreview(form.Project);var full=Exporter.Build(form.Project);
        if(!plan.HasLinePositions||Math.Abs(plan.Start2d-full.Start2d)>1e-6||Math.Abs(plan.Finish2d-full.Finish2d)>1e-6||Math.Abs(plan.PitStart2d-full.PitStart2d)>1e-6)throw new Exception("Drag preview start/pit placement differs from full track");
        checks.Add("Lightweight drag preview retains the same start and pit placement");
        var culture=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("pl-PL");if(TrackCanvas.TrackLabel(new(){Length=999.3})!="999,3 m")throw new Exception("Track lengths are rounded or use the wrong decimal separator");}finally{CultureInfo.CurrentCulture=culture;}
        checks.Add("Canvas distance labels keep one decimal place using the local number format");
        var language=type.GetMethod("ChangeLanguage",Private)!;
        foreach(var entry in UiLanguage.Languages){language.Invoke(form,[entry.Code]);if(form.MainMenuStrip!.Items[0].Text!=UiLanguage.T("File"))throw new Exception(entry.Code+" menu did not translate");}
        language.Invoke(form,["pl"]);
        if(AppPreferences.Load().Language!="pl"||UiLanguage.T("Node Number Labels")=="Node Number Labels")throw new Exception("Language selection did not translate or persist");
        using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(root,"analysis/finished-polish.png"));}
        foreach(string code in new[]{"en","pl","ja"}){
            language.Invoke(form,[code]);
            var node=new Node{X=-435.1423511091981,Y=-1319.5267132143424,Z=4.123456789,Radius=90};
            using var dialog=new NodePropertiesDialog(node);dialog.Show(form);Application.DoEvents();
            var fields=All(dialog).OfType<TextBox>().ToArray();if(fields.Any(c=>c.Font.Name!=dialog.Font.Name||c.Font.Size!=dialog.Font.Size))throw new Exception("Node editor font differs across fields");
            using(var bitmap=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bitmap,new(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(root,$"analysis/node-properties-{code}.png"));}
            if(dialog.Height>370||dialog.Width>470||dialog.ClientSize.Width<380||fields.Any(c=>c.Width<200))throw new Exception($"Node editor bounds: {dialog.Size}, client {dialog.ClientSize}; field widths {string.Join(',',fields.Select(c=>c.Width))}");
            foreach(var control in All(dialog).Where(c=>c.Visible&&c is Button or TextBox)){
                var bounds=dialog.RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
                if(!dialog.ClientRectangle.Contains(bounds))throw new Exception("Node editor input/button extends outside the dialog: "+control.Text);
            }
            typeof(NodePropertiesDialog).GetMethod("ApplyChanges",Private)!.Invoke(dialog,null);
            if(dialog.Result.X!=node.X||dialog.Result.Y!=node.Y||dialog.Result.Z!=node.Z)throw new Exception("Compact coordinate display changed the unedited values");
        }
        language.Invoke(form,["en"]);checks.Add("All nine languages switch live; Polish choice persists; English/Polish/Japanese node editors render compactly and retain exact coordinates");
    }
}
