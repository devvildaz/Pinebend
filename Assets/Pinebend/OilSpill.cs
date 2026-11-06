using System.Collections.Generic;
using UnityEngine;

namespace Pinebend
{
    public sealed class OilSpill : MonoBehaviour
    {
        public const float Lifetime=60;
        public const int MaxPasses=5;
        public int Passes { get; private set; }
        float born;
        Material material;
        readonly HashSet<TestCar> inside=new();
        readonly Dictionary<TestCar,Vector3> previous=new();

        public static GameObject Spawn(Vector3 point,Vector3 normal,Material basis,bool replicated=false)
        {
            var root=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name="Oil spill";
            var collider=root.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            root.transform.SetPositionAndRotation(point+normal*(replicated ? 0 : .025f),Quaternion.FromToRotation(Vector3.up,normal));
            root.transform.localScale=new Vector3(5.4f,.015f,5.4f);
            var spill=root.AddComponent<OilSpill>();
            spill.born=Time.time;
            spill.material=new Material(basis){color=new Color(.035f,.045f,.06f)};
            root.GetComponent<Renderer>().sharedMaterial=spill.material;
            RaceReplication.Attach(root,"oil");
            return root;
        }

        void FixedUpdate()
        {
            if(!PeerRace.Authority)return;
            if(Time.time-born>=Lifetime){Destroy(gameObject);return;}
            foreach(var car in TestCar.Racers)
            {
                if(!car)continue;
                // Unscaled coordinates let the contact radius remain independent of the visual.
                Vector3 current=Quaternion.Inverse(transform.rotation)*(car.transform.position-transform.position);
                bool grounded=!car.Respawning && System.Array.Exists(car.wheels,w=>w.isGrounded);
                bool wasInside=inside.Contains(car);
                bool touching=grounded && Mathf.Abs(current.y)<1.6f && new Vector2(current.x,current.z).sqrMagnitude<(wasInside ? 10.89f : 7.29f);
                bool crossed=false;
                if(grounded && !wasInside && previous.TryGetValue(car,out var last) && (current-last).sqrMagnitude<400)
                {
                    Vector3 delta=current-last;
                    float t=Mathf.Clamp01(-(last.x*delta.x+last.z*delta.z)/Mathf.Max(.001f,delta.x*delta.x+delta.z*delta.z));
                    Vector3 nearest=last+delta*t;
                    crossed=Mathf.Abs(nearest.y)<1.6f && nearest.x*nearest.x+nearest.z*nearest.z<7.29f;
                }
                previous[car]=current;
                // Give the dropping car time to leave. It can be affected on later laps.
                if(Time.time-born<.75f)continue;
                if(touching || crossed)
                {
                    car.ApplyOil();
                    if(!wasInside && ++Passes>=MaxPasses){Destroy(gameObject);return;}
                }
                if(touching)inside.Add(car);else inside.Remove(car);
            }
        }

        void OnDestroy(){if(material)Destroy(material);}
    }
}
