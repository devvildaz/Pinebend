using UnityEngine;
using UnityEngine.InputSystem;

namespace Pinebend
{
    public class TestCar : MonoBehaviour
    {
        public WheelCollider[] wheels;
        public Transform[] tires;
        public Transform[] route;
        public Camera chase;

        [SerializeField, Range(.15f, .8f)] float slidingGrip = .32f;
        [SerializeField] float gripRecovery = 1.65f;
        [SerializeField] float handbrakeTorque = 1800f;

        public static readonly System.Collections.Generic.List<TestCar> Racers = new();
        RallyBot bot;
        RallyWeapons weapons;
        int segment;
        bool routeLocated;
        float oilyUntil;
        readonly CarPowerState powerState=new();
        public bool Shielded=>PeerRace.IsProxy(this) ? networkShield : powerState.Shielded(Time.time);
        public bool Boosting=>PeerRace.IsProxy(this) ? networkTurbo : powerState.Boosting(Time.time);
        public bool Oiled=>PeerRace.IsProxy(this) ? networkOil : Time.time<oilyUntil;
        public bool BoostThrust { get; private set; }
        public bool Grounded=>System.Array.Exists(wheels,w=>w.isGrounded);
        public bool UseShield()=>PeerRace.Authority && !Respawning && !RaceStartSequence.Holding && powerState.ActivateShield(Time.time);
        public bool UseTurbo()=>PeerRace.Authority && !Respawning && !RaceStartSequence.Holding && powerState.ActivateTurbo(Time.time);
        public bool Repair()
        {
            if(!PeerRace.Authority || Respawning || RaceStartSequence.Holding)return false;
            Health=powerState.RepairedHealth(Health,MaxHealth);return true;
        }
        float[] frontGrip, forwardGrip;
        public void ApplyOil(){if(PeerRace.Authority)oilyUntil=Time.time+2.5f;}
        float raceProgress;
        public const int MaxHealth = 100;
        public int Health { get; private set; } = MaxHealth;
        public bool Respawning { get; private set; }
        public float RespawnSeconds => Mathf.Max(0, returnAt - Time.time);
        float returnAt, offTrackTime, stuckTime;
        public bool CanRecover => offTrack || transform.up.y < .45f || stuckTime >= 2.5f;
        int safeSegment;
        bool offTrack;
        Renderer[] visuals;
        Collider[] colliders;
        public int Position
        {
            get
            {
                int position = 1;
                foreach (var rival in Racers)
                    if (rival != this && rival.raceProgress > raceProgress) position++;
                return position;
            }
        }

        void OnEnable() => Racers.Add(this);
        void OnDisable() => Racers.Remove(this);

        void UpdateRaceProgress()
        {
            float best = float.PositiveInfinity;
            float fraction = 0, nearestDistance=float.PositiveInfinity;
            int previousSegment=segment;
            for (int i = 0; i < route.Length; i++)
            {
                Vector3 start = route[i].position;
                Vector3 edge = route[(i+1)%route.Length].position - start;
                float t = Mathf.Clamp01(Vector3.Dot(body.position-start,edge)/Mathf.Max(.001f,edge.sqrMagnitude));
                Vector3 lateral = body.position-start-edge*t;
                float distance = lateral.x*lateral.x + lateral.z*lateral.z;
                if(RaceSession.Selected.version>=2) distance+=lateral.y*lateral.y;
                float score=distance;
                // Keep the route branch through a level crossing, even when the other lane is closer.
                if(routeLocated && RaceSession.Selected.version>=3 && Mathf.Abs(Mathf.DeltaAngle(previousSegment*360f/route.Length,i*360f/route.Length))>28)score+=100;
                if (score >= best) continue;
                best = score;
                nearestDistance=distance;
                segment = i;
                fraction = i+t;
            }
            routeLocated=true;
            offTrack = nearestDistance > 7.5f * 7.5f;
            if (!offTrack && body.position.y > route[segment].position.y-.5f && transform.up.y > .65f) safeSegment = segment;
            int sectorLength = route.Length/4;
            int passedSector = (nextGate+3)%4;
            float within = Mathf.DeltaAngle((passedSector*sectorLength)*360f/route.Length,
                fraction*360f/route.Length)*route.Length/360f;
            raceProgress = (lap-1)*route.Length + passedSector*sectorLength + Mathf.Clamp(within,0,sectorLength);
        }

