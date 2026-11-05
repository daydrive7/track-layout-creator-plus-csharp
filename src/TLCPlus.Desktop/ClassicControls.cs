using System.Drawing.Drawing2D;
namespace TLCPlus.Desktop;
public class ClassicScale:Control
{
    public double Minimum,Maximum,Step,Value;
    public string Label="";
    public event Action<double>? ValueChanged;
    public event Action? ChangeFinished;
    bool dragging;
    public ClassicScale(string label,double min,double max,double step){Label=label;AccessibleName=label;Minimum=min;Maximum=max;Step=step;Height=49;DoubleBuffered=true;TabStop=true;Cursor=Cursors.Hand;BackColor=Color.FromArgb(30,30,30);ForeColor=Color.FromArgb(224,224,224);}
    RectangleF Track=>new(2,36,Width-4,12);
    protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(BackColor);using var text=new SolidBrush(ForeColor);g.DrawString(Label,Font,text,3,1);var t=Track;using var trough=new SolidBrush(UiTheme.Button);g.FillRectangle(trough,t);using var edge=new Pen(UiTheme.Border);g.DrawRectangle(edge,t.X,t.Y,t.Width,t.Height);double f=Math.Clamp((Value-Minimum)/(Maximum-Minimum),0,1);float x=t.X+7+(t.Width-14)*(float)f;using var thumb=new SolidBrush(dragging?UiTheme.Accent:UiTheme.Border);g.FillRectangle(thumb,x-7,t.Y+1,14,t.Height-2);g.DrawRectangle(Pens.Black,x-7,t.Y+1,14,t.Height-2);g.DrawLine(Pens.Gray,x-5,t.Y+2,x-5,t.Bottom-2);g.DrawLine(Pens.LightGray,x-1,t.Y+2,x-1,t.Bottom-2);string value=Value.ToString(Step<.1?"0.00":Step<1?"0.0":"0");var size=g.MeasureString(value,Font);g.DrawString(value,Font,text,Math.Clamp(x-size.Width/2,3,Width-size.Width-3),18);}
    void SetFromX(int x){var t=Track;double f=Math.Clamp((x-t.X-7)/(t.Width-14),0,1);Value=Minimum+f*(Maximum-Minimum);if(Step>0)Value=Math.Round(Value/Step)*Step;Value=Math.Clamp(Value,Minimum,Maximum);ValueChanged?.Invoke(Value);Invalidate();}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Right||e.Clicks==2){double? value=MainForm.NumberDialog(Label,"Value",Value,Minimum,Maximum);if(value.HasValue){Value=value.Value;ValueChanged?.Invoke(Value);ChangeFinished?.Invoke();Invalidate();}return;}if(e.Button==MouseButtons.Left){dragging=true;Capture=true;Focus();SetFromX(e.X);}}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)SetFromX(e.X);}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(dragging){dragging=false;Capture=false;ChangeFinished?.Invoke();Invalidate();}}
    protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode is Keys.Left or Keys.Right){Value=Math.Clamp(Value+(e.KeyCode==Keys.Left?-Step:Step),Minimum,Maximum);ValueChanged?.Invoke(Value);ChangeFinished?.Invoke();Invalidate();e.Handled=true;}}
}
public class FlatTabs:Control
{
    public string[] Labels=["Geometry","Map & Toggles","Generators","Decorations"];
    public int Selected;
    public event Action<int>? Changed;
    public FlatTabs(){Height=38;Dock=DockStyle.Top;DoubleBuffered=true;BackColor=Color.FromArgb(30,30,30);Cursor=Cursors.Hand;AccessibleName="Sidebar tabs";}
    float[] TabWidths()
    {
        using var font=new Font("Segoe UI",8,FontStyle.Bold);var widths=Labels.Select(label=>(float)Math.Max(48,TextRenderer.MeasureText(UiLanguage.T(label),font).Width+12)).ToArray();float total=widths.Sum();return widths.Select(w=>w*Width/total).ToArray();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;var widths=TabWidths();using var font=new Font("Segoe UI",8,FontStyle.Bold);float x=0;
        for(int i=0;i<4;i++){using var brush=new SolidBrush(i==Selected?UiTheme.Button:BackColor);var r=new RectangleF(x+2,5,widths[i]-4,29);g.FillRectangle(brush,r);TextRenderer.DrawText(g,UiLanguage.T(Labels[i]),font,Rectangle.Round(r),i==Selected?UiTheme.Text:UiTheme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis);x+=widths[i];}
        using var line=new Pen(UiTheme.Button);g.DrawLine(line,4,Height-1,Width-4,Height-1);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);float edge=0;var widths=TabWidths();for(int i=0;i<4;i++){edge+=widths[i];if(e.X<edge||i==3){Selected=i;break;}}Invalidate();Changed?.Invoke(Selected);
    }

}
public sealed class DarkMenuRenderer:ToolStripProfessionalRenderer
{
    public DarkMenuRenderer():base(new DarkColors()){}
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e){}
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){e.TextColor=Color.FromArgb(224,224,224);base.OnRenderItemText(e);}
    class DarkColors:ProfessionalColorTable
    {
        public override Color MenuItemSelected=>Color.FromArgb(55,55,55);
        public override Color MenuItemBorder=>Color.FromArgb(55,55,55);
        public override Color MenuItemPressedGradientBegin=>Color.FromArgb(45,45,45);
        public override Color MenuItemPressedGradientEnd=>Color.FromArgb(45,45,45);
        public override Color ToolStripDropDownBackground=>Color.FromArgb(30,30,30);
        public override Color ImageMarginGradientBegin=>Color.FromArgb(30,30,30);
        public override Color ImageMarginGradientMiddle=>Color.FromArgb(30,30,30);
        public override Color ImageMarginGradientEnd=>Color.FromArgb(30,30,30);
    }
}
public sealed class ClassicRadio:RadioButton
{
    public ClassicRadio(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        float y=(Height-11)/2f;using var border=new Pen(UiTheme.Muted);using var fill=new SolidBrush(UiTheme.Button);e.Graphics.FillEllipse(fill,1,y,11,11);e.Graphics.DrawEllipse(border,1,y,11,11);
        if(Checked){using var selected=new SolidBrush(UiTheme.Text);e.Graphics.FillEllipse(selected,4,y+3,5,5);}
        TextRenderer.DrawText(e.Graphics,Text.TrimStart(),Font,new Rectangle(16,0,Width-16,Height),ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix);
    }
    protected override void OnCheckedChanged(EventArgs e){base.OnCheckedChanged(e);Invalidate();}
}
