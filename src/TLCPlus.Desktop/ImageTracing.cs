using TLCPlus;
namespace TLCPlus.Desktop;
public static class ImageTracing
{
    public static List<Node> Trace(Bitmap source,double width,string mode="centerline")
    {
        if(!double.IsFinite(width)||width<=0)throw new ArgumentOutOfRangeException(nameof(width),"Track size must be positive.");
        mode=mode.Trim().ToLowerInvariant();
        if(mode is "outline" or "smart-outline")return TraceAlternative(source,width,mode);
        if(mode!="centerline")throw new ArgumentException("Choose centerline, outline or smart-outline.",nameof(mode));
        double scale=Math.Min(1,768.0/Math.Max(source.Width,source.Height));int w=Math.Max(3,(int)(source.Width*scale)),h=Math.Max(3,(int)(source.Height*scale));using var image=new Bitmap(w,h);using(var g=Graphics.FromImage(image)){g.Clear(Color.White);g.DrawImage(source,0,0,w,h);}
        var pixels=new bool[w*h];double Bright(Color c)=>(c.R*299+c.G*587+c.B*114)/1000.0;
        bool invert=new[]{image.GetPixel(0,0),image.GetPixel(w-1,0),image.GetPixel(0,h-1),image.GetPixel(w-1,h-1)}.Average(Bright)<128;
        for(int y=1;y<h-1;y++)for(int x=1;x<w-1;x++){double value=Bright(image.GetPixel(x,y));pixels[y*w+x]=invert?value>150:value<105;}
        int[] neighbors=[-w,-w+1,1,w+1,w,w-1,-1,-w-1];
        for(int pass=0;pass<250;pass++){int removed=0;for(int sub=0;sub<2;sub++){var remove=new List<int>();for(int y=1;y<h-1;y++)for(int x=1;x<w-1;x++){int i=y*w+x;if(!pixels[i])continue;var n=neighbors.Select(d=>pixels[i+d]).ToArray();int count=n.Count(p=>p),trans=0;for(int k=0;k<8;k++)if(!n[k]&&n[(k+1)%8])trans++;if(count<2||count>6||trans!=1)continue;bool a=sub==0?n[0]&&n[2]&&n[4]:n[0]&&n[2]&&n[6];bool b=sub==0?n[2]&&n[4]&&n[6]:n[0]&&n[4]&&n[6];if(!a&&!b)remove.Add(i);}foreach(int i in remove)pixels[i]=false;removed+=remove.Count;}if(removed==0)break;}
        var remaining=Enumerable.Range(0,pixels.Length).Where(i=>pixels[i]).ToHashSet();HashSet<int> largest=[];
        while(remaining.Count>0){int start=remaining.First();var component=new HashSet<int>{start};var queue=new Queue<int>();queue.Enqueue(start);remaining.Remove(start);while(queue.Count>0){int i=queue.Dequeue();foreach(int d in neighbors){int j=i+d;if(remaining.Remove(j)){component.Add(j);queue.Enqueue(j);}}}if(component.Count>largest.Count)largest=component;}
        if(largest.Count<10)return [];
        int Degree(int i)=>neighbors.Count(d=>largest.Contains(i+d));int current=largest.OrderBy(i=>Degree(i)==1?0:1).ThenBy(i=>i).First();var visited=new HashSet<int>();var path=new List<Node>();int previous=-1;
        for(int steps=0;steps<=largest.Count;steps++){visited.Add(current);double x=(current%w-w/2.0)*width/w,y=(current/w-h/2.0)*width/w;path.Add(new(){X=x,Y=y,Radius=35});var next=neighbors.Select(d=>current+d).Where(j=>largest.Contains(j)&&!visited.Contains(j)).ToList();if(next.Count==0)break;int chosen=next[0];if(previous>=0){double dx=current%w-previous%w,dy=current/w-previous/w;chosen=next.OrderByDescending(j=>{double nx=j%w-current%w,ny=j/w-current/w;return(dx*nx+dy*ny)/Math.Sqrt(nx*nx+ny*ny);}).First();}previous=current;current=chosen;}
        return Importers.Simplify(path,width/w*2.5);
    }

    readonly record struct Pixel(int X,int Y);

