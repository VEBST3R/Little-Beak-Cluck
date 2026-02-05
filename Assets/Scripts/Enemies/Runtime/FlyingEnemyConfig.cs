using UnityEngine;

namespace LittleBeakCluck.Enemies
{
    [CreateAssetMenu(fileName = "FlyingEnemyConfig", menuName = "LittleBeakCluck/Flying Enemy Config", order = 10)]
    public class FlyingEnemyConfig : EnemyConfig
    {
        [Header("Patrol Zone")]
        [Tooltip("Size of the patrol area (width, height). Center is anchored to the spawn position.")]
        public Vector2 patrolZoneSize = new Vector2(4f, 3f);

        [Tooltip("Offset of patrol zone center from the spawn position (e.g. (0,2) = above spawn).")]
        public Vector2 patrolZoneOffset = new Vector2(0f, 2f);

        [Tooltip("How fast the enemy moves while patrolling.")]
        public float patrolMoveSpeed = 3f;

        [Tooltip("Smoothing time applied when moving between patrol targets.")]
        [Min(0f)]
        public float patrolSmoothingTime = 0.25f;

        [Tooltip("How long to wait at a patrol point before moving to next.")]
        public float patrolWaitTime = 1.5f;

        [Header("Attack - Parabolic Dive")]

        [Tooltip("Duration of the dive bezier (seconds).")]
        public float diveDuration = 0.9f;

        [Tooltip("How deep the dive arc bends under the player.")]
        public float diveArcDepth = 2.5f;

        [Tooltip("Delay between triggering the attack animation and actually dealing damage.")]
        public float attackImpactDelay = 0.1f;

        [Header("Attack Entry/Exit Shaping")]
        [Tooltip("Horizontal distance from the player to the attack entry and exit points.")]
        public float attackHorizontalDistance = 3.5f;

        [Tooltip("Vertical offset above the attack anchor for the entry point (before the dive).")]
        public float attackEntryHeight = 4.5f;

        [Tooltip("Vertical offset above the attack anchor for the exit point (after the climb).")]
        public float attackExitHeight = 5f;

        [Tooltip("Vertical offset applied to the attack target point (player anchor).")]
        public float attackTargetHeightOffset = 0f;

        [Header("Predictive Targeting")]
        [Tooltip("Whether the bird should lead its attacks based on the player's current motion.")]
        public bool enablePredictiveTargeting = true;

        [Tooltip("Default lead time (seconds) to use when no precise timing info is available.")]
        [Min(0f)]
        public float predictionDefaultLeadTime = 0.35f;

        [Tooltip("Maximum lead time (seconds) when projecting the player's future position.")]
        [Min(0f)]
        public float predictionMaxLeadTime = 0.9f;

        [Tooltip("Responsiveness factor for blending towards the measured player velocity (higher = snappier).")]
        [Min(0f)]
        public float predictionVelocityResponsiveness = 10f;

        [Tooltip("Smoothing time applied when repositioning the attack anchor to the predicted point.")]
        [Min(0f)]
        public float predictionSmoothingTime = 0.18f;

        [Tooltip("Within this distance from the player, the anchor is magnetized back to the player to avoid overshooting.")]
        [Min(0f)]
        public float predictionMagnetRange = 1.6f;

        [Tooltip("Maximum horizontal distance the anchor prediction can drift away from the player.")]
        [Min(0f)]
        public float predictionMaxOffset = 4f;

        [Header("Recovery")]
        [Tooltip("How fast to return to patrol zone after attack.")]
        public float returnSpeed = 4f;

        [Tooltip("Minimum time before next attack can begin.")]
        public float postAttackDelay = 2f;

        [Tooltip("Height offset above the patrol zone where the bird finishes its climb.")]
        public float climbHeight = 3f;

        [Tooltip("Duration of the climb bezier (seconds).")]
        public float climbDuration = 1.1f;

        [Tooltip("Extra upward arc applied during climb to smooth the curve.")]
        public float climbArcHeight = 2f;
    }
}
