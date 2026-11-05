using System.Drawing.Drawing2D;
using TLCPlus;
namespace TLCPlus.Desktop;
public partial class MainForm
{
    readonly ComboBox roadTypeChoice=new(){DropDownStyle=ComboBoxStyle.DropDownList,DisplayMember="Name",Height=25,Margin=new(3,4,3,4)};
    readonly Label roadTypeHint=new(){AutoSize=true,Margin=new(3,2,3,2)};
    int roadTypeScenery=-1;bool flipRoadType;
    void BuildRoadTypeControls(FlowLayoutPanel page)
    {
        roadTypeChoice.FormattingEnabled=true;roadTypeChoice.Format+=(_,e)=>{if(e.ListItem is RoadType type)e.Value=UiLanguage.T(type.Name);};
        Section(page,"ROAD TYPES");page.Controls.Add(roadTypeChoice);page.Controls.Add(roadTypeHint);
        roadTypeChoice.SelectedIndexChanged+=(_,_)=>RefreshRoadTypeHint();
        Toggle(page,"Flip road orientation",()=>flipRoadType,value=>flipRoadType=value,false);
        ActionButton(page,"Place road type",()=>ClassicTool("road"));
        ActionButton(page,"Remove road type",()=>ClassicTool("road-erase"));
        ActionButton(page,"Clear road types",()=>{if(Ted!=null)return;Project.RoadPlacements.Clear();Commit();});
        TextRow(page,"Click the road to place a complete section. Right-click to remove it. Start/finish sections stay protected.");
        Canvas.RoadClicked+=(distance,erase)=>Try(()=>ChangeRoadTypeAt(distance,erase));
    }
    void RefreshRoadTypeControls()
    {
        if(roadTypeScenery==Project.Settings.Scenery)return;roadTypeScenery=Project.Settings.Scenery;
        roadTypeChoice.Items.Clear();foreach(var type in RoadTypeCatalog.ForScenery(roadTypeScenery))roadTypeChoice.Items.Add(type);
        if(roadTypeChoice.Items.Count>0)roadTypeChoice.SelectedIndex=0;RefreshRoadTypeHint();
    }
    void RefreshRoadTypeHint()
    {
        if(roadTypeChoice.SelectedItem is RoadType type)roadTypeHint.Text=$"{type.TotalLength:0.0} m · {type.Segments.Count} section(s)\n"+UiLanguage.T("Complete road meshes; game appearance needs verification.");
        else roadTypeHint.Text=UiLanguage.T("No documented road types for this scenery.");
    }
    Course? BaseRoadPreview(bool planOnly)
    {
        if(Project.RoadPlacements.Count==0)return null;
        try{var basis=new Project{Settings=Project.Settings,Nodes=Project.Nodes};return planOnly?Exporter.BuildPlanPreview(basis):Exporter.Build(basis);}catch{return null;}
    }
    internal void ChangeRoadTypeAt(double distance,bool erase)
    {
        if(Ted!=null){
            if(erase){status.Text=UiLanguage.T("Use Undo to restore an edited TED road section.");return;}
            if(roadTypeChoice.SelectedItem is not RoadType tedType)return;
            if(tedType.Segments.Count!=1)throw new InvalidOperationException("Import the TED path as a layout to place a complete multi-part road chain.");
            double d=Exporter.To3d(Ted.Course.Banks,distance);int index=Ted.Course.Roads.FindIndex(r=>d>=r.Begin&&d<(r.End==0?Ted.Course.Length:r.End));if(index<0)return;
            var edited=TedDocument.Read(Ted.Write());edited.SetRoadType(index,tedType.Segments[0].Uuid,flipRoadType?1:0);Ted=edited;Commit();status.Text=tedType.Name+" — "+UiLanguage.T("TED road section updated");return;
        }
        if(erase){int index=Project.RoadPlacements.FindLastIndex(p=>p.Scenery==Project.Settings.Scenery&&distance>=p.Start&&distance<=p.Start+(RoadTypeCatalog.ForScenery(p.Scenery).FirstOrDefault(t=>t.Id==p.TypeId)?.TotalLength??0));if(index>=0){Project.RoadPlacements.RemoveAt(index);Commit();}return;}
        if(roadTypeChoice.SelectedItem is not RoadType type||Preview==null)return;
        var placement=new RoadPlacement{TypeId=type.Id,Scenery=Project.Settings.Scenery,Start=distance-type.TotalLength/2,Flag=flipRoadType?1:0};
        Project.RoadPlacements.Add(placement);
        try{_ = Exporter.Build(Project);}catch{Project.RoadPlacements.Remove(placement);throw;}
        Commit();status.Text=type.Name+" — "+placement.Start.ToString("0.0")+" m";
    }
}
public partial class TrackCanvas
{
    public event Action<double,bool>? RoadClicked;
    void DrawRoadPlacements(Graphics graphics,Course course)
    {
        if(Project.RoadPlacements.Count==0)return;var state=graphics.Save();graphics.SetClip(new Rectangle(22,22,Math.Max(1,Width-22),Math.Max(1,Height-22)),CombineMode.Intersect);
        using var pen=new Pen(Color.FromArgb(210,255,159,10),5);using var labelFont=new Font("Segoe UI",8);using var text=new SolidBrush(UiTheme.CanvasText);using var background=new SolidBrush(UiTheme.Canvas);
        foreach(var item in Project.RoadPlacements){if(item.Scenery!=Project.Settings.Scenery)continue;var type=RoadTypeCatalog.ForScenery(item.Scenery).FirstOrDefault(t=>t.Id==item.TypeId);if(type==null)continue;double end=item.Start+type.TotalLength;if(item.Start<0||end>course.PlanLength)continue;var points=new List<PointF>();for(double d=item.Start;d<end;d+=Math.Max(2,4/Zoom))points.Add(Screen(Geometry.At(course.Points,d).Point));points.Add(Screen(Geometry.At(course.Points,end).Point));if(points.Count>1)graphics.DrawLines(pen,points.ToArray());
            var middle=Screen(Geometry.At(course.Points,item.Start+type.TotalLength/2).Point);string label=UiLanguage.T(type.Name);var size=graphics.MeasureString(label,labelFont);graphics.FillRectangle(background,middle.X-size.Width/2-3,middle.Y-27,size.Width+6,size.Height);graphics.DrawString(label,labelFont,text,middle.X-size.Width/2,middle.Y-27);
        }
        graphics.Restore(state);
    }
}
