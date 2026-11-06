using UnityEngine;

namespace Pinebend
{
    public class RallyAmmoPickup : MonoBehaviour
    {
        Transform symbol;

        float readyAt;
        int networkId;
        static readonly System.Collections.Generic.Dictionary<int,RallyAmmoPickup> pickups=new();
        public static void Hide(int id,float seconds){if(pickups.TryGetValue(id,out var pickup) && pickup){pickup.readyAt=Time.time+seconds;pickup.symbol.gameObject.SetActive(false);}}
        void OnDestroy(){if(pickups.TryGetValue(networkId,out var current) && current==this)pickups.Remove(networkId);}

        public void Initialize(Transform display,int id)
        {
            symbol=display;networkId=id;pickups[id]=this;

        }

        void Update()
        {
            if (Time.time < readyAt) return;
            symbol.gameObject.SetActive(true);
            symbol.localRotation=Quaternion.Euler(0,Time.time*73,12);
            symbol.localPosition=Vector3.up*(.15f*Mathf.Sin(Time.time*3));
        }

        void OnTriggerEnter(Collider other)
        {
            if (!PeerRace.Authority || Time.time < readyAt || !other.attachedRigidbody) return;
            if (!other.attachedRigidbody.TryGetComponent<RallyWeapons>(out var weapons) || !weapons.Collect()) return;
            readyAt=Time.time+8;
            PeerRace.Publish(new RaceProtocol.Event{kind="pickup",id=networkId,value=8});
            symbol.gameObject.SetActive(false);
        }
    }
}

