namespace TLCPlus;

/// <summary>Road-mesh replacement measured along the plan-view centerline.</summary>
public sealed class RoadPlacement
{
    public string TypeId {get;set;}="";
    public int Scenery {get;set;}
    public double Start {get;set;}
    public int Flag {get;set;}
}

public sealed record RoadUnit(ulong Uuid,double Length);
public sealed record RoadType(string Id,string Name,int Scenery,IReadOnlyList<RoadUnit> Segments)
{
    public double TotalLength=>Segments.Sum(segment=>segment.Length);
}

/// <summary>
/// Ordinary road units from the GT6-TPE-UUID reference. Catalog entries with
/// transitions contain the whole ordered group, not an isolated middle mesh.
/// RailUnit metadata flags are deliberately not treated as TED ROAD flags.
/// </summary>
public static class RoadTypeCatalog
{
    static readonly Dictionary<int,IReadOnlyList<RoadType>> cache=[];
    public static IReadOnlyList<RoadType> ForScenery(int scenery)
    {
        if(cache.TryGetValue(scenery,out var result))return result;
        string family=scenery switch{3=>"andalusia",2 or 5=>"eifel",1=>"death-valley",_=>""};
        if(family=="")return [];
        using var doc=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(Terrain.AssetsPath,"road-types.json")));
        result=doc.RootElement.EnumerateArray().Where(item=>{
            var ids=item.GetProperty("scenery");return ids.ValueKind==System.Text.Json.JsonValueKind.Array?ids.EnumerateArray().Any(id=>id.GetInt32()==scenery):ids.GetInt32()==scenery;
        }).Select(item=>new RoadType(family+"-"+item.GetProperty("id").GetString(),item.GetProperty("name").GetString()!,scenery,
            item.GetProperty("segments").EnumerateArray().Select(segment=>new RoadUnit(Convert.ToUInt64(segment.GetProperty("uuid").GetString(),16),segment.GetProperty("len").GetDouble())).ToArray())).ToArray();
        cache[scenery]=result;return result;
    }
    public static RoadType Find(int scenery,string id)=>ForScenery(scenery).FirstOrDefault(type=>type.Id==(id=="andalusia-parking"?"andalusia-restaurant":id))
        ??throw new InvalidDataException("This road type is not available for the selected scenery.");
    public static bool IsEditableUuid(int scenery,ulong uuid)
    {
        if(scenery is not (1 or 2 or 3 or 5))return false;
        if(ForScenery(scenery).Any(type=>type.Segments.Count==1&&type.Segments[0].Uuid==uuid))return true;
        return Catalog.Roads(scenery).GetProperty("normal").EnumerateArray().Any(row=>row[0].GetUInt64()==uuid);
    }
    public static bool IsProtectedUuid(int scenery,ulong uuid)=>new[]{"start","startsprint","endsprint"}
        .SelectMany(name=>Catalog.Roads(scenery).GetProperty(name).EnumerateArray()).Any(row=>row[0].GetUInt64()==uuid);
}

public static class RoadPlacementEngine
{
    const double Epsilon=1e-7;
    static HashSet<ulong> ProtectedUuids(int scenery)
    {
        var data=Catalog.Roads(scenery);return new[]{"start","startsprint","endsprint"}
            .SelectMany(name=>data.GetProperty(name).EnumerateArray()).Select(row=>row[0].GetUInt64()).ToHashSet();
    }
    /// <summary>Return the unchanged input when no placements exist.</summary>
    public static List<Road> Apply(Project project,Course course,List<Road> roads)
    {
        if(project.RoadPlacements.Count==0)return roads;
        if(!double.IsFinite(course.PlanLength)||course.PlanLength<=0)throw new InvalidDataException("Create a track before placing road types.");
        var protectedIds=ProtectedUuids(project.Settings.Scenery);
        double total3d=course.Length>0?course.Length:course.Banks.Sum(bank=>bank.Vlen);
        double End(Road road)=>road.End==0&&project.Settings.Circuit?total3d:road.End;
        var placements=project.RoadPlacements.OrderBy(item=>item.Start).ToList();
        var ranges=new List<(RoadPlacement Placement,RoadType Type,double Begin,double End)>();double previousEnd=-1;
        foreach(var placement in placements){
            if(placement.Scenery!=project.Settings.Scenery)throw new InvalidDataException("Road placements belong to a different scenery. Remove them before changing scenery.");
            if(placement.Flag is not (0 or 1))throw new InvalidDataException("A placed road type must use side flag 0 or 1.");
            var type=RoadTypeCatalog.Find(placement.Scenery,placement.TypeId);double finish=placement.Start+type.TotalLength;
            if(!double.IsFinite(placement.Start)||placement.Start<0||finish>course.PlanLength+Epsilon)throw new InvalidDataException("The complete road type must fit inside the track without crossing its start/end.");
            if(placement.Start<previousEnd-Epsilon)throw new InvalidDataException("Road type placements cannot overlap.");
            previousEnd=finish;double a=Exporter.To3d(course.Banks,placement.Start),b=Exporter.To3d(course.Banks,Math.Min(finish,course.PlanLength));
            if(roads.Any(road=>road.Begin<b-Epsilon&&End(road)>a+Epsilon&&(road.Flag is not (0 or 1)||protectedIds.Contains(road.Uuid))))
                throw new InvalidDataException("Place road types outside the start/finish and other protected road sections.");
            ranges.Add((placement,type,a,b));
        }
        // Subtract all stamp ranges from existing ordinary meshes, preserving
        // each fragment's UUID and flags, including unmapped original types.
        var result=new List<Road>();
        foreach(var road in roads){
            double cursor=road.Begin,finish=End(road);
            foreach(var range in ranges){
                if(range.End<=cursor+Epsilon||range.Begin>=finish-Epsilon)continue;
                if(range.Begin>cursor+Epsilon)result.Add(road with{Begin=cursor,End=Math.Min(range.Begin,finish)});
                cursor=Math.Max(cursor,range.End);if(cursor>=finish-Epsilon)break;
            }
            if(cursor<finish-Epsilon)result.Add(road with{Begin=cursor,End=finish});
        }
        foreach(var range in ranges){
            double position=range.Placement.Start;
            foreach(var segment in range.Type.Segments){double finish=position+segment.Length;result.Add(new(segment.Uuid,range.Placement.Flag,Exporter.To3d(course.Banks,position),Exporter.To3d(course.Banks,Math.Min(finish,course.PlanLength))));position=finish;}
        }
        result=result.OrderBy(road=>road.Begin).ToList();
        if(project.Settings.Circuit&&result.Count>0)result[^1]=result[^1] with{End=0};
        return result;
    }
}
