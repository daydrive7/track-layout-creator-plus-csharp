namespace TLCPlus;
using static System.Math;
public record Arc(double? Sweep,double Radius,Vec Start,Vec End,Vec Center);
public class Curve
{
    public List<Arc> Arcs=[];
    public double Radius,Length,LineLength,Turn;
    public int Segments;
    public Vec Angular,Euler;
    public bool Hairpin;
    public Vec Start=>Arcs.Count>0?Arcs[0].Start:new();
    public Vec End=>Arcs.Count>0?Arcs[^1].End:new();
    public double Sweep=>PI-Abs(Geometry.VectorAngle(Start,End,Angular));
    public void Place(Vec p){var delta=p-Angular;Angular+=delta;Euler+=delta;Arcs=Arcs.Select(a=>a with{Start=a.Start+delta,End=a.End+delta,Center=a.Center+delta}).ToList();}
}
public static class Geometry
{
    sealed class Distances
    {
        public readonly double[] Values;
        public Distances(List<ControlPoint> points){Values=new double[points.Count];for(int i=1;i<points.Count;i++)Values[i]=Values[i-1]+points[i].Length;}
    }
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<List<ControlPoint>,Distances> distanceCache=new();
    public static double Length(Vec a,Vec b){var d=b-a;return Sqrt(d.X*d.X+d.Y*d.Y);}
    public static double Angle(Vec a,Vec b)=>-Atan2(b.Y-a.Y,b.X-a.X);
    public static Vec Interpolate(Vec center,double radius,double start,double sweep,double f)=>new(center.X+radius*Cos(start+sweep*f),center.Y-radius*Sin(start+sweep*f));
    public static double VectorAngle(Vec a,Vec b,Vec center=default){a-=center;b-=center;double n=Atan2(b.X,b.Y)-Atan2(a.X,a.Y);if(n>PI)n-=2*PI;else if(n< -PI)n+=2*PI;return -n;}
    public static Vec Intersection(Vec a,Vec b,Vec c,Vec d){double Det(Vec p,Vec q)=>p.X*q.Y-p.Y*q.X;Vec xd=new(a.X-b.X,c.X-d.X),yd=new(a.Y-b.Y,c.Y-d.Y);double div=Det(xd,yd);if(div==0)return (a+b)*.5;Vec v=new(Det(a,b),Det(c,d));return new(Det(v,xd)/div,Det(v,yd)/div);}
    public static List<Curve> Round(List<Node> nodes,bool circuit)
    {
        if(nodes.Count<2 || (circuit&&nodes.Count<3))throw new InvalidDataException("Place at least three nodes for a circuit, or two for a stage.");
        var p=nodes.ToList();if(circuit)p.Add(nodes[0]);var curves=new List<Curve>();
        Curve Corner(Vec p0,Vec p1,Node ap,double d0,double d1){double radius=ap.Radius==0?90:ap.Radius;radius=Max(radius,1);var c=FromPoints(p0,p1,ap.XY,Min(d0,d1),ap.Segments,radius);return c;}
        int n=p.Count;
        for(int index=0;index<(circuit?n-1:n-2);index++){
            if(index+2>=n)break;var point=p[index];var ap=p[index+1];Vec p1=p[index+2].XY,p0;double d0;
            if(index==0){p0=point.XY;d0=Length(p0,ap.XY);if(circuit)d0-=Min(d0/4,30);}
            else{p0=curves[^1].End;d0=Length(p0,ap.XY)-15;if(ap.Segments==1)d0+=15;}
            double d1=Length(p1,ap.XY);d1-=Min(d1/4,30);if(d0<0)d0=.1;if(d1<0)d1=.1;
            curves.Add(Corner(p0,p1,ap,d0,d1));
        }
        if(circuit&&n>2){var ap=p[^1];var p0=curves[^1].End;var p1=curves[0].Start;double d0=Length(p0,ap.XY)-15;if(ap.Segments==1)d0+=15;double d1=Length(p1,ap.XY);if(d0<0)d0=.1;if(d1<0)d1=.1;curves.Add(Corner(p0,p1,ap,d0,d1));}
        return curves;
    }
    public static Curve FromPoints(Vec p0,Vec p1,Vec center,double maxLength,int segments,double radius)
    {
        double start=Angle(p0,center),corner=VectorAngle(p0-center,p1-center),sweep=corner>0?PI-corner:-PI-corner;
        return Abs(sweep)>140*PI/180&&radius>10&&radius<30?Hairpin(start,sweep,radius,maxLength,segments,center):Euler(start,sweep,radius,maxLength,segments,center);
    }
    static Curve Euler(double start,double sweep,double radius,double maxLength,int segments,Vec position)
    {
        double angle=Min(Abs(sweep),60*PI/180);int maxSegments=segments==0?5:segments;
        var curve=new Curve{Radius=radius,Segments=segments,Turn=Abs(sweep)<.00001?1:sweep/Abs(sweep)};
        if(sweep<0)start+=PI;
        void Construct(){
            int seg=curve.Radius<1?1:(int)Floor(angle*curve.Radius/15-1);seg=Min(Max(seg,1),maxSegments);curve.Segments=seg;
            var a=new List<(double A,double R)>();for(int i=0;i<seg;i++){double r=curve.Radius*seg/(i+1);a.Add((r<.001?0:15/r,r));}
            double total=0;foreach(var x in a)total+=x.A;double overshoot=Abs(sweep)-total*2;
            if(a.Count>0)a[^1]=(a[^1].A+overshoot/2,a[^1].R);
            var all=a.Concat(a.AsEnumerable().Reverse()).ToList();curve.Arcs=[];double running=start;
            foreach(var s in all){double sa=s.A*curve.Turn;Vec sp=new(Sin(running)*s.R,Cos(running)*s.R);Vec ep=new(Sin(running+sa)*s.R,Cos(running+sa)*s.R);Vec cp=new();if(curve.Arcs.Count>0){var delta=curve.Arcs[^1].End-sp;cp+=delta;sp+=delta;ep+=delta;}curve.Arcs.Add(new(sa,s.R,sp,ep,cp));running+=sa;}
            var first=curve.Arcs[0];var last=curve.Arcs[^1];
            curve.Euler=curve.Radius==0?first.Center:Abs(sweep)%PI>.00001?Intersection(first.Start,first.Center,last.End,last.Center):(first.Center+last.Center)*.5;
            double er=Length(curve.Euler,curve.End),alpha=sweep/2,beta=start+alpha-.5*PI;
            double sec=Abs(Cos(alpha))<.00001?1000000:1/Cos(alpha);
            double l=Abs(Cos(alpha))<.001?er:Abs(sec*er);
            curve.Angular=new(curve.Euler.X+Cos(beta)*l,curve.Euler.Y-Sin(beta)*l);
            curve.LineLength=Length(curve.Angular,curve.End);curve.Length=0;foreach(var s in curve.Arcs)curve.Length+=Abs(s.Sweep!.Value*s.Radius);
        }
        Construct();int tries=0;while(curve.LineLength>maxLength&&tries<20){if(curve.Segments>1){maxSegments=curve.Segments-1;Construct();tries++;}else break;}
        if(curve.LineLength>.001&&curve.Segments==1&&curve.LineLength>maxLength){curve.Radius=Max(.1,curve.Radius*(maxLength/curve.LineLength));Construct();}
        curve.Place(position);return curve;
    }
    static Curve Hairpin(double start,double sweep,double radius,double maxLength,int factor,Vec position)
    {
        var curve=new Curve{Radius=radius,Segments=4,Hairpin=true,Turn=Abs(sweep)<.00001?1:sweep/Abs(sweep)};
        double b=90*PI/180-(PI-Abs(sweep))/2,c=90*PI/180-b;if(c<=0)c=.0001;
        double r1=0,r2=0,d1=0,d2=0;
        for(int counter=0;counter<10;counter++){r1=radius;if(c==0)c=.0001;r2=Max(30/c,90);double r3=r2/Cos(c/2);d1=r1/Sin(c);d2=r3*Sin(c/2);if(d1+d2>maxLength){if(d2>=maxLength)break;double scale=r2>90?maxLength/(d1+d2):(maxLength-d2)/d1;if(scale<=0)break;double nr=radius*scale;if(nr<1)break;radius=nr;}else break;}
        double straight=(15+radius*.5)*factor;curve.Radius=radius;
        (Vec a,Vec b,Vec c,Vec d,Vec e,Vec f,Vec g) Points(double angle,double turn){double aa=angle-PI;Vec pa=new(position.X+(d1+d2)*Cos(aa),position.Y+(d1+d2)*-Sin(aa));double ab=aa+PI/2*turn;Vec pb=new(pa.X+r2*Cos(ab),pa.Y+r2*-Sin(ab));double ac=ab-PI;Vec pc=Interpolate(pb,r2,ac,c*-turn,.5);double ad=ac+c*-turn;Vec pd=new(pb.X+r2*Cos(ad),pb.Y+r2*-Sin(ad));double ae=angle+c*-turn;Vec pe=new(pd.X+straight*Cos(ae),pd.Y+straight*-Sin(ae));Vec pf=new(pe.X+r1*Cos(ad),pe.Y+r1*-Sin(ad));Vec pg=Interpolate(pf,r1,ad-PI,PI*turn,.5);return(pa,pb,pc,pd,pe,pf,pg);}
        var p=Points(start,curve.Turn);curve.Arcs.Add(new(c/2*-curve.Turn,r2,p.a,p.c,p.b));curve.Arcs.Add(new(c/2*-curve.Turn,r2,p.c,p.d,p.b));if(straight!=0)curve.Arcs.Add(new(null,0,p.d,p.e,new()));curve.Arcs.Add(new(PI/2*curve.Turn,r1,p.e,p.g,p.f));
        p=Points(start+sweep-PI,-curve.Turn);curve.Arcs.Add(new(PI/2*curve.Turn,r1,p.g,p.e,p.f));if(straight!=0)curve.Arcs.Add(new(null,0,p.e,p.d,new()));curve.Arcs.Add(new(c/2*-curve.Turn,r2,p.d,p.c,p.b));curve.Arcs.Add(new(c/2*-curve.Turn,r2,p.c,p.a,p.b));
        foreach(var a in curve.Arcs)curve.Length+=a.Sweep is null?Length(a.Start,a.End):Abs(a.Sweep.Value)*a.Radius;
        curve.Angular=position;curve.LineLength=Length(position,curve.End);return curve;
    }
    public static List<ControlPoint> ControlPoints(Project p)
    {
        var s=p.Settings;var curves=Round(p.Nodes,s.Circuit);var nodes=p.Nodes;
        var cps=new List<ControlPoint>{new(){XY=s.Circuit?curves[^1].End:nodes[0].XY,Z=nodes[0].Z}};
        for(int index=0;index<curves.Count;index++){
            var curve=curves[index];var node=nodes[(index+1)%nodes.Count];var prev=curves[(index+curves.Count-1)%curves.Count];
            if(Length(prev.End,curve.Start)>.001)cps.Add(new(){XY=curve.Start,Z=node.Z});else cps[^1].XY=curve.Start;
            var reference=curve.Arcs[curve.Segments];double camber=node.Camber;
            if(camber== -1){double minLen=curve.Hairpin?Abs(reference.Sweep??0)*reference.Radius*2:curve.Length;camber=Min(s.CamberRate*minLen/2,s.MaxCamber);if(Abs(curve.Sweep)<30*PI/180)camber*=Abs(curve.Sweep)/(30*PI/180);}
            for(int i=0;i<curve.Arcs.Count;i++){var a=curve.Arcs[i];if(a.Sweep is null)cps.Add(new(){XY=a.End,Z=node.Z});else if(a.Radius>0){double cf=reference.Radius==0||a.Radius==0?0:1/(a.Radius/reference.Radius);cf=Min(cf,1);cps.Add(new(){XY=a.End,Center=a.Center,Form=(a.Sweep>0?1u:2147483649u)+(uint)(i%2),Radius=a.Radius,StartAngle=Angle(a.Center,a.Start),SweepAngle=a.Sweep.Value,Camber=camber,CamberFactor=cf,Z=node.Z});}}
        }
        if(!s.Circuit&&(curves.Count==0||curves[^1].End!=nodes[^1].XY))cps.Add(new(){XY=nodes[^1].XY,Z=nodes[^1].Z});
        for(int i=1;i<cps.Count;i++){var cp=cps[i];cp.Length=cp.Form is 0 or 3?Length(cps[i-1].XY,cp.XY):Abs(cp.Radius*cp.SweepAngle);}
        return cps;
    }
    public static (Vec Point,double Angle) At(List<ControlPoint> cps,double distance)
    {
        var cumulative=distanceCache.GetValue(cps,points=>new Distances(points)).Values;
        int lo=1,hi=cps.Count;while(lo<hi){int mid=(lo+hi)/2;if(cumulative[mid]<=distance)lo=mid+1;else hi=mid;}
        int i=Min(lo,cps.Count-1);double d=cumulative[i-1];
        var cp=cps[i];double f=cp.Length==0?0:Clamp((distance-d)/cp.Length,0,1);
        if(cp.Form is 0 or 3)return(cps[i-1].XY+(cp.XY-cps[i-1].XY)*f,Angle(cps[i-1].XY,cp.XY));
        double turn=Abs(cp.SweepAngle)<1e-6?1:cp.SweepAngle/Abs(cp.SweepAngle);
        return(Interpolate(cp.Center,cp.Radius,cp.StartAngle,cp.SweepAngle,f),cp.StartAngle+cp.SweepAngle*f+90*PI/180*turn);
    }
}
