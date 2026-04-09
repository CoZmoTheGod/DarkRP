using Sandbox.UI;

public sealed class AdminSystem : GameObjectSystem<AdminSystem>
{
	public sealed class Entry
	{
		public string DisplayName { get; set; }
		public AdminRole Role { get; set; }
	}

	Dictionary<long, Entry> _entries = new();
	bool _loaded;

	public AdminSystem( Scene scene ) : base( scene )
	{
	}

	public AdminRole GetRole( Connection connection )
	{
		if ( connection is null )
			return AdminRole.None;

		if ( connection.IsHost )
			return AdminRole.SuperAdmin;

		return GetRole( connection.SteamId );
	}

	public AdminRole GetRole( SteamId steamId )
	{
		EnsureLoaded();
		return _entries.TryGetValue( steamId, out var entry ) ? entry.Role : AdminRole.None;
	}

	public bool HasAdminAccess( Connection connection ) => GetRole( connection ) >= AdminRole.Admin;
	public bool HasSuperAdminAccess( Connection connection ) => GetRole( connection ) >= AdminRole.SuperAdmin;

	public IReadOnlyDictionary<SteamId, Entry> GetEntries()
	{
		if ( !Networking.IsHost )
			return new Dictionary<SteamId, Entry>();

		EnsureLoaded();
		return _entries.ToDictionary( x => (SteamId)x.Key, x => x.Value );
	}

	public void SetRole( SteamId steamId, AdminRole role, string displayName = null )
	{
		Assert.True( Networking.IsHost, "Only the host may modify admin roles." );

		if ( steamId.Value <= 0 )
			return;

		var targetConnection = Connection.All.FirstOrDefault( x => x.SteamId == steamId );
		if ( targetConnection?.IsHost == true )
			return;

		EnsureLoaded();

		if ( role == AdminRole.None )
		{
			if ( _entries.Remove( steamId ) )
			{
				Save();
			}
		}
		else
		{
			_entries[steamId] = new Entry
			{
				DisplayName = string.IsNullOrWhiteSpace( displayName ) ? steamId.ToString() : displayName.Trim(),
				Role = role
			};

			Save();
		}

		ApplyRoleToOnlinePlayer( steamId );
	}

	public void RefreshPlayerRole( Player player )
	{
		if ( !Networking.IsHost || !player.IsValid() )
			return;

		player.SetAdminRole( GetRole( player.Network.Owner ) );
	}

	void ApplyRoleToOnlinePlayer( SteamId steamId )
	{
		var connection = Connection.All.FirstOrDefault( x => x.SteamId == steamId );
		if ( connection is null )
			return;

		RefreshPlayerRole( Player.FindForConnection( connection ) );
	}

	void EnsureLoaded()
	{
		if ( _loaded || !Networking.IsHost )
			return;

		_entries = LocalData.Get<Dictionary<long, Entry>>( "admins", new() ) ?? new();
		_loaded = true;
	}

	void Save()
	{
		EnsureLoaded();
		LocalData.Set( "admins", _entries );
	}
}
