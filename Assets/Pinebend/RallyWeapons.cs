using System.Collections.Generic;
using UnityEngine;

namespace Pinebend
{
    public class RallyWeapons : MonoBehaviour
    {
        public const int MissileCapacity = 2;
        public const int GunCapacity = 30;
        public const int GunDamage = 10;
        public enum Power { Missile, Minigun, Mine, Guided, Smoke, Oil, Shield, Turbo, Repair, Decoy }
        public sealed class Item
        {
            public readonly Power Type;
            public readonly float RevealAt;
            public int Ammo;
            public Item() {Type=GameplayBalance.Current.Roll(Random.value);RevealAt=Time.time+.85f;Ammo=Type==Power.Minigun ? GunCapacity : Type==Power.Missile ? MissileCapacity : 1;}
            public Item(RaceProtocol.Item state){Type=(Power)state.type;Ammo=state.ammo;RevealAt=Time.time+state.reveal;}
            public bool Rolling => Time.time < RevealAt;
            public string Preview
            {
                get
                {
                    Power showing = Rolling ? (Power)(Mathf.FloorToInt((RevealAt-Time.time)*12)%10) : Type;
                    return showing switch { Power.Missile => "MISSILE", Power.Minigun => "MINIGUN", Power.Guided => "GUIDED MISSILE", Power.Smoke => "SMOKE BOMB", Power.Oil => "OIL SPILL", Power.Shield => "SHIELD", Power.Turbo => "TURBO", Power.Repair => "REPAIR KIT", Power.Decoy => "DECOY FLARES", _ => "MINE" };
                }
            }
        }

        public Item Active { get; private set; }
        public Item Queued { get; private set; }
        int AmmoFor(Power type) => Active != null && !Active.Rolling && Active.Type == type ? Active.Ammo : 0;
        public int Missiles => AmmoFor(Power.Missile);
        public int Rounds => AmmoFor(Power.Minigun);
        public TestCar LockedTarget { get; private set; }
        public int Guided => AmmoFor(Power.Guided);
        public int Mines => AmmoFor(Power.Mine);
        public int SmokeBombs => AmmoFor(Power.Smoke);
        public int OilSpills => AmmoFor(Power.Oil);
        readonly List<(Transform model,float armedAt)> mines = new();
        public int Shields=>AmmoFor(Power.Shield);
        public int Turbos=>AmmoFor(Power.Turbo);
        public int Repairs=>AmmoFor(Power.Repair);
        public int Decoys=>AmmoFor(Power.Decoy);
        static readonly HashSet<RallyWeapons> sources=new();
        void OnEnable()=>sources.Add(this);
        void OnDisable()=>sources.Remove(this);
        float nextPickup;
        TestCar car;
        Rigidbody owner;
        Material orange, ivory, aqua, soot;
        public Material SmokeMaterial=>soot;
        float nextBullet;
        const float ShotRange = 210;
        readonly List<Round> rockets = new();
        readonly List<Pulse> effects = new();
        readonly HashSet<Rigidbody> pushed = new();

        class Round
        {
            public Transform model;
            public Vector3 direction;
            public TestCar target;
            public Transform lure;
            public float remaining = ShotRange;
        }

        struct Pulse
        {
            public Transform visual;
            public float start, life;
            public Vector3 scale, velocity;
        }

        void Awake()
        {
            car = GetComponent<TestCar>();
            owner = GetComponent<Rigidbody>();
            Material basis = GetComponentInChildren<MeshRenderer>().sharedMaterial;
            orange = Tint(basis,new Color(1,.36f,.05f));
            ivory = Tint(basis,new Color(1,.91f,.62f));
            aqua = Tint(basis,new Color(.1f,.88f,.94f));
            soot = Tint(basis,new Color(.20f,.21f,.22f));
            if (!PeerRace.BuildPickups(car)) return;

            AddRow(24);
            AddRow(76);
        }

        Material Tint(Material basis, Color color)
        {
            var material = new Material(basis);
            material.color = color;
            return material;
        }

        static Transform Shape(PrimitiveType shape, string label, Vector3 size, Material material)
        {
            var obj = GameObject.CreatePrimitive(shape);
            obj.name = label;
            var collider = obj.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            obj.GetComponent<MeshRenderer>().sharedMaterial = material;
            obj.transform.localScale = size;
            return obj.transform;
        }

