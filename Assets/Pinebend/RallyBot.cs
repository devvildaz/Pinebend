using UnityEngine;

namespace Pinebend
{
    public class RallyBot : MonoBehaviour
    {
        public float laneOffset;
        public float cruisingSpeed = 35;
        float stalledTime;
        float missileReady,nextTactic;
        bool safeToBoost;

        public void Drive(TestCar car, Rigidbody body, int segment, out float steer, out float pedal, out bool recover, out bool handbrake)
        {
            var route = car.route;
            Vector3 velocity = Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up);
            float speed = velocity.magnitude;
            float lookAhead = Mathf.Clamp(6 + speed * .48f, 7, 22);
            var balance=GameplayBalance.Current;
            float desiredSpeed = car.Boosting ? 48 : Mathf.Min(40.5f,cruisingSpeed*balance.BotSpeed);
            bool straight=true;
            float distance = 0;
            int index = segment;
            Vector3 target = route[index].position;
            Vector3 targetRight = route[index].right;
            float turn = 0;
            // Work backwards from the speed each upcoming curve can support.
            // Distant corners leave room to accelerate before the braking point.
            for (int step = 0; step < 24 && distance < 105; step++)
            {
                int next = (index+1)%route.Length;
                Transform from = route[index], to = route[next];
                float length = Vector3.Distance(from.position,to.position);
                float bend = Vector3.SignedAngle(from.forward,to.forward,Vector3.up)*Mathf.Deg2Rad;
                float curvature = Mathf.Abs(bend)/Mathf.Max(.1f,length);
                if(distance<95 && (curvature>.008f || Mathf.Abs(to.position.y-from.position.y)/Mathf.Max(1,length)>.12f))straight=false;
                float cornerSpeedSquared = balance.CornerGrip*(car.Oiled ? .45f : 1)/Mathf.Max(.001f,curvature);
                desiredSpeed = Mathf.Min(desiredSpeed,Mathf.Sqrt(cornerSpeedSquared+2*8f*distance));
                if (distance <= lookAhead)
                {
                    float t = Mathf.Clamp01((lookAhead-distance)/Mathf.Max(.1f,length));
                    target = Vector3.Lerp(from.position,to.position,t);
                    targetRight = Vector3.Lerp(from.right,to.right,t);
                    turn = Mathf.Clamp(bend/Mathf.Max(.1f,length)*30,-1,1);
                }
                distance += length;
                index = next;
            }

            float lane = Mathf.Clamp(laneOffset*.65f + turn*1.6f,-3.5f,3.5f);
            TestCar blocker = null;
            float gap = 18;
            foreach (var rival in TestCar.Racers)
            {
                if (rival == car || rival.Respawning) continue;
                var relative = transform.InverseTransformPoint(rival.transform.position);
                if (relative.z <= 0 || relative.z >= gap || Mathf.Abs(relative.x) > 2.5f || Mathf.Abs(relative.y)>2.5f) continue;
                blocker = rival;
                gap = relative.z;
            }
            if (blocker)
            {
                float otherLane = Vector3.Dot(blocker.transform.position-route[segment].position,route[segment].right);
                float ownLane = Vector3.Dot(body.position-route[segment].position,route[segment].right);
                float passSide = Mathf.Abs(ownLane-otherLane)<.25f ? (laneOffset>=0 ? 1 : -1) : Mathf.Sign(ownLane-otherLane);
                if (Mathf.Abs(otherLane)>1.6f) passSide = -Mathf.Sign(otherLane);
                lane = Mathf.Clamp(otherLane+passSide*3.3f,-4,4);
                if (gap < 5 && Mathf.Abs(ownLane-otherLane)<1.7f)
                    desiredSpeed = Mathf.Min(desiredSpeed,Mathf.Max(8,blocker.Speed/3.6f));
            }

