using TLCPlus;
namespace TLCPlus.Desktop;
public partial class MainForm
{
    void OpenIsometric()
    {
        if(Preview==null){ShowErrorNotice("Create or load a track first.","Isometric view");return;}
        var a=Canvas.World(new Point(22,22));var b=Canvas.World(new Point(Canvas.Width,Canvas.Height));
        var bounds=RectangleF.FromLTRB((float)a.X,(float)a.Y,(float)b.X,(float)b.Y);
        new IsometricForm(Project,Preview,bounds,UiTheme.Classic).Show(this);
    }
}
