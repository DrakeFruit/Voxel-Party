using System;
using System.Threading.Tasks;
using Sandbox.Network;
using Sandbox.UI;
namespace VoxelParty.UI;

public partial class ServerList : Panel
{
	List<LobbyInformation> Lobbies { get; set; } = new();
	bool Refreshing { get; set; } = false;
	DateTime LastRefreshTime { get; set; } = DateTime.MinValue;

	protected override void OnAfterTreeRender( bool firstTime )
	{
		base.OnAfterTreeRender( firstTime );

		if ( firstTime )
			_ = FetchLobbies();
	}
	async Task FetchLobbies()
	{
		Lobbies.Clear();
		Refreshing = true;
		LastRefreshTime = DateTime.Now;
		Lobbies = await Networking.QueryLobbies();
		Refreshing = false;
	}

	bool CanRefresh()
	{
		return !Refreshing && (DateTime.Now - LastRefreshTime).TotalSeconds >= 10;
	}
	int GetRemainingSeconds()
	{
		var elapsed = (DateTime.Now - LastRefreshTime).TotalSeconds;
		return Math.Max( 0, (int)(10 - elapsed) );
	}

	protected override int BuildHash() => HashCode.Combine( IsVisible, Lobbies, GetRemainingSeconds(), CanRefresh() );
}