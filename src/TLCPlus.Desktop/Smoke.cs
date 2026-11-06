using TLCPlus;
using System.Text.Json;
namespace TLCPlus.Desktop;
static class Smoke
{
    public static int Run(string root)
    {
        Directory.CreateDirectory(Path.Combine(root,"analysis"));var checks=new List<string>();
        var testPreferences=AppPreferences.Load();string originalLanguage=testPreferences.Language;testPreferences.Language="en";testPreferences.Save();
        try{
            using var form=new MainForm();form.Show();Application.DoEvents();
            var sidebar=form.Controls.OfType<SplitContainer>().First().Panel1;
            var tabstrip=sidebar.Controls.OfType<FlatTabs>().First();var host=sidebar.Controls.OfType<Panel>().First();
            if(host.Top<tabstrip.Bottom)throw new Exception("Sidebar overlaps its tabs");
            var menu=form.MainMenuStrip!;var edit=menu.Items.OfType<ToolStripMenuItem>().First(i=>i.Text=="Edit");
            if(edit.DropDownItems.Count!=2)throw new Exception("Edit menu differs from Python");
            checks.Add("Python sidebar layout and two-item Edit menu");
            if(tabstrip.Labels.Any(l=>l.Contains("&&")))throw new Exception("Escaped ampersand is visible in tab text");
            var mapPage=host.Controls.OfType<FlowLayoutPanel>().First(p=>p.Controls.Cast<Control>().Any(c=>c.Text=="DISPLAY TOGGLES"));
            if(mapPage.Controls.OfType<Button>().Any(b=>b.Text is "Fit Track" or "Isometric 3D View"))throw new Exception("Redundant map commands remain");
            if(menu.Items.OfType<ToolStripMenuItem>().SelectMany(i=>i.DropDownItems.Cast<ToolStripItem>()).Any(i=>i.Text?.Contains("Complete Project")==true))throw new Exception("Separate project-save command remains");
            checks.Add("Removed extra actions and corrected tab label");
            form.Generate("gp");Application.DoEvents();if(form.Preview==null)throw new Exception("Generator failed");checks.Add("Grand Prix generator and native rendering");
            RevisionChecks.Run(form,root,checks);
            ManualHeightChecks.Run(form,root,checks);bool priorProperties=form.Canvas.ShowNodeProperties;form.Canvas.ShowNodeProperties=false;
            var nodeLabels=mapPage.Controls.OfType<CheckBox>().Single(c=>c.Text.Trim()=="Node Number Labels");bool priorNumbers=nodeLabels.Checked;
            nodeLabels.Checked=true;using(var shown=form.Canvas.Render()){
                nodeLabels.Checked=false;if(form.Canvas.ShowNumbers||AppPreferences.Load().ShowNodeNumbers)throw new Exception("Node label toggle did not disable and save");
                using var hidden=form.Canvas.Render();var point=Point.Round(form.Canvas.Screen(form.Project.Nodes[0].XY));bool changed=false;
                for(int py=Math.Max(0,point.Y-18);py<Math.Min(shown.Height,point.Y+5);py++)for(int px=Math.Max(0,point.X+5);px<Math.Min(shown.Width,point.X+35);px++)if(shown.GetPixel(px,py)!=hidden.GetPixel(px,py))changed=true;
                if(!changed)throw new Exception("Node numbers remain visible after disabling their toggle");
            }
            nodeLabels.Checked=true;if(!form.Canvas.ShowNumbers||!AppPreferences.Load().ShowNodeNumbers)throw new Exception("Node label toggle did not restore and save");nodeLabels.Checked=priorNumbers;
            checks.Add("Node number switch hides/restores rendered labels and persists its setting");
            form.Canvas.ShowNodeProperties=priorProperties;
            ReleaseReviewChecks.Run(form,root,checks);
            ErrorNoticeChecks.Run(form,root,checks);
            RoadToolChecks.Run(form,root,checks);
            int before=form.Project.Nodes.Count;form.Canvas.Selected=[1];form.Delete();if(form.Project.Nodes.Count!=before-1)throw new Exception("Delete failed");form.Undo();if(form.Project.Nodes.Count!=before)throw new Exception("Undo failed");form.Redo();if(form.Project.Nodes.Count!=before-1)throw new Exception("Redo failed");form.Undo();checks.Add("Node deletion, undo, redo");
            double x=form.Project.Nodes[0].X;form.Canvas.Selected.Clear();form.Transform("rotate",15);form.Undo();if(form.Project.Nodes[0].X!=x)throw new Exception("Transform undo failed");checks.Add("Selection and transforms");
            string project=Path.Combine(root,"work/smoke.tlcp.json");Directory.CreateDirectory(Path.GetDirectoryName(project)!);form.Project.Save(project);var loaded=Project.Load(project);if(loaded.Nodes.Count!=before)throw new Exception("Project roundtrip failed");checks.Add("Layout state serialization");
            var trk=TrkFormat.Write(form.Project);var course=Exporter.Build(TrkFormat.Read(trk));var ted=Exporter.Write(form.Project,course,timestamp:0xE245A123);var parsed=TedDocument.Read(ted);if(!parsed.Write().AsSpan().SequenceEqual(ted))throw new Exception("TED preservation failed");checks.Add("Native TRK5 and TED export/import");
            var timed=Exporter.Write(form.Project,course);if((Binary.U32(timed,32)>>26)!=(uint)(DateTime.Now.Year-1970))throw new Exception("Current timestamp export failed");checks.Add("Normal TED export with current unsigned timestamp");
            File.WriteAllBytes(Path.Combine(root,"work/smoke.ted"),ted);form.OpenFile(Path.Combine(root,"work/smoke.ted"));if(form.Ted==null)throw new Exception("TED UI load failed");checks.Add("TED document preservation load");
            form.OpenFile(project);form.Canvas.Fit();Application.DoEvents();using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(root,"analysis/desktop-preview.png"));}
            object Tree(Control control,int depth)=>new{type=control.GetType().Name,text=control.Text,bounds=control.Bounds.ToString(),visible=control.Visible,children=depth<4?control.Controls.Cast<Control>().Select(c=>Tree(c,depth+1)).ToArray():[]};
            File.WriteAllText(Path.Combine(root,"work/ui-layout.json"),JsonSerializer.Serialize(Tree(form,0),new JsonSerializerOptions{WriteIndented=true}));
            foreach(string style in new[]{"tech","oval","rally","chaos"}){if(style=="rally")form.Project.Settings.Circuit=false;form.Generate(style);if(form.Preview==null)throw new Exception(style+" failed");}checks.Add("All five procedural generators");
            using(var terrain=new Bitmap(1200,700)){using(var graphics=Graphics.FromImage(terrain))TerrainRenderer.Draw(graphics,Terrain.Get(2),2,.03,0,0,1200,700,3);if(terrain.GetPixel(50,350).A!=0)throw new Exception("Terrain extends outside physical map bounds");terrain.Save(Path.Combine(root,"analysis/terrain-bounds-check.png"));}checks.Add("Terrain contours clipped to physical map bounds");
            int artwork=0;foreach(int scene in new[]{1,2,3,4,5})foreach(var item in DecorationArtwork.Entries(scene)){using var icon=DecorationArtwork.Palette(scene,item.Type,item.TwoSided);if(icon.Width!=44)throw new Exception("Palette icon size differs");artwork++;}checks.Add($"{artwork} original decoration palette entries rendered");
            PerformanceChecks.Run(form,root);checks.Add("240-event node drag: no full export during movement; complete preview on release");
            form.Project.Decorations.Clear();var place=typeof(MainForm).GetMethod("PlaceDecoration",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
            for(int i=0;i<2001;i++)place.Invoke(form,[1000.0,false,null]);
            if(form.Project.Decorations.Count!=2000)throw new Exception("Decoration placement does not stop at 2,000");
            var populated=Exporter.Build(form.Project);var populatedTed=Exporter.Write(form.Project,populated,timestamp:0xE245A123);
            if(TedDocument.Read(populatedTed).Course.Decorations.Count<2000)throw new Exception("2,000 decorations were not exported");
            string densePath=Path.Combine(root,"work/2000-decorations.trk5");DocumentFiles.SaveLayout(densePath,form.Project);
            if(DocumentFiles.LoadLayout(densePath).Decorations.Count!=2000)throw new Exception("2,000 decorations were not retained on save");
            form.Project.Decorations.Clear();checks.Add("2,000 decoration placement, cap, TED export and saved-layout retention");
            using(var nodeDialog=new NodePropertiesDialog(new Node{X=20,Y=30,Z=4,Radius=80,Camber=-1,Segments=3},2)){var a=new Node{X=20,Y=30,Z=4,Radius=80,Camber=-1,Segments=3};var b=new Node{X=60,Y=70,Z=8,Radius=120,Camber=3,Segments=1};nodeDialog.ApplyTo([a,b]);if(a.X!=20||b.X!=60||b.Radius!=80||b.Camber!=-1||b.Z!=8)throw new Exception("Node multi-selection behavior differs");}checks.Add("Original node property multi-selection semantics");
            form.Generate("gp");form.Project.Settings.Scenery=2;form.Project.Settings.ManualHeight=true;form.Project.Settings.PitOffset=180;for(int i=0;i<form.Project.Nodes.Count;i++)form.Project.Nodes[i].Z=Math.Sin(i*.7)*35;
            var variants=DecorationArtwork.Variants(2,1);for(int i=0;i<8&&variants.Count>0;i++)form.Project.Decorations.Add(new(){Type=1,Uuid=variants[0].GetProperty("uuid").GetUInt64(),Vpos=120+i*120,Side=i%2==0?"left":"right",Track="AB"});form.Commit();form.Canvas.Fit();Application.DoEvents();
            string layoutPath=Path.Combine(root,"work/metadata-save.trk5");DocumentFiles.SaveLayout(layoutPath,form.Project);var saved=File.ReadAllBytes(layoutPath);if(!saved.AsSpan().SequenceEqual(TrkFormat.Write(form.Project)))throw new Exception("Layout metadata altered TRK5 bytes");var layout=DocumentFiles.LoadLayout(layoutPath);if(layout.Decorations.Count!=form.Project.Decorations.Count||!layout.Settings.ManualHeight)throw new Exception("TRK5 metadata did not reopen");checks.Add("Standard TRK5 bytes with automatic decoration and height metadata retention");
            var camera=(form.Canvas.Zoom,form.Canvas.PanX,form.Canvas.PanY);using(var iso=new IsometricForm(form.Project,form.Preview!)){using var image=iso.RenderPreview();image.Save(Path.Combine(root,"analysis/finished-isometric.png"));}if(camera!=(form.Canvas.Zoom,form.Canvas.PanX,form.Canvas.PanY))throw new Exception("Isometric preview changed 2D camera");checks.Add("Independent Python-style isometric preview leaves 2D placement unchanged");
            using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(root,"analysis/finished-terrain.png"));}
            typeof(MainForm).GetMethod("ShowPage",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(form,[3]);Application.DoEvents();using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(root,"analysis/finished-decorations.png"));}
            using(var timer=new System.Windows.Forms.Timer{Interval=100}){timer.Tick+=(_,_)=>{var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Text=="Preferences");if(dialog==null)return;timer.Stop();using(var image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(root,"analysis/finished-preferences.png"));}dialog.Close();};timer.Start();typeof(MainForm).GetMethod("Preferences",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(form,null);}checks.Add("Restored preferences dialog opens with original customization controls");
            using(var image=new Bitmap(350,250)){using(var g=Graphics.FromImage(image)){g.Clear(Color.White);using var pen=new Pen(Color.Black,10);g.DrawEllipse(pen,40,40,260,160);}foreach(string mode in new[]{"centerline","outline","smart-outline"}){var traced=ImageTracing.Trace(image,2000,mode);if(traced.Count<5)throw new Exception(mode+" image tracing failed");}checks.Add("Centerline, Outline and Smart Fill image tracing");}
            File.WriteAllText(Path.Combine(root,"analysis/desktop-smoke-results.json"),JsonSerializer.Serialize(new{passed=true,checks},new JsonSerializerOptions{WriteIndented=true}));return 0;
        }catch(Exception e){File.WriteAllText(Path.Combine(root,"analysis/desktop-smoke-results.json"),JsonSerializer.Serialize(new{passed=false,checks,error=e.ToString()},new JsonSerializerOptions{WriteIndented=true}));return 1;}
        finally{var preferences=AppPreferences.Load();preferences.Language=originalLanguage;preferences.Save();UiLanguage.Use(originalLanguage);}
    }
}
