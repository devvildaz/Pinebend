using System;
using System.IO;

namespace Pinebend.Editor
{
    public static class CarPowerCheck
    {
        public static void Check()
        {
            void Require(bool condition,string message){if(!condition)throw new Exception(message);}
            var state=new CarPowerState();
            Require(state.ActivateShield(10),"Shield activation failed");
            Require(!state.ActivateShield(11),"Shield duration can be stacked");
            Require(!state.Absorb(0,true,11) && state.Shielded(11),"Zero damage consumed shield");
            Require(state.Absorb(40,true,11) && !state.Shielded(11),"Blast did not consume shield");
            Require(!state.Absorb(40,true,11),"Second blast was absorbed");
            state.ActivateShield(20);
            for(int i=0;i<CarPowerState.ShieldBulletBudget/RallyWeapons.GunDamage;i++)Require(state.Absorb(RallyWeapons.GunDamage,false,21),"Shield failed before its configured bullet budget");
            Require(!state.Absorb(RallyWeapons.GunDamage,false,21),"Bullet after budget exhaustion should pass through");
            state.ActivateShield(30);
            Require(state.Shielded(30+CarPowerState.ShieldDuration-.01f) && !state.Shielded(30+CarPowerState.ShieldDuration),"Shield expiry incorrect");
            Require(state.ActivateTurbo(40) && !state.ActivateTurbo(41),"Turbo can stack");
            Require(state.Boosting(42.99f) && !state.Boosting(43),"Turbo expiry incorrect");
            Require(state.RepairedHealth(50,100)==85 && state.RepairedHealth(90,100)==100 && state.RepairedHealth(100,100)==100,"Repair cap incorrect");
            state.ActivateShield(50);state.ActivateTurbo(50);state.Clear();
            Require(!state.Shielded(51) && !state.Boosting(51),"Effects survived recovery");
            Directory.CreateDirectory("Builds");
            File.WriteAllText("Builds/power-check.txt","Passed edit-mode checks: one-blast shield, configured bullet budget, expiry boundaries, no stacking, repair cap, recovery reset. No gameplay simulation performed.");
        }
    }
}
