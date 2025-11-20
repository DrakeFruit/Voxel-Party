using Sandbox.UI;

namespace VoxelParty.UI;

public partial class Dropdown : Panel
{
    [Property, TextArea] public string Text { get; set; } = " ";
    [Property] public bool Disabled { get; set; } = false;
    [Property] public System.Action<string> ValueChanged { get; set; } = null;
    public bool IsOpen { get; set; } = false;

    public OptionPanel OptionsPanel { get; set; } = null;

    public RenderFragment Options { get; set; }
    /// <summary>
    /// the hash determines if the system should be rebuilt. If it changes, it will be rebuilt
    /// </summary>
    protected override int BuildHash() => System.HashCode.Combine( Text );

    public Dropdown()
    {
        AcceptsFocus = true;
        this.BindClass( "disabled", () => Disabled );
    }

    public void OnOptionClick( Option option )
    {
        Text = option.Text;
        this.ValueChanged?.Invoke( Text );

    }

    public void HandleBlur()
    {
        if ( InputFocus.Current.ParentOfType<OptionPanel>() == this.OptionsPanel && InputFocus.Current is Option )
        {
            return;
        }
        this.Close();
    }

    public void Open()
    {
        if ( Disabled ) return;
        if ( this.IsOpen )
        {
            this.Close();
            return;
        }
        this.IsOpen = true;
        if ( this.OptionsPanel == null )
        {
            this.OptionsPanel = new OptionPanel();
            this.RootPanel().AddChild( this.OptionsPanel );
        }
        // Rip elements from .dropdownOptions and throw them into the OptionsPanel
        foreach ( var child in this.GetChild( 2 ).Children.ToList() )
        {
            this.OptionsPanel.AddChild( child );
        }
        this.OptionsPanel.Source = this;
        this.OptionsPanel.SetClass( "open", true );
        var rect = this.Box.Rect * this.ScaleFromScreen;
        this.OptionsPanel.Style.Top = rect.Bottom;
        this.OptionsPanel.Style.Left = rect.Left + 1;
        this.OptionsPanel.Style.Width = rect.Width - 4;
    }

    public void Close()
    {
        this.IsOpen = false;
        if ( this.OptionsPanel != null )
        {
            this.OptionsPanel.SetClass( "open", false );
            this.OptionsPanel.DeleteChildren();
        }
    }
}