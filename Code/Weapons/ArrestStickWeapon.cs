public sealed class ArrestStickWeapon : MeleeWeapon
{
	protected override bool WantsPrimaryAttack() => Input.Pressed( "attack1" );

	public override void OnControl( Player player )
	{
		if ( !player.IsValid() || !player.GameObject.IsValid() )
			return;

		if ( WantsPrimaryAttack() && CanAttack() )
		{
			TryArrestTarget( player );
		}

		base.OnControl( player );
	}

	void TryArrestTarget( Player player )
	{
		if ( !player.IsValid() || !player.GameObject.IsValid() || !player.CanArrestPlayers )
			return;

		var forward = player.EyeTransform.Rotation.Forward;
		var tr = Scene.Trace.Ray( player.EyeTransform.ForwardRay with { Forward = forward }, Range )
			.IgnoreGameObjectHierarchy( player.GameObject )
			.WithoutTags( "playercontroller" )
			.Radius( SwingRadius )
			.UseHitboxes()
			.Run();

		if ( !tr.GameObject.IsValid() )
			return;

		var targetRoot = tr.GameObject.Root;
		if ( !targetRoot.IsValid() )
			return;

		var target = targetRoot.GetComponent<Player>();
		if ( !target.IsValid() || target == player )
			return;

		player.RequestArrestPlayer( target.PlayerId );
	}
}
