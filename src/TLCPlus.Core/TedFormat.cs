namespace TLCPlus;
public sealed class TedDocument
{
    public byte[] Bytes {get;private set;}=[];
    public Course Course {get;private set;}=new();
    public Settings Settings {get;private set;}=new();
    public static TedDocument Read(byte[] b)
    {
        if(b.Length<156||Binary.U64(b,0)!=5139792810303815680)throw new InvalidDataException("Invalid or truncated GT6TED header.");
        var doc=new TedDocument{Bytes=(byte[])b.Clone()};var c=doc.Course;var s=doc.Settings;
        s.Scenery=(int)Binary.U32(b,12);s.RoadWidth=Binary.F32(b,16);s.Circuit=Binary.U32(b,36)!=0;c.Length=Binary.F32(b,28);c.Corners=(int)Binary.U32(b,56);
        (int O,int N) Table(int at,int stride){uint o=Binary.U32(b,at),n=Binary.U32(b,at+4);if(o>b.Length||(ulong)o+(ulong)n*(uint)stride>(ulong)b.Length||n>5000000)throw new InvalidDataException($"TED table at header {at} is outside the file.");return((int)o,(int)n);}
        var (off,n)=Table(76,20);for(int i=0;i<n;i++){int p=off+i*20;var cp=new ControlPoint{Form=Binary.U32(b,p),XY=new(Binary.F32(b,p+4),Binary.F32(b,p+8)),Center=new(Binary.F32(b,p+12),Binary.F32(b,p+16))};if(i>0&&cp.Form is not (0 or 3)){cp.Radius=Geometry.Length(cp.Center,cp.XY);cp.StartAngle=Geometry.Angle(cp.Center,c.Points[^1].XY);cp.SweepAngle=Geometry.Angle(cp.Center,cp.XY)-cp.StartAngle;if(cp.Form is 1 or 2&&cp.SweepAngle<0)cp.SweepAngle+=2*Math.PI;else if(cp.Form is not (1 or 2)&&cp.SweepAngle>0)cp.SweepAngle-=2*Math.PI;}if(i>0)cp.Length=cp.Form is 0 or 3?Geometry.Length(c.Points[^1].XY,cp.XY):Math.Abs(cp.Radius*cp.SweepAngle);c.Points.Add(cp);}
        c.PlanLength=c.Points.Sum(p=>p.Length);
        (off,n)=Table(108,28);double pos2=0;for(int i=0;i<n;i++){int p=off+i*28;double l2=i+1<c.Points.Count?c.Points[i+1].Length:0;c.Banks.Add(new(){Angle=Binary.F32(b,p),ShiftPrev=Binary.F32(b,p+4),ShiftNext=Binary.F32(b,p+8),Div=checked((int)Binary.U32(b,p+12)),Index=checked((int)Binary.U32(b,p+16)),Vpos=Binary.F32(b,p+20),Vlen=Binary.F32(b,p+24),Pos2d=pos2,Len2d=l2});pos2+=l2;}
        (off,n)=Table(116,4);for(int i=0;i<n;i++)c.Heights.Add(Binary.F32(b,off+i*4));
        (off,n)=Table(124,4);for(int i=0;i<n;i++)c.Checkpoints.Add(Binary.F32(b,off+i*4));
        (off,n)=Table(132,20);for(int i=0;i<n;i++){int p=off+i*20;c.Roads.Add(new(Binary.U64(b,p),unchecked((int)Binary.U32(b,p+8)),Binary.F32(b,p+12),Binary.F32(b,p+16)));}
        (off,n)=Table(140,24);for(int i=0;i<n;i++){int p=off+i*24;c.Decorations.Add(new(Binary.U64(b,p),Binary.U32(b,p+8),Binary.F32(b,p+12),Binary.F32(b,p+16),Binary.U32(b,p+20)));}
        c.Start2d=Exporter.From3d(c.Banks,Binary.F32(b,64));c.Finish2d=Exporter.From3d(c.Banks,Binary.F32(b,60));c.HasLinePositions=true;
        return doc;
    }
    public byte[] Write()=>(byte[])Bytes.Clone();
    public void SetRoadWidth(double width){if(!double.IsFinite(width)||width<=0)throw new ArgumentOutOfRangeException(nameof(width));Settings.RoadWidth=width;Binary.WF(Bytes,16,width);Binary.WF(Bytes,20,width/2);Binary.WF(Bytes,24,width/2);}
}
