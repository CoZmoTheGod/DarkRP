using Sandbox.UI;

public sealed partial class Player
{
	[Property, Sync( SyncFlags.FromHost )]
	public int Money { get; private set; } = 2500;

	[Property, Sync( SyncFlags.FromHost )]
	public string JobTitle { get; private set; } = "Citizen";

	public bool CanAfford( int amount )
	{
		return amount >= 0 && amount <= Money;
	}

	public void GiveMoney( int amount )
	{
		if ( !Networking.IsHost || amount <= 0 )
			return;

		Money += amount;
	}

	public bool TryTakeMoney( int amount )
	{
		if ( !Networking.IsHost || amount < 0 || !CanAfford( amount ) )
			return false;

		Money -= amount;
		return true;
	}

	public void SetMoney( int amount )
	{
		if ( !Networking.IsHost )
			return;

		Money = Math.Max( 0, amount );
	}

	public void SetJobTitle( string title )
	{
		if ( !Networking.IsHost )
			return;

		JobTitle = string.IsNullOrWhiteSpace( title ) ? "Citizen" : title.Trim();
	}

	[Rpc.Host]
	public void RequestDropMoney( int amount )
	{
		if ( Rpc.Caller != Network.Owner )
			return;

		if ( amount <= 0 )
		{
			Notices.SendNotice( Network.Owner, "block", Color.Red, "Enter a valid amount to drop.", 3 );
			return;
		}

		if ( !TryTakeMoney( amount ) )
		{
			Notices.SendNotice( Network.Owner, "block", Color.Red, "You don't have enough money.", 3 );
			return;
		}

		if ( MoneyStack.TrySpawn( this, amount ) )
			return;

		GiveMoney( amount );
		Notices.SendNotice( Network.Owner, "block", Color.Red, "Unable to drop money right now.", 3 );
	}
}
