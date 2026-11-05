using System.Globalization;
using TLCPlus;

namespace TLCPlus.Desktop;

/// <summary>
/// Numeric node editor corresponding to v1.3.0 editPointProperties().
/// Coordinate changes apply as an offset to a selection; the curve settings
/// apply as absolute values, as they do in the Python application.
/// </summary>
public sealed class NodePropertiesDialog : Form
{
    readonly Node original;
    readonly TextBox x = Entry(), y = Entry(), z = Entry();
    readonly TextBox radius = Entry(), camber = Entry(), segments = Entry();
    readonly Label error = new();
    readonly ToolTip hints = new() { InitialDelay = 300, AutoPopDelay = 12000 };

    public Node Result { get; private set; }
    public Vec PositionOffset => new(Result.X - original.X, Result.Y - original.Y);
    public double HeightOffset => Result.Z - original.Z;

    public NodePropertiesDialog(Node node, int selectionCount = 1)
    {
        original = node.Copy();
        Result = node.Copy();
        Text = UiLanguage.T("Node Properties");
        ClientSize = new Size(390, 1);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = false;
        Font = new Font("Segoe UI", 9);
        BackColor = UiTheme.Panel; ForeColor = UiTheme.Text;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimumSize = new Size(406,0);
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = ShowInTaskbar = false;
        var layout=new TableLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Top,Padding=new(14),ColumnCount=1,Width=390,MinimumSize=new(390,0)};
        layout.SizeChanged+=(_,_)=>{if(ClientSize.Height!=layout.Height)ClientSize=new(ClientSize.Width,layout.Height);};
        layout.ColumnStyles.Add(new(SizeType.Percent,100));Controls.Add(layout);
        if(selectionCount>1)layout.Controls.Add(new Label{Text=string.Format(UiLanguage.T("{0} selected nodes; position changes are relative"),selectionCount),AutoSize=true,MaximumSize=new(362,0),Margin=new(0,0,0,10),ForeColor=UiTheme.Muted});
        var fields=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Top,ColumnCount=2,Margin=Padding.Empty,Width=362};
        fields.ColumnStyles.Add(new(SizeType.Absolute,110));fields.ColumnStyles.Add(new(SizeType.Percent,100));layout.Controls.Add(fields);
        AddRow(fields,"X (m):",x,node.X,0,"Horizontal coordinate.");
        AddRow(fields,"Y (m):",y,node.Y,1,"Plan-view vertical coordinate.");
        AddRow(fields,"Z (Height m):",z,node.Z,2,"Manual height offset above the terrain.");
        AddRow(fields,"Radius (m):",radius,node.Radius,3,"Corner radius.");
        AddRow(fields,"Camber (°):",camber,node.Camber,4,"-1 = global camber.");
        AddRow(fields,"Segments:",segments,node.Segments,5,"0 = automatic Euler; 1 = circular; 2+ = Euler subdivisions.");
        var explanation=new Label{Text=UiLanguage.T("Camber: −1 = global\nSegments: 0 = auto, 1 = circular, 2+ = Euler"),AutoSize=true,MaximumSize=new(362,0),Margin=new(0,7,0,10),ForeColor=UiTheme.Muted};layout.Controls.Add(explanation);
        error.AutoSize=true;error.MaximumSize=new(362,0);error.ForeColor=Color.FromArgb(255,128,128);error.Margin=new(0,0,0,8);error.Visible=false;layout.Controls.Add(error);
        var buttons=new FlowLayoutPanel{AutoSize=true,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,Dock=DockStyle.Top,Margin=Padding.Empty,Padding=Padding.Empty};
        var cancel=new Button{Text=UiLanguage.T("Cancel"),AutoSize=true,MinimumSize=new(88,30),FlatStyle=FlatStyle.Flat,BackColor=UiTheme.Button,ForeColor=UiTheme.Text,DialogResult=DialogResult.Cancel,Margin=Padding.Empty};
        var apply=new Button{Text=UiLanguage.T("Apply Changes"),AutoSize=true,MinimumSize=new(120,30),FlatStyle=FlatStyle.Flat,BackColor=UiTheme.Accent,ForeColor=Color.Black,Margin=new(0,0,8,0)};
        apply.FlatAppearance.BorderSize=cancel.FlatAppearance.BorderSize=0;apply.Click+=(_,_)=>ApplyChanges();buttons.Controls.AddRange([cancel,apply]);layout.Controls.Add(buttons);
        AcceptButton=apply;CancelButton=cancel;Shown+=(_,_)=>{layout.PerformLayout();ClientSize=new(ClientSize.Width,layout.Height);x.Focus();x.SelectAll();};
    }

    readonly Dictionary<TextBox,(string Text,double Value)> initialValues=[];
    static TextBox Entry()=>new(){BorderStyle=BorderStyle.FixedSingle,BackColor=Color.FromArgb(245,245,245),ForeColor=Color.FromArgb(20,20,20),TextAlign=HorizontalAlignment.Left,Font=new("Segoe UI",9)};
    void AddRow(TableLayoutPanel fields,string label,TextBox entry,double value,int row,string hint)
    {
        fields.RowStyles.Add(new(SizeType.Absolute,29));
        var caption=new Label{Text=UiLanguage.T(label),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Margin=new(0,0,8,0),AutoEllipsis=true,Font=Font};
        entry.Anchor=AnchorStyles.Left|AnchorStyles.Right;entry.Margin=new(0,3,0,3);entry.Font=Font;
        entry.Text=value.ToString("0.###",CultureInfo.CurrentCulture);initialValues[entry]=(entry.Text,value);
        entry.TabIndex=row;entry.Enter+=(_,_)=>entry.SelectAll();fields.Controls.Add(caption,0,row);fields.Controls.Add(entry,1,row);
        hints.SetToolTip(caption,UiLanguage.T(hint));hints.SetToolTip(entry,UiLanguage.T(hint));
    }

    static bool TryNumber(TextBox input, out double value)
    {
        string text = input.Text.Trim();
        if ((double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            && double.IsFinite(value)) return true;
        return false;
    }

    void ApplyChanges()
    {
        TextBox[] entries = [x, y, z, radius, camber];
        var numbers = new double[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            if(initialValues.TryGetValue(entries[i],out var initial)&&entries[i].Text==initial.Text){numbers[i]=initial.Value;continue;}
            if (TryNumber(entries[i], out numbers[i])) continue;
            error.Visible=true;error.Text = UiLanguage.T("Please enter a finite numeric value in each field.");
            entries[i].Focus();
            entries[i].SelectAll();
            return;
        }
        if (!int.TryParse(segments.Text.Trim(), NumberStyles.Integer,
                CultureInfo.CurrentCulture, out int subdivisionCount)
            || subdivisionCount < 0 || subdivisionCount > 10000)
        {
            error.Visible=true;error.Text = UiLanguage.T("Segments must be a whole number from 0 to 10,000.");
            segments.Focus();
            segments.SelectAll();
            return;
        }
        Result = new Node
        {
            X = numbers[0], Y = numbers[1], Z = numbers[2],
            Radius = numbers[3], Camber = numbers[4], Segments = subdivisionCount
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    /// <summary>Use after OK to reproduce the Python selection semantics.</summary>
    public void ApplyTo(IEnumerable<Node> nodes)
    {
        double dx = Result.X - original.X;
        double dy = Result.Y - original.Y;
        double dz = Result.Z - original.Z;
        foreach (var node in nodes)
        {
            node.X += dx;
            node.Y += dy;
            node.Z += dz;
            node.Radius = Result.Radius;
            node.Camber = Result.Camber;
            node.Segments = Result.Segments;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) hints.Dispose();
        base.Dispose(disposing);
    }
}
