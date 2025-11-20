using Sandbox.UI;
namespace VoxelParty.UI;

public partial class Option : Panel
{
    [Property, TextArea] public string Text { get; set; } = " ";
    /// <summary>
    /// the hash determines if the system should be rebuilt. If it changes, it will be rebuilt
    /// </summary>
    protected override int BuildHash() => System.HashCode.Combine( Text );

    public Option()
    {
        AcceptsFocus = true;
    }
}