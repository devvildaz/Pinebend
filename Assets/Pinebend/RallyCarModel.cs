using UnityEngine;

namespace Pinebend
{
    [CreateAssetMenu(menuName="Pinebend/Car visual model")]
    public sealed class RallyCarModel : ScriptableObject
    {
        public GameObject bodyPrefab;
        public GameObject wheelPrefab;
        public Vector3 bodyOffset;
        public Vector3 bodyRotation;
        [Min(.001f)] public float bodyScale=1;
        [Min(.001f)] public float wheelScale=1;

        public void Install(Transform shell,Transform[] tires)
        {
            if(bodyPrefab) Replace(shell,bodyPrefab,bodyOffset,bodyRotation,bodyScale);
            if(wheelPrefab)
                foreach(var tire in tires) Replace(tire,wheelPrefab,Vector3.zero,Vector3.zero,wheelScale);
        }
        static void Replace(Transform socket,GameObject prefab,Vector3 offset,Vector3 angles,float scale)
        {
            if(prefab.GetComponentInChildren<Collider>(true) || prefab.GetComponentInChildren<Rigidbody>(true))
            {
                Debug.LogWarning("A car visual prefab contains physics components. Use a mesh-only prefab.",prefab);
                return;
            }
            foreach(Transform old in socket) old.gameObject.SetActive(false);
            var visual=Instantiate(prefab,socket,false).transform;
            visual.localPosition=offset;
            visual.localRotation=Quaternion.Euler(angles);
            visual.localScale*=scale;
        }
    }
}

