using UnityEngine;

namespace Pinebend
{
    public sealed class ReplicatedProp : MonoBehaviour
    {
        public int id;
        public bool moving;
        Vector3 point;
        Quaternion facing;
        bool received;
        public void Target(Vector3 position,Quaternion rotation){point=position;facing=rotation;received=true;}
        void Update()
        {
            if(PeerRace.Authority || !moving || !received)return;
            float blend=1-Mathf.Exp(-45*Time.deltaTime);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position,point,blend),Quaternion.Slerp(transform.rotation,facing,blend));
        }
        void OnDestroy()=>RaceReplication.Removed(this);
    }
}
