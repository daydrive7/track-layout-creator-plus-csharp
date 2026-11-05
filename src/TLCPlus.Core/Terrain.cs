using System.Buffers.Binary;
namespace TLCPlus;
using static System.Math;
public class Terrain
{
    readonly byte[] bytes;
    readonly int cols,rows;
    readonly double min,max,intervalX,intervalY;
    public double Size {get;}
    static readonly Dictionary<int,Terrain> cache=[];
    public static string AssetsPath {get;set;}=Path.Combine(AppContext.BaseDirectory,"Assets");
    public static Terrain Get(int scenery)
    {
        if(!cache.TryGetValue(scenery,out var terrain)){string name=scenery switch{1=>"death_valley",2=>"eifel",3=>"andalusia",_=>"eifel_flat"};double size=scenery switch{1=>6000,2=>12000,3=>7000,_=>12000};terrain=new(File.ReadAllBytes(Path.Combine(AssetsPath,"maps",name)),size);cache[scenery]=terrain;}return terrain;
    }
    Terrain(byte[] b,double size){bytes=b;Size=size;cols=(int)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(0x1028));rows=(int)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(0x102c));min=BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(0x1030));max=BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(0x1034));intervalX=size/(cols-1);intervalY=size/(rows-1);}
    double Raw(int x,int y)=> (double)BitConverter.UInt16BitsToHalf(BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x1040+2*(y*cols+x))));
    public double Sample(double x,double y)
    {
        x=Clamp(x+Size*.5,0,Size);y=Clamp(y+Size*.5,0,Size);
        int col=Clamp((int)Floor(x/intervalX),0,cols-2),row=Clamp((int)Floor(y/intervalY),0,rows-2);
        double fx=(x-col*intervalX)/intervalX,fy=(y-row*intervalY)/intervalY;
        double az=Raw(col,row),bz=Raw(col+1,row),cz=Raw(col,row+1),dz=Raw(col+1,row+1);
        double top=az+(bz-az)*fx,bottom=cz+(dz-cz)*fx;
        return (top+(bottom-top)*fy)*(max-min)+min;
    }
}
public static class Elevation
{
    public static List<(double D,double Z)> Smooth(List<(double D,double Z)> heights)
    {
        double Slope(List<(double D,double Z)> s)=>s[^1].D==s[0].D?0:(s[^1].Z-s[0].Z)/(s[^1].D-s[0].D);
        var slopes=new List<List<(double D,double Z)>>();int pos=0;
        while(heights.Count-pos>=5){var current=heights.GetRange(pos,5);pos+=5;double slope=Slope(current);
            while(heights.Count-pos>=5&&current.Count<=60){var next=heights[pos];if(Abs(Slope([current[^1],next])-slope)<.04){current.Add(next);pos++;}else break;}
            if(heights.Count-pos<5){current.AddRange(heights.Skip(pos));pos=heights.Count;}slopes.Add(current);
        }
        for(int i=1;i<slopes.Count-1;i++){var s=new List<(double D,double Z)>{slopes[i-1][^1]};s.AddRange(slopes[i]);double k=Slope(s),x=s[0].D,z=s[0].Z;slopes[i]=s.Skip(1).Select(p=>(p.D,z+(p.D-x)*k)).ToList();}
        var curves=new List<(double D,double Z)>();
        for(int i=0;i<slopes.Count-1;i++){
            var sa=slopes[i];var sb=slopes[i+1];double m0=Slope(sa),m1=Slope(sb);int ia=sa.Count-sa.Count/2,ib=sb.Count/2;var a=sa[ia];var b=sb[ib];
            foreach(var p in sa.Skip(ia+1).Concat(sb.Take(ib))){double p1=b.D==a.D?0:(b.Z-a.Z)/(b.D-a.D),t=b.D==a.D?0:(p.D-a.D)/(b.D-a.D);double t3=Pow(t,3),t2=Pow(t,2);double v=(t3-2*t2+t)*m0+(-2*t3+3*t2)*p1+(t3-t2)*m1;curves.Add((p.D,v*(b.D-a.D)+a.Z));}
        }
        var cx=curves.Select(p=>p.D).ToHashSet();var assembly=slopes.SelectMany(s=>s).Where(p=>!cx.Contains(p.D)).Concat(curves).OrderBy(p=>p.D).ToList();return assembly;
    }
    public static double Manual(double distance,List<(double D,double Z)> nodes,Settings s)
    {
        for(int i=0;i<nodes.Count-1;i++){var a=nodes[i];var b=nodes[i+1];if(a.D<=distance&&distance<=b.D){if(a.D==b.D)return a.Z;double raw=(distance-a.D)/(b.D-a.D),f=raw;
            if(b.Z>a.Z){if(s.ManualHeightIn<=.001)f=raw<.999?0:1;else{double start=1-s.ManualHeightIn;f=raw<=start?0:(raw-start)/s.ManualHeightIn;}}
            else if(b.Z<a.Z){if(s.ManualHeightOut<=.001)f=raw>.001?1:0;else f=raw>=s.ManualHeightOut?1:raw/s.ManualHeightOut;}
            f=Clamp(f,0,1);if(s.ManualHeightEase&&f>0&&f<1)f=f*f*(3-2*f);return a.Z+(b.Z-a.Z)*f;}}
        return nodes[^1].Z;
    }
    public static List<double> Build(Project p,List<ControlPoint> cps,List<Bank> banks)
    {
        var s=p.Settings;var terrain=Terrain.Get(s.Scenery);double border=s.RoadWidth+(s.NarrowRoad?16:40),roadRadius=s.RoadWidth/2;
        var znodes=new List<(double D,double Z)>{(0,cps[0].Z)};double cum=0;for(int i=1;i<cps.Count;i++){cum+=cps[i].Length;znodes.Add((cum,cps[i].Z));}
        var rough=new List<(double D,double Z)>();var offsets=new List<double>();
        for(int index=0;index<banks.Count;index++){
            var bank=banks[index];double prev=banks[(index+banks.Count-1)%banks.Count].Angle,next=banks[(index+1)%banks.Count].Angle,angle=bank.Angle;
            double start=index==0?angle:index==1?prev:prev+(angle-prev)/2;
            double end=index==0?angle:index==banks.Count-1?(s.Circuit?next:angle):angle+(next-angle)/2;
            double nextStart=bank.Vlen-bank.ShiftNext;
            for(int i=index==0?-1:0;i<bank.Div;i++){
                double rel=(i+1)*(bank.Vlen/bank.Div),distance=bank.Vpos+rel,camber=angle;
                if(rel<bank.ShiftPrev)camber=start+(angle-start)*(rel/bank.ShiftPrev);
                else if(rel>nextStart)camber=angle+(end-angle)*((rel-nextStart)/bank.ShiftNext);
                offsets.Add(Sin(Abs(camber*PI/180))*roadRadius*-s.CamberAxis);
                var (point,a)=Geometry.At(cps,distance);double r=border/2,dx=r*Cos(a+PI/2),dy=r*-Sin(a+PI/2);
                double h=Min(terrain.Sample(point.X-dx,point.Y-dy),terrain.Sample(point.X+dx,point.Y+dy));rough.Add((distance,h));
            }
        }
        var smooth=Smooth(rough);
        double Smoothed(double d){if(d<=smooth[0].D)return smooth[0].Z;if(d>=smooth[^1].D)return smooth[^1].Z;int lo=0,hi=smooth.Count;while(lo<hi){int mid=(lo+hi)/2;if(smooth[mid].D<=d)lo=mid+1;else hi=mid;}var a=smooth[lo-1];var b=smooth[lo];return b.D==a.D?a.Z:a.Z+(b.Z-a.Z)*((d-a.D)/(b.D-a.D));}
        var result=new List<double>();for(int i=0;i<rough.Count;i++){var (d,z)=rough[i];double h=smooth.Count==0?z:Smoothed(d)*s.SmoothMix+z*(1-s.SmoothMix);if(s.ManualHeight)h+=Manual(d,znodes,s);result.Add(h+offsets[i]);}return result;
    }
}
