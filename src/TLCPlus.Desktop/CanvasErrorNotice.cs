using System.Diagnostics;
using System.Drawing.Drawing2D;
namespace TLCPlus.Desktop;

// A composited, click-through notice fades without repainting the track every frame.
internal sealed class CanvasErrorNotice : Form
{
    readonly MainForm main;
    readonly System.Windows.Forms.Timer timer=new(){Interval=16};
    readonly Stopwatch clock=new();
    string heading="",message="";
    double duration=3.5;
    internal string Message=>message;
    internal double Duration=>duration;
    internal bool Animating=>timer.Enabled;
    internal static double FadeOpacity(double elapsed,double seconds)
    {
        double progress=Math.Clamp(elapsed/AppPreferences.NoticeDuration(seconds),0,1);
        return progress<=.4?1:Math.Pow((1-progress)/.6,1.4);
    }
    public CanvasErrorNotice(MainForm owner)
    {
        main=owner;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;
        StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.Dpi;
        Font=new("Segoe UI",9);DoubleBuffered=true;AccessibleRole=AccessibleRole.Alert;
        timer.Tick+=(_,_)=>Advance(clock.Elapsed.TotalSeconds);
        main.LocationChanged+=Reposition;main.SizeChanged+=Reposition;
        main.Canvas.SizeChanged+=Reposition;main.Canvas.LocationChanged+=Reposition;
    }
    protected override bool ShowWithoutActivation=>true;
    protected override CreateParams CreateParams
    {
        get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x80|0x20;return p;}
    }
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==0x84){m.Result=(IntPtr)(-1);return;}
        base.WndProc(ref m);
    }
    internal void Present(string text,string caption,double seconds)
    {
        message=text;heading=caption=="TLC+"?UiLanguage.T("Error"):caption;
        AccessibleName=heading+": "+message;duration=AppPreferences.NoticeDuration(seconds);
        Text=heading;Opacity=1;clock.Restart();Reposition(null,EventArgs.Empty);
        if(!Visible)Show(main);
        Reposition(null,EventArgs.Empty);Invalidate();timer.Start();
        AccessibilityNotifyClients(AccessibleEvents.SystemAlert,-1);
    }
    internal void Advance(double elapsed)
    {
        if(elapsed>=duration){timer.Stop();clock.Stop();Hide();return;}
        Opacity=FadeOpacity(elapsed,duration);
    }
    void Reposition(object? sender,EventArgs e)
    {
        if(IsDisposed||main.IsDisposed||!main.Canvas.IsHandleCreated)return;
        double scale=main.DeviceDpi/96.0;
        int gap=Math.Min((int)(16*scale),main.Canvas.Width/10);
        int left=Math.Min((int)(108*scale),Math.Max(gap,main.Canvas.Width/4));
        var canvas=main.Canvas.RectangleToScreen(main.Canvas.ClientRectangle);
        int available=Math.Max(1,canvas.Width-left-gap);
        int width=Math.Min((int)(680*scale),available);
        int padding=(int)(18*scale);
        int textHeight=TextRenderer.MeasureText(message,Font,new Size(Math.Max(1,width-padding*2),int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height;
        int height=Math.Min(Math.Max((int)(78*scale),textHeight+(int)(53*scale)),Math.Max(1,canvas.Height-(int)(48*scale)));
        Bounds=new Rectangle(canvas.Left+left+(available-width)/2,canvas.Top+(int)(32*scale),width,height);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        Color red=UiTheme.Classic?Color.FromArgb(180,42,36):Color.FromArgb(255,112,104);
        BackColor=UiTheme.Panel;g.Clear(BackColor);
        using(var border=new Pen(red,1))g.DrawRectangle(border,0,0,Width-1,Height-1);
        using(var stripe=new SolidBrush(red))g.FillRectangle(stripe,0,0,4,Height);
        int pad=(int)(18*DeviceDpi/96.0),line=Font.Height+5;
        using var bold=new Font(Font,FontStyle.Bold);
        TextRenderer.DrawText(g,heading,bold,new Rectangle(pad,pad-3,Width-pad*2,line),red,TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g,message,Font,new Rectangle(pad,pad+line,Width-pad*2,Math.Max(1,Height-pad*2-line)),UiTheme.Text,TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis);
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing){timer.Dispose();main.LocationChanged-=Reposition;main.SizeChanged-=Reposition;main.Canvas.SizeChanged-=Reposition;main.Canvas.LocationChanged-=Reposition;}
        base.Dispose(disposing);
    }
}

public partial class MainForm
{
    internal CanvasErrorNotice? ErrorNotice {get;private set;}
    internal void ShowErrorNotice(string text,string caption="TLC+")
    {
        if(IsDisposed||Disposing)return;
        if(InvokeRequired){BeginInvoke(()=>ShowErrorNotice(text,caption));return;}
        if(ErrorNotice==null||ErrorNotice.IsDisposed)ErrorNotice=new(this);
        ErrorNotice.Present(UiLanguage.T(text),UiLanguage.T(caption),uiPreferences.ErrorNoticeSeconds);
    }
    internal static bool RouteError(IWin32Window? owner,string text,string caption)
    {
        var form=owner as MainForm??(owner as Control)?.FindForm() as MainForm??Application.OpenForms.OfType<MainForm>().FirstOrDefault(f=>!f.IsDisposed);
        if(form==null)return false;
        form.ShowErrorNotice(text,caption);return true;
    }
}
