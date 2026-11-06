using TLCPlus;
namespace TLCPlus.Desktop;
public partial class MainForm
{
    sealed record TedRoadChoice(string Name,ulong Uuid);
    void EditTedRoadSections()
    {
        using var open=new OpenFileDialog{Filter="GT6 TED|*.ted",Title="Edit TED road sections"};if(open.ShowDialog(this)!=DialogResult.OK)return;
        var document=TedDocument.Read(File.ReadAllBytes(open.FileName));
        using var dialog=new Form{Text=UiLanguage.T("TED road sections"),ClientSize=new(820,490),StartPosition=FormStartPosition.CenterParent,Font=new("Segoe UI",9),BackColor=UiTheme.Panel,ForeColor=UiTheme.Text};
        var info=new Label{Dock=DockStyle.Top,Height=62,Padding=new(12),Text=UiLanguage.T("Changes keep the selected section lengths. Use Decorations → Road Types for complete road chains. Start/finish sections stay protected. Game appearance still needs in-game verification.")};
        var choices=new ComboBox{DisplayMember="Name",DropDownStyle=ComboBoxStyle.DropDownList,Width=225};
        choices.Items.Add(new TedRoadChoice(UiLanguage.T("Normal road"),Catalog.Roads(document.Settings.Scenery).GetProperty("normal")[0][0].GetUInt64()));
        foreach(var type in RoadTypeCatalog.ForScenery(document.Settings.Scenery).Where(t=>t.Segments.Count==1))choices.Items.Add(new TedRoadChoice(type.Name,type.Segments[0].Uuid));choices.SelectedIndex=0;
        var rows=new ListView{Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,MultiSelect=true,HideSelection=false,BackColor=UiTheme.Panel,ForeColor=UiTheme.Text};
        foreach(var (title,width) in new[]{("Section",65),("Road type",220),("Start (m)",105),("End (m)",105),("Length (m)",105),("Flag",60)})rows.Columns.Add(title,width);
        void Fill(){rows.Items.Clear();for(int i=0;i<document.Course.Roads.Count;i++){var road=document.Course.Roads[i];double end=road.End==0?document.Course.Length:road.End;string name=choices.Items.Cast<TedRoadChoice>().FirstOrDefault(c=>c.Uuid==road.Uuid)?.Name??road.Uuid.ToString("X16");rows.Items.Add(new ListViewItem(new[]{i.ToString(),name,road.Begin.ToString("0.0"),end.ToString("0.0"),(end-road.Begin).ToString("0.0"),road.Flag.ToString()}));}}
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=44,Padding=new(6)};actions.Controls.Add(choices);
        void Button(string label,Action action){var button=new Button{Text=label,AutoSize=true,Height=30,FlatStyle=FlatStyle.Flat,BackColor=UiTheme.Button,ForeColor=UiTheme.Text};button.Click+=(_,_)=>Try(action);actions.Controls.Add(button);}
        void Change(){var selected=rows.SelectedIndices.Cast<int>().ToArray();if(selected.Length==0||choices.SelectedItem is not TedRoadChoice choice)return;var copy=TedDocument.Read(document.Write());foreach(int index in selected)copy.SetRoadType(index,choice.Uuid);document=copy;Fill();}
        Button(UiLanguage.T("Apply road type"),Change);Button(UiLanguage.T("Save TED copy…"),()=>{using var save=new SaveFileDialog{Filter="GT6 TED|*.ted",FileName=Path.GetFileNameWithoutExtension(open.FileName)+"-roads.ted"};if(save.ShowDialog(dialog)==DialogResult.OK)File.WriteAllBytes(save.FileName,document.Write());});
        dialog.Controls.Add(rows);dialog.Controls.Add(info);dialog.Controls.Add(actions);Fill();dialog.ShowDialog(this);
    }
}
