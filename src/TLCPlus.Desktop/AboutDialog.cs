namespace TLCPlus.Desktop;
public partial class MainForm
{
    internal static string CombinedCredits()=>UiLanguage.T("TLC — created by eran0004")+" (GTPlanet)\n"+UiLanguage.T("TLC+ (Python, C#, and Web) — created by daydrive7")+" (Discord, GitHub)\n"+UiLanguage.T("MIT License")+" — "+UiLanguage.T("Native interface and some behavior differ from Python.");
    void ShowAbout()
    {
        using var dialog=new Form{Text=UiLanguage.T("About / Credits"),ClientSize=new(600,220),FormBorderStyle=FormBorderStyle.FixedDialog,StartPosition=FormStartPosition.CenterParent,MaximizeBox=false,MinimizeBox=false,Font=new("Segoe UI",9),BackColor=UiTheme.Panel,ForeColor=UiTheme.Text};
        var body=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new(16),ColumnCount=1,RowCount=4};body.ColumnStyles.Add(new(SizeType.Percent,100));
        foreach(float height in new[]{30f,25f})body.RowStyles.Add(new(SizeType.Absolute,height));body.RowStyles.Add(new(SizeType.Percent,100));body.RowStyles.Add(new(SizeType.Absolute,42));dialog.Controls.Add(body);
        body.Controls.Add(new Label{Text=UiLanguage.T("Track Layout Creator + (TLC+)"),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,Font=new("Segoe UI",12,FontStyle.Bold),ForeColor=UiTheme.Header},0,0);
        body.Controls.Add(new Label{Text="C# 1.0.3 · TLC+ Python 1.3.0",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=UiTheme.Muted},0,1);
        body.Controls.Add(new RichTextBox{Text=CombinedCredits(),Dock=DockStyle.Fill,ReadOnly=true,BorderStyle=BorderStyle.None,BackColor=UiTheme.Button,ForeColor=UiTheme.Text,Font=new("Segoe UI",9),ScrollBars=RichTextBoxScrollBars.Vertical,DetectUrls=false,Margin=new(0,4,0,4)},0,2);
        var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,Padding=new(0,7,0,0)};body.Controls.Add(footer,0,3);
        var close=new Button{Text=UiLanguage.T("Close"),DialogResult=DialogResult.OK,FlatStyle=FlatStyle.Flat,BackColor=UiTheme.Button,ForeColor=UiTheme.Text,Width=90,Height=28,Margin=Padding.Empty};footer.Controls.Add(close);
        var link=new LinkLabel{Text="GitHub",AutoSize=true,LinkColor=UiTheme.Header,Margin=new(0,6,14,0)};link.LinkClicked+=(_,_)=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/"+ReleaseUpdates.Repository){UseShellExecute=true});footer.Controls.Add(link);
        dialog.AcceptButton=dialog.CancelButton=close;dialog.ShowDialog(this);
    }
}
