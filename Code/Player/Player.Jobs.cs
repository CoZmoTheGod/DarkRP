using Sandbox.UI;

public sealed partial class Player
{
	const string PrisonerJumpsuitClothingPath = "models/citizen_clothes/shirt/jumpsuit/prison_jumpsuit.clothing";
	const string PrisonerShoesClothingPath = "models/citizen_clothes/shoes/boots/black_boots.clothing";

	sealed class BodyAppearanceSnapshot
	{
		public Model Model { get; init; }
		public ulong BodyGroups { get; init; }
		public string MaterialGroup { get; init; }
		public Material MaterialOverride { get; init; }
		public Dictionary<string, float> Morphs { get; init; }
	}

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
		await ApplyClothingAsync( definition?.Clothing );
	}

	async Task ApplyPrisonerClothingAsync()
	{
		await ApplyClothingAsync( [PrisonerJumpsuitClothingPath, PrisonerShoesClothingPath] );
	}

	async Task RestoreJobClothingAsync()
	{
		await ApplyJobClothingAsync( CurrentJobDefinition );
	}

	async Task ApplyClothingAsync( IEnumerable<string> clothingPaths )
	{
		if ( !Body.IsValid() )
			return;

		var dresser = Body.GetComponentInChildren<Dresser>( true );
		if ( !dresser.IsValid() )
			return;

		var bodyRenderer = dresser.BodyTarget.IsValid()
			? dresser.BodyTarget
			: Body.GetComponent<SkinnedModelRenderer>( true );

		var appearance = CaptureBodyAppearance( bodyRenderer );

		dresser.Clothing.Clear();

		foreach ( var clothingPath in clothingPaths ?? [] )
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
		await dresser.Apply();
		RestoreBodyAppearance( bodyRenderer, appearance );
		Body.Network?.Refresh();
		GameObject.Network?.Refresh();
	}

	static BodyAppearanceSnapshot CaptureBodyAppearance( SkinnedModelRenderer renderer )
	{
		if ( !renderer.IsValid() )
			return null;

		Dictionary<string, float> morphs = null;
		if ( renderer.SceneModel is not null )
		{
			morphs = renderer.Morphs.Names.ToDictionary( name => name, name => renderer.SceneModel.Morphs.Get( name ) );
		}

		return new BodyAppearanceSnapshot
		{
			Model = renderer.Model,
			BodyGroups = renderer.BodyGroups,
			MaterialGroup = renderer.MaterialGroup,
			MaterialOverride = renderer.MaterialOverride,
			Morphs = morphs
		};
	}

	static void RestoreBodyAppearance( SkinnedModelRenderer renderer, BodyAppearanceSnapshot appearance )
	{
		if ( !renderer.IsValid() || appearance is null )
			return;

		renderer.Model = appearance.Model;
		renderer.BodyGroups = appearance.BodyGroups;
		renderer.MaterialGroup = appearance.MaterialGroup;
		renderer.MaterialOverride = appearance.MaterialOverride;

		if ( appearance.Morphs is null || renderer.SceneModel is null )
			return;

		foreach ( var name in renderer.Morphs.Names )
		{
			renderer.SceneModel.Morphs.Reset( name );
		}

		foreach ( var (name, value) in appearance.Morphs )
		{
			renderer.SceneModel.Morphs.Set( name, value );
		}
	}
}
