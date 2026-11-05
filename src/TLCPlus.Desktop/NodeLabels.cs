using TLCPlus;
namespace TLCPlus.Desktop;
public partial class TrackCanvas
{
    internal string NodeDetails(int index)
    {
        var node=Project.Nodes[index];string text=ShowNumbers?$"[{index}]\n":"";
        if(Tool=="camber")return text+(node.Camber==-1?"C "+UiLanguage.T("Global"):$"C {node.Camber:0.0}°");
        double sweep=NodeSweep(index)*180/Math.PI;
        text+=$"R {node.Radius:0.0}\n{sweep:0.0}°";
        if(node.Z!=0)text+=$"\nH: {node.Z:0.0} m";
        if(Math.Pow(Math.Abs(sweep),1.5)<node.Radius)text+="!";
        return text;
    }
    double NodeSweep(int index)
    {
        var nodes=Project.Nodes;double corner=Geometry.VectorAngle(nodes[(index+nodes.Count-1)%nodes.Count].XY,nodes[(index+1)%nodes.Count].XY,nodes[index].XY);
        return corner>0?Math.PI-corner:-Math.PI-corner;
    }
    void DrawNodeProperties(Graphics graphics,int index,Font font,bool selected)
    {
        var nodes=Project.Nodes;var node=nodes[index];var previous=nodes[(index+nodes.Count-1)%nodes.Count];double sweep=NodeSweep(index);
        double angle=Geometry.Angle(previous.XY,node.XY)+sweep/2-Math.PI/2;if(sweep<0)angle+=Math.PI;
        var point=Screen(node.XY);if(point.X<22||point.X>Width||point.Y<22||point.Y>Height)return;double offset=LabelFontSize*4;point.X+=(float)(Math.Cos(angle)*offset);point.Y-=(float)(Math.Sin(angle)*offset);
        string text=NodeDetails(index);var size=graphics.MeasureString(text,font);using var brush=new SolidBrush(selected?Color.FromArgb(255,183,78):UiTheme.CanvasText);
        point.X=Math.Clamp(point.X,24+size.Width/2,Math.Max(24+size.Width/2,Width-2-size.Width/2));point.Y=Math.Clamp(point.Y,24+size.Height/2,Math.Max(24+size.Height/2,Height-2-size.Height/2));
        using var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center};
        graphics.DrawString(text,font,brush,new RectangleF(point.X-size.Width/2,point.Y-size.Height/2,size.Width+2,size.Height+2),format);
    }
}
