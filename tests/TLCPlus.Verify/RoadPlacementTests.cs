using TLCPlus;
static class RoadPlacementTests
{
    static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Reject(Action action,string message){try{action();}catch(InvalidDataException){return;}throw new Exception(message);}
    static Project Fixture(string root,int scenery=3)=>TrkFormat.Read(File.ReadAllBytes(Path.Combine(root,$"tests/fixtures/{scenery}_1_normal.trk5")));
    static double SafeStart(Course course)=>Exporter.From3d(course.Banks,course.Roads.First(road=>road.Flag==0).Begin)+20;
    static void Coverage(Project p,Course c)
    {
        double previous=0;
        foreach(var road in c.Roads){Assert(Math.Abs(road.Begin-previous)<1e-6,"Road edit leaves a gap or overlap");double end=road.End==0&&p.Settings.Circuit?c.Length:road.End;Assert(end>road.Begin,"Road edit has non-positive length");previous=end;}
        Assert(Math.Abs(previous-c.Length)<1e-6,"Road edit changes total covered distance");
    }
    public static void Run(string root)
    {
        var p=Fixture(root);var original=Exporter.Build(p);byte[] originalTed=Exporter.Write(p,original,0xE245A123);
        Assert(ReferenceEquals(RoadPlacementEngine.Apply(p,original,original.Roads),original.Roads),"Empty placement list changes legacy roads");
        double start=SafeStart(original);p.RoadPlacements.Add(new(){Scenery=3,TypeId="andalusia-parking",Start=start});
        var copied=p.Copy();copied.RoadPlacements[0].Start+=10;Assert(p.RoadPlacements[0].Start==start,"Undo snapshot shares mutable road placement instances");
        var edited=Exporter.Build(p);var preview=Exporter.BuildPlanPreview(p);var restaurant=edited.Roads.Single(road=>road.Uuid==0xB4CB0024E8474511UL);
        Assert(Math.Abs(Exporter.From3d(edited.Banks,restaurant.Begin)-start)<1e-6,"Restaurant starts at wrong plan distance");
        Assert(Math.Abs(Exporter.From3d(edited.Banks,restaurant.End)-start-150)<1e-6,"Restaurant does not use the documented 150m mesh span");
        Assert(Math.Abs(edited.Start2d-original.Start2d)<1e-6&&Math.Abs(edited.Finish2d-original.Finish2d)<1e-6,"Stamp moved the start/finish line");
        Assert(Math.Abs(preview.Start2d-edited.Start2d)<1e-6&&Math.Abs(preview.Finish2d-edited.Finish2d)<1e-6,"Preview line placement differs");
        Coverage(p,edited);Coverage(p,preview);
        Assert(TedDocument.Read(Exporter.Write(p,edited,0xE245A123)).Course.Roads.Any(road=>road.Uuid==restaurant.Uuid),"TED lost restaurant placement");
        Assert(original.Roads.Where(road=>road.Flag==2).SequenceEqual(edited.Roads.Where(road=>road.Flag==2)),"Protected roads changed");
        p.RoadPlacements.Add(new(){Scenery=3,TypeId="andalusia-medic",Start=start+200,Flag=1});var multiple=Exporter.Build(p);Coverage(p,multiple);Assert(multiple.Roads.Any(road=>road.Uuid==0x93E20024E8474511UL&&road.Flag==1),"Road side flag was not retained");
        p.RoadPlacements.Add(new(){Scenery=3,TypeId="andalusia-parking",Start=start+30});Reject(()=>Exporter.Build(p),"Overlapping stamp accepted");p.RoadPlacements.RemoveAt(2);
        p.RoadPlacements[0].Start=0;Reject(()=>Exporter.Build(p),"Protected start section accepted");p.RoadPlacements[0].Start=-1;Reject(()=>Exporter.Build(p),"Negative stamp accepted");p.RoadPlacements[0].Start=original.PlanLength-149;Reject(()=>Exporter.Build(p),"Wrapping/out-of-bounds stamp accepted");p.RoadPlacements[0].Start=start;
        p.RoadPlacements[0].Flag=2;Reject(()=>Exporter.Build(p),"Special stamp flag accepted");p.RoadPlacements[0].Flag=0;p.RoadPlacements[0].Scenery=2;Reject(()=>Exporter.Build(p),"Foreign scenery accepted");p.RoadPlacements[0].Scenery=3;
        string work=Path.Combine(root,"work");Directory.CreateDirectory(work);string path=Path.Combine(work,"road-placement-save.trk5");var standard=TrkFormat.Write(p);DocumentFiles.SaveLayout(path,p);Assert(standard.AsSpan().SequenceEqual(File.ReadAllBytes(path)),"Road metadata changed standard TRK5 bytes");var reloaded=DocumentFiles.LoadLayout(path);Assert(reloaded.RoadPlacements.Count==2&&reloaded.RoadPlacements[1].Flag==1,"Road metadata did not reopen");Assert(Exporter.Write(p,timestamp:0xE245A123).AsSpan().SequenceEqual(Exporter.Write(reloaded,timestamp:0xE245A123)),"Save/reopen changed exported road placements");
        p.RoadPlacements.Clear();Assert(originalTed.AsSpan().SequenceEqual(Exporter.Write(p,timestamp:0xE245A123)),"Removing placements failed to restore legacy export");
        var eifel=Fixture(root,2);double forestStart=SafeStart(Exporter.Build(eifel));eifel.RoadPlacements.Add(new(){Scenery=2,TypeId="eifel-forest",Start=forestStart});Coverage(eifel,Exporter.Build(eifel));
        int catalogCases=0;
        foreach(int scenery in new[]{1,2,3,5})foreach(var type in RoadTypeCatalog.ForScenery(scenery)){
            var scene=Fixture(root,scenery);var baseline=Exporter.Build(scene);double at=SafeStart(baseline);
            scene.RoadPlacements.Add(new(){Scenery=scenery,TypeId=type.Id,Start=at,Flag=1});var placed=Exporter.Build(scene);var draft=Exporter.BuildPlanPreview(scene);Coverage(scene,placed);Coverage(scene,draft);
            double a=Exporter.To3d(placed.Banks,at),b=Exporter.To3d(placed.Banks,at+type.TotalLength);
            var chain=placed.Roads.Where(road=>road.Begin>=a-1e-6&&road.End<=b+1e-6&&road.End>0).ToArray();
            Assert(chain.Select(road=>road.Uuid).SequenceEqual(type.Segments.Select(segment=>segment.Uuid)),"Catalog chain order differs: "+type.Id);
            Assert(chain.All(road=>road.Flag==1),"Catalog chain side flag differs: "+type.Id);
            for(int i=0;i<chain.Length;i++)Assert(Math.Abs(Exporter.From3d(placed.Banks,chain[i].End)-Exporter.From3d(placed.Banks,chain[i].Begin)-type.Segments[i].Length)<1e-6,"Catalog chain nominal length differs: "+type.Id);
            Assert(Math.Abs(placed.Start2d-baseline.Start2d)<1e-6&&Math.Abs(draft.Start2d-baseline.Start2d)<1e-6,"Catalog placement moved start line: "+type.Id);
            catalogCases++;
        }
        var edge=Fixture(root);var edgeBase=Exporter.Build(edge);edge.RoadPlacements.Add(new(){Scenery=3,TypeId="andalusia-restaurant",Start=edgeBase.PlanLength-150});var edgeCourse=Exporter.Build(edge);Coverage(edge,edgeCourse);Assert(edgeCourse.Roads[^1].Uuid==0xB4CB0024E8474511UL&&edgeCourse.Roads[^1].End==0,"Loop-ending stamp lost the End=0 convention");
        Console.WriteLine($"PASS {catalogCases} scenery/type chains retain exact nominal spans and full/plan start placement");
        Console.WriteLine("PASS road stamps: exact spans, full/plan coverage, protected meshes, overlap/bounds/scenery rejection, side flags, metadata and unchanged legacy exports");
    }
}
