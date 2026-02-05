using UnityEngine;

namespace LittleBeakCluck.Combat
{
    public struct DamageInfo
    {
        public float Amount;           // базова кількість шкоди
        public VoiceWaveType WaveType; // тип хвилі (для майбутніх резистів / ефектів)
        public Vector2 HitPoint;       // точка влучання
        public Vector2 Direction;      // напрямок від джерела до цілі
        public Rigidbody2D TargetRigidbody; // RB цілі, в яку влучили (для коректного нокбеку на дочірніх колайдерах)
        public float KnockbackForce;   // сила відкидання
        public Object Source;          // хто наніс (може бути PlayerAttack, VoiceWaveHit, ворог тощо)
    }
}
