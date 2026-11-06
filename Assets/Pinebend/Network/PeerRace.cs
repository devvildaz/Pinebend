using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pinebend
{
    // The host alone simulates every car, item and road contact. Guests send controls,
    // never damage/position claims, and render the host's snapshots and ordered events.
    [DefaultExecutionOrder(-500)]
    public sealed class PeerRace : MonoBehaviour
    {
        class Guest
        {
            public PeerLink link;
            public bool hello,ready,rejected;
            public int slot,lastSequence=-1,lastLaunch,lastRecover;
            public float since,lastInput;
            public RaceProtocol.Input input=new();
        }
        static PeerRace session;
        public static string Notice="";
        public static bool Active=>session!=null;
        public static bool Authority=>!Active || session.host;
        public static bool Driving=>Active && session.go;
        public static bool IsLocal(TestCar car)=>!Active ? !car.GetComponent<RallyBot>() : session.grid!=null && car==session.grid[session.localSlot];
        public static bool IsProxy(TestCar car)=>Active && !session.host;
        public static bool Lobby=>Active && !session.started;
        public static bool CanStart=>Lobby && session.host && session.guests.Any(g=>g.hello);
        public static string Status=>!Active ? Notice : session.host ? "Host lobby • "+(1+session.guests.Count(g=>g.hello))+" / 8 drivers" : session.status;
        public static int Slot(TestCar car)=>Active && session.grid!=null ? Array.IndexOf(session.grid,car) : -1;
        public static TestCar CarAt(int slot)=>Active && session.grid!=null && slot>=0 && slot<session.grid.Length ? session.grid[slot] : null;
        public static bool BuildPickups(TestCar car)=>IsLocal(car);
        TcpListener listener;
        TcpClient connecting;
        Task connection;
        PeerLink upstream;
        readonly List<Guest> guests=new();
        readonly RaceProtocol.Input controls=new();
        bool host,started,go,previousBackground,gridReady,welcomed;
        string failure;
        int localSlot,count,finishers;
        TestCar[] grid;
        float nextSend,deadline,greenAt,lastWorld;
        string status="Connecting…";

        public static void Open(bool hosting,string address,int port)
        {
            if(Active)return;
            if(port<1 || port>65535)throw new ArgumentOutOfRangeException(nameof(port));
            session=new GameObject("Peer race authority").AddComponent<PeerRace>();
            DontDestroyOnLoad(session.gameObject);
            session.previousBackground=Application.runInBackground;Application.runInBackground=true;
            session.host=hosting;Notice="";
            try
            {
                if(hosting){session.listener=new TcpListener(IPAddress.Any,port);session.listener.Start(7);}
                else
                {
                    session.connecting=new TcpClient();
                    session.connection=session.connecting.ConnectAsync(address.Trim(),port);
                    session.deadline=Time.realtimeSinceStartup+15;
                }
            }
            catch{Close();throw;}
        }
        public static void StartGrid(TrackRecipe track)
        {
            if(!CanStart)return;
            var s=session;s.listener.Stop();s.listener=null;
            foreach(var pending in s.guests.Where(g=>!g.hello).ToArray()){pending.link.Dispose();s.guests.Remove(pending);}
            s.count=1+s.guests.Count;
            for(int i=0;i<s.guests.Count;i++)
            {
                s.guests[i].slot=i+1;
                s.guests[i].link.Send("START "+JsonUtility.ToJson(new RaceProtocol.Start{slot=i+1,count=s.count,track=track}));
            }
            s.Load(track);
        }
        void Load(TrackRecipe track)
        {
            started=true;deadline=Time.realtimeSinceStartup+60;lastWorld=Time.unscaledTime;
            RaceSession.Selected=track;SceneManager.LoadScene("Pinebend");
        }
        public static void ConfigureGrid(TestCar[] racers)
        {
            RaceReplication.Reset();
            if(!Active)return;
            var s=session;
            var sorted=racers.OrderBy(c=>c.name,StringComparer.Ordinal).ToArray();
            s.grid=sorted.Take(s.count).ToArray();
            var camera=sorted[0].chase;
            for(int i=0;i<sorted.Length;i++)
            {
                var car=sorted[i];
                if(i>=s.count){car.gameObject.SetActive(false);continue;}
                var bot=car.GetComponent<RallyBot>();if(bot)bot.enabled=false;
                car.chase=i==s.localSlot ? camera : null;
                if(s.host)continue;
                // Keep hulls queryable for mouse aim. Guest hulls are kinematic; wheels never simulate.
                foreach(var wheel in car.wheels)wheel.enabled=false;
                car.GetComponent<Rigidbody>().isKinematic=true;
            }
        }
        public static void GridReady(bool ready)
        {
            if(!Active)return;
            session.gridReady=ready;
            if(ready && !session.host){try{session.upstream.Send("READY");}catch(Exception error){session.failure=error.Message;}}
        }
        void StartCountdown()
        {
            if(greenAt>0)return;
            greenAt=Time.unscaledTime+RaceStartSequence.Duration;
            deadline=Time.realtimeSinceStartup+RaceStartSequence.Duration+10;
            RaceStartSequence.Begin();
        }
        public static void Controls(float steer,float pedal,bool handbrake,bool recover)
        {
            if(!Active || Authority)return;
            var input=session.controls;input.steer=steer;input.pedal=pedal;input.handbrake=handbrake;
            if(recover)input.recover++;
        }
        public static void Weapon(Vector3 aim,bool launch,bool gun,bool rear,TestCar target)
        {
            if(!Active || Authority)return;
            var input=session.controls;input.aim=aim;input.gun=gun;input.rear=rear;input.target=Slot(target);
            if(launch)input.launch++;
        }
        public static void RemoteControls(TestCar car)
        {
            if(!Active || !Authority || IsLocal(car))return;
            var guest=session.guests.FirstOrDefault(g=>g.slot==Slot(car));
            if(guest==null)return;
            var input=guest.input;
            if(Time.unscaledTime-guest.lastInput>.5f){car.SetRemoteControls(0,0,true,false);return;}
            bool recover=input.recover>guest.lastRecover;
            guest.lastRecover=input.recover;
            car.SetRemoteControls(input.steer,input.pedal,input.handbrake,recover);
            car.SetRearView(input.rear);
            bool launch=input.launch>guest.lastLaunch;guest.lastLaunch=input.launch;
            if(car.Respawning || car.FinishOrder>0)return;
            var weapons=car.GetComponent<RallyWeapons>();
            var target=CarAt(input.target);
            if(target==car || !target || target.Respawning || (target.transform.position-car.transform.position).sqrMagnitude>210*210 ||
                Vector3.Dot((target.transform.position-car.transform.position).normalized,car.transform.forward*(input.rear ? -1 : 1))<.1f || !weapons.CanHit(target))target=null;
            var aim=input.aim;
            if(weapons.Missiles>0 && (aim-car.transform.position).sqrMagnitude>350*350)launch=false;
            weapons.FireAt(aim,launch,input.gun,target);
        }
        void Update(){try{Pump();}catch(Exception error){Abort(error.GetBaseException().Message);}}
        void Pump()
        {
            if(failure!=null)throw new InvalidOperationException(failure);
            if(connection!=null)
            {
                if(!connection.IsCompleted){if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("Host connection timed out. Check the VPN, address and host firewall.");return;}
                connection.GetAwaiter().GetResult();connection=null;
                upstream=new PeerLink(connecting);connecting=null;upstream.Send(RaceProtocol.Hello);
                status="Connected. Waiting for the host.";deadline=Time.realtimeSinceStartup+8;
            }
            if(!host && upstream!=null && !welcomed && Time.realtimeSinceStartup>deadline)throw new TimeoutException("The host did not complete the game handshake. Check the port and build version.");
            if(listener!=null && listener.Pending())
            {
                var client=listener.AcceptTcpClient();
                if(guests.Count>=7)client.Close();else guests.Add(new Guest{link=new PeerLink(client),since=Time.realtimeSinceStartup});
            }
            if(host)
            {
                for(int i=guests.Count-1;i>=0;i--)
                {
                    var guest=guests[i];
                    try
                    {
                        guest.link.Read(line=>FromGuest(guest,line));
                        if(!guest.hello && Time.realtimeSinceStartup-guest.since>8)throw new TimeoutException();
                    }
                    catch when(!started){guest.link.Dispose();guests.RemoveAt(i);}
                }
            }
            else upstream?.Read(FromHost);
            if(!started || grid==null || !gridReady)return;
            if(!go && greenAt==0 && host && guests.All(g=>g.ready))
            {
                Broadcast("GREEN");StartCountdown();
            }
            if(host && !go && greenAt>0 && Time.unscaledTime>=greenAt)go=true;
            if(!go && Time.realtimeSinceStartup>deadline)throw new TimeoutException("A driver did not reach the starting grid.");
            if(!host && go && Time.unscaledTime-lastWorld>8)throw new TimeoutException("The host stopped responding.");
            if(host && go && guests.Any(g=>Time.unscaledTime-g.lastInput>8))throw new TimeoutException("A driver's connection stopped responding.");
            if(Time.unscaledTime<nextSend)return;
            nextSend=Time.unscaledTime+.033333f;
            if(!host){controls.sequence++;upstream.Send("INPUT "+JsonUtility.ToJson(controls));return;}
            Broadcast("WORLD "+JsonUtility.ToJson(new RaceProtocol.World{cars=grid.Select((c,i)=>c.CaptureNetwork(i)).ToArray(),green=go}));
            RaceReplication.SendMovingProps();
        }
        void FromGuest(Guest guest,string line)
        {
            if(guest.rejected)return;
            if(!guest.hello)
            {
                if(line!=RaceProtocol.Hello){guest.rejected=true;guest.since=Time.realtimeSinceStartup-7;guest.link.Send("ERROR Incompatible build. Everyone needs the same Pinebend ZIP.");return;}
                guest.hello=true;guest.link.Send("WELCOME");return;
            }
            if(!started)return;
            if(line=="READY"){guest.ready=true;guest.lastInput=Time.unscaledTime;return;}
            if(!guest.ready || !line.StartsWith("INPUT "))return;
            var input=JsonUtility.FromJson<RaceProtocol.Input>(line.Substring(6));
            if(input==null || !input.Valid())throw new FormatException("Invalid driver input.");
            if(input.sequence<=guest.lastSequence)return;
            guest.lastSequence=input.sequence;guest.lastInput=Time.unscaledTime;guest.input=input;
        }
        void FromHost(string line)
        {
            if(line=="WELCOME"){welcomed=true;return;}
            if(line.StartsWith("ERROR "))throw new InvalidOperationException(line.Substring(6));
            if(!started && line.StartsWith("START "))
            {
                var start=JsonUtility.FromJson<RaceProtocol.Start>(line.Substring(6));
                if(start.track==null || !start.track.Supported || start.count<2 || start.count>8 || start.slot<1 || start.slot>=start.count)
                    throw new FormatException("Incompatible race setup.");
                localSlot=start.slot;count=start.count;Load(start.track);return;
            }
            if(line=="GREEN" && started){StartCountdown();return;}
            if(grid==null || !gridReady)return;
            if(line.StartsWith("EVENT ")){RaceReplication.Receive(JsonUtility.FromJson<RaceProtocol.Event>(line.Substring(6)));return;}
            if(!line.StartsWith("WORLD "))return;
            var world=JsonUtility.FromJson<RaceProtocol.World>(line.Substring(6));
            if(world?.cars==null || world.cars.Length!=grid.Length)throw new FormatException("Incomplete world snapshot.");
            foreach(var car in world.cars)
            {
                if(car.slot<0 || car.slot>=grid.Length || !RaceProtocol.Finite(car.position) || car.position.sqrMagnitude>100000000)throw new FormatException("Invalid car snapshot.");
                grid[car.slot].ApplyNetwork(car);
            }
            go=world.green;lastWorld=Time.unscaledTime;
        }
        void Broadcast(string line){foreach(var guest in guests)if(guest.hello)guest.link.Send(line);}
        public static void Publish(RaceProtocol.Event value)
        {
            if(Active && Authority && session.started && session.gridReady)
            {
                try{session.Broadcast("EVENT "+JsonUtility.ToJson(value));}
                catch(Exception error){session.failure=error.Message;}
            }
        }
        public static int Finish(TestCar car)
        {
            if(!Active || !Authority)return 0;
            return ++session.finishers;
        }
        void OnGUI()
        {
            if(!started || grid==null || !grid[localSlot] || grid[localSlot].FinishOrder==0)return;
            GUILayout.BeginArea(new Rect(Screen.width/2-150,80,300,100),GUI.skin.box);
            GUILayout.Label("Finished • Place "+grid[localSlot].FinishOrder+" / "+count);
            GUILayout.Label("Wait for the other drivers. Esc leaves.");
            GUILayout.EndArea();
        }
        static void Abort(string message)
        {
            bool inRace=Active && session.started;Notice=message;Close();if(inRace)RaceSession.Menu();
        }
        public static void Close()
        {
            var s=session;if(!s)return;session=null;s.enabled=false;
            Application.runInBackground=s.previousBackground;
            s.listener?.Stop();s.connecting?.Close();s.upstream?.Dispose();
            foreach(var guest in s.guests)guest.link.Dispose();
            RaceReplication.Reset();Destroy(s.gameObject);
        }
        void OnApplicationQuit(){if(session==this)Close();}
    }
}