        Rigidbody body;
        Quaternion heading;
        Transform shell;
        RallyHud hud;
        RallyDust dust;
        float steering, pedal, slide, yawSlip, slipAngle;
        bool handbrake, reset, rearGrounded;
        int lap, nextGate;
        float elapsed, last;
        int finishPlace;
        public int Lap => Mathf.Clamp(lap,1,RaceSession.Laps);
        public float LapTime => elapsed;
        public float LastLap => last;
        public float Speed => Vector3.ProjectOnPlane(PeerRace.IsProxy(this) ? networkVelocity : body.linearVelocity, Vector3.up).magnitude * 3.6f;
        public int Gear { get; private set; } = 1;
        public bool Handbrake => handbrake;
        public bool Drifting => PeerRace.IsProxy(this) ? networkDrift : rearGrounded && Speed > 15 && Mathf.Abs(slipAngle) > 8;

        [SerializeField] RallyCarModel model;
        public float Progress => raceProgress;
        public int FinishOrder => finishPlace;
        Vector3 ghostTarget;
        Quaternion ghostRotation;
        bool ghostReceived;
        Vector3 networkVelocity;
        bool networkShield,networkTurbo,networkOil,networkDrift;
        float networkSteer,networkSpin;
        Vector3[] tireRest;

        public RaceProtocol.Car CaptureNetwork(int slot)=>new RaceProtocol.Car{
            slot=slot,position=body.position,rotation=body.rotation,velocity=body.linearVelocity,
            health=Health,lap=lap,gear=Gear,place=finishPlace,progress=raceProgress,
            elapsed=elapsed,last=last,respawn=RespawnSeconds,hidden=Respawning,
            shield=Shielded,turbo=Boosting,thrust=BoostThrust,oil=Oiled,handbrake=handbrake,drift=Drifting,
            steer=wheels[0].steerAngle,slip=yawSlip,active=weapons.CaptureItem(false),queued=weapons.CaptureItem(true)};

        public void ApplyNetwork(RaceProtocol.Car state)
        {
            if(!PeerRace.IsProxy(this))return;
            bool recovered=Respawning && !state.hidden;
            if(recovered)ghostReceived=false;
            SetGhostPose(state.position,state.rotation,state.hidden,state.progress);
            networkVelocity=state.velocity;Health=state.health;lap=state.lap;Gear=state.gear;finishPlace=state.place;
            elapsed=state.elapsed;last=state.last;Respawning=state.hidden;returnAt=Time.time+state.respawn;
            networkShield=state.shield;networkTurbo=state.turbo;BoostThrust=state.thrust;networkOil=state.oil;
            networkDrift=state.drift;handbrake=state.handbrake;networkSteer=state.steer;yawSlip=state.slip;
            foreach(var collider in colliders)collider.enabled=!state.hidden && !(collider is WheelCollider);
            weapons.ApplyItems(state.active,state.queued);
        }
        public void SetRemoteControls(float turn,float throttle,bool brake,bool recover)
        {
            steering=Mathf.MoveTowards(steering,turn,4*Time.fixedDeltaTime);pedal=throttle;handbrake=brake;reset|=recover;
        }
        public void SetGhostPose(Vector3 point,Quaternion rotation,bool hidden,float progress)
        {
            if(!ghostReceived || (transform.position-point).sqrMagnitude>64) transform.SetPositionAndRotation(point,rotation);
            ghostReceived=true; ghostTarget=point; ghostRotation=rotation;
            raceProgress=progress;
            foreach(var visual in visuals) visual.enabled=!hidden;
        }

        void Awake()
        {
            frontGrip=System.Array.ConvertAll(wheels,w=>w.sidewaysFriction.stiffness);
            forwardGrip=System.Array.ConvertAll(wheels,w=>w.forwardFriction.stiffness);
            tireRest=System.Array.ConvertAll(tires,t=>t.localPosition);
            body = GetComponent<Rigidbody>();
            body.centerOfMass = new Vector3(0, -.27f, .05f);
            heading = transform.rotation;
            shell = new GameObject("Sprung bodywork").transform;
            shell.SetParent(transform, false);
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform part = transform.GetChild(i);
                if (part.TryGetComponent<MeshRenderer>(out _)) part.SetParent(shell, true);
            }
            if (model) model.Install(shell,tires);
            visuals = GetComponentsInChildren<Renderer>();
            colliders = GetComponentsInChildren<Collider>();
            dust = gameObject.AddComponent<RallyDust>();
            bot = PeerRace.Active ? null : GetComponent<RallyBot>();
            weapons = gameObject.AddComponent<RallyWeapons>();
            gameObject.AddComponent<CarPowerFeedback>();
            UpdateRaceProgress();
            // RaceSession reserves and places the entire grid before any car awakens.
            if (!PeerRace.IsLocal(this)) return;
            gameObject.AddComponent<RallyWeaponInput>();
            hud = gameObject.AddComponent<RallyHud>();
            hud.Initialize(this);
            SnapCamera();
        }

