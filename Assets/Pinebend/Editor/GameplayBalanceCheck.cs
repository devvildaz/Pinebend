using System;
using System.IO;
using UnityEngine;

namespace Pinebend.Editor
{
    public static class GameplayBalanceCheck
    {
        public static void Check()
        {
            var balance=GameplayBalance.Current;
            if(!balance.ValidOdds())throw new Exception("Item probabilities must total 100% with one entry per power-up.");
            var counts=new int[balance.itemPercent.Length];
            for(int i=0;i<10000;i++)counts[(int)balance.Roll((i+.5f)/10000)]++;
            for(int i=0;i<counts.Length;i++)
                if(counts[i]!=balance.itemPercent[i]*100)throw new Exception("Incorrect weighted item interval: "+(RallyWeapons.Power)i);
            if((int)balance.Roll(1)!=Array.FindLastIndex(balance.itemPercent,value=>value>0))throw new Exception("Upper random boundary is invalid");
            var roundTrip=JsonUtility.FromJson<RaceProtocol.Item>(JsonUtility.ToJson(new RaceProtocol.Item{type=(int)RallyWeapons.Power.Decoy,ammo=1,reveal=.85f}));
            if(new RallyWeapons.Item(roundTrip).Type!=RallyWeapons.Power.Decoy)throw new Exception("Decoy inventory replication failed");
            CarPowerCheck.Check();
            CheckRepairInventory();
            Directory.CreateDirectory("Builds");
            File.WriteAllText("Builds/balance-check.txt","Fixed odds: all 10,000 intervals match. Decoy inventory round trip, shield budget/expiry, full-health repair consumption and queue promotion passed. Minigun: 30 rounds, 10 damage; shield retains configured hit count. Edit-mode rules only; no gameplay simulation.");
        }

        static void CheckRepairInventory()
        {
            if(Application.isPlaying || PeerRace.Active || RaceStartSequence.Holding)
                throw new Exception("Run inventory checks in edit mode outside an active race.");
            // Inactive fixture: call item rules without Awake, Update, physics or a scene load.
            var fixture=new GameObject("Repair inventory check");
            fixture.SetActive(false);
            try
            {
                var car=fixture.AddComponent<TestCar>();
                var weapons=fixture.AddComponent<RallyWeapons>();
                fixture.AddComponent<CarPowerFeedback>();
                typeof(RallyWeapons).GetField("car",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(weapons,car);
                var repair=new RaceProtocol.Item{type=(int)RallyWeapons.Power.Repair,ammo=1,reveal=-1};
                var gun=new RaceProtocol.Item{type=(int)RallyWeapons.Power.Minigun,ammo=RallyWeapons.GunCapacity,reveal=-1};
                weapons.ApplyItems(repair,gun);
                weapons.FireAt(Vector3.zero,true,false);
                if(car.Health!=TestCar.MaxHealth || weapons.Active?.Type!=RallyWeapons.Power.Minigun || weapons.Rounds!=RallyWeapons.GunCapacity || weapons.Queued!=null)
                    throw new Exception("Full-health repair did not consume and promote the reserve.");
                weapons.ApplyItems(repair,null);
                weapons.FireAt(Vector3.zero,true,false);
                if(car.Health!=TestCar.MaxHealth || weapons.Active!=null)
                    throw new Exception("Full-health repair did not clear an unqueued slot.");
                if(RallyWeapons.GunCapacity!=30 || RallyWeapons.GunDamage!=10)
                    throw new Exception("Unexpected minigun tuning.");
            }
            finally { UnityEngine.Object.DestroyImmediate(fixture); }
        }
    }
}
