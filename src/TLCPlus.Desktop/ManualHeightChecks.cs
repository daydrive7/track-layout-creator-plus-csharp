using System.Reflection;
using TLCPlus;
namespace TLCPlus.Desktop;
static class ManualHeightChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static IEnumerable<Control> All(Control root){foreach(Control child in root.Controls){yield return child;foreach(var descendant in All(child))yield return descendant;}}
    public static void Run(MainForm form,string root,List<string> checks)
    {
        var file=(ToolStripMenuItem)form.MainMenuStrip!.Items[0];if(UiLanguage.OriginalText(file.DropDownItems[^1])!="Exit"||UiLanguage.OriginalText(file.DropDownItems[^3])!="Language")throw new Exception("Language must precede the final Exit item");
        bool originalManual=form.Project.Settings.ManualHeight;double originalZ=form.Project.Nodes[0].Z;
        form.Project.Settings.ManualHeight=true;form.Project.Nodes[0].Z=0;form.Commit();form.Canvas.Tool="select";
        var graph=(ElevationView)typeof(MainForm).GetField("elevation",Private)!.GetValue(form)!;
        double[] Profile(){using var bitmap=new Bitmap(graph.Width,graph.Height);graph.DrawToBitmap(bitmap,new(0,0,bitmap.Width,bitmap.Height));return ((double[])typeof(ElevationView).GetField("heights",Private)!.GetValue(graph)!).ToArray();}
        var before=Profile();var point=Point.Round(form.Canvas.Screen(form.Project.Nodes[0].XY));
        using(var timer=new System.Windows.Forms.Timer{Interval=80}){
            timer.Tick+=(_,_)=>{var dialog=Application.OpenForms.OfType<NodePropertiesDialog>().FirstOrDefault();if(dialog==null)return;timer.Stop();((TextBox)typeof(NodePropertiesDialog).GetField("z",Private)!.GetValue(dialog)!).Text="80";typeof(NodePropertiesDialog).GetMethod("ApplyChanges",Private)!.Invoke(dialog,null);};timer.Start();
            typeof(TrackCanvas).GetMethod("OnMouseDown",Private)!.Invoke(form.Canvas,[new MouseEventArgs(MouseButtons.Left,2,point.X,point.Y,0)]);
            typeof(TrackCanvas).GetMethod("OnMouseDoubleClick",Private)!.Invoke(form.Canvas,[new MouseEventArgs(MouseButtons.Left,2,point.X,point.Y,0)]);
        }
        if(form.Canvas.IsInteracting||form.Project.Nodes[0].Z!=80||Profile().SequenceEqual(before))throw new Exception("Double-click height editing did not refresh the elevation profile");
        var raised=Profile();form.Undo();if(form.Project.Nodes[0].Z!=0||Profile().SequenceEqual(raised))throw new Exception("Height undo did not update graph");form.Redo();
        var ramp=All(form).OfType<ClassicScale>().First(c=>UiLanguage.OriginalText(c)=="Ramp UP Transition (In)");int builds=form.FullPreviewBuilds;
        typeof(ClassicScale).GetMethod("OnMouseDown",Private)!.Invoke(ramp,[new MouseEventArgs(MouseButtons.Left,1,ramp.Width/2,40,0)]);
        if(form.FullPreviewBuilds<=builds)throw new Exception("Height slider does not refresh before release");
        typeof(ClassicScale).GetMethod("OnMouseUp",Private)!.Invoke(ramp,[new MouseEventArgs(MouseButtons.Left,1,ramp.Width/2,40,0)]);
        form.Canvas.ShowNodeProperties=true;form.Canvas.ShowNumbers=true;
        var propertyToggle=All(form).OfType<CheckBox>().Single(c=>UiLanguage.OriginalText(c).Trim()=="Node Property Labels");propertyToggle.Checked=false;
        if(form.Canvas.ShowNodeProperties||AppPreferences.Load().ShowNodeProperties)throw new Exception("Node property toggle did not disable and save");propertyToggle.Checked=true;
        if(!form.Canvas.ShowNodeProperties||!AppPreferences.Load().ShowNodeProperties)throw new Exception("Node property toggle did not restore and save");
        if(!form.Canvas.NodeDetails(0).Contains("H: 80")||!form.Canvas.NodeDetails(0).Contains("R "))throw new Exception("Node property labels omit height or radius");
        using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new(0,0,image.Width,image.Height));image.Save(Path.Combine(root,"analysis/manual-height-labels.png"));}
        form.Project.Settings.ManualHeight=originalManual;form.Project.Nodes[0].Z=originalZ;form.Commit();
        checks.Add("Double-click height editing, live height sliders and undo/redo refresh the graph; node labels include height/radius; Language precedes Exit");
    }
}
