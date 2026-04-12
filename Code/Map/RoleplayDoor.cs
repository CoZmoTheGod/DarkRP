using Facepunch;
using System.Text.Json.Serialization;

public sealed class RoleplayDoor : Component
{
	const string GovernmentJobCategory = "Government";

	[RequireComponent] public Door Door { get; set; }

	[Property, Sync( SyncFlags.FromHost )]
	public int PurchasePrice { get; set; } = 500;

	[Property, Sync( SyncFlags.FromHost )]
	public bool IsGovernment { get; set; }

	[Property, Group( "Sound" )]
	public SoundEvent LockSound { get; set; } = new( "entities/door/sounds/door_lock.sound" );

	[Property, Group( "Sound" )]
	public SoundEvent UnlockSound { get; set; } = new( "entities/door/sounds/door_unlock.sound" );

	[Sync( SyncFlags.FromHost )]
	private Guid _ownerId { get; set; }

	[Property, ReadOnly, JsonIgnore]
	public Connection Owner
	{
		get => Connection.All.FirstOrDefault( x => x.Id == _ownerId );
		private set => _ownerId = value?.Id ?? Guid.Empty;
	}

	public bool IsOwned => Owner is not null;
	public bool CanBePurchased => !IsGovernment && !IsOwned;

	public bool IsOwnedBy( Connection connection )
	{
		if ( connection is null )
			return false;

		return Owner == connection;
	}

	public bool CanPress( IPressable.Event e, Door.DoorState state )
	{
		return state is Door.DoorState.Open or Door.DoorState.Closed;
	}

	public bool Press( IPressable.Event e, Door.DoorState state )
	{
		if ( !CanPress( e, state ) )
			return false;

		if ( IsGovernment )
		{
			if ( !e.Source.IsValid() )
				return false;

			Door.Toggle( e.Source.GameObject );
			return true;
		}

		if ( !IsOwned )
			return true;

		if ( !e.Source.IsValid() )
			return false;

		Door.Toggle( e.Source.GameObject );
		return true;
	}

	public bool TryBuy( Player buyer, out string error )
	{
		error = null;

		if ( !Networking.IsHost || !buyer.IsValid() )
		{
			error = "Invalid door purchase request.";
			return false;
		}

		if ( IsGovernment )
		{
			error = "Government doors can't be bought.";
			return false;
		}

		if ( IsOwned )
		{
			error = "This door is already owned.";
			return false;
		}

		var price = Math.Max( 0, PurchasePrice );
		if ( !buyer.TryTakeMoney( price ) )
		{
			error = "You don't have enough money.";
			return false;
		}

		Owner = buyer.Network.Owner;
		Door.IsLocked = false;
		return true;
	}

	public bool TrySell( Player seller, out int refund, out string error )
	{
		refund = 0;
		error = null;

		if ( !Networking.IsHost || !seller.IsValid() )
		{
			error = "Invalid door sale request.";
			return false;
		}

		if ( IsGovernment )
		{
			error = "Government doors can't be sold.";
			return false;
		}

		if ( !IsOwnedBy( seller.Network.Owner ) )
		{
			error = IsOwned ? "Only the door owner can sell it." : "Buy this door first.";
			return false;
		}

		refund = Math.Max( 0, PurchasePrice );

		if ( Door.State is Door.DoorState.Open or Door.DoorState.Opening )
		{
			Door.CloseFromServer( seller.GameObject );
		}

		Door.IsLocked = false;
		Owner = null;

		if ( refund > 0 )
		{
			seller.GiveMoney( refund );
		}

		return true;
	}

