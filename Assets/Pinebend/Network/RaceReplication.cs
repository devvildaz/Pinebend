using System.Collections.Generic;
using UnityEngine;

namespace Pinebend
{
    public static class RaceReplication
    {
        static int nextId;
        static readonly Dictionary<int,ReplicatedProp> props=new();
        public static void Reset(){props.Clear();nextId=0;}
        public static void Attach(GameObject obj,string kind,float value=0,Vector3 velocity=default)
        {
            if(!PeerRace.Active || !PeerRace.Authority)return;
            var prop=obj.AddComponent<ReplicatedProp>();prop.id=++nextId;prop.moving=kind=="rocket";
            props[prop.id]=prop;
            PeerRace.Publish(new RaceProtocol.Event{kind=kind,id=prop.id,position=obj.transform.position,rotation=obj.transform.rotation,value=value,end=velocity});
        }
        public static void Removed(ReplicatedProp prop)
        {
            if(!props.TryGetValue(prop.id,out var current) || current!=prop)return;
            props.Remove(prop.id);
            if(PeerRace.Active && PeerRace.Authority)PeerRace.Publish(new RaceProtocol.Event{kind="remove",id=prop.id});
        }
        public static void SendMovingProps()
        {
            foreach(var pair in props)
                if(pair.Value && pair.Value.moving)PeerRace.Publish(new RaceProtocol.Event{kind="move",id=pair.Key,position=pair.Value.transform.position,rotation=pair.Value.transform.rotation});
        }
        public static void Receive(RaceProtocol.Event message)
        {
            if(message==null || PeerRace.Authority)return;
            if(message.kind=="remove")
            {
                if(props.TryGetValue(message.id,out var removed) && removed)Object.Destroy(removed.gameObject);
                props.Remove(message.id);return;
            }
            if(message.kind=="move")
            {
                if(props.TryGetValue(message.id,out var moved) && moved)moved.Target(message.position,message.rotation);
                return;
            }
            if(message.kind=="pickup"){RallyAmmoPickup.Hide(message.id,message.value);return;}
            if(message.kind=="crater"){RallyRoad.Instance?.Crater(message.position);return;}
            var car=PeerRace.CarAt(message.slot) ?? PeerRace.CarAt(0);
            if(!car)return;
            if(message.kind=="repair"){car.GetComponent<CarPowerFeedback>().Repaired();return;}
            var weapons=car.GetComponent<RallyWeapons>();
            if(message.kind=="burst"){weapons.RemoteBurst(message.position);return;}
            if(message.kind=="shot"){weapons.RemoteShot(message.position,message.end);return;}
            if(props.ContainsKey(message.id))return;
            GameObject obj;
            if(message.kind=="decoys")obj=CountermeasureBurst.Spawn(message.position,message.rotation,message.end,null,weapons.SmokeMaterial);
            else if(message.kind=="smoke")obj=WreckSmoke.Spawn(message.position,weapons.SmokeMaterial,message.value);
            else if(message.kind=="oil")obj=OilSpill.Spawn(message.position,message.rotation*Vector3.up,weapons.SmokeMaterial,true);
            else if(message.kind=="rocket" || message.kind=="mine")obj=weapons.RemoteProp(message.kind,message.position,message.rotation);
            else return;
            var prop=obj.AddComponent<ReplicatedProp>();prop.id=message.id;prop.moving=message.kind=="rocket";
            props[prop.id]=prop;
        }
    }
}
