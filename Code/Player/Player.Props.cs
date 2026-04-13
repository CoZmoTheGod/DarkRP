using Sandbox.UI;

public sealed partial class Player
{
	public const int PropLimit = 300;

	public bool HasUnlimitedProps => HasAdminAccess;

	public int GetSpawnedPropCount( SpawnedProp ignoredProp = null )
	{
		var owner = Network.Owner;
		if ( owner is null || Game.ActiveScene is null )
			return 0;

		return Game.ActiveScene.GetAllComponents<SpawnedProp>()
			.Count( prop => prop.IsValid()
				&& prop != ignoredProp
				&& prop.GameObject.IsValid()
				&& prop.GameObject.Components.Get<Ownable>()?.Owner == owner );
	}

	public void SendPropLimitNotice( SpawnedProp ignoredProp = null )
	{
		if ( !Networking.IsHost || !this.IsValid() || Network.Owner is null )
			return;

		var limit = HasUnlimitedProps ? "unlimited" : PropLimit.ToString();
		Notices.SendNotice( Network.Owner, "category", Color.Cyan, $"Props: {GetSpawnedPropCount( ignoredProp )}/{limit}", 2 );
	}

	public bool CanSpawnProp( out string error )
	{
		error = null;

		if ( !Networking.IsHost || !this.IsValid() || Network.Owner is null )
		{
			error = "Invalid prop spawn request.";
			return false;
		}

		if ( HasUnlimitedProps )
			return true;

		if ( GetSpawnedPropCount() < PropLimit )
			return true;

		error = $"Prop limit reached ({PropLimit}/{PropLimit}).";
		return false;
	}
}
