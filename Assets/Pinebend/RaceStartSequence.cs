using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Pinebend
{
    [DefaultExecutionOrder(1000)]
    public sealed class RaceStartSequence : MonoBehaviour
    {
        public const float IntroSeconds=3.5f, CountdownSeconds=3;
        public const float Duration=IntroSeconds+CountdownSeconds;
        static RaceStartSequence instance;
        public static bool Holding => instance && !instance.released;
        public static bool Introducing => Holding && (!instance.begun || instance.Age<IntroSeconds);
        readonly Dictionary<Rigidbody,RigidbodyConstraints> locks=new();
        TestCar[] cars;
        TestCar player;
        Camera view;
        Vector3 gridCenter,gridForward;
        bool begun,released;
        float began;
        string error;
        GUIStyle countStyle,captionStyle;
        AudioSource speaker;
        AudioClip beep,go;
        int lastBeat=-1;
        float Age=>Time.unscaledTime-began;

        public void Initialize(TestCar[] racers,bool validGrid)
        {
            instance=this;
            cars=racers.Where(c=>c.gameObject.activeInHierarchy).ToArray();
            error=validGrid ? "" : "No safe starting grid was found. Choose another track.";
            foreach(var car in cars)
            {
                var body=car.GetComponent<Rigidbody>();
                if(body.isKinematic)continue;
                locks.Add(body,body.constraints);
                // Let suspension settle vertically, but prevent drift, steering and collisions from moving a grid slot.
                body.constraints=RigidbodyConstraints.FreezePositionX|RigidbodyConstraints.FreezePositionZ|RigidbodyConstraints.FreezeRotation;
                foreach(var wheel in car.wheels){wheel.motorTorque=0;wheel.brakeTorque=10000;}
                if(!validGrid)body.isKinematic=true;
            }
        }

        void Start()
        {
            player=cars.FirstOrDefault(c=>c.chase && PeerRace.IsLocal(c));
            if(player)view=player.chase;
            foreach(var car in cars)gridCenter+=car.transform.position;
            gridCenter/=Mathf.Max(1,cars.Length);
            gridForward=player ? player.transform.forward : Vector3.forward;
            speaker=gameObject.AddComponent<AudioSource>();speaker.spatialBlend=0;speaker.volume=.25f;
            beep=Tone(640,.14f);go=Tone(960,.28f);
            if(!PeerRace.Active)Begin();
        }

        public static void Begin()
        {
            if(!instance || instance.begun || !string.IsNullOrEmpty(instance.error))return;
            instance.begun=true;instance.began=Time.unscaledTime;
        }

        void Update()
        {
            if(!begun || released)return;
            int beat=Mathf.FloorToInt(Age-IntroSeconds);
            if(beat>=0 && beat<=3 && beat!=lastBeat)
            {
                lastBeat=beat;speaker.PlayOneShot(beat==3 ? go : beep);
            }
            if(Age<Duration || (PeerRace.Active && !PeerRace.Driving))return;
            foreach(var pair in locks)
            {
                if(!pair.Key)continue;
                pair.Key.constraints=pair.Value;
                pair.Key.linearVelocity=pair.Key.angularVelocity=Vector3.zero;
            }
            foreach(var car in cars)
                foreach(var wheel in car.wheels)wheel.brakeTorque=wheel.motorTorque=0;
            released=true;
        }

        void LateUpdate()
        {
            if(!Holding || !view || !player)return;
            float age=begun ? Age : 0;
            var chasePosition=player.transform.position+player.transform.rotation*new Vector3(0,3.5f,-7.3f);
            var chaseRotation=Quaternion.LookRotation(player.transform.position+player.transform.rotation*new Vector3(0,.9f,3.8f)-chasePosition);
            float sweep=Mathf.SmoothStep(0,1,Mathf.Clamp01(age/2.5f));
            var direction=Quaternion.AngleAxis(Mathf.Lerp(40,125,sweep),Vector3.up)*gridForward;
            var position=gridCenter+direction*Mathf.Max(15,cars.Length*2.5f)+Vector3.up*8;
            var rotation=Quaternion.LookRotation(gridCenter+Vector3.up*.4f-position);
            float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.5f,IntroSeconds,age));
            position=Vector3.Lerp(position,chasePosition,blend);
            // Keep the introduction camera above hilly roads and terrain.
            if(Physics.Raycast(position+Vector3.up*20,Vector3.down,out var hit,40,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                position.y=Mathf.Max(position.y,hit.point.y+1.2f);
            view.transform.SetPositionAndRotation(position,Quaternion.Slerp(rotation,chaseRotation,blend));
        }

        void OnGUI()
        {
            if(released && Age>Duration+1)return;
            countStyle ??= new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};
            captionStyle ??= new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,wordWrap=true};
            var saved=GUI.color;
            float w=Screen.width,h=Screen.height;
            captionStyle.fontSize=Mathf.Clamp(Mathf.RoundToInt(h*.032f),16,28);
            captionStyle.normal.textColor=Color.white;
            if(!string.IsNullOrEmpty(error))
            {
                GUI.Box(new Rect(20,h*.4f,w-40,130),GUIContent.none);
                GUI.Label(new Rect(30,h*.4f,w-60,75),error,captionStyle);
                if(GUI.Button(new Rect(w*.5f-90,h*.4f+80,180,36),"Track selection"))RaceSession.Menu();
                return;
            }
            if(!begun || Age<IntroSeconds)
            {
                GUI.color=new Color(0,0,0,.8f);
                GUI.DrawTexture(new Rect(0,0,w,52),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0,h-60,w,60),Texture2D.whiteTexture);
                GUI.color=Color.white;
                GUI.Label(new Rect(12,h-58,w-24,50),begun ? "STARTING GRID  •  "+cars.Length+" DRIVERS" : "WAITING FOR DRIVERS",captionStyle);
            }
            else
            {
                float tick=Age-IntroSeconds;
                bool green=tick>=CountdownSeconds;
                float pulse=green ? Mathf.Clamp01(tick-CountdownSeconds) : tick-Mathf.Floor(tick);
                countStyle.fontSize=Mathf.RoundToInt(Mathf.Lerp(120,86,pulse)*Mathf.Clamp(h/760f,.65f,1.5f));
                countStyle.normal.textColor=green ? new Color(.35f,1,.5f,1-pulse) : new Color(1,.78f,.2f);
                GUI.Label(new Rect(0,h*.23f,w,h*.32f),green ? "GO!" : Mathf.CeilToInt(CountdownSeconds-tick).ToString(),countStyle);
                for(int i=0;i<3;i++)
                {
                    GUI.color=green ? new Color(.25f,1,.4f,1-pulse) : i<=Mathf.FloorToInt(tick) ? new Color(1,.2f,.12f) : new Color(.15f,.17f,.2f);
                    GUI.DrawTexture(new Rect(w*.5f-66+i*46,h*.57f,40,12),Texture2D.whiteTexture);
                }
            }
            GUI.color=saved;
        }

        static AudioClip Tone(float frequency,float seconds)
        {
            const int rate=22050;
            var samples=new float[Mathf.CeilToInt(rate*seconds)];
            for(int i=0;i<samples.Length;i++)
                samples[i]=Mathf.Sin(i*2*Mathf.PI*frequency/rate)*Mathf.Min(1,i/(rate*.008f))*Mathf.Clamp01((samples.Length-i)/(rate*.04f));
            var clip=AudioClip.Create("Start signal",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }

        void OnDestroy()
        {
            if(instance==this)instance=null;
            foreach(var pair in locks)if(pair.Key)pair.Key.constraints=pair.Value;
            if(beep)Destroy(beep);if(go)Destroy(go);
        }
    }
}
