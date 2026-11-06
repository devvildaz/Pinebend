using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Pinebend.Editor
{
    public static class PeerNetworkCheck
    {
        public static void Check()
        {
            void Require(bool ok,string message){if(!ok)throw new Exception(message);}
            var controls=new RaceProtocol.Input{sequence=1,launch=1,recover=1,steer=-1,pedal=1,target=7,aim=new Vector3(42,3,25)};
            Require(controls.Valid(),"Valid controls rejected");
            controls.steer=float.NaN;Require(!controls.Valid(),"NaN controls accepted");
            controls.steer=2;Require(!controls.Valid(),"Out-of-range steering accepted");
            controls.steer=0;controls.target=8;Require(!controls.Valid(),"Invalid target accepted");
            controls.target=2;controls.aim=new Vector3(float.PositiveInfinity,0,0);Require(!controls.Valid(),"Infinite aim accepted");
            var world=new RaceProtocol.World{green=true,cars=Enumerable.Range(0,8).Select(i=>new RaceProtocol.Car{
                slot=i,position=new Vector3(123.456f,17.321f,-122.01f),velocity=new Vector3(37.7f,2.5f,17.1f),rotation=Quaternion.Euler(12,176,8),
                health=63,lap=2,gear=4,place=0,progress=117.77f,elapsed=123.999f,last=112.332f,respawn=.7f,steer=22,slip=16,
                active=new RaceProtocol.Item{type=8,ammo=1,reveal=.42f},queued=new RaceProtocol.Item{type=3,ammo=1,reveal=.8f},shield=true,turbo=true,thrust=true
            }).ToArray()};
            string packet="WORLD "+JsonUtility.ToJson(world);
            int bytes=Encoding.UTF8.GetByteCount(packet);
            Require(bytes<PeerLink.MaxFrameBytes,"Eight-car snapshot exceeds transport frame limit");
            var copy=JsonUtility.FromJson<RaceProtocol.World>(packet.Substring(6));
            Require(copy.cars.Length==8 && copy.cars[7].active.type==8 && copy.cars[0].shield,"Snapshot round trip lost gameplay state");

            var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
            var client=new TcpClient();
            try
            {
                client.Connect(IPAddress.Loopback,((IPEndPoint)listener.LocalEndpoint).Port);
                using var server=new PeerLink(listener.AcceptTcpClient());
                using var sender=new PeerLink(client);
                var received=new List<string>();
                byte[] partial=Encoding.UTF8.GetBytes("FRAG");client.GetStream().Write(partial,0,partial.Length);
                Thread.Sleep(10);server.Read(received.Add);
                Require(received.Count==0,"Partial TCP frame was dispatched");
                byte[] end=Encoding.UTF8.GetBytes("MENT\n");client.GetStream().Write(end,0,end.Length);
                for(int i=0;i<96;i++)sender.Send(i%8==0 ? packet : "EVENT "+i);
                var watch=Stopwatch.StartNew();
                while(received.Count<97 && watch.ElapsedMilliseconds<4000){server.Read(received.Add);Thread.Sleep(1);}
                Require(received.Count==97 && received[0]=="FRAGMENT","Reliable frame delivery failed");
                for(int i=0;i<96;i++)Require(received[i+1]==(i%8==0 ? packet : "EVENT "+i),"Combat event order changed");
                bool oversized=false;try{sender.Send(new string('x',PeerLink.MaxFrameBytes+1));}catch(IOException){oversized=true;}
                Require(oversized,"Oversized outbound frame accepted");
                byte[] overflow=Encoding.UTF8.GetBytes(new string('x',PeerLink.MaxFrameBytes+1));
                client.GetStream().Write(overflow,0,overflow.Length);oversized=false;watch.Restart();
                while(!oversized && watch.ElapsedMilliseconds<2000)
                {
                    try{server.Read(_=>{});}catch(IOException){oversized=true;}
                    Thread.Sleep(1);
                }
                Require(oversized,"Oversized inbound frame accepted");
            }
            finally{client.Dispose();listener.Stop();}
            Directory.CreateDirectory("Builds");
            File.WriteAllText("Builds/network-check.txt",$"Passed edit-mode protocol checks: input validation; eight-car combat snapshot round trip ({bytes} bytes); fragmented TCP reassembly; 96 ordered mixed snapshots/events; inbound/outbound frame limits. Loopback transport only; no game was launched and no friend/VPN connection was tested.");
        }
    }
}
