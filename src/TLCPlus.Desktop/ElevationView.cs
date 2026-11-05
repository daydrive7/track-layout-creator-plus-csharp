using System.Drawing.Drawing2D;
using TLCPlus;
namespace TLCPlus.Desktop;

// Display terrain-relative elevation and slope, before TED camber adjustments.
public class ElevationView:Control
{
    public Course? Course;
    public Project? Project;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public double? CursorDistance {get;set;}
    public event Action<double>? CursorMoved;
    public event Action? CursorLeft;
    Course? cachedCourse;Project? cachedProject;int cachedWidth;
    double[] distances=[],heights=[],slopes=[];
    float plotLeft=42,plotRight;
    public ElevationView(){DoubleBuffered=true;BackColor=Color.FromArgb(30,30,30);Height=100;MinimumSize=new(0,70);SetStyle(ControlStyles.ResizeRedraw,true);}
    void Profile(Course c)
    {
        if(cachedCourse==c&&cachedProject==Project&&cachedWidth==Width)return;cachedCourse=c;cachedProject=Project;cachedWidth=Width;
        if(c.Points.Count<2){distances=heights=slopes=[];return;}
        bool original=Project is {Nodes.Count:>1};var settings=Project?.Settings??new Settings();int scenery=settings.Scenery;
        double offset=TerrainRenderer.BaseElevation(scenery),total=c.PlanLength>0?c.PlanLength:c.Length;if(total<=0){distances=heights=slopes=[];return;}
        if(original)
        {
            int n=Math.Clamp(Width/(settings.ManualHeightEase?2:10),2,2400);distances=new double[n+1];heights=new double[n+1];
            var terrain=Terrain.Get(scenery);double radius=(settings.RoadWidth+(settings.NarrowRoad?16:40))/2;
            var zNodes=new List<(double D,double Z)>{(0,c.Points[0].Z)};double cum=0;for(int i=1;i<c.Points.Count;i++){cum+=c.Points[i].Length;zNodes.Add((cum,c.Points[i].Z));}
            for(int i=0;i<=n;i++){double d=total*i/n;distances[i]=d;var (v,angle)=Geometry.At(c.Points,d);double dx=radius*Math.Cos(angle+Math.PI/2),dy=radius*-Math.Sin(angle+Math.PI/2);double h=Math.Min(terrain.Sample(v.X-dx,v.Y-dy),terrain.Sample(v.X+dx,v.Y+dy))-offset;if(settings.ManualHeight)h+=Elevation.Manual(d,zNodes,settings);heights[i]=h;}
            if(settings.ManualHeightEase&&heights.Length>4){var smooth=new double[heights.Length];for(int i=0;i<heights.Length;i++){int a=Math.Max(0,i-4),b=Math.Min(heights.Length,i+5);double sum=0;for(int j=a;j<b;j++)sum+=heights[j];smooth[i]=sum/(b-a);}heights=smooth;}
        }
        else
        {
            distances=new double[c.Heights.Count];heights=c.Heights.Select(z=>z-offset).ToArray();
            // Bank division counts vary, so sample indices are not evenly spaced in distance.
            foreach(var bank in c.Banks)for(int i=0;i<=bank.Div;i++){int index=bank.Index+i;if(index<distances.Length)distances[index]=bank.Pos2d+bank.Len2d*i/Math.Max(1,bank.Div);}
            if(c.Banks.Count==0)for(int i=0;i<distances.Length;i++)distances[i]=total*i/Math.Max(1,distances.Length-1);
        }
        slopes=new double[heights.Length];for(int i=0;i<heights.Length-1;i++){double d=distances[i+1]-distances[i];slopes[i]=d==0?0:(heights[i+1]-heights[i])/d;}if(slopes.Length>1)slopes[^1]=settings.Circuit?slopes[0]:slopes[^2];
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(BackColor);if(Course is not {Heights.Count:>1} c)return;Profile(c);if(heights.Length<2)return;
        double min=heights.Min(),max=heights.Max();if(max-min<10){double mid=(max+min)/2;min=mid-5;max=mid+5;}
        using var font=new Font("Segoe UI",8);Color blue=UiTheme.NavLine,orange=UiTheme.NavSlope;string upper=$"{max:0}m",lower=$"{min:0}m";
        plotLeft=Math.Max(28,Math.Max(g.MeasureString(upper,font).Width,g.MeasureString(lower,font).Width)+7);plotRight=Width-38;float top=17,bottom=Math.Max(top+16,Height-17),plotWidth=plotRight-plotLeft,plotHeight=bottom-top;if(plotWidth<=0)return;
        double total=distances[^1]>0?distances[^1]:1;float X(double d)=>plotLeft+(float)(d/total*plotWidth);float Z(double z)=>bottom-(float)((z-min)/(max-min)*plotHeight);float S(double slope)=>bottom-(float)((slope+.3)/.6*plotHeight);
        using var blueBrush=new SolidBrush(blue);using var orangeBrush=new SolidBrush(orange);using var greyBrush=new SolidBrush(Color.FromArgb(115,115,115));g.DrawString(UiLanguage.T("Elevation"),font,blueBrush,plotLeft+5,1);var slopeLabel=g.MeasureString("Slope",font);g.DrawString(UiLanguage.T("Slope"),font,orangeBrush,plotRight-slopeLabel.Width,1);
        using var grid=new Pen(UiTheme.NavGrid);using var dashGrid=new Pen(UiTheme.NavGrid){DashPattern=[2,2]};for(int i=0;i<5;i++){double slope=-.3+i*.15;float y=S(slope);g.DrawLine(i%2==1?dashGrid:grid,plotLeft,y,plotRight,y);g.DrawString($"{slope*100:0}%",font,orangeBrush,plotRight+3,y-6);}
        g.DrawString(upper,font,blueBrush,plotLeft-g.MeasureString(upper,font).Width-4,top-6);g.DrawString(lower,font,blueBrush,plotLeft-g.MeasureString(lower,font).Width-4,bottom-6);
        double step=NiceStep(total/Math.Max(2,plotWidth/130));for(double d=0;d<=total+1e-6;d+=step){float x=X(d);g.DrawLine(dashGrid,x,top,x,bottom);string text=$"{d:0}m";var size=g.MeasureString(text,font);g.DrawString(text,font,greyBrush,Math.Clamp(x-size.Width/2,plotLeft,plotRight-size.Width),bottom+2);}
        var zLine=new PointF[heights.Length];var slopeLine=new PointF[heights.Length];for(int i=0;i<heights.Length;i++){zLine[i]=new(X(distances[i]),Z(heights[i]));slopeLine[i]=new(X(distances[i]),S(slopes[i]));}
        var clip=g.Save();g.SetClip(new RectangleF(plotLeft,top,plotWidth,plotHeight+1));using(var slopePen=new Pen(orange,1))g.DrawLines(slopePen,slopeLine);using(var zPen=new Pen(blue,1.4f))g.DrawLines(zPen,zLine);if(CursorDistance is double cursor){cursor=Math.Clamp(cursor,0,total);float x=X(cursor);using var cursorPen=new Pen(UiTheme.NavCursor,1);g.DrawLine(cursorPen,x,top,x,bottom);}g.Restore(clip);
    }
    static double NiceStep(double value){double power=Math.Pow(10,Math.Floor(Math.Log10(Math.Max(.001,value)))),f=value/power;return(f<=1?1:f<=2?2:f<=2.5?2.5:f<=5?5:10)*power;}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(distances.Length==0||plotRight<=plotLeft)return;double d=Math.Clamp((e.X-plotLeft)/(plotRight-plotLeft),0,1)*distances[^1];CursorDistance=d;CursorMoved?.Invoke(d);Invalidate();}
    protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);CursorDistance=null;CursorLeft?.Invoke();Invalidate();}
    protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);if(!Visible){CursorDistance=null;CursorLeft?.Invoke();}}
}
