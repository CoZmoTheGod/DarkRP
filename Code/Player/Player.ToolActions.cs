using Sandbox.UI;

public sealed partial class Player
{
	const float ToolActionCooldownSeconds = 1.0f;
	const float ToolActionDeniedNoticeCooldownSeconds = 1.0f;

	TimeSince _timeSinceToolAction = ToolActionCooldownSeconds;
	TimeSince _timeSinceToolActionDeniedNotice = ToolActionDeniedNoticeCooldownSeconds;

	public bool TryUseToolActionCooldown( string toolName, out string error )
	{
		error = null;

		if ( !Networking.IsHost || !this.IsValid() || Network.Owner is null )
		{
			error = "Invalid tool request.";
			return false;
		}

		if ( HasAdminAccess )
			return true;

		if ( _timeSinceToolAction >= ToolActionCooldownSeconds )
		{
			_timeSinceToolAction = 0;
			return true;
		}

		var remaining = MathF.Ceiling( (ToolActionCooldownSeconds - _timeSinceToolAction) * 10.0f ) / 10.0f;
		var name = string.IsNullOrWhiteSpace( toolName ) ? "this tool" : toolName;
		error = $"Slow down before using {name} again ({remaining:0.0}s).";
		return false;
	}

	public void SendToolActionDeniedNotice( string error )
	{
		if ( !Networking.IsHost || !this.IsValid() || Network.Owner is null || string.IsNullOrWhiteSpace( error ) )
			return;

		if ( _timeSinceToolActionDeniedNotice < ToolActionDeniedNoticeCooldownSeconds )
			return;

		_timeSinceToolActionDeniedNotice = 0;
		Notices.SendNotice( Network.Owner, "block", Color.Red, error, 2 );
	}
}
