using System.Net.Http;
using System.Text.Json;
namespace TLCPlus.Desktop;
public partial class MainForm
{
    readonly AppPreferences uiPreferences=AppPreferences.Load();
    bool preferencesInitialized,checkingUpdates;
    readonly List<Form> preferenceWindows=[];
    void SwitchScene(int scenery)
    {
        if(Project.Settings.Scenery==scenery)return;
        if(Project.Decorations.Count>0||Project.RoadPlacements.Count>0){if(MessageBox.Show("Changing scenery clears placed decorations and road types because each scenery uses different models. Continue?","Change scenery",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;Project.Decorations.Clear();Project.RoadPlacements.Clear();}
        Project.Settings.Scenery=scenery;Commit();BuildPalette();
    }
    void InitializePreferences()
    {
        if(preferencesInitialized)return;preferencesInitialized=true;
        Canvas.DraftMode=uiPreferences.PerformanceDraft;Canvas.ShowNumbers=uiPreferences.ShowNodeNumbers;Canvas.ShowNodeProperties=uiPreferences.ShowNodeProperties;if(verticalSplit!=null)verticalSplit.Panel2Collapsed=!uiPreferences.ShowElevation;ApplyPreferences();RefreshPythonControls();
        FormClosing+=(_,_)=>{uiPreferences.PerformanceDraft=Canvas.DraftMode;uiPreferences.ShowElevation=verticalSplit!=null&&!verticalSplit.Panel2Collapsed;SavePreferences();};
        Shown+=(_,_)=>{if(uiPreferences.RemindUpdates){var timer=new System.Windows.Forms.Timer{Interval=5000};timer.Tick+=async(_,_)=>{timer.Stop();timer.Dispose();if(!IsDisposed)await CheckForUpdatesAsync(false);};timer.Start();}};
    }
    void SavePreferences(){try{uiPreferences.Save();}catch(IOException e){status.Text="Could not save preferences: "+e.Message;}catch(UnauthorizedAccessException e){status.Text="Could not save preferences: "+e.Message;}}
    void ApplyPreferences()
    {
        UiTheme.Apply(uiPreferences);
        Bg=UiTheme.Panel;TextColor=UiTheme.Text;Accent=UiTheme.Header;
        void Walk(Control control)
        {
            bool chrome=control==MainMenuStrip;for(Control? ancestor=control;ancestor!=null;ancestor=ancestor.Parent)if(ancestor is Panel{Dock:DockStyle.Top,Height:34}){chrome=true;break;}
            if(control is SplitContainer splitter)splitter.BackColor=UiTheme.Muted;
            else if(control==Canvas)control.BackColor=UiTheme.Canvas;
            else if(control==elevation)control.BackColor=UiTheme.NavBackground;
            else if(control is Button button){bool icon=Equals(button.Tag,"icon-button");button.BackColor=UiLanguage.OriginalText(button)=="Sidebar"?UiTheme.Accent:chrome?UiTheme.Chrome:icon?UiTheme.Panel:UiTheme.Button;button.ForeColor=chrome?UiTheme.Contrast(UiTheme.Chrome):UiTheme.Text;button.FlatAppearance.MouseOverBackColor=icon?UiTheme.Button:UiTheme.Accent;button.FlatAppearance.MouseDownBackColor=UiTheme.Button;}
            else{control.BackColor=chrome?UiTheme.Chrome:UiTheme.Panel;control.ForeColor=chrome?UiTheme.Contrast(UiTheme.Chrome):control is Label label&&label.Font.Bold?UiTheme.Header:UiTheme.Text;}
            if(control is Label brand&&(brand.Text=="TLC+"||brand.Text=="TLC+ C# 1.0.0")){brand.Visible=uiPreferences.ShowTitle;brand.ForeColor=UiTheme.Header;brand.BackColor=UiTheme.Chrome;}
            if(control is ToolStrip strip){strip.BackColor=UiTheme.Chrome;strip.ForeColor=UiTheme.Text;strip.Renderer=new PreferencesMenuRenderer();foreach(ToolStripItem item in strip.Items)ThemeMenu(item);}
            foreach(Control child in control.Controls)Walk(child);
        }
        void ThemeMenu(ToolStripItem item){item.ForeColor=UiTheme.Text;item.BackColor=item.Owner is MenuStrip?UiTheme.Chrome:UiTheme.Panel;if(item is ToolStripDropDownItem drop)foreach(ToolStripItem child in drop.DropDownItems)ThemeMenu(child);}
        SuspendLayout();Walk(this);ApplyModuleVisibility();ReloadToolIcons();RefreshToolButtons();foreach(var window in preferenceWindows.Where(w=>!w.IsDisposed))ThemePreferenceWindow(window);ResumeLayout(true);Canvas.Invalidate();elevation.Invalidate();classicTabs.Invalidate();
    }
    void ApplyModuleVisibility()
    {
        if(classicPages.Length<3||classicPages[2]==null)return;bool visible=true;foreach(Control control in classicPages[2].Controls){if(control is Label){if(UiLanguage.OriginalText(control)=="PROCEDURAL TRACK BUILDER")visible=uiPreferences.ShowProcedural;else if(UiLanguage.OriginalText(control)=="TRACK TRACER")visible=uiPreferences.ShowTracer;else if(UiLanguage.OriginalText(control)=="EXTERNAL PATH IMPORTS")visible=uiPreferences.ShowImports;}control.Visible=visible;}ResizeRows(classicPages[2]);
    }
    void ReloadToolIcons()
    {
        string folder=Path.Combine(TLCPlus.Terrain.AssetsPath,"icons",UiTheme.Classic?"tools_light":"tools");var names=new Dictionary<string,string>{{"Pen tool","pen"},{"Selection tool","selection"},{"Camber tool","camber"},{"Panning tool","pan"},{"Decoration tool","decorate"},{"Zoom in","zoom_in"},{"Zoom out","zoom_out"},{"Reverse direction","reverse"},{"Save screenshot","screenshot"},{"Rotate Tool","rotate"},{"Scale Tool","scale"},{"Randomize Selected Nodes","random"}};
        void Walk(Control control){if(control is Button button){if(button.AccessibleName=="Road type tool"){var previous=button.Image;button.Image=RoadToolIcon();previous?.Dispose();}else if(names.TryGetValue(button.AccessibleName??"",out string? name)){string path=Path.Combine(folder,name+".gif");if(File.Exists(path)){using var source=Image.FromFile(path);var previous=button.Image;button.Image=new Bitmap(source);previous?.Dispose();}}}foreach(Control child in control.Controls)Walk(child);}Walk(this);
    }
    void ThemePreferenceWindow(Form window)
    {
        void Walk(Control control){if(!Equals(control.Tag,"swatch")){control.BackColor=UiTheme.Chrome;control.ForeColor=control is Label label&&label.Font.Bold?UiTheme.Header:UiTheme.Text;}if(control is Button button&&!Equals(control.Tag,"swatch")){button.BackColor=UiTheme.Button;button.FlatAppearance.MouseOverBackColor=UiTheme.Accent;}foreach(Control child in control.Controls)Walk(child);}Walk(window);window.Invalidate(true);
    }
    void Preferences()
    {
        InitializePreferences();var existing=preferenceWindows.FirstOrDefault(w=>!w.IsDisposed);if(existing!=null){existing.Activate();return;}
        using var dialog=new Form{Text="Preferences",ClientSize=new(420,570),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,ShowInTaskbar=false,AutoScaleMode=AutoScaleMode.Dpi,Font=new("Segoe UI",9)};preferenceWindows.Add(dialog);
        var body=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new(28,12,28,10)};dialog.Controls.Add(body);
        void Header(string title){body.Controls.Add(new Label{Text=title,Width=350,Height=25,Font=new("Segoe UI",9,FontStyle.Bold),Margin=new(0,2,0,5),TextAlign=ContentAlignment.BottomLeft});}
        void Separator(){body.Controls.Add(new Panel{Width=350,Height=1,BackColor=UiTheme.Border,Margin=new(0,10,0,5),Tag="swatch"});}
        void Toggle(string text,bool value,Action<bool> update,int top=1){var checkbox=new CheckBox{Text=" "+text,Width=350,Height=24,FlatStyle=FlatStyle.Flat,Checked=value,Margin=new(0,top,0,1)};checkbox.CheckedChanged+=(_,_)=>{update(checkbox.Checked);SavePreferences();};body.Controls.Add(checkbox);}
        Header("Sidebar Module Manager");
        Toggle("Show Procedural Track Builder",uiPreferences.ShowProcedural,v=>{uiPreferences.ShowProcedural=v;ApplyModuleVisibility();});Toggle("Show Track Tracer",uiPreferences.ShowTracer,v=>{uiPreferences.ShowTracer=v;ApplyModuleVisibility();});Toggle("Show External Path Imports",uiPreferences.ShowImports,v=>{uiPreferences.ShowImports=v;ApplyModuleVisibility();});Toggle("Allow Circuit on Rally Stages (Exp.)",uiPreferences.AllowRallyCircuit,v=>uiPreferences.AllowRallyCircuit=v,8);
        Separator();Header("UI THEME (W.I.P)");Toggle("Light Mode (W.I.P) — classic TLC look",UiTheme.Classic,v=>{uiPreferences.Theme=v?"Classic":"Dark";ApplyPreferences();});Separator();
        Header("ERROR NOTICES");
        body.Controls.Add(new Label{Text="Errors appear above the canvas and fade away automatically.",Width=350,Height=34,Margin=new(0,0,0,4),Font=new("Segoe UI",8)});
        var noticeRow=new Panel{Width=350,Height=32,Margin=new(0,0,0,6)};
        noticeRow.Controls.Add(new Label{Text="Display duration (seconds)",Dock=DockStyle.Left,Width=245,TextAlign=ContentAlignment.MiddleLeft});
        var noticeDuration=new NumericUpDown{Dock=DockStyle.Right,Width=90,Minimum=.5m,Maximum=5m,Increment=.5m,DecimalPlaces=1,Value=(decimal)AppPreferences.NoticeDuration(uiPreferences.ErrorNoticeSeconds),AccessibleName=UiLanguage.T("Display duration (seconds)")};
        noticeDuration.ValueChanged+=(_,_)=>{uiPreferences.ErrorNoticeSeconds=(double)noticeDuration.Value;SavePreferences();};noticeRow.Controls.Add(noticeDuration);body.Controls.Add(noticeRow);Separator();
        Toggle("Remind me about updates on startup",uiPreferences.RemindUpdates,v=>uiPreferences.RemindUpdates=v);
        body.Controls.Add(new Label{Text="TLC+ checks GitHub at launch and reminds you once if a newer version is available.",Width=328,Height=34,Margin=new(20,0,0,0),Font=new("Segoe UI",8)});
        var details=new LinkLabel{Text="Details",Width=328,Height=19,Margin=new(20,0,0,2),LinkColor=UiTheme.Header,Font=new("Segoe UI",8)};var detailBody=new Label{Text="When ON, TLC+ checks GitHub a few seconds after launch and shows a reminder if a newer version is available. When OFF, you must use the Update menu entry manually. No popup is shown when you are already up to date.",Width=328,Height=49,Margin=new(20,0,0,0),Font=new("Segoe UI",8),Visible=false};details.LinkClicked+=(_,_)=>{detailBody.Visible=!detailBody.Visible;details.Text=UiLanguage.T(detailBody.Visible?"Hide details":"Details");};body.Controls.Add(details);body.Controls.Add(detailBody);
        Toggle("Show TLC+ title in top bar",uiPreferences.ShowTitle,v=>{uiPreferences.ShowTitle=v;ApplyPreferences();},7);Separator();Header("UI ACCENT COLOR");body.Controls.Add(new Label{Text="Pick the highlight color used for section headers, buttons, the active tab, sliders and the polygon outline.",Width=350,Height=35,Margin=new(0,0,0,4),Font=new("Segoe UI",8)});
        var swatches=new FlowLayoutPanel{Width=350,Height=29,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,Margin=Padding.Empty};body.Controls.Add(swatches);var current=new Label{Width=350,Height=27,Font=new("Consolas",9),Margin=new(0,4,0,3)};void SelectAccent(Color color){uiPreferences.Accent=$"#{color.R:X2}{color.G:X2}{color.B:X2}";ApplyPreferences();current.Text=UiLanguage.T("Current:")+"  "+uiPreferences.Accent;SavePreferences();}
        foreach(string hex in new[]{"#0A84FF","#64D2FF","#30D158","#FFD60A","#FF9F0A","#FF453A","#FF8080","#BF5AF2"}){var swatch=new Button{Width=25,Height=24,BackColor=ColorTranslator.FromHtml(hex),FlatStyle=FlatStyle.Flat,Tag="swatch",Margin=new(1,1,1,1),AccessibleName=hex};swatch.FlatAppearance.BorderSize=1;swatch.FlatAppearance.BorderColor=UiTheme.Text;swatch.Click+=(_,_)=>SelectAccent(ColorTranslator.FromHtml(hex));swatches.Controls.Add(swatch);}
        var custom=new Button{Text="Custom…",Width=83,Height=25,FlatStyle=FlatStyle.Flat,Margin=new(6,0,0,0)};custom.Click+=(_,_)=>{using var picker=new ColorDialog{Color=UiTheme.Accent,FullOpen=true};if(picker.ShowDialog(dialog)==DialogResult.OK)SelectAccent(picker.Color);};swatches.Controls.Add(custom);current.Text=UiLanguage.T("Current:")+"  "+uiPreferences.Accent;body.Controls.Add(current);body.Controls.Add(new Label{Text="Looking for Track Settings?\nRoad width, Camber, Toggles, and Start/Pit alignments are located directly in the main Left Sidebar.",Width=350,Height=54,TextAlign=ContentAlignment.MiddleCenter,Margin=new(0,6,0,4),Font=new("Segoe UI",8,FontStyle.Italic)});
        ThemePreferenceWindow(dialog);UiLanguage.Apply(dialog);dialog.ShowDialog(this);preferenceWindows.Remove(dialog);SavePreferences();
    }
    async Task CheckForUpdatesAsync(bool manual)
    {
        if(checkingUpdates)return;checkingUpdates=true;
        try{var update=await ReleaseUpdates.Check();
            if(update.Newer&&(manual||update.Tag!=uiPreferences.LastNotifiedVersion)){
                uiPreferences.LastNotifiedVersion=update.Tag;SavePreferences();
                if(MessageBox.Show(this,string.Format(UiLanguage.T("Version {0} is available. Open its download page?"),update.Tag),UiLanguage.T("Check for Updates"),MessageBoxButtons.YesNo,MessageBoxIcon.Information)==DialogResult.Yes)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(update.Url!){UseShellExecute=true});
            }else if(manual)MessageBox.Show(this,UiLanguage.T(update.Message),UiLanguage.T("Check for Updates"),MessageBoxButtons.OK,MessageBoxIcon.Information);
        }catch(Exception e)when(e is HttpRequestException or TaskCanceledException or JsonException or IOException){if(manual)MessageBox.Show(this,UiLanguage.T("Could not check for updates.")+"\n"+e.Message,UiLanguage.T("Check for Updates"),MessageBoxButtons.OK,MessageBoxIcon.Error);}
        finally{checkingUpdates=false;}
    }
    sealed class PreferencesMenuRenderer:ToolStripProfessionalRenderer
    {
        public PreferencesMenuRenderer():base(new Colors()){}
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e){}
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){e.TextColor=UiTheme.Text;base.OnRenderItemText(e);}
        sealed class Colors:ProfessionalColorTable
        {
            public override Color MenuItemSelected=>UiTheme.Button;public override Color MenuItemBorder=>UiTheme.Button;public override Color MenuItemPressedGradientBegin=>UiTheme.Button;public override Color MenuItemPressedGradientEnd=>UiTheme.Button;public override Color ToolStripDropDownBackground=>UiTheme.Panel;public override Color MenuBorder=>UiTheme.Border;public override Color SeparatorDark=>UiTheme.Border;public override Color SeparatorLight=>UiTheme.Panel;public override Color ImageMarginGradientBegin=>UiTheme.Panel;public override Color ImageMarginGradientMiddle=>UiTheme.Panel;public override Color ImageMarginGradientEnd=>UiTheme.Panel;
        }
    }
}
