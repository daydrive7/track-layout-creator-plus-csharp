using System.Buffers.Binary;
using System.Drawing.Drawing2D;
using TLCPlus;
namespace TLCPlus.Desktop;

public static class TerrainRenderer
{
    static readonly Color[] HeightColors=[Color.FromArgb(0,100,200),Color.FromArgb(0,120,110),Color.FromArgb(120,120,0),Color.FromArgb(200,0,0),Color.FromArgb(130,0,130)];
    static readonly Dictionary<int,double> bases=[];
    static Bitmap? cached;static string? cacheKey;
    public static double BaseElevation(int scenery)
    {
        if(bases.TryGetValue(scenery,out var min))return min;
        string name=scenery switch{1=>"death_valley",2 or 4=>"eifel",3=>"andalusia",_=>"eifel_flat"};
        using var stream=File.OpenRead(Path.Combine(Terrain.AssetsPath,"maps",name));stream.Position=0x1030;Span<byte> bytes=stackalloc byte[4];stream.ReadExactly(bytes);min=BinaryPrimitives.ReadSingleLittleEndian(bytes);bases[scenery]=min;return min;
    }
    public static Color HeightColor(double fraction)
    {
        fraction=Math.Clamp(fraction,0,1);double scaled=fraction*4;int i=Math.Min(3,(int)scaled);double f=scaled-i;var a=HeightColors[i];var b=HeightColors[i+1];return Color.FromArgb((int)(a.R+(b.R-a.R)*f),(int)(a.G+(b.G-a.G)*f),(int)(a.B+(b.B-a.B)*f));
    }
    public static void Draw(Graphics g,Terrain terrain,int scenery,double zoom,double panX,double panY,int width,int height,int fidelity,bool showLabels=true,bool shaded=false)
    {
        if(width<1||height<1||!double.IsFinite(zoom)||zoom<=0)return;string key=$"{scenery}|{zoom:R}|{panX:R}|{panY:R}|{width}|{height}|{fidelity}|{showLabels}|{shaded}|{UiTheme.Classic}";
        if(cacheKey!=key||cached==null){cached?.Dispose();cached=Render(terrain,scenery,zoom,panX,panY,width,height,fidelity,showLabels,shaded);cacheKey=key;}g.DrawImageUnscaled(cached,0,0);
    }
    public static void ClearCache(){cached?.Dispose();cached=null;cacheKey=null;}
    static Bitmap Render(Terrain terrain,int scenery,double zoom,double panX,double panY,int width,int height,int fidelity,bool labels,bool shaded)
    {
        var bitmap=new Bitmap(width,height);using var g=Graphics.FromImage(bitmap);g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Color.Transparent);
        double originX=width/2.0+panX,originY=height/2.0+panY,limit=terrain.Size/2;PointF Screen(double x,double y)=>new((float)(originX+x*zoom),(float)(originY+y*zoom));
        var tl=Screen(-limit,-limit);var br=Screen(limit,limit);var mapRect=new RectangleF(tl.X,tl.Y,br.X-tl.X,br.Y-tl.Y);using(var background=new SolidBrush(UiTheme.Canvas))g.FillRectangle(background,mapRect);
        var oldClip=g.Save();g.SetClip(mapRect,CombineMode.Intersect);
        double left=Math.Max(-limit,-originX/zoom),right=Math.Min(limit,(width-originX)/zoom),top=Math.Max(-limit,-originY/zoom),bottom=Math.Min(limit,(height-originY)/zoom);
        if(left>=right||top>=bottom){g.Restore(oldClip);return bitmap;}
        // World-aligned grid: eight pixels per cell, never beyond the map.
        double interval=Math.Max(terrain.Size/1023,8/zoom);left=Math.Max(-limit,Math.Floor(left/interval)*interval);top=Math.Max(-limit,Math.Floor(top/interval)*interval);
        int cols=Math.Max(1,(int)Math.Ceiling((right-left)/interval)),rows=Math.Max(1,(int)Math.Ceiling((bottom-top)/interval));
        var xs=Enumerable.Range(0,cols+1).Select(i=>Math.Min(limit,left+i*interval)).ToArray();var ys=Enumerable.Range(0,rows+1).Select(i=>Math.Min(limit,top+i*interval)).ToArray();
        var z=new double[cols+1,rows+1];double zmin=double.PositiveInfinity,zmax=double.NegativeInfinity,baseHeight=BaseElevation(scenery);
        for(int y=0;y<=rows;y++)for(int x=0;x<=cols;x++){double value=terrain.Sample(xs[x],ys[y])-baseHeight;z[x,y]=value;zmin=Math.Min(zmin,value);zmax=Math.Max(zmax,value);}
        double range=zmax-zmin;if(range<.00001){g.Restore(oldClip);using var border=new Pen(Color.FromArgb(75,75,75));g.DrawRectangle(border,mapRect.X,mapRect.Y,mapRect.Width,mapRect.Height);return bitmap;}
        double[] steps=[50,25,10,5,2.5,1];double step=steps[Math.Clamp(fidelity,0,5)];int bold=step is 2.5 or 5 or 25?4:5;var contours=new SortedDictionary<double,List<(Vec A,Vec B)>>();
        for(int y=0;y<rows;y++)for(int x=0;x<cols;x++)
        {
            double a=z[x,y],b=z[x+1,y],c=z[x+1,y+1],d=z[x,y+1],lo=Math.Min(Math.Min(a,b),Math.Min(c,d)),hi=Math.Max(Math.Max(a,b),Math.Max(c,d));
            if(shaded){var color=HeightColor(((a+b+c+d)*.25-zmin)/range);using var fill=new SolidBrush(Color.FromArgb(38,color));var p=Screen(xs[x],ys[y]);var q=Screen(xs[x+1],ys[y+1]);g.FillRectangle(fill,p.X,p.Y,q.X-p.X+1,q.Y-p.Y+1);}
            if(hi-lo<.00001)continue;
            for(double level=Math.Ceiling(lo/step)*step;level<=hi;level+=step)
            {
                int mask=(a>level?8:0)|(b>level?4:0)|(c>level?2:0)|(d>level?1:0);if(mask is 0 or 15)continue;if((mask is 5 or 10)&&(a+b+c+d)*.25>level)mask^=15;
                (int,int)[] links=mask switch{1 or 14=>[(3,2)],2 or 13=>[(2,1)],3 or 12=>[(3,1)],4 or 11=>[(1,0)],5=>[(1,0),(3,2)],6 or 9=>[(2,0)],7 or 8=>[(3,0)],10=>[(0,3),(2,1)],_=>[]};
                double Lerp(double first,double second,double origin,double end){double delta=second-first;return origin+(end-origin)*(Math.Abs(delta)<.00001?.5:(level-first)/delta);}
                Vec Edge(int id)=>id switch{0=>new(Lerp(a,b,xs[x],xs[x+1]),ys[y]),1=>new(xs[x+1],Lerp(b,c,ys[y],ys[y+1])),2=>new(Lerp(d,c,xs[x],xs[x+1]),ys[y+1]),_=>new(xs[x],Lerp(a,d,ys[y],ys[y+1]))};
                if(!contours.TryGetValue(level,out var segments))contours[level]=segments=[];foreach(var (first,second) in links)segments.Add((Edge(first),Edge(second)));
            }
        }
        using var font=new Font("Segoe UI",8);var occupied=new List<RectangleF>();
        foreach(var (level,segments) in contours)
        {
            bool major=Math.Abs(level/(step*bold)-Math.Round(level/(step*bold)))<.000001;Color color=major?HeightColor((level-zmin)/range):UiTheme.Muted;using var pen=new Pen(color,major?2:1){LineJoin=LineJoin.Round};
            foreach(var line in Stitch(segments))
            {
                if(line.Count<2)continue;var screen=line.Select(p=>Screen(p.X,p.Y)).ToArray();g.DrawLines(pen,screen);if(!major||!labels)continue;
                double length=0;for(int i=1;i<screen.Length;i++)length+=Math.Sqrt(Math.Pow(screen[i].X-screen[i-1].X,2)+Math.Pow(screen[i].Y-screen[i-1].Y,2));if(length<125)continue;
                var mid=screen[screen.Length/2];string text=$"{level:0.#} m";var size=g.MeasureString(text,font);var rect=new RectangleF(mid.X-size.Width/2-2,mid.Y-size.Height/2-1,size.Width+4,size.Height+2);
                if(!mapRect.Contains(rect)||rect.Left<26||rect.Top<26||rect.Right>width-8||rect.Bottom>height-8||occupied.Any(r=>r.IntersectsWith(rect)))continue;using var labelBg=new SolidBrush(UiTheme.Canvas);using var labelText=new SolidBrush(color);g.FillRectangle(labelBg,rect);g.DrawString(text,font,labelText,rect.X+2,rect.Y+1);occupied.Add(RectangleF.Inflate(rect,24,12));
            }
        }
        g.Restore(oldClip);using(var border=new Pen(Color.FromArgb(75,75,75)))g.DrawRectangle(border,mapRect.X,mapRect.Y,mapRect.Width,mapRect.Height);return bitmap;
    }
    static IEnumerable<List<Vec>> Stitch(List<(Vec A,Vec B)> segments)
    {
        (long,long) Key(Vec p)=>((long)Math.Round(p.X*1000),(long)Math.Round(p.Y*1000));var adj=new Dictionary<(long,long),List<int>>();
        for(int i=0;i<segments.Count;i++)foreach(var key in new[]{Key(segments[i].A),Key(segments[i].B)}){if(!adj.TryGetValue(key,out var list))adj[key]=list=[];list.Add(i);}var used=new bool[segments.Count];
        for(int i=0;i<segments.Count;i++){if(used[i])continue;used[i]=true;var points=new LinkedList<Vec>();points.AddLast(segments[i].A);points.AddLast(segments[i].B);foreach(bool front in new[]{false,true})while(true){Vec p=front?points.First!.Value:points.Last!.Value;var key=Key(p);int next=adj[key].FirstOrDefault(j=>!used[j],-1);if(next<0)break;used[next]=true;var s=segments[next];Vec other=Key(s.A)==key?s.B:s.A;if(front)points.AddFirst(other);else points.AddLast(other);}yield return points.ToList();}
    }
}
