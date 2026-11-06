using System.Collections.Generic;
using UnityEngine;

namespace Pinebend
{
    // Harmless flares are guidance targets, not invulnerability or physical obstacles.
    public sealed class CountermeasureBurst : MonoBehaviour
    {
        public const float Duration=3, AttractionRadius=40;
        static readonly List<CountermeasureBurst> bursts=new();
        readonly List<Transform> flares=new();
        readonly List<Vector3> velocities=new();
        TestCar owner;
        Material material;
        float born;

        public static GameObject Spawn(Vector3 point,Quaternion rotation,Vector3 inheritedVelocity,TestCar source,Material basis)
        {
            var obj=new GameObject("Countermeasure flares");obj.transform.SetPositionAndRotation(point,rotation);
            var burst=obj.AddComponent<CountermeasureBurst>();burst.owner=source;burst.born=Time.time;
            burst.material=new Material(basis){color=new Color(1,.85f,.35f)};
            for(int i=0;i<6;i++)
            {
                float side=i%2==0 ? -1 : 1;
                var flare=GameObject.CreatePrimitive(PrimitiveType.Sphere);
                var collider=flare.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
                flare.name="Flare";flare.transform.SetParent(obj.transform,false);
                flare.transform.localPosition=new Vector3(side*(.85f+i*.12f),.2f,-.2f);
                flare.transform.localScale=Vector3.one*.26f;
                flare.GetComponent<Renderer>().sharedMaterial=burst.material;
                burst.flares.Add(flare.transform);
                burst.velocities.Add(Quaternion.Inverse(rotation)*inheritedVelocity*.35f+new Vector3(side*(8+i*.8f),3+i*.35f,-8-i));
            }
            RaceReplication.Attach(obj,"decoys",Duration,inheritedVelocity);
            return obj;
        }
        public static Transform FindLure(TestCar target,Vector3 missile,Vector3 heading)
        {
            Transform best=null;float nearest=AttractionRadius*AttractionRadius;
            foreach(var burst in bursts)
            {
                if(burst.owner!=target || Time.time-burst.born>=Duration)continue;
                foreach(var flare in burst.flares)
                {
                    Vector3 delta=flare.position-missile;
                    if(delta.sqrMagnitude>=nearest || Vector3.Dot(delta.normalized,heading)<.15f)continue;
                    if(Physics.Linecast(missile,flare.position,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))continue;
                    nearest=delta.sqrMagnitude;best=flare;
                }
            }
            return best;
        }
        void OnEnable()=>bursts.Add(this);
        void OnDisable()=>bursts.Remove(this);
        void Update()
        {
            float age=Time.time-born;
            if(age>=Duration){Destroy(gameObject);return;}
            for(int i=0;i<flares.Count;i++)
            {
                float side=i%2==0 ? -1 : 1;
                flares[i].localPosition=new Vector3(side*(.85f+i*.12f),.2f,-.2f)+velocities[i]*age+(Quaternion.Inverse(transform.rotation)*Vector3.down)*1.5f*age*age;
                flares[i].localScale=Vector3.one*(.26f*Mathf.Clamp01((Duration-age)*2));
            }
        }
        void OnDestroy(){if(material)Destroy(material);}
    }
}
