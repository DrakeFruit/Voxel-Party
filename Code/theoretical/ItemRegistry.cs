using System;

public static class ItemRegistry
{

	public static bool FinishedLoading = false;
	public static Dictionary<string, Item> CachedIdentifierRegistry = new();
	public static void UpdateRegistry()
	{
		var newIdentifierRegistry = new Dictionary<string, Item>();
		foreach ( var item in ResourceLibrary.GetAll<Item>() )
		{
			if (string.IsNullOrWhiteSpace(item.Identifier))
			{
				Log.Warning($"Item {item.ResourcePath} does not have a valid Identifier. Please set a unique identifier for this item.");
            }
            else
            {
				if ( newIdentifierRegistry.ContainsKey( item.Identifier ) )
                {
					Log.Warning( $"Duplicate identifier registry entry: {item.Identifier} = {item.ResourcePath} vs {newIdentifierRegistry[item.Identifier].ResourcePath}" );
                }
                else
                {
					newIdentifierRegistry[item.Identifier] = item;
                }
            }
		}
		CachedIdentifierRegistry = newIdentifierRegistry;
	}

	public static Item GetItem( string name )
	{
		var item = ResourceLibrary.GetAll<Item>().FirstOrDefault( x => x.Name.Equals( name, System.StringComparison.CurrentCultureIgnoreCase ) );
		if ( item is not null && item.IsValid() )
			return item;
		return null;
	}

	public static Item GetItemByIdentifier( string identifier )
	{
		if ( string.IsNullOrWhiteSpace( identifier ) )
			identifier = "voxelparty:air";
		if ( CachedIdentifierRegistry.TryGetValue( identifier, out Item i ) )
			return i;
		return null;
	}

	public static Item GetItem( Vector3Int position )
	{
		var item = GetItemByIdentifier( World.Active.GetBlock( position ).BlockID );
		if ( item is not null && item.IsValid() )
			return item;
		return null;
	}
	
	public static Block GetBlock( string name )
	{
		var item = ResourceLibrary.GetAll<Item>().FirstOrDefault( x => x.Name.Equals( name, System.StringComparison.CurrentCultureIgnoreCase ) );
		if ( item != null && item.IsValid() )
			return item.Block;
		return null;
	}

	public static Block GetBlockByIdentifier( string identifier )
	{
		if ( string.IsNullOrWhiteSpace( identifier ) )
			identifier = "voxelparty:air";
		if ( CachedIdentifierRegistry.TryGetValue( identifier, out Item i ) )
			return i.Block;
		Log.Warning( $"Block with identifier {identifier} not found in registry." );
		return null;
	}
}

public class RegisterItemAttribute : System.Attribute
{
	public int ItemID { get; }

	public RegisterItemAttribute( int itemID )
	{
		ItemID = itemID;
	}
}
