using System.Buffers.Binary;
using System.ComponentModel;
using System.Text.Json;
namespace TLCPlus;

public record struct Vec(double X, double Y)
{
    public static Vec operator +(Vec a, Vec b) => new(a.X+b.X,a.Y+b.Y);
    public static Vec operator -(Vec a, Vec b) => new(a.X-b.X,a.Y-b.Y);
    public static Vec operator *(Vec a, double b) => new(a.X*b,a.Y*b);
}
public class Node
{
    [Description("Horizontal position in metres.")]
    public double X {get;set;}
    [Description("Vertical position on the plan, in metres.")]
    public double Y {get;set;}
    [Description("Manual elevation offset. Enable manual height in Geometry to apply it.")]
    public double Z {get;set;}
    [Description("Corner radius in metres. Corners are automatically fitted to the available space.")]
    public double Radius {get;set;}=90;
    [Description("Banking angle in degrees. Use -1 to calculate from the global settings.")]
    public double Camber {get;set;}=-1;
    [Description("1 = circular curve. 0 = automatic Euler spiral. 2 or more = maximum Euler subdivisions.")]
    public int Segments {get;set;}=1;
    [Browsable(false)] public Vec XY {get=>new(X,Y);set{X=value.X;Y=value.Y;}}
    public Node Copy() => (Node)MemberwiseClone();
}
public class Settings
{
    [Category("Track"),DisplayName("Terrain"),TypeConverter(typeof(SceneryConverter)),Description("Terrain data from the original TLC+ v1.3.0 release.")]
    public int Scenery {get;set;}=5;
    [Category("Track"),DisplayName("Circuit"),Description("Close the layout into a circuit. Turn off for a point-to-point stage.")]
    public bool Circuit {get;set;}=true;
    [Category("Road"),DisplayName("Road width (m)")]
    public double RoadWidth {get;set;}=10;
    [Category("Banking"),DisplayName("Maximum angle (degrees)")]
    public double MaxCamber {get;set;}=3;
    [Category("Banking"),DisplayName("Angle per metre")]
    public double CamberRate {get;set;}=.15;
    [Browsable(false)]
    public int SectorScale {get;set;}=4;
    [Category("Road"),DisplayName("Segment length multiplier")]
    public double RoadBaseLength {get;set;}=1;
    [Category("Road"),DisplayName("Short home straight")]
    public bool ShortStraight {get;set;}
    [Category("Banking"),DisplayName("Pivot axis"),Description("-1, 0 or 1 moves the camber pivot across the road.")]
    public double CamberAxis {get;set;}
    [Category("Road"),DisplayName("Narrow road meshes")]
    public bool NarrowRoad {get;set;}
    [Category("Road"),DisplayName("Road flag"),Description("Original TED mesh flag; preserved in TRK5.")]
    public int RoadFlag {get;set;}
    [Category("Start and pits"),DisplayName("Start line offset (m)")]
    public double StartOffset {get;set;}
    [Category("Start and pits"),DisplayName("Pit offset (m)")]
    public double PitOffset {get;set;}
    [Category("Elevation"),DisplayName("Enable manual heights")]
    public bool ManualHeight {get;set;}
    [Category("Elevation"),DisplayName("Uphill transition"),Description("Fraction of a segment used by the uphill ramp, from 0 to 1.")]
    public double ManualHeightIn {get;set;}=1;
    [Category("Elevation"),DisplayName("Downhill transition"),Description("Fraction of a segment used by the downhill ramp, from 0 to 1.")]
    public double ManualHeightOut {get;set;}=1;
    [Category("Elevation"),DisplayName("Smooth manual ramps")]
    public bool ManualHeightEase {get;set;}=true;
    [Category("Elevation"),DisplayName("Terrain smoothing"),Description("Blend from raw terrain (0) to smoothed terrain (1).")]
    public double SmoothMix {get;set;}=1;
}
public class SceneryConverter:Int32Converter
{
    static readonly Dictionary<int,string> names=new(){[1]="Death Valley",[2]="Eifel",[3]="Andalusia",[5]="Eifel Flat"};
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context)=>true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context)=>true;
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)=>new(names.Keys.ToArray());
    public override object? ConvertTo(ITypeDescriptorContext? context,System.Globalization.CultureInfo? culture,object? value,Type destinationType)=>destinationType==typeof(string)&&value is int i?names.GetValueOrDefault(i,i.ToString()):base.ConvertTo(context,culture,value,destinationType);
    public override object? ConvertFrom(ITypeDescriptorContext? context,System.Globalization.CultureInfo? culture,object value)=>value is string text&&names.ContainsValue(text)?names.First(p=>p.Value==text).Key:base.ConvertFrom(context,culture,value);
}
public class Decoration
{
    public ulong Uuid {get;set;}
    public int Type {get;set;}=1;
    public string Side {get;set;}="left";
    public string Track {get;set;}="AB";
    public double Vpos {get;set;}
}
public class Project
{
    public const int DecorationLimit=2000;
    public Settings Settings {get;set;}=new();
    public List<Node> Nodes {get;set;}=[];
    public List<Decoration> Decorations {get;set;}=[];
    public List<RoadPlacement> RoadPlacements {get;set;}=[];
    public short ReservedCt {get;set;}
    public short ReservedCm {get;set;}
    public ushort? OriginalSceneryCopy {get;set;}
    public int? LoadedScenery {get;set;}
    public Project Copy()=>JsonSerializer.Deserialize<Project>(JsonSerializer.Serialize(this))!;
    public void Save(string path)=>File.WriteAllText(path,JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));
    public static Project Load(string path)=>JsonSerializer.Deserialize<Project>(File.ReadAllText(path))??throw new InvalidDataException("Empty project");
}
public class ControlPoint
{
    public Vec XY, Center;
    public uint Form;
    public double Radius,StartAngle,SweepAngle,Camber,CamberFactor,Z,Length;
}
public class Bank
{
    public double Angle,ShiftPrev,ShiftNext,Vpos,Vlen,Corner,Pos2d,Len2d;
    public int Div,Index;
}
public record Road(ulong Uuid,int Flag,double Begin,double End);
public record DecoEntry(ulong Uuid,uint Rotation,double Begin,double End,uint TrackType);
public static class Binary
{
    public static ushort U16(byte[] b,int o)=>BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(o));
    public static short I16(byte[] b,int o)=>BinaryPrimitives.ReadInt16BigEndian(b.AsSpan(o));
    public static uint U32(byte[] b,int o)=>BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(o));
    public static ulong U64(byte[] b,int o)=>BinaryPrimitives.ReadUInt64BigEndian(b.AsSpan(o));
    public static double F32(byte[] b,int o)=>BinaryPrimitives.ReadSingleBigEndian(b.AsSpan(o));
    public static void W16(byte[] b,int o,int n)=>BinaryPrimitives.WriteInt16BigEndian(b.AsSpan(o),checked((short)n));
    public static void W32(byte[] b,int o,uint n)=>BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(o),n);
    public static void W64(byte[] b,int o,ulong n)=>BinaryPrimitives.WriteUInt64BigEndian(b.AsSpan(o),n);
    public static void WF(byte[] b,int o,double n)=>BinaryPrimitives.WriteSingleBigEndian(b.AsSpan(o),(float)n);
}
public static class TrkFormat
{
    public static Project Read(byte[] b)
    {
        if(b.Length<32) throw new InvalidDataException("Track header is truncated.");
        string id=System.Text.Encoding.ASCII.GetString(b,0,4).TrimEnd('\0');
        if(id is not ("TRK" or "TRK2" or "TRK3" or "TRK4" or "TRK5")) throw new InvalidDataException("Unknown track signature.");
        int header=id=="TRK5"?36:32, stride=id is "TRK5" or "TRK4"?24:id=="TRK3"?20:id=="TRK2"?16:12;
        if(b.Length<header || (b.Length-header)%stride!=0) throw new InvalidDataException("Track point table is truncated.");
        int offset=id=="TRK5"?6:4;
        int Field(int i)=>id=="TRK5"?Binary.I16(b,offset+i*2):Binary.U16(b,offset+i*2);
        var p=new Project{ReservedCt=(short)Field(8),ReservedCm=(short)Field(9)};
        var s=p.Settings;
        s.Scenery=Field(0);p.LoadedScenery=s.Scenery;s.Circuit=Field(1)!=0;s.RoadWidth=Field(2)/20.0;s.MaxCamber=Field(3)/10.0;
        s.CamberRate=Field(4)/100.0;s.SectorScale=Field(5);s.RoadBaseLength=Field(6)/10.0;s.ShortStraight=Field(7)!=0;
        s.CamberAxis=Field(10)/10.0-1;s.NarrowRoad=Field(11)!=0;
        if(id is "TRK5" or "TRK4")s.RoadFlag=Field(12);
        if(id=="TRK5"){s.StartOffset=Binary.I16(b,32);s.PitOffset=Binary.I16(b,34);p.OriginalSceneryCopy=Binary.U16(b,4);}
        for(int o=header;o<b.Length;o+=stride){
            var n=new Node{X=Binary.F32(b,o),Y=Binary.F32(b,o+4)};
            if(id is "TRK5" or "TRK4"){n.Z=Binary.F32(b,o+8);n.Radius=Binary.F32(b,o+12);n.Camber=Binary.F32(b,o+16);n.Segments=(int)Binary.F32(b,o+20);}
            else if(id=="TRK3"){n.Radius=Binary.F32(b,o+8);n.Camber=Binary.F32(b,o+12);n.Segments=(int)Binary.F32(b,o+16);}
            else{n.Y=-n.Y;n.Radius=Binary.F32(b,o+8);if(id=="TRK2")n.Camber=Binary.F32(b,o+12);}
            if(!double.IsFinite(n.X+n.Y+n.Z+n.Radius+n.Camber)||n.Segments<0||n.Segments>10000)throw new InvalidDataException("Track contains invalid point values.");
            p.Nodes.Add(n);
        }
        return p;
    }
    public static byte[] Write(Project p)
    {
        var s=p.Settings;var b=new byte[checked(36+p.Nodes.Count*24)];"TRK5"u8.CopyTo(b);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(4),p.LoadedScenery==s.Scenery&&p.OriginalSceneryCopy.HasValue?p.OriginalSceneryCopy.Value:checked((ushort)s.Scenery));
        double[] vals=[s.Scenery,s.Circuit?1:0,s.RoadWidth*20,s.MaxCamber*10,s.CamberRate*100,s.SectorScale,s.RoadBaseLength*10,s.ShortStraight?1:0,p.ReservedCt,p.ReservedCm,(1+s.CamberAxis)*10,s.NarrowRoad?1:0,s.RoadFlag,s.StartOffset,s.PitOffset];
        for(int i=0;i<vals.Length;i++)Binary.W16(b,6+2*i,checked((int)vals[i]));
        int o=36;foreach(var n in p.Nodes){double[] v=[n.X,n.Y,n.Z,n.Radius,n.Camber,n.Segments];foreach(var x in v){Binary.WF(b,o,x);o+=4;}}
        return b;
    }
}