            Vector3 local = transform.InverseTransformPoint(target+targetRight*lane);
            float angle = Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;
            // Match the front-wheel angle to the pursuit arc; hold a straight wheel in flight.
            float wheelAngle = Mathf.Atan2(4.6f*local.x,Mathf.Max(1,local.x*local.x+local.z*local.z))*Mathf.Rad2Deg;
            float maxAngle = Mathf.Lerp(31,13,speed/35);
            steer = Mathf.Clamp(wheelAngle/maxAngle,-1,1);
            if (Mathf.Abs(angle)>55) steer = Mathf.Sign(angle);
            pedal = speed < desiredSpeed ? 1 : speed > desiredSpeed+1.2f ? -1 : 0;
            if (!car.wheels[0].isGrounded && !car.wheels[1].isGrounded)
            {
                steer = 0;
                pedal = 0;
            }
            safeToBoost=straight && !blocker && Mathf.Abs(angle)<9 && speed>10 && !car.Oiled && !car.Drifting &&
                System.Array.TrueForAll(car.wheels,w=>w.isGrounded);
            // Brief turn-in handbrake; release while sliding so grip can recover.
            handbrake = car.Grounded && speed > 9 && speed < 29 && Mathf.Abs(angle) > 22 && Mathf.Abs(angle) < 85 && !car.Drifting && !car.Oiled;
            if(handbrake)pedal=0;
            bool stuck = transform.up.y < .45f || (speed < 1.4f && pedal > 0) || Mathf.Abs(angle) > 110;
            stalledTime = stuck ? stalledTime + Time.fixedDeltaTime : 0;
            recover = stalledTime > 3;
            if (recover) stalledTime = 0;
        }

        public void UseItem(TestCar car, RallyWeapons weapons)
        {
            bool decisionReady=Time.time>=nextTactic;
            if(decisionReady)nextTactic=Time.time+Random.Range(GameplayBalance.Current.ReactionMin,GameplayBalance.Current.ReactionMax);
            if(weapons.Repairs>0)
            {
                if(!decisionReady)return;
                if(car.Health<=TestCar.MaxHealth-CarPowerState.RepairAmount || weapons.Queued!=null)weapons.FireAt(Vector3.zero,true,false);
                return;
            }
            if(weapons.Decoys>0)
            {
                if(decisionReady && RallyWeapons.GuidedThreatNearby(car))weapons.FireAt(Vector3.zero,true,false);
                return;
            }
            if(weapons.Shields>0)
            {
                if(!decisionReady)return;
                if(!car.Shielded && RallyWeapons.ThreatNearby(car))weapons.FireAt(Vector3.zero,true,false);
                return;
            }
            if (WreckSmoke.Obscurity(transform.position) > .45f) return;
            if(weapons.Turbos>0)
            {
                if(!decisionReady)return;
                if(safeToBoost && !car.Boosting)weapons.FireAt(Vector3.zero,true,false);
                return;
            }
            if (weapons.Mines + weapons.SmokeBombs + weapons.OilSpills > 0)
            {
                if(!decisionReady || !car.Grounded || car.Speed<25)return;
                foreach (var rival in TestCar.Racers)
                {
                    if (rival == car || rival.Respawning) continue;
                    Vector3 behind = transform.InverseTransformPoint(rival.transform.position);
                    if (behind.z < -8 && behind.z > -30 && Mathf.Abs(behind.x)<4 && Mathf.Abs(behind.y)<3)
                    {
                        weapons.FireAt(Vector3.zero,true,false);
                        break;
                    }
                }
                return;
            }
            if (weapons.Missiles + weapons.Guided + weapons.Rounds == 0) return;
            foreach (var rival in TestCar.Racers)
            {
                if (rival == car || rival.Respawning) continue;
                Vector3 target = rival.transform.position + Vector3.up * .15f;
                Vector3 local = transform.InverseTransformPoint(target);
                if (local.z < 5 || local.z > 36 || Mathf.Abs(local.x) > local.z * .20f || !weapons.CanHit(rival)) continue;
                bool launch = (weapons.Missiles + weapons.Guided) > 0 && local.z > 15 && Time.time >= missileReady;
                if(launch && weapons.Missiles>0)
                {
                    float lead=GameplayBalance.Current.botDifficulty==BotDifficulty.Easy ? .35f : GameplayBalance.Current.botDifficulty==BotDifficulty.Hard ? 1 : .7f;
                    target+=rival.GetComponent<Rigidbody>().linearVelocity*(local.magnitude/90f)*lead;
                }
                weapons.FireAt(target, launch, Mathf.Abs(local.x) < 1.4f, rival);
                if (launch) missileReady = Time.time + GameplayBalance.Current.MissileDelay;
                break;
            }
        }
    }
}




