[Alias( "upright_joint" )]
public sealed class UprightJoint : Component
{
	[Property, Sync]
	public GameObject Body { get; set; }

	[Property, Sync]
	public float Hertz { get; set; } = 2.0f;

	[Property, Sync]
	public float DampingRatio { get; set; } = 0.7f;

	[Property, Sync]
	public float MaxTorque { get; set; } = 50000.0f;

	private Rigidbody _anchorBody;
	private Rigidbody _targetBody;
	private Rotation _worldTargetRotation;
	private Rotation _relativeTargetRotation;
	private bool _initialized;

	protected override void OnStart()
	{
		base.OnStart();

		InitializeTarget();
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( !Networking.IsHost ) return;

		if ( !_initialized || !_anchorBody.IsValid() )
			InitializeTarget();

		if ( !_anchorBody.IsValid() || !_anchorBody.MotionEnabled )
			return;

		var targetRotation = GetTargetRotation();
		var torque = GetCorrectionTorque( WorldRotation, targetRotation, GetRelativeAngularVelocity() );

		if ( torque.Length.AlmostEqual( 0.0f ) )
			return;

		_anchorBody.ApplyTorque( torque );

		if ( _targetBody.IsValid() && _targetBody != _anchorBody && _targetBody.MotionEnabled )
			_targetBody.ApplyTorque( -torque );
	}

	private void InitializeTarget()
	{
		_anchorBody = GetComponentInParent<Rigidbody>( true );
		_targetBody = Body?.GetComponentInParent<Rigidbody>( true );

		_worldTargetRotation = WorldRotation;
		_relativeTargetRotation = Body.IsValid()
			? Body.WorldRotation.Inverse * WorldRotation
			: Rotation.Identity;

		_initialized = true;
	}

	private Rotation GetTargetRotation()
	{
		if ( Body.IsValid() )
			return Body.WorldRotation * _relativeTargetRotation;

		return _worldTargetRotation;
	}

	private Vector3 GetRelativeAngularVelocity()
	{
		if ( _targetBody.IsValid() && _targetBody != _anchorBody )
			return _anchorBody.AngularVelocity - _targetBody.AngularVelocity;

		return _anchorBody.AngularVelocity;
	}

	private Vector3 GetCorrectionTorque( Rotation current, Rotation target, Vector3 angularVelocity )
	{
		var stiffness = MathF.Max( Hertz, 0.0f );
		var damping = MathF.Max( DampingRatio, 0.0f );

		var correction =
			Vector3.Cross( current.Up, target.Up ) +
			Vector3.Cross( current.Forward, target.Forward );

		var torque = correction * MaxTorque * stiffness;
		torque -= angularVelocity * MaxTorque * damping * 0.01f;

		return torque.ClampLength( MaxTorque );
	}
}
