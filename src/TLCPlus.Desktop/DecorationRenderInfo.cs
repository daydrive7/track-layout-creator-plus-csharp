using System.Text.Json;
using TLCPlus;

namespace TLCPlus.Desktop;

// Cache catalogue lookups used on every decoration paint.
internal static class DecorationRenderInfo
{
    internal readonly record struct Rail(int Type,double Length);
    static readonly Dictionary<int,Dictionary<ulong,Rail>> rails=[];
    static readonly Dictionary<(int Scenery,int Type),RectangleF> bounds=[];
    static JsonDocument? artwork;

    static Dictionary<ulong,Rail> Rails(int scenery)
    {
        if(rails.TryGetValue(scenery,out var result))return result;
        result=[];
        foreach(var item in Catalog.Decorations(scenery).GetProperty("variants").EnumerateArray())
        {
            double length=item.GetProperty("len").GetDouble();
            result[item.GetProperty("uuid").GetUInt64()]=new(item.GetProperty("type").GetInt32(),length==0?50:length);
        }
        rails[scenery]=result;
        return result;
    }

    internal static Rail Resolve(int scenery,ulong uuid,int fallbackType=-1)
    {
        if(Rails(scenery).TryGetValue(uuid,out var result))return result;
        foreach(int other in new[]{1,2,3,4,5})if(other!=scenery&&Rails(other).TryGetValue(uuid,out result))return result;
        return new(fallbackType,50);
    }

    internal static RectangleF SymbolBounds(int scenery,int type)
    {
        if(type==17)type=9;
        if(bounds.TryGetValue((scenery,type),out var result))return result;
        artwork??=JsonDocument.Parse(File.ReadAllText(Path.Combine(Terrain.AssetsPath,"decoration-artwork.json")));
        var scene=artwork.RootElement.TryGetProperty(scenery.ToString(),out var found)?found:artwork.RootElement.GetProperty("2");
        if(!scene.GetProperty("symbols").TryGetProperty(type.ToString(),out var symbol))return new(-.5f,-.5f,1,1);
        float left=float.PositiveInfinity,top=float.PositiveInfinity,right=float.NegativeInfinity,bottom=float.NegativeInfinity;
        foreach(var command in symbol.GetProperty("commands").EnumerateArray())
        {
            var coords=command.GetProperty("coords").EnumerateArray().Select(v=>(float)v.GetDouble()/100f).ToArray();
            for(int i=0;i+1<coords.Length;i+=2){left=Math.Min(left,coords[i]);right=Math.Max(right,coords[i]);top=Math.Min(top,coords[i+1]);bottom=Math.Max(bottom,coords[i+1]);}
        }
        result=float.IsFinite(left)?RectangleF.FromLTRB(left,top,right,bottom):new(-.5f,-.5f,1,1);
        bounds[(scenery,type)]=result;
        return result;
    }
}