        void Update()
        {
            if (PeerRace.Authority && !RaceStartSequence.Holding && lap > 0 && finishPlace==0) elapsed += Time.deltaTime;
            if (!PeerRace.IsLocal(this)) return;
            var keys = Keyboard.current;
            if(keys?.escapeKey.wasPressedThisFrame==true){RaceSession.Menu();return;}
            if(RaceStartSequence.Holding){pedal=steering=0;handbrake=reset=false;return;}
            if(!Application.isFocused){pedal=steering=0;handbrake=true;reset=false;PeerRace.Controls(0,0,true,false);return;}
            if (Gamepad.current != null) reset |= Gamepad.current.buttonNorth.wasPressedThisFrame;
            if (keys == null){PeerRace.Controls(steering,pedal,handbrake,reset);if(PeerRace.IsProxy(this))reset=false;return;}
            bool gas = keys.wKey.isPressed || keys.upArrowKey.isPressed;
            bool reverse = keys.sKey.isPressed || keys.downArrowKey.isPressed;
            bool right = keys.dKey.isPressed || keys.rightArrowKey.isPressed;
            bool left = keys.aKey.isPressed || keys.leftArrowKey.isPressed;
            pedal = (gas ? 1 : 0) - (reverse ? 1 : 0);
            steering = Mathf.MoveTowards(steering, (right ? 1 : 0) - (left ? 1 : 0), 4 * Time.deltaTime);
            handbrake = keys.spaceKey.isPressed;
            reset |= keys.rKey.wasPressedThisFrame;
            PeerRace.Controls(steering,pedal,handbrake,reset);
            if(PeerRace.IsProxy(this))reset=false;

        }

