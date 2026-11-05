using System.Text.Json;
namespace TLCPlus;
using static System.Math;
public class Course
{
    public List<ControlPoint> Points=[];
    public List<Bank> Banks=[];
    public List<double> Heights=[],Checkpoints=[];
    public List<Road> Roads=[];
    public List<DecoEntry> Decorations=[];
    public int Corners;
    public double PlanLength,Length;
    // Preview positions remain in plan-view metres, as in the Python canvas.
    public double Start2d,Finish2d,PitStart2d,PitLength;
    public bool HasLinePositions;
}
public static class Catalog
{
    static JsonDocument? roads,decorations;
    public static JsonElement Roads(int scenery){roads??=JsonDocument.Parse(File.ReadAllText(Path.Combine(Terrain.AssetsPath,"roads.json")));return roads.RootElement.TryGetProperty(scenery.ToString(),out var r)?r:roads.RootElement.GetProperty("2");}
    public static JsonElement Decorations(int scenery){decorations??=JsonDocument.Parse(File.ReadAllText(Path.Combine(Terrain.AssetsPath,"decorations.json")));return decorations.RootElement.TryGetProperty(scenery.ToString(),out var r)?r:decorations.RootElement.GetProperty("2");}
    public static IEnumerable<JsonElement> Variants(int scenery)=>Decorations(scenery).GetProperty("variants").EnumerateArray().Where(v=>v.GetProperty("type").GetInt32()!=17);
}
public static class Exporter
{
    static double Sum(IEnumerable<double> values){double sum=0;foreach(double x in values)sum+=x;return sum;}
    public static Course Build(Project p)
    {
        if(p.Nodes.Any(n=>!double.IsFinite(n.X+n.Y+n.Z+n.Radius+n.Camber)||n.Segments<0||n.Segments>10000))throw new InvalidDataException("Nodes must contain finite coordinates and 0–10,000 curve subdivisions.");
        if(!double.IsFinite(p.Settings.RoadWidth+p.Settings.MaxCamber+p.Settings.CamberRate+p.Settings.RoadBaseLength+p.Settings.CamberAxis+p.Settings.StartOffset+p.Settings.PitOffset+p.Settings.SmoothMix+p.Settings.ManualHeightIn+p.Settings.ManualHeightOut)||p.Settings.RoadWidth<=0)throw new InvalidDataException("Road width must be positive and settings must be finite numbers.");
        var s=p.Settings;var cps=Geometry.ControlPoints(p);var c=new Course{Points=cps,Corners=s.Circuit?p.Nodes.Count:p.Nodes.Count-2};
        double cum=0;int divsum=0;
        for(int i=1;i<cps.Count;i++){
            var cp=cps[i];double len=cp.Length,corner=cp.Form is 0 or 3||cp.SweepAngle==0?0:cp.SweepAngle/Abs(cp.SweepAngle);
            int div=Max(6,(int)Ceiling(len/8));if(div>2000000)throw new InvalidDataException("Track segment is too long.");
            c.Banks.Add(new(){Angle=cp.Camber*cp.CamberFactor*corner,Div=div,Index=divsum,Vpos=cum,Vlen=len,Corner=corner});cum+=len;divsum+=div;
        }
        if(divsum>5000000)throw new InvalidDataException("Track requires too many elevation samples.");
        c.PlanLength=cum;
        for(int i=0;i<c.Banks.Count;i++){var bank=c.Banks[i];var prev=c.Banks[(i+c.Banks.Count-1)%c.Banks.Count];double shift=Min(prev.Vlen,bank.Vlen)*.5;prev.ShiftNext=shift;bank.ShiftPrev=shift;}
        c.Heights=Elevation.Build(p,cps,c.Banks);double cum2=0,cum3=0;
        foreach(var b in c.Banks){double step=b.Vlen/b.Div,len=0;for(int k=0;k<b.Div;k++){int i=b.Index+k+1;double dz=i<c.Heights.Count?c.Heights[i]-c.Heights[i-1]:0;len+=Sqrt(step*step+dz*dz);}b.Pos2d=cum2;b.Len2d=b.Vlen;b.Vpos=cum3;b.Vlen=len;cum2+=b.Len2d;cum3+=len;}
        c.Length=cum3;double track2=Sum(c.Banks.Select(b=>b.Len2d)),limit=s.Circuit?track2:track2-1000,next=1000;cum=0;
        foreach(var b in c.Banks){cum+=b.Len2d;while(next<=limit+1e-9&&cum>=next-1e-9){c.Checkpoints.Add(To3d(c.Banks,next));next+=1000;}}
        if(c.Checkpoints.Count==0)c.Checkpoints.Add(To3d(c.Banks,track2*.5));
        c.Roads=Roads(p,c);c.Decorations=Decorations(p,c);
        SetLinePositions(p,c);return c;
    }
    // Same plan-view road/start/pit positions as a full build, without terrain
    // sampling, height smoothing, banking or decoration export during a drag.
    public static Course BuildPlanPreview(Project p)
    {
        var c=new Course{Points=Geometry.ControlPoints(p),Corners=p.Settings.Circuit?p.Nodes.Count:p.Nodes.Count-2};
        foreach(var cp in c.Points.Skip(1)){c.Banks.Add(new(){Pos2d=c.PlanLength,Vpos=c.PlanLength,Len2d=cp.Length,Vlen=cp.Length});c.PlanLength+=cp.Length;}
        c.Length=c.PlanLength;c.Roads=Roads(p,c);SetLinePositions(p,c);return c;
    }
    static void SetLinePositions(Project p,Course c)
    {
        var s=p.Settings;
        c.Start2d=Line(p,c,true);c.Finish2d=s.Circuit&&c.PlanLength>0?Mod(s.PitOffset,c.PlanLength):Line(p,c,false);
        c.PitStart2d=s.Circuit&&c.PlanLength>0?Mod(s.PitOffset,c.PlanLength):0;
        var data=Catalog.Roads(s.Scenery);var st=RoadSet(data,s.ShortStraight||!s.Circuit?"startsprint":"start");
        c.PitLength=s.ShortStraight?st[LineSet(data,"startlineP2P").Index].Len:Sum(st.Select(r=>r.Len));
        c.HasLinePositions=true;
    }
    public static double To3d(List<Bank> banks,double distance)=>ConvertDistance(banks,distance,true);
    public static double From3d(List<Bank> banks,double distance)=>ConvertDistance(banks,distance,false);
    static double ConvertDistance(List<Bank> banks,double distance,bool to3){
        for(int i=0;i<banks.Count;i++){var b=banks[i];double len=to3?b.Len2d:b.Vlen;if(distance<=len||i==banks.Count-1){double f=len==0?0:Clamp(distance/len,0,1);return (to3?b.Vpos:b.Pos2d)+(to3?b.Vlen:b.Len2d)*f;}distance-=len;}return 0;}
    public static double Mod(double x,double y){double m=x%y;if(m!=0&&Sign(m)!=Sign(y))m+=y;return m;}
    static List<(ulong Id,double Len)> RoadSet(JsonElement data,string name)=>data.GetProperty(name).EnumerateArray().Select(a=>(a[0].GetUInt64(),a[1].GetDouble())).ToList();
    static (int Index,double Factor) LineSet(JsonElement data,string name){var a=data.GetProperty(name);return(a[0].GetInt32(),a[1].GetDouble());}
    static List<Road> Roads(Project p,Course c)
    {
        var s=p.Settings;var data=Catalog.Roads(s.Scenery);var starts=RoadSet(data,s.ShortStraight||!s.Circuit?"startsprint":"start");if(s.ShortStraight)starts=[starts[LineSet(data,"startlineP2P").Index]];
        var ends=s.Circuit?new List<(ulong Id,double Len)>():RoadSet(data,"endsprint");double length=Sum(c.Banks.Select(b=>b.Len2d));if(length<.001)length=1;
        double baseLen=100*s.RoadBaseLength;if(baseLen<=0)baseLen=100;double without=Max(0,length-Sum(starts.Select(r=>r.Len))-Sum(ends.Select(r=>r.Len)));int div=Max(2,1+(int)Floor(without/baseLen));double fraction=(without/div)/100;
        if(div>1000000)throw new InvalidDataException("Road scale creates too many road segments.");
        var middle=RoadSet(data,s.NarrowRoad?"narrow":"normal");var raw=new List<(ulong Id,int Flag,double Len)>();foreach(var r in starts)raw.Add((r.Id,2,r.Len));
        raw.Add((middle[0].Id,s.RoadFlag,middle[0].Len*fraction));for(int i=0;i<div-2;i++)raw.Add((middle[s.NarrowRoad?1:0].Id,s.RoadFlag,middle[s.NarrowRoad?1:0].Len*fraction));raw.Add((middle[s.NarrowRoad?2:0].Id,s.RoadFlag,middle[s.NarrowRoad?2:0].Len*fraction));foreach(var r in ends)raw.Add((r.Id,2,r.Len));
        double visual=Sum(raw.Select(r=>r.Len));if(visual>0){double f=length/visual;for(int i=0;i<raw.Count;i++){var r=raw[i];raw[i]=(r.Id,r.Flag,r.Len*f);}}
        if(s.Circuit&&Abs(s.PitOffset)>.001){double offset=Mod(s.PitOffset,length),cut=length-offset,current=0,remainder=0;int split=0,target=-1;
            for(int i=0;i<raw.Count;i++){double end=current+raw[i].Len;if(current<=cut&&cut<end-.001){target=i;remainder=cut-current;split=remainder<=.001?i:-999;break;}current=end;}
            if(split== -999&&target!= -1){var r=raw[target];raw[target]=(r.Id,r.Flag,remainder);raw.Insert(target+1,(r.Id,r.Flag,r.Len-remainder));split=target+1;}raw=raw.Skip(split).Concat(raw.Take(split)).ToList();
        }
        raw=raw.Where(r=>r.Len>.1).ToList();var roads=new List<Road>();double pos=0;for(int i=0;i<raw.Count;i++){var r=raw[i];double begin=pos;pos+=r.Len;if(i==raw.Count-1&&Abs(pos-length)<.01)pos=length;roads.Add(new(r.Id,r.Flag,To3d(c.Banks,begin),To3d(c.Banks,pos)));}
        if(s.Circuit&&roads.Count>0)roads[^1]=roads[^1] with{End=0};return RoadPlacementEngine.Apply(p,c,roads);
    }
    static List<DecoEntry> Decorations(Project p,Course c)
    {
        var data=Catalog.Decorations(p.Settings.Scenery);var variants=data.GetProperty("variants").EnumerateArray().ToList();var entries=new List<DecoEntry>();
        var cl=data.GetProperty("cliff_left").EnumerateArray().Select(x=>x.GetUInt64()).ToArray();var cr=data.GetProperty("cliff_right").EnumerateArray().Select(x=>x.GetUInt64()).ToArray();
        foreach(var d in p.Decorations.OrderBy(d=>d.Vpos)){
            var v=variants.FirstOrDefault(v=>v.GetProperty("uuid").GetUInt64()==d.Uuid);double len=50;uint rot=0;ulong uuid=d.Uuid;
            if(v.ValueKind!=JsonValueKind.Undefined){len=v.GetProperty("len").GetDouble();if(len==0)len=50;int side=d.Side=="right"?1:0;var rotations=v.GetProperty("rot");rot=(uint)rotations[side].GetInt32();}
            double begin=d.Vpos-len*.5;if(begin<0)continue;uint sideIndex=d.Side=="right"?1u:0u;int code=d.Track switch{"CURB"=>1,"A"=>2,"B"=>3,_=>4};uint tt=code>=2?(uint)(code-2)+sideIndex*3:6+sideIndex;
            if((uuid==cl[1]||uuid==cr[1])&&(cl[1]!=0||cr[1]!=0)){var chain=uuid==cl[1]?cl:cr;double pos=begin;for(int i=0;i<3;i++){int index=(rot&1)!=0?2-i:i;double seg=len*(i==1?.5:.25);entries.Add(new(chain[index],rot,To3d(c.Banks,pos),To3d(c.Banks,pos+seg),tt));pos+=seg;}}
            else entries.Add(new(uuid,rot,To3d(c.Banks,begin),To3d(c.Banks,begin+len),tt));
        }
        return entries;
    }
    static double Line(Project p,Course c,bool start)
    {
        var s=p.Settings;if(!start&&s.Circuit)return 0;var data=Catalog.Roads(s.Scenery);string name=start?(s.ShortStraight||!s.Circuit?"startlineP2P":"startline"):"finishlineP2P";var (index,factor)=LineSet(data,name);var set=RoadSet(data,start?(s.ShortStraight||!s.Circuit?"startsprint":"start"):"endsprint");ulong target=set[index].Id;
        foreach(var road in c.Roads)if(road.Uuid==target){double a=From3d(c.Banks,road.Begin),b=From3d(c.Banks,road.End);double len=b==0?c.PlanLength-a:b<a?c.PlanLength-a+b:b-a;double d=a+len*factor;if(start)d+=s.StartOffset;return c.PlanLength>0?Mod(d,c.PlanLength):d;}
        double dist=0;if(!(start&&s.ShortStraight))for(int i=0;i<index;i++)dist+=set[i].Len;dist+=set[index].Len*factor;if(!start)dist+=c.PlanLength;
        if(c.PlanLength>.001){var st=RoadSet(data,s.ShortStraight||!s.Circuit?"startsprint":"start");double sl=s.ShortStraight?st[LineSet(data,"startlineP2P").Index].Len:Sum(st.Select(x=>x.Len)),el=s.Circuit?0:Sum(RoadSet(data,"endsprint").Select(x=>x.Len));double without=Max(0,c.PlanLength-sl-el),baseLen=100*s.RoadBaseLength;if(baseLen<=0)baseLen=100;int div=Max(2,1+(int)Floor(without/baseLen));double f=(without/div)/100;var mid=RoadSet(data,s.NarrowRoad?"narrow":"normal");double ml=mid[0].Len*f+mid[s.NarrowRoad?1:0].Len*f*(div-2)+mid[s.NarrowRoad?2:0].Len*f;double visual=sl+ml+el;if(visual>0)dist*=c.PlanLength/visual;}
        double result=dist+(start?s.StartOffset:0)+(s.Circuit?s.PitOffset:0);return c.PlanLength>0?Mod(result,c.PlanLength):result;
    }
    public static uint PackedDate(DateTime t)
    {
        int year=t.Year-1970;
        if(year is <0 or >63)throw new ArgumentOutOfRangeException(nameof(t),"TED timestamps support years 1970 through 2033.");
        return ((uint)year<<26)|((uint)t.Month<<22)|((uint)t.Day<<17)|((uint)t.Hour<<12)|((uint)t.Minute<<6)|(uint)t.Second;
    }
    public static byte[] Write(Project p,Course? course=null,uint? timestamp=null)
    {
        var c=course??Build(p);var s=p.Settings;int co=156,bo=co+c.Points.Count*20,ho=bo+c.Banks.Count*28,ck=ho+c.Heights.Count*4,ro=ck+c.Checkpoints.Count*4,de=ro+c.Roads.Count*20;
        var b=new byte[de+c.Decorations.Count*24];Binary.W64(b,0,5139792810303815680);Binary.W32(b,8,104);Binary.W32(b,12,(uint)s.Scenery);Binary.WF(b,16,s.RoadWidth);Binary.WF(b,20,s.RoadWidth/2);Binary.WF(b,24,s.RoadWidth/2);Binary.WF(b,28,Sum(c.Banks.Select(x=>x.Vlen)));Binary.W32(b,32,timestamp??PackedDate(DateTime.Now));Binary.W32(b,36,s.Circuit?1u:0u);
        Binary.WF(b,48,c.Banks.Where(x=>x.Corner==0).Select(x=>x.Vlen).DefaultIfEmpty(0).Max());Binary.WF(b,52,c.Heights.Count==0?0:c.Heights.Max()-c.Heights.Min());Binary.W32(b,56,(uint)c.Corners);double finish=Line(p,c,false),start=Line(p,c,true);Binary.WF(b,60,finish!=0?To3d(c.Banks,finish):0);Binary.WF(b,64,start!=0?To3d(c.Banks,start):0);
        void Table(int o,int offset,int count){Binary.W32(b,o,(uint)offset);Binary.W32(b,o+4,(uint)count);}
        Table(76,co,c.Points.Count);Table(84,bo,0);Table(92,bo,0);Table(100,bo,0);Table(108,bo,c.Banks.Count);Table(116,ho,c.Heights.Count);Table(124,ck,c.Checkpoints.Count);Table(132,ro,c.Roads.Count);Table(140,de,c.Decorations.Count);
        int pos=co;foreach(var cp in c.Points){Binary.W32(b,pos,cp.Form);Binary.WF(b,pos+4,cp.XY.X);Binary.WF(b,pos+8,cp.XY.Y);Binary.WF(b,pos+12,cp.Center.X);Binary.WF(b,pos+16,cp.Center.Y);pos+=20;}
        foreach(var bank in c.Banks){Binary.WF(b,pos,bank.Angle);Binary.WF(b,pos+4,bank.ShiftPrev);Binary.WF(b,pos+8,bank.ShiftNext);Binary.W32(b,pos+12,(uint)bank.Div);Binary.W32(b,pos+16,(uint)bank.Index);Binary.WF(b,pos+20,bank.Vpos);Binary.WF(b,pos+24,bank.Vlen);pos+=28;}
        foreach(double h in c.Heights){Binary.WF(b,pos,h);pos+=4;}foreach(double cp in c.Checkpoints){Binary.WF(b,pos,cp);pos+=4;}
        foreach(var r in c.Roads){Binary.W64(b,pos,r.Uuid);Binary.W32(b,pos+8,unchecked((uint)r.Flag));Binary.WF(b,pos+12,r.Begin);Binary.WF(b,pos+16,r.End);pos+=20;}
        foreach(var d in c.Decorations){Binary.W64(b,pos,d.Uuid);Binary.W32(b,pos+8,d.Rotation);Binary.WF(b,pos+12,d.Begin);Binary.WF(b,pos+16,d.End);Binary.W32(b,pos+20,d.TrackType);pos+=24;}return b;
    }
}
