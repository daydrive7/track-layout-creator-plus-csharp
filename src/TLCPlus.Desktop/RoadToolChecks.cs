using System.Reflection;
using TLCPlus;
namespace TLCPlus.Desktop;
static class RoadToolChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    public static void Run(MainForm form,string root,List<string> checks)
    {
        form.Project.RoadPlacements.Clear();form.Project.Settings.Scenery=3;form.Commit();form.Canvas.Fit();
        var choice=(ComboBox)typeof(MainForm).GetField("roadTypeChoice",Private)!.GetValue(form)!;
        var type=choice.Items.Cast<RoadType>().First(t=>t.Id is "andalusia-parking" or "andalusia-restaurant");choice.SelectedItem=type;
        form.Canvas.Tool="road";using(var image=form.Canvas.Render()){}
        var nearestTrack=typeof(TrackCanvas).GetMethod("NearestTrack",Private)!;
        double distance=-1;Point location=default;
        for(double d=type.TotalLength/2;d<form.Preview!.PlanLength-type.TotalLength/2;d+=50){
            var pixel=Point.Round(form.Canvas.Screen(Geometry.At(form.Preview.Points,d).Point));
            var picked=((double Distance,double Pixels))nearestTrack.Invoke(form.Canvas,[pixel])!;
            if(picked.Pixels>35)continue;
            // Validate the actual pixel hit: rounding can move a boundary click into protected road.
            var copy=form.Project.Copy();copy.RoadPlacements.Add(new(){TypeId=type.Id,Scenery=3,Start=picked.Distance-type.TotalLength/2});
            try{_ = Exporter.Build(copy);distance=picked.Distance;location=pixel;break;}catch(InvalidDataException){}
        }
        if(distance<0)throw new Exception("No ordinary road span available for the placement test");
        var down=typeof(TrackCanvas).GetMethod("OnMouseDown",Private)!;
        down.Invoke(form.Canvas,[new MouseEventArgs(MouseButtons.Left,1,location.X,location.Y,0)]);
        if(form.Project.RoadPlacements.Count!=1||!form.Preview!.Roads.Any(r=>r.Uuid==0xB4CB0024E8474511UL))throw new Exception("Canvas road click did not insert a restaurant span");
        form.Undo();if(form.Project.RoadPlacements.Count!=0)throw new Exception("Road placement undo failed");form.Redo();if(form.Project.RoadPlacements.Count!=1)throw new Exception("Road placement redo failed");
        string path=Path.Combine(root,"work/road-placement-save.trk5");DocumentFiles.SaveLayout(path,form.Project);
        if(!File.ReadAllBytes(path).AsSpan().SequenceEqual(TrkFormat.Write(form.Project))||DocumentFiles.LoadLayout(path).RoadPlacements.Count!=1)throw new Exception("Road placement metadata roundtrip failed");
        typeof(MainForm).GetMethod("ShowPage",Private)!.Invoke(form,[3]);Application.DoEvents();
        using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new(0,0,image.Width,image.Height));image.Save(Path.Combine(root,"analysis/road-placement-preview.png"));}
        down.Invoke(form.Canvas,[new MouseEventArgs(MouseButtons.Right,1,location.X,location.Y,0)]);if(form.Project.RoadPlacements.Count!=0)throw new Exception("Road placement right-click removal failed");
        form.Project.RoadPlacements.Add(new(){TypeId=type.Id,Scenery=3,Start=-1});form.Commit();
        if(form.Preview!=null||form.Canvas.Course==null)throw new Exception("Invalid stamp did not keep a recoverable base preview");form.Project.RoadPlacements.Clear();form.Commit();
        string tedPath=Path.Combine(root,"work/road-click-source.ted");File.WriteAllBytes(tedPath,Exporter.Write(form.Project));form.OpenFile(tedPath);form.Canvas.Tool="road";
        byte[] before=form.Ted!.Write();form.ChangeRoadTypeAt(distance,false);byte[] after=form.Ted!.Write();
        int changedRoad=form.Ted.Course.Roads.FindIndex(r=>r.Uuid==0xB4CB0024E8474511UL);if(changedRoad<0||form.Canvas.Tool!="road")throw new Exception("Direct TED click failed or exited road mode");
        int offset=(int)Binary.U32(before,132)+changedRoad*20;for(int i=0;i<before.Length;i++)if((i<offset||i>=offset+8)&&before[i]!=after[i])throw new Exception("Direct TED click changed unrelated bytes");
        form.Undo();if(!form.Ted!.Write().AsSpan().SequenceEqual(before))throw new Exception("TED road edit undo differs");form.Redo();if(!form.Ted!.Write().AsSpan().SequenceEqual(after))throw new Exception("TED road edit redo differs");
        form.OpenFile(path);form.Project.RoadPlacements.Clear();form.Commit();
        form.Canvas.Tool="pan";checks.Add("Road tool canvas click inserts restaurant, exports mesh, supports undo/redo, metadata save/reopen and right-click removal");
        checks.Add("Invalid road stamps keep a recoverable base preview; direct TED click preserves unrelated bytes, road mode and undo/redo");
    }
}
