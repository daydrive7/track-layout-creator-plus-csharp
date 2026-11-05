using System.Text.Json;
using TLCPlus;
namespace TLCPlus.Desktop;
public sealed class DecorationDialog:Form
{
    public Decoration Result {get;private set;}=new();
    record Item(ulong Id,int Type,double Length){public override string ToString()=>$"{Names.GetValueOrDefault(Type,"Decoration")} · {Length:0.#} m · {Id}";}
    static readonly Dictionary<int,string> Names=new(){[0]="Grass",[1]="Tree",[2]="Tree group",[3]="Flowers",[4]="Cactus",[5]="Cactus group",[6]="Spectator curb",[7]="House / tent",[8]="Rock",[9]="Cliff",[10]="Power lines",[11]="Street lights",[12]="Circuit gate",[13]="Stands",[14]="Marshal post",[15]="Aurora vision",[16]="Banner",[18]="Rally gate",[19]="Audience",[20]="Billboard"};
    public DecorationDialog(int scenery)
    {
        Text="Place decoration";Width=690;Height=290;StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=MinimizeBox=false;
        var variants=new ComboBox{Left=140,Top=22,Width=515,DropDownStyle=ComboBoxStyle.DropDownList};foreach(var v in Catalog.Variants(scenery))variants.Items.Add(new Item(v.GetProperty("uuid").GetUInt64(),v.GetProperty("type").GetInt32(),v.GetProperty("len").GetDouble()));if(variants.Items.Count>0)variants.SelectedIndex=0;
        var side=new ComboBox{Left=140,Top=65,Width=120,DropDownStyle=ComboBoxStyle.DropDownList};side.Items.AddRange(["left","right"]);side.SelectedIndex=0;var track=new ComboBox{Left=400,Top=65,Width=100,DropDownStyle=ComboBoxStyle.DropDownList};track.Items.AddRange(["A","B","AB","CURB"]);track.SelectedIndex=2;
        var position=new NumericUpDown{Left=140,Top=108,Width=150,Maximum=10000000,DecimalPlaces=2,Value=150};var button=new Button{Text="Place",Left=550,Top=174,Width=100,Height=35};button.Click+=(_,_)=>{if(variants.SelectedItem is not Item v)return;Result=new(){Uuid=v.Id,Type=v.Type,Side=side.Text,Track=track.Text,Vpos=(double)position.Value};DialogResult=DialogResult.OK;};
        Controls.AddRange([new Label{Text="Rail unit",Left=20,Top=26,Width=110},variants,new Label{Text="Side",Left=20,Top=69},side,new Label{Text="Track",Left=330,Top=69,Width=60},track,new Label{Text="Distance (m)",Left=20,Top=112,Width=110},position,button]);AcceptButton=button;
    }
}
