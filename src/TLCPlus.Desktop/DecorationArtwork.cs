using System.Drawing.Drawing2D;
using System.Text.Json;
using TLCPlus;

namespace TLCPlus.Desktop;

/// <summary>
/// Renders the original drawDecoSymbol paths, stored at radius 100 in the artwork asset.
/// </summary>
public static class DecorationArtwork
{
    public sealed record PaletteItem(string Name,string Symbol,Color Color,int Type,bool TwoSided);
    static JsonDocument? document;
    static JsonElement Scenery(int scenery)
    {
        document??=JsonDocument.Parse(File.ReadAllText(Path.Combine(Terrain.AssetsPath,"decoration-artwork.json")));
        return document.RootElement.TryGetProperty(scenery.ToString(),out var set)?set:document.RootElement.GetProperty("2");
    }
    static JsonElement Symbol(int scenery,int type)
    {
        var symbols=Scenery(scenery).GetProperty("symbols");
        if(type==17)type=9;
        return symbols.TryGetProperty(type.ToString(),out var symbol)?symbol:default;
    }
    public static IReadOnlyList<PaletteItem> Entries(int scenery)
        =>Scenery(scenery).GetProperty("palette").EnumerateArray().Select(e=>new PaletteItem(e[0].GetString()!,e[1].GetString()!,ColorTranslator.FromHtml(e[2].GetString()!),(int)e[3].GetInt32(),e[4].GetBoolean())).ToList();
    public static string Name(int scenery,int type)
    {
        var symbol=Symbol(scenery,type);
        return symbol.ValueKind==JsonValueKind.Undefined?$"Type {type}":symbol.GetProperty("name").GetString()!;
    }
    public static Color TypeColor(int scenery,int type)
    {
        var symbol=Symbol(scenery,type);
        return symbol.ValueKind==JsonValueKind.Undefined?Color.LightGray:ColorTranslator.FromHtml(symbol.GetProperty("colour").GetString()!);
    }
    public static string SymbolName(int scenery,int type)
    {
        var symbol=Symbol(scenery,type);
        return symbol.ValueKind==JsonValueKind.Undefined?"dot":symbol.GetProperty("symbol").GetString()!;
    }
    public static Bitmap Palette(int scenery,int type,bool twoSided=true,int pixels=44)
    {
        var bitmap=new Bitmap(pixels,pixels,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics=Graphics.FromImage(bitmap);
        graphics.SmoothingMode=SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        DrawWorld(graphics,scenery,type,pixels*.5f,pixels*26/44f,pixels*11/44f);
        if(!twoSided)
        {
            using var brush=new SolidBrush(Color.FromArgb(235,235,235));
            using var font=new Font("Segoe UI",7,FontStyle.Bold,GraphicsUnit.Pixel);
            graphics.DrawString("1",font,brush,pixels-8,pixels-10);
        }
        return bitmap;
    }
    public static int TypeFromUuid(int scenery,ulong uuid)
    {
        foreach(var variant in Catalog.Decorations(scenery).GetProperty("variants").EnumerateArray())
            if(variant.GetProperty("uuid").GetUInt64()==uuid)return variant.GetProperty("type").GetInt32();
        foreach(int scene in new[]{1,2,3,4,5})
            foreach(var variant in Catalog.Decorations(scene).GetProperty("variants").EnumerateArray())
                if(variant.GetProperty("uuid").GetUInt64()==uuid)return variant.GetProperty("type").GetInt32();
        return -1;
    }
    public static IReadOnlyList<JsonElement> Variants(int scenery,int type,string side="left",string track="AB",bool? twoSided=null)
    {
        var catalog=Catalog.Decorations(scenery);
        var excluded=catalog.GetProperty("cliff_left").EnumerateArray().Where((_,i)=>i!=1)
            .Concat(catalog.GetProperty("cliff_right").EnumerateArray().Where((_,i)=>i!=1)).Select(e=>e.GetUInt64()).ToHashSet();
        var pool=catalog.GetProperty("variants").EnumerateArray().Where(v=>v.GetProperty("type").GetInt32()==type&&!excluded.Contains(v.GetProperty("uuid").GetUInt64())).ToList();
        if(twoSided.HasValue&&type is 12 or 18)
        {
            var matched=pool.Where(v=>
            {
                var disp=v.GetProperty("disp");
                bool both=disp[0].GetInt32()!=0&&disp[1].GetInt32()!=0;
                return both==twoSided.Value;
            }).ToList();
            if(matched.Count>0)pool=matched;
        }
        int index=side=="right"?1:0;
        int code=track switch{"CURB"=>1,"A"=>2,"B"=>3,_=>4};
        var exact=pool.Where(v=>v.GetProperty("disp")[index].GetInt32()==code).ToList();
        return exact.Count>0?exact:pool;
    }
    public static void DrawWorld(Graphics graphics,int scenery,int type,float x,float y,float radius)
    {
        var symbol=Symbol(scenery,type);
        if(symbol.ValueKind==JsonValueKind.Undefined)
        {
            using var brush=new SolidBrush(Color.LightGray);
            graphics.FillEllipse(brush,x-radius*.5f,y-radius*.5f,radius,radius);
            return;
        }
        float scale=radius/100f;
        var saved=graphics.Save();
        graphics.SmoothingMode=SmoothingMode.AntiAlias;
        foreach(var command in symbol.GetProperty("commands").EnumerateArray())
        {
            var coords=command.GetProperty("coords").EnumerateArray().Select(e=>(float)e.GetDouble()).ToArray();
            var points=Enumerable.Range(0,coords.Length/2).Select(i=>new PointF(x+coords[i*2]*scale,y+coords[i*2+1]*scale)).ToArray();
            string shape=command.GetProperty("shape").GetString()!;
            string fill=command.TryGetProperty("fill",out var fc)?fc.GetString()??"":"";
            string outline=command.TryGetProperty("outline",out var oc)?oc.GetString()??"":shape=="line"?"":"#000000";
            float minimum=command.TryGetProperty("minimumWidth",out var mw)?(float)mw.GetDouble():1;
            float linear=command.TryGetProperty("linearWidth",out var lw)?(float)lw.GetDouble():0;
            float slope=command.TryGetProperty("slopeWidth",out var sw)?(float)sw.GetDouble():0;
            float width=linear*radius+Math.Max(minimum,slope*radius);
            bool smooth=command.TryGetProperty("smooth",out var sm)&&sm.GetBoolean();
            bool round=command.TryGetProperty("capstyle",out var cap)&&cap.GetString()=="round";
            using var brush=string.IsNullOrEmpty(fill)?null:new SolidBrush(ColorTranslator.FromHtml(fill));
            using var pen=string.IsNullOrEmpty(shape=="line"?fill:outline)?null:new Pen(ColorTranslator.FromHtml(shape=="line"?fill:outline),width)
                {StartCap=round?LineCap.Round:LineCap.Flat,EndCap=round?LineCap.Round:LineCap.Flat,LineJoin=LineJoin.Round};
            if(shape is "oval" or "rectangle" or "arc")
            {
                var r=RectangleF.FromLTRB(Math.Min(points[0].X,points[1].X),Math.Min(points[0].Y,points[1].Y),Math.Max(points[0].X,points[1].X),Math.Max(points[0].Y,points[1].Y));
                if(shape=="oval")
                {
                    if(brush!=null)graphics.FillEllipse(brush,r);
                    if(pen!=null)graphics.DrawEllipse(pen,r);
                }
                else if(shape=="rectangle")
                {
                    if(brush!=null)graphics.FillRectangle(brush,r);
                    if(pen!=null)graphics.DrawRectangle(pen,r.X,r.Y,r.Width,r.Height);
                }
                else
                {
                    float start=command.TryGetProperty("start",out var st)?-(float)st.GetDouble():0;
                    float sweep=command.TryGetProperty("extent",out var ex)?-(float)ex.GetDouble():-90;
                    if(brush!=null)graphics.FillPie(brush,r.X,r.Y,r.Width,r.Height,start,sweep);
                    if(pen!=null)graphics.DrawArc(pen,r,start,sweep);
                }
            }
            else if(shape=="polygon")
            {
                using var path=VectorPath(points,true,smooth);
                if(brush!=null)graphics.FillPath(brush,path);
                if(pen!=null)graphics.DrawPath(pen,path);
            }
            else if(shape=="line"&&pen!=null&&points.Length>1)
            {
                using var path=VectorPath(points,false,smooth);
                graphics.DrawPath(pen,path);
            }
        }
        graphics.Restore(saved);
    }
    static GraphicsPath VectorPath(PointF[] points,bool closed,bool smooth)
    {
        var path=new GraphicsPath();
        if(!smooth||points.Length<3)
        {
            if(closed)path.AddPolygon(points);else path.AddLines(points);
            return path;
        }
        // Tk smooth paths use quadratic B-splines; GDI+ needs cubic Bezier spans.
        PointF Mid(PointF a,PointF b)=>new((a.X+b.X)*.5f,(a.Y+b.Y)*.5f);
        PointF Blend(PointF a,PointF b,float f)=>new(a.X+(b.X-a.X)*f,a.Y+(b.Y-a.Y)*f);
        if(closed)
        {
            for(int i=0;i<points.Length;i++)
            {
                var a=Mid(points[(i+points.Length-1)%points.Length],points[i]);
                var b=points[i];var c=Mid(points[i],points[(i+1)%points.Length]);
                path.AddBezier(a,Blend(a,b,2f/3),Blend(c,b,2f/3),c);
            }
            path.CloseFigure();
        }
        else
        {
            for(int i=1;i<points.Length-1;i++)
            {
                var a=i==1?points[0]:Mid(points[i-1],points[i]);
                var b=points[i];var c=i==points.Length-2?points[^1]:Mid(points[i],points[i+1]);
                path.AddBezier(a,Blend(a,b,2f/3),Blend(c,b,2f/3),c);
            }
        }
        return path;
    }
}
