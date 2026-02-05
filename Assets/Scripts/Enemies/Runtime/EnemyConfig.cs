using UnityEngine;

namespace LittleBeakCluck.Enemies
{
    [CreateAssetMenu(fileName = "EnemyConfig", menuName = "LittleBeakCluck/Enemy Config", order = 0)]
    public class EnemyConfig : ScriptableObject
    {
        [Header("Health")]
        [Min(1f)]
        public float maxHealth = 50f;

        [Header("Movement")]
        [Min(0f)]
        public float maxMoveSpeed = 3f;
        [Min(0f)]
        public float moveAcceleration = 20f;
        [Min(0f)]
        public float moveDeceleration = 30f;

        [Header("Attack")]
        [Min(0.1f)]
        public float attackCooldown = 2f;
        [Min(0.1f)]
        public float attackDamage = 10f;
        [Tooltip("Layer(s) considered a valid damage target")]
        public LayerMask attackLayer;
        [Tooltip("Width/height of the damage zone relative to attack origin")]
        public Vector2 attackBoxSize = new Vector2(1.2f, 1.0f);
        [Tooltip("Offset in local space relative to attack origin")]
        public Vector2 attackBoxOffset = new Vector2(0.8f, 0.0f);

        [Header("Knockback")]
        [Min(0f)]
        public float knockbackRecoverTime = 0.25f;
        [Range(0f, 1f)]
        public float knockbackHorizontalDampen = 0.85f;

        [Header("Death")]
        [Tooltip("How long the defeated enemy remains before destroying the GO")]
        [Min(0f)]
        public float deathDespawnDelay = 2f;
        [Tooltip("Impulse applied when the enemy dies")]
        public float deathPushForce = 6f;
        [Tooltip("Torque applied when the enemy dies")]
        public float deathTorque = 5f;
        [Tooltip("Gravity scale for ragdoll fall")]
        public float deathGravityScale = 2f;

        [Header("HUD")]
        [Tooltip("World-space offset applied when positioning the enemy hud elements.")]
        public Vector3 hudWorldOffset = new Vector3(0f, 1.5f, 0f);
        [Tooltip("Sprite displayed next to the health bar when the enemy is on screen.")]
        public Sprite waveTypeIcon;
        [Tooltip("Tint applied to the health fill when the enemy is on screen.")]
        public Color hudBarColor = Color.white;
        [Tooltip("Tint applied to the off-screen arrow if overridden. Leave transparent for default.")]
        public Color hudArrowColor = new Color(1f, 1f, 1f, 0.9f);

        [Header("Audio")]
        [Tooltip("Унікальний звук, що грає один раз, коли ворога вперше бачить камера.")]
        public AudioClip revealSfx;
        [Tooltip("Звук, який програється під час успішного удару по гравцю.")]
        public AudioClip attackHitSfx;
    }
}