        void AddRow(int marker)
        {
            Transform anchor = car.route[marker];
            for (int lane = -1; lane <= 1; lane++)
            {
                var station = new GameObject("Mystery box");
                station.transform.SetPositionAndRotation(anchor.position + anchor.right * (lane * 3.5f) + Vector3.up, anchor.rotation);
                var trigger = station.AddComponent<BoxCollider>();
                trigger.size = new Vector3(1.7f, 2.4f, 1.7f);
                trigger.isTrigger = true;
                Transform box = Shape(PrimitiveType.Cube,"Mystery cube",Vector3.one*1.15f,aqua);
                box.SetParent(station.transform,false);
                for (int face = 0; face < 4; face++)
                {
                    var glyph = new GameObject("Question mark").AddComponent<TextMesh>();
                    glyph.transform.SetParent(box,false);
                    glyph.transform.localRotation = Quaternion.Euler(0,90*face,0);
                    glyph.transform.localPosition = glyph.transform.localRotation * Vector3.back * .51f;
                    glyph.text = "?";
                    glyph.anchor = TextAnchor.MiddleCenter;
                    glyph.fontSize = 48;
                    glyph.characterSize = .17f;
                }
                station.AddComponent<RallyAmmoPickup>().Initialize(box,marker*3+lane+1);
            }
        }

        public bool Collect()
        {
            if (!PeerRace.Authority || RaceStartSequence.Holding || car.Respawning || car.FinishOrder>0 || Queued != null || Time.time < nextPickup) return false;
            if (Active == null) Active = new Item();
            else Queued = new Item();
            nextPickup = Time.time + .45f;
            return true;
        }

        public RaceProtocol.Item CaptureItem(bool queued)
        {
            var item=queued ? Queued : Active;
            return item==null ? new RaceProtocol.Item() : new RaceProtocol.Item{type=(int)item.Type,ammo=item.Ammo,reveal=Mathf.Max(0,item.RevealAt-Time.time)};
        }
        public void ApplyItems(RaceProtocol.Item active,RaceProtocol.Item queued)
        {
            Active=active==null || active.type<0 ? null : new Item(active);
            Queued=queued==null || queued.type<0 ? null : new Item(queued);
        }
        void SpendRound()
        {
            if (--Active.Ammo > 0) return;
            Active = Queued;
            Queued = null;
        }

        public bool CanHit(TestCar rival)
        {
            Vector3 start=transform.TransformPoint(new Vector3(0,.65f,1.95f));
            Vector3 delta=rival.transform.position+Vector3.up*.2f-start;
            return FirstHit(new Ray(start,delta),delta.magnitude+1,out var hit) && hit.rigidbody==rival.GetComponent<Rigidbody>();
        }

        public static bool GuidedThreatNearby(TestCar target)
        {
            foreach(var source in sources)
            foreach(var rocket in source.rockets)
                if(rocket.target==target && (rocket.model.position-target.transform.position).sqrMagnitude<45*45)return true;
            return false;
        }

        public static bool ThreatNearby(TestCar target)
        {
            foreach(var source in sources)
            {
                if(!source || !source.car)continue;
                foreach(var rocket in source.rockets)
                {
                    Vector3 delta=target.transform.position+Vector3.up*.2f-rocket.model.position;
                    if(delta.sqrMagnitude>32*32)continue;
                    float ahead=Vector3.Dot(delta,rocket.direction);
                    if(rocket.target==target || (ahead>0 && (delta-rocket.direction*ahead).sqrMagnitude<16))return true;
                }
                foreach(var mine in source.mines)
                    if(Time.time>=mine.armedAt && (mine.model.position-target.transform.position).sqrMagnitude<49)return true;
                if(source.car==target || source.car.Respawning || source.Rounds==0)continue;
                var local=source.transform.InverseTransformPoint(target.transform.position);
                if(local.z>0 && local.z<36 && Mathf.Abs(local.x)<2 && Mathf.Abs(local.y)<2 && source.CanHit(target))return true;
            }
            return false;
        }
        bool VisibleFrom(Camera camera, TestCar rival)
        {
            Vector3 delta = rival.transform.position + Vector3.up*.2f - camera.transform.position;
            return delta.sqrMagnitude < ShotRange*ShotRange &&
                FirstHit(new Ray(camera.transform.position,delta),delta.magnitude+2,out var hit) &&
                hit.rigidbody == rival.GetComponent<Rigidbody>();
        }