    // Outline modes follow imageTraceService.py: luma threshold, four-neighbour
    // dilation, then boundary walking or nearest-neighbour chaining.
    static List<Node> TraceAlternative(Bitmap source,double targetSize,string mode)
    {
        double factor=Math.Min(1,768.0/Math.Max(source.Width,source.Height));
        int width=Math.Max(3,(int)(source.Width*factor));
        int height=Math.Max(3,(int)(source.Height*factor));
        using var image=new Bitmap(width,height);
        using(var graphics=Graphics.FromImage(image))
        {
            graphics.Clear(Color.White);
            graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(source,0,0,width,height);
        }
        var luma=new byte[width*height];double sum=0;
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            var color=image.GetPixel(x,y);
            byte value=(byte)((color.R*299+color.G*587+color.B*114+500)/1000);
            luma[y*width+x]=value;sum+=value;
        }
        double mean=sum/luma.Length;
        bool darkOnLight=mean>128;
        double threshold=mean*(darkOnLight?.90:1.10);
        var mask=new bool[luma.Length];
        for(int i=0;i<luma.Length;i++)mask[i]=darkOnLight?luma[i]<threshold:luma[i]>threshold;
        var dilated=(bool[])mask.Clone();
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            int i=y*width+x;if(!mask[i])continue;
            if(y>0)dilated[i-width]=true;if(y+1<height)dilated[i+width]=true;
            if(x>0)dilated[i-1]=true;if(x+1<width)dilated[i+1]=true;
        }
        var ink=new List<Pixel>();
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            if(dilated[y*width+x])ink.Add(new(x,y));
        if(ink.Count<50)return [];
        var raw=mode=="outline"?MooreBoundary(dilated,width,height,ink[0]):SmartFill(ink);
        return FilterPath(raw,targetSize,mode);
    }

    static List<Pixel> MooreBoundary(bool[] mask,int width,int height,Pixel start)
    {
        (int X,int Y)[] directions=[(0,-1),(1,-1),(1,0),(1,1),(0,1),(-1,1),(-1,0),(-1,-1)];
        var path=new List<Pixel>{start};var current=start;int backtrack=6;
        // Bound the walk for self-touching components that may not return to the start.
        for(int iteration=0;iteration<30000;iteration++)
        {
            bool found=false;
            for(int i=0;i<8;i++)
            {
                int direction=(backtrack+1+i)%8;
                var delta=directions[direction];
                int x=current.X+delta.X,y=current.Y+delta.Y;
                if(x<0||x>=width||y<0||y>=height||!mask[y*width+x])continue;
                current=new(x,y);path.Add(current);backtrack=(direction+4)%8;found=true;break;
            }
            if(!found||current==start)break;
        }
        return path;
    }

    static List<Pixel> SmartFill(List<Pixel> ink)
    {
        // Keep the source's subsampling and swap/pop order for deterministic results.
        int step=Math.Max(1,ink.Count/1500);
        var remaining=new List<Pixel>();
        for(int i=0;i<ink.Count;i+=step)remaining.Add(ink[i]);
        var path=new List<Pixel>(remaining.Count);
        path.Add(remaining[^1]);remaining.RemoveAt(remaining.Count-1);
        while(remaining.Count>0)
        {
            var current=path[^1];int best=0;long distance=long.MaxValue;
            for(int i=0;i<remaining.Count;i++)
            {
                long x=remaining[i].X-current.X,y=remaining[i].Y-current.Y;
                long candidate=x*x+y*y;
                if(candidate<distance){best=i;distance=candidate;}
            }
            path.Add(remaining[best]);remaining[best]=remaining[^1];remaining.RemoveAt(remaining.Count-1);
        }
        return path;
    }

    static List<Node> FilterPath(List<Pixel> raw,double targetSize,string mode)
    {
        if(raw.Count<5)return [];
        int minX=raw.Min(p=>p.X),maxX=raw.Max(p=>p.X);
        int minY=raw.Min(p=>p.Y),maxY=raw.Max(p=>p.Y);
        double size=Math.Max(1,Math.Max(maxX-minX,maxY-minY));
        double epsilon=size*(mode=="outline"?.005:.008);
        var pixelNodes=raw.Select(p=>new Node{X=p.X,Y=p.Y}).ToList();
        var simplified=Importers.Simplify(pixelNodes,epsilon);
        double physicalScale=targetSize/size,centerX=(minX+maxX)/2.0,centerY=(minY+maxY)/2.0;
        var physical=simplified.Select(p=>new Node
        {
            X=(p.X-centerX)*physicalScale,Y=(p.Y-centerY)*physicalScale,
            Radius=40,Camber=-1,Segments=1
        }).ToList();
        var spaced=new List<Node>{physical[0]};
        foreach(var point in physical.Skip(1))
            if(Geometry.Length(spaced[^1].XY,point.XY)>35)spaced.Add(point);
        if(spaced.Count<3)return [];
        var result=new List<Node>{spaced[0]};
        for(int i=1;i<spaced.Count-1;i++)
        {
            var previous=result[^1];var current=spaced[i];var next=spaced[i+1];
            double a=Math.Atan2(current.Y-previous.Y,current.X-previous.X);
            double b=Math.Atan2(next.Y-current.Y,next.X-current.X);
            double difference=Math.Abs(a-b);if(difference>Math.PI)difference=2*Math.PI-difference;
            if(difference>8*Math.PI/180)result.Add(current);
        }
        result.Add(spaced[^1]);
        if(result.Count>3&&Geometry.Length(result[0].XY,result[^1].XY)<targetSize*.15)
            result[^1].XY=result[0].XY;
        return result.Count<3?[]:result;
    }
}
