using Sandbox.UI;

public sealed partial class Player
{
	const float DuplicatorSpawnCooldownSeconds = 1.0f;
	const float DuplicatorSpawnDeniedNoticeCooldownSeconds = 1.0f;

	TimeSince _timeSinceDuplicatorSpawn = DuplicatorSpawnCooldownSeconds;
	TimeSince _timeSinceDuplicatorSpawnDeniedNotice = DuplicatorSpawnDeniedNoticeCooldownSeconds;

	public bool CanSpawnDuplication( out string error )
	{
		error = null;

		if ( !Networking.IsHost || !this.IsValid() || Network.Owner is null )
		{
			error = "Invalid duplication request.";
			return false;
		}

		if ( HasAdminAccess )
			return true;

		if ( _timeSinceDuplicatorSpawn >= DuplicatorSpawnCooldownSeconds )
			return true;

		var remaining = MathF.Ceiling( (DuplicatorSpawnCooldownSeconds - _timeSinceDuplicatorSpawn) * 10.0f ) / 10.0f;
		error = $"Slow down before placing another duplication ({remaining:0.0}s).";
		return false;
	}

	public void MarkDuplicationSpawned()
	{
		if ( !Networking.IsHost )
			return;

		_timeSinceDuplicatorSpawn = 0;
	}

	public void SendDuplicationSpawnDeniedNotice( string error )
	{
		if ( !Networking.IsHost || !this.IsValid() || Network.Owner is null || string.IsNullOrWhiteSpace( error ) )
			return;

		if ( _timeSinceDuplicatorSpawnDeniedNotice < DuplicatorSpawnDeniedNoticeCooldownSeconds )
			return;

		_timeSinceDuplicatorSpawnDeniedNotice = 0;
		Notices.SendNotice( Network.Owner, "block", Color.Red, error, 2 );
	}
}
