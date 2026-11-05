using System.Drawing.Drawing2D;
using TLCPlus;

namespace TLCPlus.Desktop;

/// <summary>
/// Isometric terrain and track preview with a camera independent of the 2D editor.
/// </summary>
public sealed class IsometricForm : Form
{
    readonly IsoCanvas canvas;
    readonly ToolTip hints=new(){InitialDelay=450,AutoPopDelay=10000};

    public IsometricForm(Project project,Course course,RectangleF? worldBounds=null,bool classic=false)
    {
        Text=UiLanguage.T("Isometric view");
        ClientSize=new Size(800,600);
        MinimumSize=new Size(450,340);
        StartPosition=FormStartPosition.CenterParent;
        Font=new Font("Segoe UI",9);
        BackColor=classic?Color.FromArgb(240,240,240):Color.FromArgb(45,45,45);
        string iconPath=Path.Combine(Terrain.AssetsPath,"icons/windowicon.ico");
        if(File.Exists(iconPath))Icon=new Icon(iconPath);
        canvas=new IsoCanvas(project,course,worldBounds,classic){Dock=DockStyle.Fill};
        Controls.Add(canvas);
        var toolbar=new Panel{Left=6,Top=6,Width=68,Height=108,BorderStyle=BorderStyle.FixedSingle,BackColor=classic?Color.FromArgb(240,240,240):Color.FromArgb(30,30,30)};
        Button? pan=null,rotate=null;
        Color normal=toolbar.BackColor,selected=classic?Color.FromArgb(205,205,205):Color.FromArgb(60,60,60);
        Button Add(string name,string tip,int x,int y,Action action)
        {
            var button=new Button{Width=30,Height=30,Left=x,Top=y,FlatStyle=FlatStyle.Flat,BackColor=normal,Margin=Padding.Empty,AccessibleName=tip};
            button.FlatAppearance.BorderSize=0;
            button.FlatAppearance.MouseOverBackColor=selected;
            string path=Path.Combine(Terrain.AssetsPath,classic?"icons/iso_light":"icons/iso",name+".gif");
            if(!File.Exists(path))path=Path.Combine(Terrain.AssetsPath,"icons/tools",name+".gif");
            if(File.Exists(path)){using var original=Image.FromFile(path);button.Image=new Bitmap(original);}
            button.Click+=(_,_)=>action();hints.SetToolTip(button,UiLanguage.T(tip));toolbar.Controls.Add(button);return button;
        }
        void Tool(bool panning){canvas.PanMode=panning;pan!.BackColor=panning?selected:normal;rotate!.BackColor=panning?normal:selected;canvas.Cursor=panning?Cursors.Hand:Cursors.SizeAll;}
        pan=Add("pan","Panning tool",2,2,()=>Tool(true));
        rotate=Add("rotate","Rotate tool (drag left/right or up/down)",34,2,()=>Tool(false));
        Add("zoom_in","Zoom in",2,34,()=>canvas.Zoom(1/.75));
        Add("zoom_out","Zoom out",34,34,()=>canvas.Zoom(.75));
        toolbar.Controls.Add(new Panel{Left=2,Top=68,Width=62,Height=1,BackColor=Color.Gray});
        Add("screenshot","Save screenshot",2,73,SaveScreenshot);
        canvas.Controls.Add(toolbar);toolbar.BringToFront();Tool(false);
        KeyPreview=true;
        KeyDown+=(_,e)=>{if(e.KeyCode==Keys.R){canvas.ResetView();e.Handled=true;}else if(e.KeyCode is Keys.Add or Keys.Oemplus){canvas.Zoom(1/.75);e.Handled=true;}else if(e.KeyCode is Keys.Subtract or Keys.OemMinus){canvas.Zoom(.75);e.Handled=true;}else if(e.KeyCode==Keys.P){SaveScreenshot();e.Handled=true;}};
        Shown+=(_,_)=>canvas.ResetView();
    }

