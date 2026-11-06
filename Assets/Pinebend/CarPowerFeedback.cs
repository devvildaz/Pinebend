using UnityEngine;

namespace Pinebend
{
    // Small world-space cues only; item selection continues to use the existing HUD.
    public sealed class CarPowerFeedback : MonoBehaviour
    {
        TestCar car;
        LineRenderer shield,repair,leftJet,rightJet;
        Material material;
        float repairUntil;
        LineRenderer[] jets;
        void Awake()
        {
            car=GetComponent<TestCar>();
            material=new Material(GetComponentInChildren<MeshRenderer>().sharedMaterial);
            shield=Line("Shield",new Color(.12f,.85f,1),.055f);
            repair=Line("Repair",new Color(.25f,1,.3f),.08f);
            foreach(var ring in new[]{shield,repair})
            {
                ring.loop=true;ring.positionCount=48;
                for(int i=0;i<48;i++)
                {
                    float angle=i*Mathf.PI*2/48;
                    ring.SetPosition(i,new Vector3(Mathf.Cos(angle)*1.2f,.35f,Mathf.Sin(angle)*2.15f));
                }
            }
            leftJet=Line("Left turbo",new Color(1,.55f,.1f),.16f);
            rightJet=Line("Right turbo",new Color(1,.55f,.1f),.16f);
            jets=new[]{leftJet,rightJet};
        }
        LineRenderer Line(string name,Color color,float width)
        {
            var obj=new GameObject(name);obj.transform.SetParent(transform,false);
            var line=obj.AddComponent<LineRenderer>();
            line.sharedMaterial=material;line.useWorldSpace=false;line.startColor=line.endColor=color;
            var tint=new MaterialPropertyBlock();tint.SetColor("_BaseColor",color);line.SetPropertyBlock(tint);
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            line.startWidth=width;line.endWidth=width*.6f;line.enabled=false;
            return line;
        }
        public void Repaired()=>repairUntil=Time.time+.6f;
        void LateUpdate()
        {
            shield.enabled=!car.Respawning && car.Shielded;
            repair.enabled=!car.Respawning && Time.time<repairUntil;
            repair.transform.localPosition=Vector3.up*(.6f-Mathf.Max(0,repairUntil-Time.time));
            int side=-1;
            foreach(var jet in jets)
            {
                jet.enabled=!car.Respawning && car.BoostThrust;
                if(jet.enabled)
                {
                    jet.positionCount=2;
                    jet.SetPosition(0,new Vector3(side*.55f,.06f,-1.85f));
                    jet.SetPosition(1,new Vector3(side*.55f,.06f,-2.8f-.3f*Mathf.Sin(Time.time*60)));
                }
                side=1;
            }
        }
        void OnDestroy(){if(material)Destroy(material);}
    }
}
