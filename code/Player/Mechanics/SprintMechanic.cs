namespace RogueFPS;

/// <summary>
/// A sprinting mechanic with a toggle feature, stops sprinting when not moving.
/// </summary>
public partial class SprintMechanic : PlayerMechanic
{
	public bool IsSprinting = false;

	public override bool ShouldBecomeActive()
	{
		// Check if the player is grounded
		if ( !PlayerController.IsGrounded ) return false;

		var wish = PlayerController.WishMove;

		// Stop sprinting if the player is not moving
		if ( wish.Length.AlmostEqual( 0 ) )
		{
			IsSprinting = false;
			return false;
		}

		// Toggle sprinting when the Run key is pressed
		if ( Input.Pressed( "Run" ) )
		{
			IsSprinting = !IsSprinting;
		}
		
		// Check if player is currently sprinting and not sliding
		return IsSprinting && !HasAnyTag( "slide" );
	}

	public override IEnumerable<string> GetTags()
	{
		yield return "sprint";
	}

	public override float? GetSpeed()
	{
		return Player.Stats.UpgradedStats[Stats.PlayerUpgradedStats.WalkSpeed] * Player.Stats.UpgradedStats[Stats.PlayerUpgradedStats.SprintMultiplier];
	}
}
