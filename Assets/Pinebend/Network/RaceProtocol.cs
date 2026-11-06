using System;
using UnityEngine;

namespace Pinebend
{
    public static class RaceProtocol
    {
        public const string Hello="PB-RALLY/7-BALANCE";
        [Serializable] public sealed class Input
        {
            public int sequence,launch,recover,target=-1;
            public float steer,pedal;
            public bool handbrake,gun,rear;
            public Vector3 aim;
            public bool Valid()=>sequence>=0 && launch>=0 && recover>=0 && Finite(steer) && Finite(pedal) &&
                Mathf.Abs(steer)<=1 && Mathf.Abs(pedal)<=1 && Finite(aim) && aim.sqrMagnitude<100000000 && target>=-1 && target<8;
        }
        [Serializable] public sealed class Item
        {
            public int type=-1,ammo;
            public float reveal;
        }
        [Serializable] public sealed class Car
        {
            public int slot,health,lap,gear,place;
            public Vector3 position,velocity;
            public Quaternion rotation;
            public float progress,elapsed,last,respawn,steer,slip;
            public bool hidden,shield,turbo,thrust,handbrake,drift,oil;
            public Item active,queued;
        }
        [Serializable] public sealed class World { public Car[] cars; public bool green; }
        [Serializable] public sealed class Start { public int slot,count; public TrackRecipe track; }
        [Serializable] public sealed class Event
        {
            public string kind;
            public int id,slot;
            public Vector3 position,end;
            public Quaternion rotation=Quaternion.identity;
            public float value;
        }
        public static bool Finite(float value)=>!float.IsNaN(value) && !float.IsInfinity(value);
        public static bool Finite(Vector3 value)=>Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