        void FixedUpdate()
        {
            if (RaceStartSequence.Holding || PeerRace.IsProxy(this) || (PeerRace.Active && !PeerRace.Driving)) return;
            PeerRace.RemoteControls(this);
            if(finishPlace>0){foreach(var wheel in wheels){wheel.motorTorque=0;wheel.brakeTorque=1800;}return;}
            if (Respawning)
            {
                if (Time.time >= returnAt) ReturnToTrack();
                return;
            }
            if(finishPlace==0) UpdateRaceProgress();
            offTrackTime = offTrack ? offTrackTime + Time.fixedDeltaTime : 0;
            if (bot)
            {
                bot.Drive(this, body, segment, out float desiredSteer, out pedal, out reset, out handbrake);
                steering = Mathf.MoveTowards(steering, desiredSteer, 5 * Time.fixedDeltaTime);
                bot.UseItem(this, weapons);
            }
            bool immobilized = body.linearVelocity.sqrMagnitude < 2f && Mathf.Abs(pedal) > .1f;
            stuckTime = immobilized ? stuckTime + Time.fixedDeltaTime : 0;
            bool requestedRecovery = reset && CanRecover;
            reset = false;
            if (requestedRecovery || body.position.y < -4 || offTrackTime >= 3)
            {
                BeginRespawn(false);
                return;
            }
            Vector3 local = transform.InverseTransformDirection(body.linearVelocity);
            rearGrounded = wheels[2].isGrounded || wheels[3].isGrounded;
            slipAngle = Mathf.Atan2(local.x, Mathf.Max(3, Mathf.Abs(local.z))) * Mathf.Rad2Deg;
            float targetSlide = handbrake && rearGrounded ? 1 : 0;
            slide = Mathf.MoveTowards(slide, targetSlide, (handbrake ? 8 : gripRecovery) * Time.fixedDeltaTime);
            bool braking = pedal * local.z < -1;
            float forwardKph = local.z * 3.6f;
            Gear = forwardKph < -2 || (pedal < 0 && !braking) ? -1 : Mathf.Clamp(1 + (int)(Mathf.Max(0,forwardKph) / 30), 1, 5);
            float drive = pedal > 0 ? Mathf.Lerp(540, 320, Speed / 140f) : -290;
            BoostThrust=Boosting && pedal>0 && !braking && !handbrake && Grounded;
            if(BoostThrust)drive*=CarPowerState.TurboTorque;
            float limit=Boosting ? CarPowerState.TurboSpeed : 41;
            if (pedal == 0 || braking || local.z > limit || local.z < -9) drive = 0;

            for (int i = 0; i < wheels.Length; i++)
            {
                WheelCollider wheel = wheels[i];
                bool front = i < 2;
                wheel.steerAngle = front ? steering * Mathf.Lerp(31, 13, Mathf.Abs(local.z) / 35) : 0;
                wheel.motorTorque = !front && handbrake ? 0 : drive;
                float serviceBrake = braking ? 1600 : pedal == 0 ? 28 : 0;
                wheel.brakeTorque = !front && handbrake ? Mathf.Max(handbrakeTorque, serviceBrake) : serviceBrake;
                float oilGrip=Time.time<oilyUntil ? .22f : 1;
                var friction = wheel.sidewaysFriction;
                friction.stiffness = (front ? frontGrip[i] : Mathf.Lerp(1.10f, slidingGrip, slide))*oilGrip;
                wheel.sidewaysFriction = friction;
                var traction=wheel.forwardFriction;traction.stiffness=forwardGrip[i]*oilGrip;wheel.forwardFriction=traction;
            }

            // Assist turn-in while the rear is loose. Countersteer stays effective throughout.
            if (rearGrounded && local.z > 3)
            {
                float yawRate = Vector3.Dot(body.angularVelocity, transform.up);
                float targetYaw = steering * Mathf.Lerp(.9f, 1.55f, slide);
                float assist = (targetYaw - yawRate) * slide * 3.5f;
                float catchSlide = Mathf.Clamp(slipAngle / 32, -1, 1) * (1 - slide) * 1.8f * (Time.time<oilyUntil ? .15f : 1);
                body.AddTorque(transform.up * (assist + catchSlide), ForceMode.Acceleration);
            }
        }

        public bool TakeDamage(int amount,bool explosion=false)
        {
            if (!PeerRace.Authority || RaceStartSequence.Holding || Respawning || finishPlace>0 || amount <= 0) return false;
            if(powerState.Absorb(amount,explosion,Time.time))return false;
            Health = Mathf.Max(0, Health - amount);
            if (Health == 0) BeginRespawn(true);
            return true;
        }

        void BeginRespawn(bool wrecked)
        {
            powerState.Clear();BoostThrust=false;
            Respawning = true;
            returnAt = Time.time + (wrecked ? 1.35f : .55f);
            if (wrecked) weapons.Wreck(body.worldCenterOfMass);
            body.linearVelocity = body.angularVelocity = Vector3.zero;
            foreach (var wheel in wheels) wheel.motorTorque = wheel.brakeTorque = 0;
            foreach (var collider in colliders) collider.enabled = false;
            body.isKinematic = true;
            foreach (var visual in visuals) visual.enabled = false;
            dust.Clear();
        }

        static readonly float[] SpawnLanes = { 0, -3.5f, 3.5f };