        public void SelectTarget(Vector2 sight)
        {
            if (Guided == 0 || car.Respawning) return;
            float best = Screen.height * .052f;
            TestCar selection = null;
            foreach (var rival in TestCar.Racers)
            {
                if (rival == car || rival.Respawning) continue;
                Vector3 screen = car.chase.WorldToScreenPoint(rival.transform.position+Vector3.up*.2f);
                float error = Vector2.Distance(sight,(Vector2)screen);
                if (screen.z <= 0 || error >= best || !VisibleFrom(car.chase,rival)) continue;
                best = error;
                selection = rival;
            }
            LockedTarget = selection;
        }

        public void Command(Vector2 sight, bool launch, bool fire)
        {
            Vector3 target = Vector3.zero;
            if (launch && Missiles > 0)
            {
                Ray ray = car.chase.ScreenPointToRay(sight);
                target = FirstHit(ray, ShotRange, out var hit) ? hit.point : ray.GetPoint(ShotRange);
            }
            if (Guided == 0 || (LockedTarget && (LockedTarget.Respawning || car.chase.WorldToViewportPoint(LockedTarget.transform.position).z<=0 || !VisibleFrom(car.chase,LockedTarget)))) LockedTarget=null;
            if(!PeerRace.Authority){PeerRace.Weapon(target,launch,fire,car.LookingBack,LockedTarget);return;}
            FireAt(target, launch, fire, LockedTarget);
        }

        public void FireAt(Vector3 target, bool launch, bool fire, TestCar selected = null)
        {
            if (!PeerRace.Authority || RaceStartSequence.Holding || car.Respawning || car.FinishOrder>0) return;
            if(launch && Decoys>0)
            {
                CountermeasureBurst.Spawn(transform.TransformPoint(new Vector3(0,.6f,-2.3f)),transform.rotation,owner.linearVelocity,car,ivory);
                SpendRound();return;
            }
            if(launch && (Shields>0 || Turbos>0 || Repairs>0))
            {
                bool used=Shields>0 ? car.UseShield() : Turbos>0 ? car.UseTurbo() : car.Repair();
                if(used)
                {
                    if(Repairs>0){GetComponent<CarPowerFeedback>().Repaired();PeerRace.Publish(new RaceProtocol.Event{kind="repair",slot=PeerRace.Slot(car)});}
                    SpendRound();
                }
                return;
            }
            if (launch && (SmokeBombs>0 || OilSpills>0))
            {
                Vector3 position=transform.TransformPoint(new Vector3(0,0,-3));
                if(!FirstHit(new Ray(position+Vector3.up*2,Vector3.down),7,out var ground) || ground.rigidbody) return;
                if(SmokeBombs>0) WreckSmoke.Spawn(ground.point,soot,10);
                else OilSpill.Spawn(ground.point,ground.normal,soot);
                SpendRound();return;
            }
            if (launch && Mines > 0)
            {
                if(DropMine())SpendRound();
                return;
            }
            bool guided = Guided > 0 && selected && !selected.Respawning;
            if (launch && (Missiles > 0 || guided))
            {
                if (guided) target = selected.transform.position + Vector3.up*.2f;
                Vector3 muzzle = transform.TransformPoint(new Vector3(0,1.18f,car.LookingBack ? -.95f : .45f));
                Vector3 direction = (target-muzzle).normalized;
                var model = Shape(PrimitiveType.Capsule,"Missile",new Vector3(.20f,.48f,.20f),orange);
                model.SetPositionAndRotation(muzzle,Quaternion.FromToRotation(Vector3.up,direction));
                RaceReplication.Attach(model.gameObject,"rocket");
                rockets.Add(new Round {model=model,direction=direction,target=guided ? selected : null});
                LockedTarget=null;
                SpendRound();
                return;
            }
            if (!fire || Time.time < nextBullet || Rounds == 0) return;
            nextBullet = Time.time + .085f;
            SpendRound();
            Vector3 start = transform.TransformPoint(new Vector3(.47f,.40f,1.95f));
            Ray shot = new Ray(start, transform.forward);
            Vector3 end = shot.GetPoint(ShotRange);
            if (FirstHit(shot,ShotRange,out var impact))
            {
                end = impact.point;
                if (impact.rigidbody)
                {
                    bool unblocked=!impact.rigidbody.TryGetComponent<TestCar>(out var victim) || victim.TakeDamage(GunDamage);
                    if(unblocked && !impact.rigidbody.isKinematic)impact.rigidbody.AddForceAtPosition(shot.direction*90,end,ForceMode.Impulse);
                }
                Flash(end,Vector3.one*.28f,.11f,ivory,Vector3.zero);
            }
            RemoteShot(start,end);
            PeerRace.Publish(new RaceProtocol.Event{kind="shot",slot=PeerRace.Slot(car),position=start,end=end});
        }