    public void ResetView()=>canvas.ResetView();
    public Bitmap RenderPreview()=>canvas.RenderPreview();

    void SaveScreenshot()
    {
        using var dialog=new SaveFileDialog{Filter="PNG image|*.png",DefaultExt="png",FileName="isometric-view.png",OverwritePrompt=true};
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        using var bitmap=canvas.RenderPreview();bitmap.Save(dialog.FileName,System.Drawing.Imaging.ImageFormat.Png);
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing){hints.Dispose();foreach(Control panel in canvas.Controls)foreach(Control child in panel.Controls)if(child is Button button){var image=button.Image;button.Image=null;image?.Dispose();}}
        base.Dispose(disposing);
    }

    sealed class IsoCanvas:Control
    {
        const double Cos30=.86602540378443864676;
        readonly record struct P3(double X,double Y,double Z);
        readonly record struct Contour(P3 A,P3 B,double Level);
        readonly Course course;
        readonly Terrain terrain;
        readonly double left,top,right,bottom,centerX,centerY,baseHeight;
        readonly P3[,] grid;
        readonly List<Contour> contours=[];
        readonly List<List<P3>> baseline=[],roadLeft=[],roadRight=[],borderLeft=[],borderRight=[],simple=[];
        readonly Color gridMinor,gridMajor,roadColor,borderColor;
        readonly double zmin,zmax;
        double rotation,elevation=Math.PI/6,zScale=4,pixelScale=1,panX,panY,fitCenterX,fitCenterY;
        bool panning,rotating,ready;
        Point previous;
        public bool PanMode;

        public IsoCanvas(Project project,Course course,RectangleF? bounds,bool classic)
        {
            this.course=course;terrain=Terrain.Get(project.Settings.Scenery);baseHeight=TerrainRenderer.BaseElevation(project.Settings.Scenery);
            BackColor=classic?Color.FromArgb(240,240,240):Color.FromArgb(45,45,45);
            gridMinor=classic?Color.FromArgb(144,144,149):Color.FromArgb(37,37,38);
            gridMajor=classic?Color.FromArgb(132,132,134):Color.FromArgb(30,30,30);
            roadColor=classic?Color.FromArgb(45,45,45):Color.FromArgb(224,224,224);
            borderColor=classic?Color.FromArgb(100,100,100):Color.FromArgb(133,133,133);
            DoubleBuffered=true;TabStop=true;SetStyle(ControlStyles.ResizeRedraw,true);
            RectangleF region;
            if(bounds is RectangleF supplied&&supplied.Width>1&&supplied.Height>1)region=supplied;
            else if(course.Points.Count>0){double x0=course.Points.Min(p=>p.XY.X),x1=course.Points.Max(p=>p.XY.X),y0=course.Points.Min(p=>p.XY.Y),y1=course.Points.Max(p=>p.XY.Y);double pad=Math.Max(100,Math.Max(x1-x0,y1-y0)*.12);region=RectangleF.FromLTRB((float)(x0-pad),(float)(y0-pad),(float)(x1+pad),(float)(y1+pad));}
            else region=new RectangleF(-500,-500,1000,1000);
            centerX=region.Left+region.Width/2;centerY=region.Top+region.Height/2;
            double side=Math.Max(region.Width,region.Height);left=centerX-side/2;top=centerY-side/2;right=centerX+side/2;bottom=centerY+side/2;
            const int divisions=40;grid=new P3[divisions+1,divisions+1];double min=double.PositiveInfinity,max=double.NegativeInfinity;
            for(int y=0;y<=divisions;y++)for(int x=0;x<=divisions;x++){double wx=left+side*x/divisions,wy=top+side*y/divisions,z=terrain.Sample(wx,wy)-baseHeight;grid[x,y]=new(wx,wy,z);min=Math.Min(min,z);max=Math.Max(max,z);}
            zmin=min;zmax=max;
            BuildContours(divisions);BuildTrack(project.Settings.RoadWidth,project.Settings.RoadWidth+(project.Settings.NarrowRoad?16:40),side);
        }

        void BuildTrack(double roadWidth,double borderWidth,double side)
        {
            if(course.Points.Count<2||course.PlanLength<=0)return;
            var c=new List<P3>();var b=new List<P3>();var l=new List<P3>();var r=new List<P3>();var lb=new List<P3>();var rb=new List<P3>();
            void Flush(){if(c.Count>1){simple.Add(c);baseline.Add(b);roadLeft.Add(l);roadRight.Add(r);borderLeft.Add(lb);borderRight.Add(rb);}c=[];b=[];l=[];r=[];lb=[];rb=[];}
            double step=Math.Max(course.PlanLength/12000,Math.Max(1,side/70));
            int count=Math.Max(1,(int)Math.Ceiling(course.PlanLength/step));
            for(int i=0;i<=count;i++){
                double distance=course.PlanLength*i/count;var (v,angle)=Geometry.At(course.Points,distance);
                if(v.X<left||v.X>right||v.Y<top||v.Y>bottom){Flush();continue;}
                double ox=Math.Cos(angle+Math.PI/2),oy=-Math.Sin(angle+Math.PI/2),z=terrain.Sample(v.X,v.Y)-baseHeight;
                c.Add(new(v.X,v.Y,z));b.Add(new(v.X,v.Y,zmin));
                l.Add(new(v.X+ox*roadWidth/2,v.Y+oy*roadWidth/2,z));r.Add(new(v.X-ox*roadWidth/2,v.Y-oy*roadWidth/2,z));
                lb.Add(new(v.X+ox*borderWidth/2,v.Y+oy*borderWidth/2,z));rb.Add(new(v.X-ox*borderWidth/2,v.Y-oy*borderWidth/2,z));
            }
            Flush();
        }

        void BuildContours(int divisions)
        {
            if(zmax-zmin<.00001)return;
            for(int y=0;y<divisions;y++)for(int x=0;x<divisions;x++){
                P3[] p=[grid[x,y],grid[x+1,y],grid[x+1,y+1],grid[x,y+1]];
                double lo=p.Min(p=>p.Z),hi=p.Max(p=>p.Z);
                for(double level=Math.Ceiling(lo/10)*10;level<=hi;level+=10){var points=new List<P3>();for(int i=0;i<4;i++){int j=(i+1)%4;if((p[i].Z>level)==(p[j].Z>level))continue;double f=(level-p[i].Z)/(p[j].Z-p[i].Z);points.Add(new(p[i].X+(p[j].X-p[i].X)*f,p[i].Y+(p[j].Y-p[i].Y)*f,level));}for(int i=0;i+1<points.Count;i+=2)contours.Add(new(points[i],points[i+1],level));}
            }
        }

        Vec Project(P3 point)
        {
            double tx=point.X-centerX,ty=point.Y-centerY,cos=Math.Cos(rotation),sin=Math.Sin(rotation);
            double rx=tx*cos+ty*sin,ry=-tx*sin+ty*cos;
            return new((rx-ry)*Cos30,(rx+ry)*Math.Sin(elevation)-(point.Z-zmin)*zScale*(Math.Cos(elevation)/Cos30));
        }
        PointF Screen(P3 p){var v=Project(p);return new((float)(Width/2.0+panX+(v.X-fitCenterX)*pixelScale),(float)(Height/2.0+panY+(v.Y-fitCenterY)*pixelScale));}
        public void ResetView(){rotation=0;elevation=Math.PI/6;zScale=4;panX=panY=0;FitProjection();ready=true;Invalidate();}
        void FitProjection()
        {
            var points=grid.Cast<P3>().Concat(new[]{new P3(left,top,zmin),new P3(right,top,zmin),new P3(right,bottom,zmin),new P3(left,bottom,zmin)}).Select(Project).ToList();
            double x0=points.Min(v=>v.X),x1=points.Max(v=>v.X),y0=points.Min(v=>v.Y),y1=points.Max(v=>v.Y);
            fitCenterX=(x0+x1)/2;fitCenterY=(y0+y1)/2;pixelScale=Math.Clamp(Math.Min(Math.Max(100,Width-70)/Math.Max(1,x1-x0),Math.Max(100,Height-70)/Math.Max(1,y1-y0)),.0001,200);
        }
        public void Zoom(double factor,Point? anchor=null){double old=pixelScale;pixelScale=Math.Clamp(pixelScale*factor,.00001,500);if(anchor is Point p){double ox=Width/2.0+panX,oy=Height/2.0+panY;panX=p.X-Width/2.0-(p.X-ox)*(pixelScale/old);panY=p.Y-Height/2.0-(p.Y-oy)*(pixelScale/old);}Invalidate();}
        public Bitmap RenderPreview(){if(!ready){FitProjection();ready=true;}var bitmap=new Bitmap(Math.Max(1,Width),Math.Max(1,Height));using var g=Graphics.FromImage(bitmap);DrawScene(g,false);return bitmap;}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(!ready){FitProjection();ready=true;}DrawScene(e.Graphics,rotating);}
        void DrawScene(Graphics g,bool basic)
        {
            g.Clear(BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
            int last=grid.GetLength(0)-1;
            for(int y=0;y<=last;y++){if(basic&&y!=0&&y!=last)continue;using var pen=new Pen(y%10==0?gridMajor:gridMinor,y%5==0?1:.5f);var line=Enumerable.Range(0,last+1).Select(x=>Screen(grid[x,y])).ToArray();g.DrawLines(pen,line);}
            for(int x=0;x<=last;x++){if(basic&&x!=0&&x!=last)continue;using var pen=new Pen(x%10==0?gridMajor:gridMinor,x%5==0?1:.5f);var line=Enumerable.Range(0,last+1).Select(y=>Screen(grid[x,y])).ToArray();g.DrawLines(pen,line);}
            P3[] corners=[grid[0,0],grid[last,0],grid[last,last],grid[0,last]];
            using(var skirt=new Pen(Color.FromArgb(133,133,133),.5f)){foreach(var c in corners)g.DrawLine(skirt,Screen(c),Screen(c with{Z=zmin}));g.DrawPolygon(skirt,corners.Select(p=>Screen(p with{Z=zmin})).ToArray());}
            if(!basic){foreach(var contour in contours){using var pen=new Pen(TerrainRenderer.HeightColor((contour.Level-zmin)/Math.Max(.0001,zmax-zmin)),.7f);g.DrawLine(pen,Screen(contour.A),Screen(contour.B));}}
            if(basic){DrawLines(simple,roadColor,1);return;}
            DrawLines(baseline,Color.FromArgb(133,133,133),.6f);DrawLines(borderLeft,borderColor,.7f);DrawLines(borderRight,borderColor,.7f);DrawLines(roadLeft,roadColor,1);DrawLines(roadRight,roadColor,1);
            void DrawLines(List<List<P3>> lines,Color color,float width){using var pen=new Pen(color,width){LineJoin=LineJoin.Round};foreach(var line in lines)if(line.Count>1)g.DrawLines(pen,line.Select(Screen).ToArray());}
        }
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);Focus();previous=e.Location;panning=e.Button is MouseButtons.Right or MouseButtons.Middle||(e.Button==MouseButtons.Left&&PanMode);rotating=e.Button==MouseButtons.Left&&!PanMode;Capture=panning||rotating;}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(panning){panX+=e.X-previous.X;panY+=e.Y-previous.Y;Invalidate();}else if(rotating){rotation+=(e.X-previous.X)*.005;elevation=Math.Clamp(elevation+(e.Y-previous.Y)*.005,Math.PI/36,Math.PI*85/180);Invalidate();}previous=e.Location;}
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);panning=rotating=false;Capture=false;Invalidate();}
        protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);Zoom(Math.Pow(1/.75,e.Delta/120.0),e.Location);}
    }
}