        void ReturnToTrack()
        {
            // Use the last stretch driven on the road so a shortcut cannot advance the car.
            var marker = route[safeSegment];
            if (safeSegment == nextGate * route.Length / 4)
                marker = route[(safeSegment + route.Length - 1) % route.Length];
            Vector3 spawn = marker.position;
            float bestClearance = -1;
            var recoveryMarker = marker;
            int first = System.Array.IndexOf(route,marker);
            for (int back=0; back<route.Length && bestClearance<25; back++)
            {
            marker = route[(first-back+route.Length)%route.Length];
            foreach (float lane in SpawnLanes)
            {
                Vector3 candidate = marker.position + marker.right * lane;
                if (!RallyRoad.Instance.TrySpawn(candidate,marker.rotation,out candidate)) continue;
                float clearance = float.PositiveInfinity;
                foreach (var rival in Racers)
                    if (rival != this && !rival.Respawning)
                        clearance = Mathf.Min(clearance,(rival.transform.position-candidate).sqrMagnitude);
                if (clearance <= bestClearance) continue;
                recoveryMarker = marker;
                spawn = candidate;
                bestClearance = clearance;
                if (clearance > 25) break;
            }
            }
            if(bestClearance<16) {returnAt=Time.time+.25f;return;}
            marker = recoveryMarker;
            segment=System.Array.IndexOf(route,recoveryMarker);
            oilyUntil=0;
            body.position = spawn;
            body.rotation = heading = marker.rotation;
            body.isKinematic = false;
            foreach (var collider in colliders) collider.enabled = true;
            foreach (var visual in visuals) visual.enabled = true;
            if (Health == 0) Health = MaxHealth;
            body.linearVelocity = marker.forward * 7f;
            slide = yawSlip = slipAngle = offTrackTime = stuckTime = 0;
            handbrake = reset = Respawning = false;
        }
        void LateUpdate()
        {
            if (PeerRace.IsProxy(this))
            {
                if(ghostReceived)
                {
                    float blendGhost=1-Mathf.Exp(-24*Time.deltaTime);
                    transform.SetPositionAndRotation(Vector3.Lerp(transform.position,ghostTarget,blendGhost),Quaternion.Slerp(transform.rotation,ghostRotation,blendGhost));
                    networkSpin+=Vector3.Dot(networkVelocity,transform.forward)/.36f*Mathf.Rad2Deg*Time.deltaTime;
                    for(int i=0;i<tires.Length;i++)
                    {
                        tires[i].localPosition=tireRest[i];
                        tires[i].localRotation=Quaternion.Euler(0,i<2 ? networkSteer : 0,0)*Quaternion.Euler(networkSpin,0,0);
                    }
                    shell.localRotation=Quaternion.Euler(0,0,-yawSlip*.14f);
                    heading=Quaternion.Slerp(heading,Quaternion.Euler(0,transform.eulerAngles.y+yawSlip*.6f,0),1-Mathf.Exp(-8*Time.deltaTime));
                    if(PeerRace.IsLocal(this))SnapCamera();
                }
                return;
            }
            if(Respawning)return;
            for (int i = 0; i < wheels.Length; i++)
            {
                wheels[i].GetWorldPose(out var position, out var rotation);
                tires[i].SetPositionAndRotation(position, rotation);
            }
            float blend = 1 - Mathf.Exp(-8 * Time.deltaTime);
            yawSlip = Mathf.Lerp(yawSlip, rearGrounded ? Mathf.Clamp(slipAngle, -36, 36) : 0, blend);
            shell.localRotation = Quaternion.Euler(0, 0, -yawSlip * .14f + steering * Mathf.Clamp01(Speed / 50) * 2);
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude > .01f)
            {
                // Follow part of the travel angle so the driver can see the car rotate into the slide.
                var view = Quaternion.AngleAxis(yawSlip * .60f, Vector3.up) * Quaternion.LookRotation(forward);
                heading = Quaternion.Slerp(heading, view, 1 - Mathf.Exp(-6 * Time.deltaTime));
            }
            dust.EmitFrom(wheels, Drifting || (handbrake && Speed > 12), body.linearVelocity);
            if (!bot) SnapCamera();
        }

        public bool LookingBack { get; private set; }

        public void SetRearView(bool held)
        {
            if(RaceStartSequence.Holding)held=false;
            if(LookingBack==held) return;
            LookingBack=held;
            SnapCamera();
        }

        void SnapCamera()
        {
            if(!chase || RaceStartSequence.Holding)return;
            Quaternion viewHeading=LookingBack ? heading*Quaternion.Euler(0,180,0) : heading;
            chase.transform.position = transform.position + viewHeading * new Vector3(0, 3.5f, -7.3f);
            chase.transform.LookAt(transform.position + viewHeading * new Vector3(0, .9f, 3.8f));
        }

        public void Gate(int number)
        {
            if (RaceStartSequence.Holding || PeerRace.IsProxy(this) || finishPlace!=0 || Respawning || number != nextGate || Vector3.Dot(body.linearVelocity, route[number * route.Length / 4].forward) < .2f) return;
            nextGate = (nextGate + 1) % 4;
            if (number != 0) return;
            if (lap > 0) last = elapsed;
            if(lap==RaceSession.Laps)
            {
                finishPlace=PeerRace.Active ? PeerRace.Finish(this) : RaceSession.Finish(!bot);
                raceProgress=float.MaxValue/finishPlace;
                return;
            }
            lap++;
            elapsed = 0;
        }
    }
}










