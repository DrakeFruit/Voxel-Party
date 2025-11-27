// Defines a stack of items as well as their data
using System;
using System.IO;
using Sandbox;

public class ItemStack
{
    public static readonly ItemStack Empty = new ItemStack { ItemIdentifier = "voxelparty:air", Count = 0 };

    public Item Item { get; set; } // The ID of the item in the stack, e.g. 0x01 for Stone.
    public int Count { get; set; } // The number of items in the stack

    [Hide]
    public string ItemIdentifier
    {
        get
        {
            return Item?.Identifier ?? "voxelparty:air"; // Return the item ID if the item is not null, otherwise return 0
        }
        set
        {
            Item = ItemRegistry.GetItem( value ); // Get the item from the registry using the provided ID
        }
    }

    public IEnumerable<byte> Serialize()
    {
        var serialized = new List<byte>
		{
			(byte)ItemIdentifier.Length
		};
        serialized.AddRange( System.Text.Encoding.UTF8.GetBytes( ItemIdentifier ) ); // Serialize the item ID
        serialized.AddRange( BitConverter.GetBytes( Count ) ); // Serialize the count
        return serialized; // Return the serialized data as a byte array
    }

    public static ItemStack Deserialize( IEnumerable<byte> data, out int size )
    {
        var binReader = new BinaryReader( new MemoryStream( data.ToArray() ) );
        byte idLength = binReader.ReadByte(); // Read the length of the item ID
        var idBytes = binReader.ReadBytes( idLength ); // Read the item ID bytes
        string itemID = System.Text.Encoding.UTF8.GetString( idBytes ); // Convert the item ID bytes to a string
        int count = binReader.ReadInt32(); // Read the count
        size = 1 + idLength + 4; // Calculate the total size of the serialized data
        return new ItemStack
        {
            ItemIdentifier = itemID, // Set the item ID
            Count = count // Set the count
        };
    }

    public static bool IsNullOrEmpty( ItemStack stack )
    {
        return stack == null || stack.ItemIdentifier == "voxelparty:air" || string.IsNullOrWhiteSpace( stack.ItemIdentifier ) || stack.Count == 0;
    }

    // Spawn an instance of this item in the world at the specified position
    public WorldItem Spawn( Vector3 position )
    {
        var go = new GameObject();
        go.WorldPosition = position;
        go.WorldRotation = Rotation.Random;
        var wi = go.AddComponent<WorldItem>();
        wi.stack = this; // Set the stack data
        return wi; // Return the WorldItem component
    }

    public ItemStack()
    {
        ItemIdentifier = "voxelparty:air"; // Default item ID
        Count = 0; // Default count
    }

    public ItemStack( Item item, int count = 1 )
    {
        if ( item == null )
        {
            Item = null;
            Count = 0;
        }
        else
        {
            Item = item; // Set the item
            Count = count; // Set the count
        }
    }

    public ItemStack Clone()
    {
        return new ItemStack
        {
            Item = this.Item,
            Count = this.Count
        }; // Create a new ItemStack with the same data
    }

    // Can we accept the other stack into this one?
    public bool CanMerge( ItemStack other )
    {
        if ( IsNullOrEmpty( other ) )
            return false; // Cannot merge with an empty stack
        if ( IsNullOrEmpty( this ) )
            return false; // Cannot merge with an empty stack
        if ( this.ItemIdentifier != other.ItemIdentifier )
            return false; // Cannot merge stacks with different item IDs
        if ( this.Count >= Item.MaxStackSize )
            return false; // Cannot merge if this stack is already at max capacity
        return true;
    }

    public ItemStack Merge( ItemStack other, bool simulate = false )
    {
        if ( !CanMerge( other ) )
            return other; // Do not mutate the original stack, return the other stack if they cannot be merged
        int newValue = Math.Min( Count + other.Count, Item.MaxStackSize );
        int delta = newValue - Count; // Calculate the difference to determine how many items to add
        if ( !simulate )
        {
            Count = newValue; // Update the count of this stack
        }
        else
        {
            other = other.Clone(); // If simulating, clone the other stack to avoid modifying it
        }
        other.Count -= delta; // Decrease the count of the other stack by the delta
        if ( other.Count <= 0 )
        {
            return ItemStack.Empty; // If the other stack is empty after merging, return an empty stack
        }
        return other; // Return the other stack, which now has a reduced count
    }

}
