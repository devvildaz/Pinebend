namespace Pinebend
{
    // Timed effects outlive the inventory slot that activated them. Explicit time keeps
    // expiry, damage and recovery rules independent of rendering and input devices.
    public sealed class CarPowerState
    {
        public static float ShieldDuration=>UnityEngine.Mathf.Clamp(GameplayBalance.Current.shieldSeconds,1,12);
        public const float TurboDuration=3;
        public static int ShieldBulletBudget=>UnityEngine.Mathf.Clamp(GameplayBalance.Current.shieldGunHits,1,12)*RallyWeapons.GunDamage;
        public const int RepairAmount=35;
        public const float TurboSpeed=52, TurboTorque=1.8f;
        float shieldUntil, turboUntil;
        int shieldBudget;

        public bool Shielded(float now)=>now<shieldUntil && shieldBudget>0;
        public bool Boosting(float now)=>now<turboUntil;
        public bool ActivateShield(float now)
        {
            if(Shielded(now))return false;
            shieldUntil=now+ShieldDuration;shieldBudget=ShieldBulletBudget;return true;
        }
        public bool ActivateTurbo(float now)
        {
            if(Boosting(now))return false;
            turboUntil=now+TurboDuration;return true;
        }
        public bool Absorb(int damage,bool explosion,float now)
        {
            if(damage<=0 || !Shielded(now))return false;
            shieldBudget=explosion ? 0 : System.Math.Max(0,shieldBudget-damage);
            return true;
        }
        public int RepairedHealth(int health,int maximum)=>System.Math.Min(maximum,health+RepairAmount);
        public void Clear(){shieldUntil=turboUntil=0;shieldBudget=0;}
    }
}
