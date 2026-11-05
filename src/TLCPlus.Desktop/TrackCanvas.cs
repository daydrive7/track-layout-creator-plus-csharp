using System.Drawing.Drawing2D;
using TLCPlus;
namespace TLCPlus.Desktop;
public partial class TrackCanvas:Control
{
    public Project Project=new();public Course? Course;public HashSet<int> Selected=[];
    public bool ShowGrid=true,ShowTerrain=false,ShowNumbers=true,Isometric=false,ShowTrack=true;
    public bool ShowNodeProperties=true;
    public bool ShowReference=true,ShowBorders=true,ShowRoadWidth=false,ShowPolygon=true,ShowCorners=true,DraftMode=true;
    public bool IsInteracting=>drag>=0||radiusDragging||camberDragging||transforming||decorationDragging;
    public bool DrawingDraft=>DraftMode&&IsInteracting&&!decorationDragging;
    public int TerrainFidelity=3;
    public double ScrollMargin=600,ReferenceScale=1;
    public float LabelFontSize=8;
    public string Stats="";
    public Course? ReferenceCourse;
    public double Zoom=.4,PanX,PanY,GridSize=25;
    public string Tool="select";
    public Bitmap? Reference;
    public double ReferenceWidth=2000;
    public event Action? SelectionChanged,Edited,EditStarted,EditFinished,EditCancelled;
    public event Action<Vec>? PointerMoved;
    public event Action<Vec>? DecorationPlaced;
    public event Action<string,Vec,Vec>? ToolGesture;
    public event Action? NodePropertyRequested;
    bool dragChanged;int drag=-1;bool panning,boxSelecting,transforming;Point last,boxStart;Rectangle selectionBox;Dictionary<int,Vec> before=[];
    public TrackCanvas(){DoubleBuffered=true;BackColor=Color.FromArgb(20,24,32);ForeColor=Color.FromArgb(210,220,235);SetStyle(ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);TabStop=true;}
    public PointF Screen(Vec v,double z=0){if(Isometric){double x=(v.X-v.Y)*.7071,y=(v.X+v.Y)*.35355-z;v=new(x,y);}return new((float)(Width/2+PanX+v.X*Zoom),(float)(Height/2+PanY+v.Y*Zoom));}
    public Vec World(Point p)=>new((p.X-Width/2-PanX)/Zoom,(p.Y-Height/2-PanY)/Zoom);
    public void Fit(){var points=Project.Nodes.Select(n=>n.XY).Concat(Course?.Points.Select(p=>p.XY)??[]).ToList();if(points.Count==0){Zoom=.4;PanX=PanY=0;Invalidate();return;}double minx=points.Min(p=>p.X),maxx=points.Max(p=>p.X),miny=points.Min(p=>p.Y),maxy=points.Max(p=>p.Y);Zoom=Math.Clamp(Math.Min((Width-130)/Math.Max(100,maxx-minx),(Height-130)/Math.Max(100,maxy-miny)),.005,30);PanX=-(minx+maxx)/2*Zoom;PanY=-(miny+maxy)/2*Zoom;Invalidate();}
    public Bitmap Render(){var b=new Bitmap(Math.Max(1,Width),Math.Max(1,Height));DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));return b;}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(BackColor);
        if(ShowTerrain&&!Isometric)DrawTerrain(g);
        if(ShowReference&&Reference!=null&&!Isometric){var a=Screen(new(-ReferenceWidth/2,-ReferenceWidth*Reference.Height/Reference.Width/2));float w=(float)(ReferenceWidth*Zoom);g.DrawImage(Reference,new RectangleF(a.X,a.Y,w,w*Reference.Height/Reference.Width));using var overlay=new SolidBrush(Color.FromArgb(120,BackColor));g.FillRectangle(overlay,new RectangleF(a.X,a.Y,w,w*Reference.Height/Reference.Width));}
        if(ShowReference&&ReferenceCourse is {Points.Count:>1} reference){var rp=new List<PointF>();for(double d=0;d<reference.PlanLength;d+=Math.Max(5,4/Zoom))rp.Add(Screen(Geometry.At(reference.Points,d).Point*ReferenceScale));if(rp.Count>1){using var pen=new Pen(Color.FromArgb(150,100,160,100),2);g.DrawLines(pen,rp.ToArray());}}
        if(ShowGrid&&!Isometric)DrawGrid(g);
        if(Course is{Points.Count:>1} c&&ShowTrack){var path=new List<PointF>();sampledDistances=[];double distance=0;for(int i=1;i<c.Points.Count;i++){var cp=c.Points[i];int divisions=Math.Clamp((int)(cp.Length*Zoom/5)+1,1,500);for(int j=i==1?0:1;j<=divisions;j++){double f=(double)j/divisions;Vec xy=cp.Form is 0 or 3?c.Points[i-1].XY+(cp.XY-c.Points[i-1].XY)*f:Geometry.Interpolate(cp.Center,cp.Radius,cp.StartAngle,cp.SweepAngle,f);double d=distance+cp.Length*f,z=0;if(Isometric&&c.Heights.Count>0){int h=(int)(d/Math.Max(c.PlanLength,1)*(c.Heights.Count-1));z=c.Heights[Math.Clamp(h,0,c.Heights.Count-1)];}path.Add(Screen(xy,z));sampledDistances.Add(d);}distance+=cp.Length;}
            sampledPath=path;DrawRoad(g,c,path);
        }
        if(Course is {Points.Count:>1} dc)DrawDecorations(g,dc);
        var nodePoints=Project.Nodes.Select(n=>Screen(n.XY,Isometric?n.Z:0)).ToArray();if(nodePoints.Length>1&&!Isometric&&ShowPolygon){using var pen=new Pen(UiTheme.Polygon,1);g.DrawLines(pen,nodePoints);if(Project.Settings.Circuit)g.DrawLine(pen,nodePoints[^1],nodePoints[0]);}
        if(!Isometric){
            using var nodeFont=new Font("Segoe UI",LabelFontSize);
            for(int i=0;i<nodePoints.Length;i++){
                var p=nodePoints[i];bool selected=Selected.Contains(i);var color=selected?Color.FromArgb(255,183,78):UiTheme.AccentLight;
                // Four-pixel markers with a one-pixel outline.
                var smoothing=g.SmoothingMode;g.SmoothingMode=SmoothingMode.None;
                using(var brush=new SolidBrush(color))g.FillRectangle(brush,p.X-2,p.Y-2,4,4);
                using(var border=new Pen(color,1))g.DrawRectangle(border,p.X-2,p.Y-2,4,4);
                g.SmoothingMode=smoothing;
                if(ShowNodeProperties&&(Project.Settings.Circuit||i>0&&i<nodePoints.Length-1))DrawNodeProperties(g,i,nodeFont,selected);
                else if(ShowNumbers){using var labelBrush=new SolidBrush(UiTheme.Header);g.DrawString((i+1).ToString(),nodeFont,labelBrush,p.X+6,p.Y-16);}
            }
        }
        if(boxSelecting){using var brush=new SolidBrush(Color.FromArgb(35,70,170,255));using var pen=new Pen(Color.DodgerBlue);g.FillRectangle(brush,selectionBox);g.DrawRectangle(pen,selectionBox);}
        DrawRulers(g);DrawStats(g);
    }
    void DrawGrid(Graphics g){double step=GridSize>0?GridSize:25;while(step*Zoom<35)step*=2;while(step*Zoom>150)step/=2;var lo=World(new(0,0));var hi=World(new(Width,Height));using var pen=new Pen(Color.FromArgb(32,42,57));using var axis=new Pen(Color.FromArgb(48,65,85));for(double x=Math.Floor(lo.X/step)*step;x<hi.X;x+=step){float sx=Screen(new(x,0)).X;g.DrawLine(Math.Abs(x)<.001?axis:pen,sx,0,sx,Height);}for(double y=Math.Floor(lo.Y/step)*step;y<hi.Y;y+=step){float sy=Screen(new(0,y)).Y;g.DrawLine(Math.Abs(y)<.001?axis:pen,0,sy,Width,sy);}}
    void DrawScale(Graphics g){double distance=100;while(distance*Zoom>150)distance/=2;while(distance*Zoom<60)distance*=2;float x=22,y=Height-25,w=(float)(distance*Zoom);using var pen=new Pen(Color.LightSlateGray,2);g.DrawLine(pen,x,y,x+w,y);g.DrawLine(pen,x,y-4,x,y+4);g.DrawLine(pen,x+w,y-4,x+w,y+4);g.DrawString($"{distance:0.##} m",Font,Brushes.LightSlateGray,x,y-21);}
    void DrawTerrain(Graphics g)=>TerrainRenderer.Draw(g,Terrain.Get(Project.Settings.Scenery),Project.Settings.Scenery,Zoom,PanX,PanY,Width,Height,TerrainFidelity);
    void DrawRulers(Graphics g){using var bg=new SolidBrush(BackColor);g.FillRectangle(bg,0,0,Width,22);g.FillRectangle(bg,0,0,22,Height);using var line=new Pen(Color.FromArgb(204,204,204));g.DrawLine(line,21,21,Width,21);g.DrawLine(line,21,21,21,Height);double step=250;while(step*Zoom<100)step*=2;while(step*Zoom>350)step/=2;var lo=World(new(22,22));var hi=World(new(Width,Height));using var font=new Font("Segoe UI",8);using var text=new SolidBrush(Color.FromArgb(133,133,133));for(double x=Math.Floor(lo.X/step)*step;x<hi.X;x+=step){float sx=Screen(new(x,0)).X;if(sx<22)continue;g.DrawString($"{x:0}m",font,text,sx-15,2);}for(double y=Math.Floor(lo.Y/step)*step;y<hi.Y;y+=step){float sy=Screen(new(0,y)).Y;if(sy<35)continue;var state=g.Save();g.TranslateTransform(2,sy+12);g.RotateTransform(-90);g.DrawString($"{-y:0}m",font,text,0,0);g.Restore(state);}}
    void DrawStats(Graphics g){if(string.IsNullOrEmpty(Stats))return;var size=g.MeasureString(Stats,Font);var r=new RectangleF(Width-size.Width-37,Height-48,size.Width+22,32);using var brush=new SolidBrush(Color.FromArgb(30,30,30));using var border=new Pen(Color.FromArgb(60,60,60));g.FillRectangle(brush,r);g.DrawRectangle(border,r.X,r.Y,r.Width,r.Height);g.DrawString(Stats,Font,Brushes.Gainsboro,r.X+10,r.Y+8);}
    int Hit(Point p){for(int i=Project.Nodes.Count-1;i>=0;i--){var s=Screen(Project.Nodes[i].XY);if(Math.Abs(s.X-p.X)<11&&Math.Abs(s.Y-p.Y)<11)return i;}return -1;}
    internal void FinishGesture()
    {
        bool editing=IsInteracting;drag=-1;radiusDragging=camberDragging=decorationDragging=transforming=panning=boxSelecting=false;Capture=false;
        if(editing){if(dragChanged)EditFinished?.Invoke();else EditCancelled?.Invoke();}dragChanged=false;
    }
    Vec Snap(Vec p)=>GridSize<=0||!ModifierKeys.HasFlag(Keys.Control)?p:new(Math.Round(p.X/GridSize)*GridSize,Math.Round(p.Y/GridSize)*GridSize);
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);Focus();last=e.Location;dragChanged=false;if(e.Button==MouseButtons.Middle){panning=true;Capture=true;return;}if(Isometric)return;
        if(e.Button==MouseButtons.Right){
            if(Tool=="decorate"){int hit=HitDecoration(e.Location);if(hit>=0){EditStarted?.Invoke();Project.Decorations.RemoveAt(hit);selectedDecoration=-1;DecorationSelected?.Invoke(-1);Edited?.Invoke();EditFinished?.Invoke();}return;}
            int rightHit=Hit(e.Location);if(rightHit>=0){if(!Selected.Contains(rightHit))Selected=[rightHit];SelectionChanged?.Invoke();radiusDragging=Tool!="camber";camberDragging=Tool=="camber";EditStarted?.Invoke();Capture=true;}else{panning=true;Capture=true;}return;
        }
        if(e.Button!=MouseButtons.Left)return;
        if(Tool=="pan"){panning=true;Capture=true;return;}
        if(Tool=="decorate"){int hit=HitDecoration(e.Location);selectedDecoration=hit;DecorationSelected?.Invoke(hit);if(hit>=0){decorationDragging=true;EditStarted?.Invoke();Capture=true;Invalidate();}else DecorationPlaced?.Invoke(World(e.Location));return;}
        if(Tool=="rotate"||Tool=="scale"){transforming=true;Capture=true;EditStarted?.Invoke();return;}
        if(Tool=="camber"){int hit=Hit(e.Location);if(hit>=0){if(!Selected.Contains(hit))Selected=[hit];SelectionChanged?.Invoke();camberDragging=true;EditStarted?.Invoke();Capture=true;}return;}
        if(Tool=="pen"&&Hit(e.Location)<0&&e.Clicks==1){EditStarted?.Invoke();Project.Nodes.Add(new(){XY=Snap(World(e.Location))});Selected=[Project.Nodes.Count-1];Edited?.Invoke();EditFinished?.Invoke();SelectionChanged?.Invoke();return;}
        int index=Hit(e.Location);if(index>=0){if(!Selected.Contains(index)){if(!ModifierKeys.HasFlag(Keys.Shift))Selected.Clear();Selected.Add(index);}else if(ModifierKeys.HasFlag(Keys.Shift)){Selected.Remove(index);SelectionChanged?.Invoke();Invalidate();return;}drag=index;before=Selected.ToDictionary(i=>i,i=>Project.Nodes[i].XY);EditStarted?.Invoke();Capture=true;SelectionChanged?.Invoke();}
        else{if(!ModifierKeys.HasFlag(Keys.Shift))Selected.Clear();boxSelecting=true;boxStart=e.Location;selectionBox=new();Capture=true;SelectionChanged?.Invoke();}Invalidate();
    }
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);PointerMoved?.Invoke(World(e.Location));if(radiusDragging||camberDragging){if(last.Y!=e.Y){dragChanged=true;AdjustNodeAttribute(last.Y-e.Y,camberDragging);}}else if(decorationDragging&&selectedDecoration>=0&&selectedDecoration<Project.Decorations.Count){var nearest=NearestTrack(e.Location);double value=SnapDecorations?Math.Round(nearest.Distance/5)*5:nearest.Distance;if(value!=Project.Decorations[selectedDecoration].Vpos){dragChanged=true;Project.Decorations[selectedDecoration].Vpos=value;Edited?.Invoke();Invalidate();}}else if(panning){PanX+=e.X-last.X;PanY+=e.Y-last.Y;Invalidate();}else if(transforming){if(last!=e.Location){dragChanged=true;ToolGesture?.Invoke(Tool,World(last),World(e.Location));Invalidate();}}else if(drag>=0){var delta=World(e.Location)-World(last);foreach(int i in Selected){var n=Project.Nodes[i];var value=Snap(n.XY+delta);if(value!=n.XY){dragChanged=true;n.XY=value;}}if(last!=e.Location){Edited?.Invoke();Invalidate();}}else if(boxSelecting){selectionBox=Rectangle.FromLTRB(Math.Min(boxStart.X,e.X),Math.Min(boxStart.Y,e.Y),Math.Max(boxStart.X,e.X),Math.Max(boxStart.Y,e.Y));Invalidate();}last=e.Location;}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(boxSelecting){for(int i=0;i<Project.Nodes.Count;i++){var p=Screen(Project.Nodes[i].XY);if(selectionBox.Contains(Point.Round(p)))Selected.Add(i);}SelectionChanged?.Invoke();}bool wasDrag=drag>=0||transforming||radiusDragging||camberDragging||decorationDragging;radiusDragging=camberDragging=decorationDragging=false;drag=-1;panning=boxSelecting=transforming=false;Capture=false;if(wasDrag){if(dragChanged){Edited?.Invoke();EditFinished?.Invoke();}else EditCancelled?.Invoke();}Invalidate();}
    protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);var before=World(e.Location);Zoom=Math.Clamp(Zoom*Math.Pow(1.2,e.Delta/120.0),.002,100);PanX=e.X-Width/2-before.X*Zoom;PanY=e.Y-Height/2-before.Y*Zoom;Invalidate();}
}
