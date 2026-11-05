using System.Globalization;
using System.Xml.Linq;
namespace TLCPlus;
public static class Importers
{
    public static List<Node> Csv(string path)
    {
        var points=new List<Node>();int row=0;
        foreach(string line in File.ReadLines(path)){row++;if(string.IsNullOrWhiteSpace(line)||line.TrimStart().StartsWith('#'))continue;var cells=line.Split([',',';','\t'],StringSplitOptions.TrimEntries);if(cells.Length<2)continue;
            if(!double.TryParse(cells[0],NumberStyles.Float,CultureInfo.InvariantCulture,out double x)||!double.TryParse(cells[1],NumberStyles.Float,CultureInfo.InvariantCulture,out double y)){if(points.Count==0)continue;throw new InvalidDataException($"Invalid coordinates at row {row}.");}
            double Read(int i,double d=0)=>i<cells.Length&&double.TryParse(cells[i],NumberStyles.Float,CultureInfo.InvariantCulture,out double n)?n:d;
            points.Add(new(){X=x,Y=y,Z=Read(2),Radius=Read(3,90),Camber=Read(4,-1),Segments=(int)Read(5,1)});
        }
        Validate(points);return points;
    }
    public static List<Node> Gpx(string path)
    {
        var xml=XDocument.Load(path);var gps=xml.Descendants().Where(e=>e.Name.LocalName is "trkpt" or "rtept").Select(e=>new{Lat=(double?)e.Attribute("lat")??0,Lon=(double?)e.Attribute("lon")??0,Z=(double?)e.Elements().FirstOrDefault(c=>c.Name.LocalName=="ele")??0}).ToList();
        if(gps.Count<2)throw new InvalidDataException("GPX has fewer than two path points.");double lat=gps.Average(p=>p.Lat),lon=gps.Average(p=>p.Lon);const double radius=6371000;
        var points=gps.Select(p=>new Node{X=(p.Lon-lon)*Math.PI/180*radius*Math.Cos(lat*Math.PI/180),Y=-(p.Lat-lat)*Math.PI/180*radius,Z=p.Z}).ToList();Validate(points);return Simplify(points,3);
    }
    static void Validate(List<Node> points){if(points.Count<2)throw new InvalidDataException("The path needs at least two coordinates.");if(points.Count>200000)throw new InvalidDataException("The path exceeds 200,000 coordinates.");if(points.Any(p=>!double.IsFinite(p.X+p.Y+p.Z+p.Radius+p.Camber)))throw new InvalidDataException("Non-finite coordinate.");}
    public static void WriteCsv(string path,IEnumerable<Node> points)=>File.WriteAllLines(path,new[]{"x,y,z,radius,camber,segments"}.Concat(points.Select(n=>string.Join(",",new double[]{n.X,n.Y,n.Z,n.Radius,n.Camber,n.Segments}.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))))));
    public static List<Node> Simplify(List<Node> points,double epsilon)
    {
        if(points.Count<3)return points;var keep=new HashSet<int>{0,points.Count-1};var stack=new Stack<(int A,int B)>();stack.Push((0,points.Count-1));
        while(stack.Count>0){var (a,b)=stack.Pop();double max=epsilon;int best=-1;var pa=points[a].XY;var pb=points[b].XY;double l=Geometry.Length(pa,pb);for(int i=a+1;i<b;i++){var p=points[i].XY;double d=l==0?Geometry.Length(p,pa):Math.Abs((pb.X-pa.X)*(pa.Y-p.Y)-(pa.X-p.X)*(pb.Y-pa.Y))/l;if(d>max){max=d;best=i;}}if(best>=0){keep.Add(best);stack.Push((a,best));stack.Push((best,b));}}
        return keep.Order().Select(i=>points[i]).ToList();
    }
    public static Project FromTed(TedDocument ted,double spacing=25)
    {
        var c=ted.Course;var p=new Project{Settings=ted.Settings};var points=new List<Node>();for(double d=0;d<c.PlanLength;d+=spacing){var (v,a)=Geometry.At(c.Points,d);points.Add(new(){XY=v,Radius=30,Segments=1});}if(!p.Settings.Circuit&&c.Points.Count>0)points.Add(new(){XY=c.Points[^1].XY,Radius=30});p.Nodes=Simplify(points,3);return p;
    }
}
public static class Generators
{
    public static Project Generate(string style,int? seed=null)
    {
        var rng=seed.HasValue?new Random(seed.Value):Random.Shared;double Uniform(double a,double b)=>a+(b-a)*rng.NextDouble();var p=new Project();int n;double ax,ay,min,max,irregular;
        switch(style){case "gp":n=rng.Next(12,19);ax=600;ay=400;min=40;max=140;irregular=.6;break;case "tech":n=rng.Next(18,27);ax=300;ay=300;min=20;max=60;irregular=.3;break;case "oval":n=4;ax=700;ay=300;min=100;max=200;irregular=.1;break;case "rally":n=rng.Next(38,56);ax=ay=min=max=irregular=0;break;default:n=rng.Next(8,31);ax=ay=500;min=10;max=150;irregular=.8;break;}
        if(style=="rally"){double x=rng.Next(-200,201);for(int i=0;i<n;i++){x=Math.Clamp(x+Uniform(-250,250),-1000,1000);p.Nodes.Add(new(){X=x,Y=-1100+i*(2200.0/(n-1)),Radius=Uniform(35,90)});}p.Settings.Circuit=false;}
        else for(int i=0;i<n;i++){double a=2*Math.PI*i/n+(style=="oval"?Math.PI/4:0),dx=ax*(1-irregular/2+rng.NextDouble()*irregular),dy=ay*(1-irregular/2+rng.NextDouble()*irregular),noise=style=="oval"?0:50;double x=Math.Cos(a)*dx+Uniform(-noise,noise),y=Math.Sin(a)*dy+Uniform(-noise,noise);double r=(min+(max-min)*Math.Sqrt(x*x+y*y)/Math.Max(ax,ay))*Uniform(.8,1.2);p.Nodes.Add(new(){X=x,Y=y,Radius=r});}return p;
    }
}
