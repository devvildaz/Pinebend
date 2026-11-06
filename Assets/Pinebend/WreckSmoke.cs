using System.Collections.Generic;
using UnityEngine;

namespace Pinebend
{
    public class WreckSmoke : MonoBehaviour
    {
        float duration=4;
        Material ownedMaterial;
        static readonly List<WreckSmoke> clouds=new();
        readonly List<Transform> puffs=new();
        float start;
        float Age => Time.time-start;
        float Density => Mathf.Clamp01(Age*6)*Mathf.Clamp01((duration-Age)/1.4f);

        public static GameObject Spawn(Vector3 center, Material material, float seconds=4)
        {
            var root=new GameObject("Wreck smoke");
            root.transform.position=center;
            var cloud=root.AddComponent<WreckSmoke>();
            cloud.start=Time.time;
            cloud.duration=seconds;
            cloud.ownedMaterial=new Material(material);
            for(int i=0;i<14;i++)
            {
                var puff=GameObject.CreatePrimitive(PrimitiveType.Sphere);
                var collider=puff.GetComponent<Collider>(); collider.enabled=false; Destroy(collider);
                puff.transform.SetParent(root.transform,false);
                var offset=Random.insideUnitSphere*2.4f;
                offset.y=Mathf.Abs(offset.y)*.65f;
                puff.transform.localPosition=offset;
                puff.GetComponent<Renderer>().sharedMaterial=cloud.ownedMaterial;
                cloud.puffs.Add(puff.transform);
            }
            RaceReplication.Attach(root,"smoke",seconds);
            return root;
        }

        public static float Obscurity(Vector3 position)
        {
            float amount=0;
            foreach(var cloud in clouds)
            {
                float distance=Vector3.Distance(position,cloud.transform.position+Vector3.up*.7f);
                amount=Mathf.Max(amount,cloud.Density*(1-Mathf.InverseLerp(1.6f,5,distance)));
            }
            return amount;
        }

        void OnDestroy(){if(ownedMaterial)Destroy(ownedMaterial);}
        void OnEnable()=>clouds.Add(this);
        void OnDisable()=>clouds.Remove(this);
        void Update()
        {
            if(Age>=duration){Destroy(gameObject);return;}
            foreach(var puff in puffs)
            {
                puff.localPosition+= (Vector3.up*.48f+puff.localPosition*.12f)*Time.deltaTime;
                puff.localScale=Vector3.one*(3.5f*Density);
            }
        }
    }
}
