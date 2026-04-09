using Sandbox.UI;

public sealed partial class Player
{
	[Property, Sync( SyncFlags.FromHost )]
	public string JobDefinitionPath { get; private set; } = JobDefinition.DefaultResourcePath;

	public JobDefinition CurrentJobDefinition => JobDefinition.Get( JobDefinitionPath ) ?? JobDefinition.GetDefault();

	public void SetJobDefinition( JobDefinition definition )
	{
		if ( !Networking.IsHost || definition is null )
			return;

		JobDefinitionPath = definition.ResourcePath;
		SetJobTitle( definition.Title );
		PlayerData?.SetJob( definition.ResourcePath, definition.Title );
		SaveRoleplayData();
	}

	public void EnsureValidJobDefinition()
	{
		if ( !Networking.IsHost )
			return;

		var definition = JobDefinition.Get( PlayerData?.JobDefinitionPath )
			?? CurrentJobDefinition
			?? JobDefinition.GetDefault();

		if ( definition is null )
			return;

		SetJobDefinition( definition );
	}

	[Rpc.Host]
	public void RequestSetJob( string resourcePath )
	{
		if ( Rpc.Caller != Network.Owner )
			return;

		var definition = JobDefinition.Get( resourcePath );
		if ( definition is null )
			return;

		if ( !JobManager.CanJoin( this, definition, out var reason ) )
		{
			Notices.SendNotice( Network.Owner, "block", Color.Red, reason, 3 );
			return;
		}

		if ( string.Equals( JobDefinitionPath, definition.ResourcePath, StringComparison.OrdinalIgnoreCase ) )
		{
			Notices.SendNotice( Network.Owner, "person", Color.Yellow, $"You are already {definition.Title}.", 3 );
			return;
		}

		SetJobDefinition( definition );
		_ = ApplyJobDefinitionAsync( definition, true );
	}

	public Task ApplyCurrentJobAfterSpawnAsync()
	{
		var definition = CurrentJobDefinition;
		return definition is null ? Task.CompletedTask : ApplyJobDefinitionAsync( definition, false );
	}

	async Task ApplyJobDefinitionAsync( JobDefinition definition, bool notifyPlayer )
	{
		if ( !Networking.IsHost || definition is null )
			return;

		SetJobDefinition( definition );

		var inventory = GetComponent<PlayerInventory>();
		if ( inventory.IsValid() )
		{
			await inventory.ApplyJobLoadoutAsync( definition.StartingItems ?? [] );
		}

		await ApplyJobClothingAsync( definition );

		if ( notifyPlayer )
		{
			Notices.SendNotice( Network.Owner, "badge", Color.Green, $"You are now {definition.Title}.", 3 );
		}
	}

	async Task ApplyJobClothingAsync( JobDefinition definition )
	{
		if ( !Body.IsValid() )
			return;

		var dresser = Body.GetComponentInChildren<Dresser>( true );
		if ( !dresser.IsValid() )
			return;

		var manualAge = dresser.ManualAge;
		var manualHeight = dresser.ManualHeight;
		var manualTint = dresser.ManualTint;

		dresser.Clothing.Clear();

		foreach ( var clothingPath in definition.Clothing ?? [] )
		{
			if ( string.IsNullOrWhiteSpace( clothingPath ) )
				continue;

			var clothing = ResourceLibrary.Get<Clothing>( clothingPath );
			if ( clothing is null )
				continue;

			dresser.Clothing.Add( new ClothingContainer.ClothingEntry
			{
				Clothing = clothing
			} );
		}

		dresser.Clear();
		dresser.Source = Dresser.ClothingSource.Manual;
		dresser.ManualAge = manualAge;
		dresser.ManualHeight = manualHeight;
		dresser.ManualTint = manualTint;
		await dresser.Apply();
		Body.Network?.Refresh();
		GameObject.Network?.Refresh();
	}
}
