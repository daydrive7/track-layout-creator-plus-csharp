using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using TLCPlus;
namespace TLCPlus.Desktop;
static class PerformanceChecks
{
    public static void Run(MainForm form,string root)
    {
        form.Project.Nodes.Clear();for(int i=0;i<120;i++){double a=i*Math.PI*2/120;form.Project.Nodes.Add(new(){X=Math.Cos(a)*2100,Y=Math.Sin(a)*1600,Radius=45,Segments=3});}
        form.Project.Settings.Scenery=2;form.Project.Settings.ManualHeight=true;form.Commit();form.Canvas.Fit();form.Canvas.Tool="select";form.Canvas.DraftMode=true;
        var baseline=new List<double>();for(int i=0;i<12;i++){var timer=Stopwatch.StartNew();_ = Exporter.Build(form.Project);baseline.Add(timer.Elapsed.TotalMilliseconds);}
        var down=typeof(TrackCanvas).GetMethod("OnMouseDown",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var move=typeof(TrackCanvas).GetMethod("OnMouseMove",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var up=typeof(TrackCanvas).GetMethod("OnMouseUp",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var start=Point.Round(form.Canvas.Screen(form.Project.Nodes[0].XY));int fullBefore=form.FullPreviewBuilds;double originalX=form.Project.Nodes[0].X;
        down.Invoke(form.Canvas,[new MouseEventArgs(MouseButtons.Left,1,start.X,start.Y,0)]);
        var frames=new List<double>();var render=new List<double>();var all=Stopwatch.StartNew();
        for(int i=0;i<240;i++){
            int x=start.X+(i%40),y=start.Y+(i%20);move.Invoke(form.Canvas,[new MouseEventArgs(MouseButtons.Left,0,x,y,0)]);
            if(i%8==0){form.BuildInteractivePreview();frames.Add(form.LastInteractivePreviewMs);var timer=Stopwatch.StartNew();using var bitmap=form.Canvas.Render();render.Add(timer.Elapsed.TotalMilliseconds);}
        }
        double eventMs=all.Elapsed.TotalMilliseconds;
        move.Invoke(form.Canvas,[new MouseEventArgs(MouseButtons.Left,0,start.X+39,start.Y+19,0)]);
        using(var latest=form.Canvas.Render()){}
        var expected=Geometry.ControlPoints(form.Project);
        if(form.Canvas.Course?.HasLinePositions!=true||!expected.Select(p=>p.XY).SequenceEqual(form.Canvas.Course.Points.Select(p=>p.XY)))throw new Exception("Rendered drag geometry lags behind the current node position or loses start/pit markers");
        if(!form.Canvas.Stats.Contains($"{form.Canvas.Course.PlanLength:0.0} m"))throw new Exception("Track length does not update during the drag");
        if(form.FullPreviewBuilds!=fullBefore)throw new Exception("Full exporter ran during a node drag");
        up.Invoke(form.Canvas,[new MouseEventArgs(MouseButtons.Left,1,start.X+39,start.Y+19,0)]);
        if(form.FullPreviewBuilds<=fullBefore||form.Preview==null||form.Preview.Heights.Count<2)throw new Exception("Full preview was not restored after node release");
        if(form.Project.Nodes[0].X==originalX)throw new Exception("Drag did not move the target node");
        var report=new{nodes=120,mouseMoves=240,fullBuildsDuringDrag=0,fullBuildsOnRelease=form.FullPreviewBuilds-fullBefore,fullExportPreparationMs=baseline.Average(),draftGeometryPreparationMs=frames.Average(),draftGeometryP95Ms=frames.Order().ElementAt((int)(frames.Count*.95)),draftCanvasRenderMs=render.Average(),allMouseEventsAndRenderedFramesMs=eventMs};
        File.WriteAllText(Path.Combine(root,"analysis/drag-performance.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
    }
}
