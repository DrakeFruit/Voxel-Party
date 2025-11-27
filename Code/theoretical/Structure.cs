using System.Threading;
using System.Threading.Tasks;

namespace Sandbox.theoretical;

[GameResource( "Structure", "struct", "a structure that can be placed", Category = "Voxel Party", Icon = "archive" )]
public partial class Structure : GameResource
{
	public enum StructureDifficulty
	{
		Easy,
		Standard,
		Hard
	}
	[Hide]
	public string StructureDataByID { get; set; }

	[Property, ToggleGroup( "SpeedBuildStructure" )]
	public bool SpeedBuildStructure { get; set; } = false;

	[Property, ToggleGroup( "SpeedBuildStructure" )]
	public int SecondsToBuild { get; set; } = 60;

	[Property, ToggleGroup( "SpeedBuildStructure" )]
	public StructureDifficulty ReplicateDifficulty { get; set; } = StructureDifficulty.Standard;
}
