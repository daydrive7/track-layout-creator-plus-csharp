namespace TLCPlus.Desktop;
static class UiThemeDialogs
{
    public static void Apply(Control root)
    {
        root.BackColor=root is Button or NumericUpDown or TextBox?UiTheme.Button:UiTheme.Panel;root.ForeColor=UiTheme.Text;root.Font=new("Segoe UI",9);
        if(root is Button button){button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderColor=UiTheme.Border;button.FlatAppearance.MouseOverBackColor=UiTheme.Border;}
        foreach(Control child in root.Controls)Apply(child);
    }
}
