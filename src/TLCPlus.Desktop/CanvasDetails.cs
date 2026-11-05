using System.Drawing.Drawing2D;
using TLCPlus;
namespace TLCPlus.Desktop;
public partial class TrackCanvas
{
    internal static string TrackLabel(ControlPoint point)=>point.Form is 0 or 3?$"{point.Length:0.0} m":$"R {point.Radius:0.0} m";
    public double? CursorDistance;

    List<PointF> sampledPath=[];
    List<double> sampledDistances=[];
    bool radiusDragging,camberDragging;
    int selectedDecoration=-1;
    bool decorationDragging;
    public int SelectedDecoration=>selectedDecoration;
    internal void SelectDecoration(int index){selectedDecoration=index;DecorationSelected?.Invoke(index);Invalidate();}
    public event Action<int>? DecorationSelected;
    public bool SnapDecorations=true;
    readonly Dictionary<int,PointF> decorationPositions=[];
    readonly List<DecorationHit> decorationHits=[];
    readonly record struct DecorationHit(int Index,RectangleF Bounds,PointF A,PointF B,float HalfWidth,bool Line);
    void DrawDecorations(Graphics g,Course course)
    {
        decorationPositions.Clear();decorationHits.Clear();
        if(course.Points.Count<2||course.PlanLength<=0)return;
        int scenery=Project.Settings.Scenery;
        var clip=g.Save();g.SetClip(new Rectangle(22,22,Math.Max(1,Width-22),Math.Max(1,Height-22)),CombineMode.Intersect);
        if(Project.Decorations.Count>0)
        {
            for(int i=0;i<Project.Decorations.Count;i++)
            {
                var deco=Project.Decorations[i];var rail=DecorationRenderInfo.Resolve(scenery,deco.Uuid,deco.Type);
                Draw(deco.Type,deco.Side=="right",deco.Track,deco.Vpos,rail.Length,i);
            }
        }
        else foreach(var deco in course.Decorations)
        {
            var rail=DecorationRenderInfo.Resolve(scenery,deco.Uuid);
            string track=deco.TrackType is 6 or 7?"CURB":(deco.TrackType%3) switch{0=>"A",1=>"B",_=>"AB"};
            double begin=Exporter.From3d(course.Banks,deco.Begin),end=Exporter.From3d(course.Banks,deco.End);
            Draw(rail.Type,deco.TrackType is 3 or 4 or 5 or 7,track,(begin+end)*.5,Math.Max(.01,end-begin),-1);
        }
        g.Restore(clip);

        void Draw(int type,bool right,string track,double distance,double itemLength,int index)
        {
            distance=Math.Clamp(distance,0,course.PlanLength);
            double begin=Math.Max(0,distance-itemLength*.5),end=Math.Min(course.PlanLength,distance+itemLength*.5);
            var point=Geometry.At(course.Points,distance).Point;
            var before=Geometry.At(course.Points,Math.Max(0,distance-1)).Point;
            var after=Geometry.At(course.Points,Math.Min(course.PlanLength,distance+1)).Point;
            double fx=after.X-before.X,fy=after.Y-before.Y,norm=Math.Sqrt(fx*fx+fy*fy);
            if(norm==0)return;fx/=norm;fy/=norm;
            // Exact Python screen-space left normal, with local tangent sampled
            // at +/-1 m rather than inferred from a different curve's angle.
            double lx=fy,ly=-fx,halfRoad=Project.Settings.RoadWidth*.5,side=right?-1:1;
            double factor=track switch{"CURB"=>.5,"A"=>.75,"B"=>2.2,_=>1.35};
            bool gate=type is 12 or 18;
            var color=DecorationArtwork.TypeColor(scenery,type);
            float root=(float)Math.Sqrt(Zoom);
            PointF position;
            float radius;
            if(gate)
            {
                var a=Screen(new(point.X-lx*(halfRoad+2),point.Y-ly*(halfRoad+2)));
                var b=Screen(new(point.X+lx*(halfRoad+2),point.Y+ly*(halfRoad+2)));
                float width=Math.Max(2,3*root);
                DrawSpan(a,b,color,width,[6/width,4/width],index);
                position=Screen(point);radius=Math.Clamp(9*root,5,14);
            }
            else
            {
                var a=Geometry.At(course.Points,begin).Point;
                var b=Geometry.At(course.Points,end).Point;
                double offset=side*halfRoad*(1+factor);
                var spanA=Screen(new(a.X+lx*offset,a.Y+ly*offset));
                var spanB=Screen(new(b.X+lx*offset,b.Y+ly*offset));
                float width=Math.Max(1,2*root);
                DrawSpan(spanA,spanB,color,width,[3/width,2/width],index);
                position=Screen(new(point.X+lx*offset,point.Y+ly*offset));radius=Math.Clamp(8*root,4,13);
                if(type==6)
                {
                    double edge=side*halfRoad*.98;
                    DrawSpan(Screen(new(a.X+lx*edge,a.Y+ly*edge)),Screen(new(b.X+lx*edge,b.Y+ly*edge)),color,Math.Max(2,3*root),null,index);
                }
            }
            DecorationArtwork.DrawWorld(g,scenery,type,position.X,position.Y,radius);
            if(index>=0)
            {
                decorationPositions[index]=position;
                var b=DecorationRenderInfo.SymbolBounds(scenery,type);
                decorationHits.Add(new(index,new RectangleF(position.X+b.X*radius-4,position.Y+b.Y*radius-4,b.Width*radius+8,b.Height*radius+8),default,default,0,false));
            }
            if(index>=0&&index==selectedDecoration)
            {
                float r=Math.Max(9,12*root);using var pen=new Pen(Color.FromArgb(255,159,10),2);
                g.DrawEllipse(pen,position.X-r,position.Y-r,r*2,r*2);
                if(index>=0)decorationHits.Add(new(index,new(position.X-r-4,position.Y-r-4,r*2+8,r*2+8),default,default,0,false));
            }
        }
        void DrawSpan(PointF a,PointF b,Color color,float width,float[]? dash,int index)
        {
            using var pen=new Pen(color,width);if(dash!=null)pen.DashPattern=dash;g.DrawLine(pen,a,b);
            if(index>=0)decorationHits.Add(new(index,RectangleF.Empty,a,b,width*.5f+4,true));
        }
    }
    int HitDecoration(Point point)
    {
        for(int i=decorationHits.Count-1;i>=0;i--)
        {
            var hit=decorationHits[i];
            if(!hit.Line){if(hit.Bounds.Contains(point))return hit.Index;continue;}
            double dx=hit.B.X-hit.A.X,dy=hit.B.Y-hit.A.Y,length=dx*dx+dy*dy;
            double f=length==0?0:Math.Clamp(((point.X-hit.A.X)*dx+(point.Y-hit.A.Y)*dy)/length,0,1);
            double x=point.X-hit.A.X-dx*f,y=point.Y-hit.A.Y-dy*f;
            if(x*x+y*y<=hit.HalfWidth*hit.HalfWidth)return hit.Index;
        }
        return -1;
    }
    void DrawRoad(Graphics g,Course course,List<PointF> path)
    {
        if(path.Count<2)return;
        var state=g.Save();g.SetClip(new Rectangle(22,22,Math.Max(1,Width-22),Math.Max(1,Height-22)));
        double borderWidth=Project.Settings.RoadWidth+(Project.Settings.NarrowRoad?16:40);
        if(ShowBorders){
            PointF[] left=new PointF[path.Count],right=new PointF[path.Count];
            for(int i=0;i<path.Count;i++){var a=path[Math.Max(0,i-1)];var b=path[Math.Min(path.Count-1,i+1)];double dx=b.X-a.X,dy=b.Y-a.Y,len=Math.Sqrt(dx*dx+dy*dy);if(len<1e-8)len=1;float ox=(float)(-dy/len*borderWidth*.5*Zoom),oy=(float)(dx/len*borderWidth*.5*Zoom);left[i]=new(path[i].X+ox,path[i].Y+oy);right[i]=new(path[i].X-ox,path[i].Y-oy);}
            using var edge=new Pen(UiTheme.RoadBorder,1);g.DrawLines(edge,left);g.DrawLines(edge,right);
        }
        using(var road=new Pen(UiTheme.Road,(float)Math.Max(1,ShowRoadWidth?Project.Settings.RoadWidth*Zoom:1)){LineJoin=LineJoin.Round})g.DrawLines(road,path.ToArray());
        if(course.HasLinePositions&&!Isometric){
            if(Project.Settings.Circuit&&Math.Abs(Project.Settings.PitOffset)>.001&&course.PlanLength>0){
                using var pit=new Pen(Color.FromArgb(130,0,130),14){LineJoin=LineJoin.Round};
                double start=course.PitStart2d,end=start+course.PitLength;DrawSection(start,Math.Min(end,course.PlanLength),pit);if(end>course.PlanLength)DrawSection(0,Math.Min(end-course.PlanLength,course.PlanLength),pit);
                var s=Screen(Geometry.At(course.Points,start).Point);g.FillEllipse(Brushes.White,s.X-7,s.Y-7,14,14);g.DrawEllipse(Pens.Black,s.X-7,s.Y-7,14,14);TextRenderer.DrawText(g,"PIT START",Font,new Point((int)s.X-30,(int)s.Y-28),Color.White);
            }
            DrawLine(course.Start2d);if(Math.Abs(course.Finish2d-course.Start2d)>1)DrawLine(course.Finish2d);
            var (point,angle)=Geometry.At(course.Points,course.Start2d);var center=Screen(point);double offset=borderWidth/2*Zoom+12;
            PointF a=new((float)(center.X-Math.Cos(angle)*45+Math.Sin(angle)*offset),(float)(center.Y+Math.Sin(angle)*45+Math.Cos(angle)*offset));
            PointF b=new((float)(center.X+Math.Cos(angle)*45+Math.Sin(angle)*offset),(float)(center.Y-Math.Sin(angle)*45+Math.Cos(angle)*offset));
            using var arrow=new Pen(UiTheme.Arrow,2);using var cap=new AdjustableArrowCap(4,5,true);arrow.CustomEndCap=cap;g.DrawLine(arrow,a,b);
        }
        if(!Isometric&&ShowPolygon&&Zoom>.08){
            double cum=0;using var font=new Font("Segoe UI",LabelFontSize);foreach(var cp in course.Points.Skip(1)){
                if(cp.Length*Zoom>100){var (pos,angle)=Geometry.At(course.Points,cum+cp.Length*.5);double offset=(borderWidth*.5*Zoom)+12;var screen=Screen(pos);screen.X+=(float)(Math.Sin(angle)*offset);screen.Y+=(float)(Math.Cos(angle)*offset);string label=TrackLabel(cp);var size=g.MeasureString(label,font);using var bg=new SolidBrush(Color.FromArgb(175,BackColor));g.FillRectangle(bg,screen.X-size.Width/2-2,screen.Y-size.Height/2-1,size.Width+4,size.Height+2);using var labelBrush=new SolidBrush(UiTheme.Text);g.DrawString(label,font,labelBrush,screen.X-size.Width/2,screen.Y-size.Height/2);}
                var end=Screen(cp.XY);g.FillEllipse(Brushes.White,end.X-1.5f,end.Y-1.5f,3,3);cum+=cp.Length;
            }
        }
        if(CursorDistance is double cursor&&cursor>=0&&cursor<=course.PlanLength){var (point,angle)=Geometry.At(course.Points,cursor);var center=Screen(point);float ox=(float)(Math.Sin(angle)*(borderWidth*Zoom*.5+8)),oy=(float)(Math.Cos(angle)*(borderWidth*Zoom*.5+8));using var cursorPen=new Pen(Color.FromArgb(255,69,58),1);g.DrawLine(cursorPen,center.X-ox,center.Y-oy,center.X+ox,center.Y+oy);}
        g.Restore(state);
        void DrawLine(double distance){if(distance<0||distance>course.PlanLength)return;var (point,angle)=Geometry.At(course.Points,distance);var center=Screen(point);double half=(Project.Settings.RoadWidth*Zoom+10)/2;float ox=(float)(Math.Sin(angle)*half),oy=(float)(Math.Cos(angle)*half);using var pen=new Pen(Color.White,3);g.DrawLine(pen,center.X-ox,center.Y-oy,center.X+ox,center.Y+oy);}
        void DrawSection(double start,double end,Pen pen){var points=new List<PointF>();double step=Math.Max(2,4/Zoom);for(double d=start;d<end;d+=step)points.Add(Screen(Geometry.At(course.Points,d).Point));points.Add(Screen(Geometry.At(course.Points,end).Point));if(points.Count>1)g.DrawLines(pen,points.ToArray());}
    }
    (double Distance,double Pixels) NearestTrack(Point point)
    {
        double best=double.MaxValue,distance=0;for(int i=1;i<sampledPath.Count;i++){var a=sampledPath[i-1];var b=sampledPath[i];double dx=b.X-a.X,dy=b.Y-a.Y,len=dx*dx+dy*dy;double f=len==0?0:Math.Clamp(((point.X-a.X)*dx+(point.Y-a.Y)*dy)/len,0,1);double x=point.X-a.X-dx*f,y=point.Y-a.Y-dy*f,delta=x*x+y*y;if(delta<best){best=delta;distance=sampledDistances[i-1]+(sampledDistances[i]-sampledDistances[i-1])*f;}}
        return(distance,Math.Sqrt(best));
    }
    protected override void OnMouseDoubleClick(MouseEventArgs e){base.OnMouseDoubleClick(e);if(Tool=="decorate")return;int hit=Hit(e.Location);if(hit>=0){FinishGesture();if(!Selected.Contains(hit))Selected=[hit];SelectionChanged?.Invoke();NodePropertyRequested?.Invoke();}}
    void AdjustNodeAttribute(int deltaY,bool camber){foreach(int i in Selected){var n=Project.Nodes[i];if(camber){if(n.Camber==0&&deltaY<-.5)n.Camber=-1;else if(n.Camber== -1){if(deltaY>.5)n.Camber=0;}else n.Camber=Math.Clamp(n.Camber+deltaY*.01,0,30);}else{double min=Project.Settings.RoadWidth*.5+6;if(n.Radius==0&&deltaY>.5)n.Radius=min;else if(n.Radius==min&&deltaY<-.5)n.Radius=0;else n.Radius+=deltaY/(2*Zoom);if(n.Radius<0)n.Radius=0;if(n.Radius>0&&n.Radius<min)n.Radius=min;}}Edited?.Invoke();Invalidate();}
}
