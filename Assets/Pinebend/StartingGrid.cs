using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Pinebend
{
    public static class StartingGrid
    {
        public delegate bool GroundProbe(Vector3 point,Quaternion rotation,out Vector3 spawn);
        public const float LaneOffset=2.6f, RowSpacing=7;

        // Reserve all slots before moving any car. Marker density never determines grid spacing.
        public static bool Plan(Vector3[] route,int count,float radius,GroundProbe probe,
            Func<Pose,bool> unobstructed,out Pose[] slots)
        {
            var reserved=new List<Pose>();
            float lap=0;
            for(int i=0;i<route.Length;i++)lap+=Vector3.Distance(route[i],route[(i+1)%route.Length]);
            float limit=Mathf.Min(lap*.23f,180);
            for(float distance=6;distance<limit && reserved.Count<count;distance+=RowSpacing)
            {
                Pose center=BehindStart(route,distance);
                foreach(float lane in new[]{-LaneOffset,LaneOffset})
                {
                    var candidate=center.position+center.rotation*Vector3.right*lane;
                    if(!probe(candidate,center.rotation,out var spawn))continue;
                    var pose=new Pose(spawn,center.rotation);
                    if(reserved.Any(p=>Vector3.ProjectOnPlane(p.position-spawn,Vector3.up).sqrMagnitude<4*radius*radius))continue;
                    if(!unobstructed(pose))continue;
                    reserved.Add(pose);
                    if(reserved.Count==count)break;
                }
            }
            slots=reserved.ToArray();
            return slots.Length==count;
        }

        public static Pose BehindStart(Vector3[] route,float distance)
        {
            for(int i=route.Length-1;i>=0;i--)
            {
                var start=route[i];var end=route[(i+1)%route.Length];
                float length=Vector3.Distance(start,end);
                if(distance<=length)
                {
                    var forward=Vector3.ProjectOnPlane(end-start,Vector3.up).normalized;
                    return new Pose(Vector3.Lerp(end,start,distance/Mathf.Max(.001f,length)),Quaternion.LookRotation(forward));
                }
                distance-=length;
            }
            throw new ArgumentOutOfRangeException(nameof(distance));
        }

        public static bool Place(TestCar[] racers)
        {
            // Preserve the solo player's last grid position. Network slots use the same name order on every peer.
            var ordered=racers.Where(c=>c.gameObject.activeInHierarchy)
                .OrderBy(c=>PeerRace.Active ? 0 : c.GetComponent<RallyBot>() ? 0 : 1)
                .ThenBy(c=>c.name,StringComparer.Ordinal).ToArray();
            if(ordered.Length==0)return false;
            var bodies=new HashSet<Rigidbody>(ordered.Select(c=>c.GetComponent<Rigidbody>()));
            float radius=2.35f;
            Vector3 half=new Vector3(1.02f,.5f,2.1f);
            foreach(var car in ordered)
            {
                var hull=car.GetComponent<BoxCollider>();
                if(!hull)continue;
                var size=Vector3.Scale(hull.size,car.transform.lossyScale)*.5f;
                half=Vector3.Max(half,size+Vector3.one*.15f);
                radius=Mathf.Max(radius,new Vector2(size.x,size.z).magnitude+.35f);
            }
            bool Clear(Pose pose)
            {
                foreach(var hit in Physics.OverlapBox(pose.position+Vector3.up*.2f,half,pose.rotation,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                    if(!hit.attachedRigidbody || !bodies.Contains(hit.attachedRigidbody))return false;
                return true;
            }
            Physics.SyncTransforms();
            if(!Plan(Array.ConvertAll(ordered[0].route,t=>t.position),ordered.Length,radius,RallyRoad.Instance.TrySpawn,Clear,out var slots))return false;
            for(int i=0;i<ordered.Length;i++)
            {
                var body=ordered[i].GetComponent<Rigidbody>();
                ordered[i].transform.SetPositionAndRotation(slots[i].position,slots[i].rotation);
                body.position=slots[i].position;body.rotation=slots[i].rotation;
                if(!body.isKinematic)body.linearVelocity=body.angularVelocity=Vector3.zero;
            }
            Physics.SyncTransforms();
            return true;
        }
    }
}