	public bool TrySetLocked( Player actor, bool locked, out string error )
	{
		error = null;

		if ( !Networking.IsHost || !actor.IsValid() )
		{
			error = "Invalid door lock request.";
			return false;
		}

		if ( IsGovernment )
		{
			if ( !CanAccessGovernmentDoor( actor ) )
			{
				error = "Only government jobs can do that.";
				return false;
			}
		}
		else if ( !IsOwnedBy( actor.Network.Owner ) )
		{
			error = IsOwned ? "Only the door owner can do that." : "Buy this door first.";
			return false;
		}

		if ( Door.IsLocked == locked )
		{
			error = locked ? "Door is already locked." : "Door is already unlocked.";
			return false;
		}

		if ( locked && Door.State is Door.DoorState.Open or Door.DoorState.Opening )
		{
			Door.CloseFromServer( actor.GameObject );
		}

		Door.IsLocked = locked;

		var sound = locked ? LockSound : UnlockSound;
		if ( sound is not null )
		{
			PlayLockSound( sound );
		}

		return true;
	}

	public IPressable.Tooltip BuildTooltip( Player player, Door.DoorState state )
	{
		var isOwner = player.IsValid() && IsOwnedBy( player.Network.Owner );
		var title = state == Door.DoorState.Open ? "Close" : "Open";
		var icon = Door.IsLocked ? "lock" : "door_front";

		if ( IsGovernment )
		{
			if ( CanAccessGovernmentDoor( player ) )
			{
				var action = Door.IsLocked ? "unlock" : "lock";
				return new IPressable.Tooltip( title, icon, $"Government door - Right click to {action}" );
			}

			return new IPressable.Tooltip( "Government Door", "lock", "Police, Police Chief and Mayor access only" );
		}

		if ( !IsOwned )
		{
			var price = Math.Max( 0, PurchasePrice );
			var progress = player.IsValid() ? player.GetDoorPurchaseProgress( this ) : 0.0f;
			var description = $"${price:n0} - Hold E to buy";

			if ( progress > 0.0f )
			{
				var percent = (int)MathF.Round( progress * 100.0f );
				description = $"${price:n0} - Hold E to buy {BuildProgressBar( progress )} {percent}%";
			}

			return new IPressable.Tooltip( "Buy Door", "$", description );
		}

		if ( isOwner )
		{
			var action = Door.IsLocked ? "unlock" : "lock";
			var sellPrice = Math.Max( 0, PurchasePrice );
			return new IPressable.Tooltip( title, icon, $"Owned by you - Right click to {action}, R to sell for ${sellPrice:n0}" );
		}

		var ownerName = Owner?.DisplayName ?? "Unknown";
		var lockState = Door.IsLocked ? "locked" : "unlocked";
		return new IPressable.Tooltip( title, icon, $"Owned by {ownerName} ({lockState})" );
	}

	public bool CanControlLock( Player player )
	{
		if ( !player.IsValid() )
			return false;

		if ( IsGovernment )
			return CanAccessGovernmentDoor( player );

		return IsOwnedBy( player.Network.Owner );
	}

	public bool CanUseDoor( Player player )
	{
		if ( !player.IsValid() )
			return false;

		if ( IsGovernment )
			return CanAccessGovernmentDoor( player );

		if ( !IsOwned )
			return false;

		if ( Door.IsLocked )
			return IsOwnedBy( player.Network.Owner );

		return true;
	}

	static Player GetPlayer( IPressable.Event e )
	{
		if ( !e.Source.IsValid() )
			return null;

		return e.Source.GameObject.Root.GetComponent<Player>();
	}

	static bool CanAccessGovernmentDoor( Player player )
	{
		if ( !player.IsValid() )
			return false;

		var category = player.CurrentJobDefinition?.Category?.Trim();
		return string.Equals( category, GovernmentJobCategory, StringComparison.OrdinalIgnoreCase );
	}

	[Rpc.Broadcast]
	void PlayLockSound( SoundEvent sound )
	{
		if ( sound is null )
			return;

		GameObject.PlaySound( sound );
	}

	static string BuildProgressBar( float progress )
	{
		const int segments = 10;
		var clamped = Math.Clamp( progress, 0.0f, 1.0f );
		var filled = (int)MathF.Round( clamped * segments );
		return $"[{new string( '#', filled )}{new string( '-', segments - filled )}]";
	}
}