        bool FirstHit(Ray ray,float distance,out RaycastHit nearest)
        {
            nearest = default;
            float best = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(ray,distance,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
            {
                if (hit.rigidbody == owner || hit.distance >= best) continue;
                nearest = hit;
                best = hit.distance;
            }
            return best < float.PositiveInfinity;
        }

        bool DropMine()
        {
            Vector3 position = transform.TransformPoint(new Vector3(0,0,-2.7f));
            if(!car.Grounded || !FirstHit(new Ray(position+Vector3.up*2,Vector3.down),6,out var ground) || ground.rigidbody)return false;
            position=ground.point;
            var model = Shape(PrimitiveType.Cylinder,"Pressure mine",new Vector3(.9f,.11f,.9f),soot);
            model.position = position + Vector3.up*.11f;
            var lamp = Shape(PrimitiveType.Sphere,"Arming light",new Vector3(.42f,.70f,.42f),orange);
            lamp.SetParent(model,false);
            lamp.localPosition = Vector3.up*.85f;
            mines.Add((model,Time.time+.75f));
            RaceReplication.Attach(model.gameObject,"mine");
            return true;
        }

        void FixedUpdate()
        {
            if(!PeerRace.Authority)return;
            for (int i=mines.Count-1; i>=0; i--)
            {
                var mine = mines[i];
                if (Time.time < mine.armedAt) continue;
                bool triggered=false;
                foreach(var racer in TestCar.Racers)
                    if(!racer.Respawning && (racer.transform.position-mine.model.position).sqrMagnitude < 5.76f)
                    { triggered=true; break; }
                if (!triggered) continue;
                if (triggered) Explode(mine.model.position);
                Destroy(mine.model.gameObject);
                mines.RemoveAt(i);
            }
            for (int i=rockets.Count-1; i>=0; i--)
            {
                Round rocket = rockets[i];
                if(rocket.target && !rocket.target.Respawning)
                {
                    var lure=CountermeasureBurst.FindLure(rocket.target,rocket.model.position,rocket.direction);
                    if(lure){rocket.lure=lure;rocket.target=null;}
                }
                if (rocket.lure || (rocket.target && !rocket.target.Respawning))
                {
                    Vector3 destination = rocket.lure ? rocket.lure.position : rocket.target.transform.position + Vector3.up*.2f;
                    Vector3 desired = (destination-rocket.model.position).normalized;
                    rocket.direction = Vector3.RotateTowards(rocket.direction,desired,4.8f*Time.fixedDeltaTime,0);
                    rocket.model.rotation = Quaternion.FromToRotation(Vector3.up,rocket.direction);
                }
                else rocket.target = null;
                var path = new Ray(rocket.model.position,rocket.direction);
                float step = Mathf.Min(rocket.remaining,90*Time.fixedDeltaTime);
                bool hit = FirstHit(path,step,out var impact);
                bool lureHit=rocket.lure && Vector3.Distance(rocket.model.position,rocket.lure.position)<Mathf.Max(1.2f,step);
                rocket.remaining -= step;
                rocket.model.position = hit ? impact.point : path.GetPoint(step);
                if (!hit && !lureHit && rocket.remaining > .001f) continue;
                Explode(rocket.model.position);
                Destroy(rocket.model.gameObject);
                rockets.RemoveAt(i);
            }
        }

        void Explode(Vector3 center)
        {
            RallyRoad.Instance?.Crater(center);
            const float radius = 11;
            pushed.Clear();
            foreach (var collider in Physics.OverlapSphere(center,radius,Physics.AllLayers,QueryTriggerInteraction.Ignore))
            {
                var rb = collider.attachedRigidbody;
                if (!rb || !pushed.Add(rb)) continue;
                bool unblocked=true;
                if (rb.TryGetComponent<TestCar>(out var victim))
                {
                    float distance = Vector3.Distance(center,rb.worldCenterOfMass);
                    int damage = Mathf.RoundToInt(40 * (1-Mathf.InverseLerp(3,radius,distance)));
                    unblocked=victim.TakeDamage(damage,true);
                }
                if(unblocked && !rb.isKinematic)rb.AddExplosionForce(2600,center,radius,.55f,ForceMode.Impulse);
            }
            Burst(center);
        }

        public void Wreck(Vector3 center)
        {
            Burst(center);
            Flash(center+Vector3.up*.4f,Vector3.one*4.5f,.25f,orange,Vector3.up);
            WreckSmoke.Spawn(center,soot);
            RallyRoad.Instance?.Crater(center);
        }

        void Burst(Vector3 center)
        {
            PeerRace.Publish(new RaceProtocol.Event{kind="burst",slot=PeerRace.Slot(car),position=center});
            Flash(center+Vector3.up*.15f,Vector3.one*2.5f,.23f,ivory,Vector3.up);
            for (int i=0; i<16; i++)
            {
                Vector3 direction = Random.onUnitSphere;
                direction.y = Mathf.Abs(direction.y);
                Flash(center,Vector3.one*Random.Range(.35f,.95f),.38f,orange,direction*Random.Range(8,18));
            }
        }

        public void RemoteBurst(Vector3 center)=>Burst(center);
        public void RemoteShot(Vector3 start,Vector3 end)
        {
            var delta=end-start;
            if(delta.sqrMagnitude<.001f)return;
            Flash((start+end)*.5f,new Vector3(.038f,.038f,delta.magnitude),.075f,ivory,Vector3.zero).rotation=Quaternion.LookRotation(delta);
        }
        public GameObject RemoteProp(string kind,Vector3 position,Quaternion rotation)
        {
            var model=kind=="rocket" ? Shape(PrimitiveType.Capsule,"Network missile",new Vector3(.20f,.48f,.20f),orange) : Shape(PrimitiveType.Cylinder,"Network mine",new Vector3(.9f,.11f,.9f),soot);
            model.SetPositionAndRotation(position,rotation);
            if(kind=="mine")
            {
                var lamp=Shape(PrimitiveType.Sphere,"Arming light",new Vector3(.42f,.70f,.42f),orange);
                lamp.SetParent(model,false);lamp.localPosition=Vector3.up*.85f;
            }
            return model.gameObject;
        }
        Transform Flash(Vector3 pos,Vector3 size,float life,Material material,Vector3 velocity)
        {
            var visual = Shape(PrimitiveType.Cube,"Weapon effect",size,material);
            visual.position = pos;
            effects.Add(new Pulse {visual=visual,start=Time.time,life=life,scale=size,velocity=velocity});
            return visual;
        }

        void LateUpdate()
        {
            for (int i=effects.Count-1; i>=0; i--)
            {
                var pulse = effects[i];
                float progress = (Time.time-pulse.start)/pulse.life;
                if (progress >= 1)
                {
                    Destroy(pulse.visual.gameObject);
                    effects.RemoveAt(i);
                    continue;
                }
                pulse.visual.position += pulse.velocity*Time.deltaTime;
                pulse.visual.localScale = pulse.velocity == Vector3.zero ? pulse.scale : pulse.scale*(1-progress);
            }
        }

        void OnDestroy()
        {
            Destroy(soot);
            Destroy(orange);
            Destroy(ivory);
            Destroy(aqua);
        }
    }
}






