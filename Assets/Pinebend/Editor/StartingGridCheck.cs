using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Pinebend.Editor
{
    public static class StartingGridCheck
    {
        public static void Check()
        {
            int layouts=0;
            float closest=float.PositiveInfinity;
            bool Ground(Vector3 p,Quaternion q,out Vector3 spawn){spawn=p+Vector3.up*.9f;return true;}
            foreach(var style in new[]{CircuitStyle.Speedway,CircuitStyle.Switchbacks,CircuitStyle.Canyon,CircuitStyle.Intersection})
            for(int seed=0;seed<64;seed++)
            {
                var route=CircuitGenerator.Build(unchecked(seed*104729-275015),style);
                if(!StartingGrid.Plan(route,8,2.35f,Ground,_=>true,out var slots))throw new Exception("Incomplete grid: "+style);
                for(int i=0;i<slots.Length;i++)
                for(int j=i+1;j<slots.Length;j++)
                {
                    float gap=Vector3.ProjectOnPlane(slots[i].position-slots[j].position,Vector3.up).magnitude;
                    if(gap<4.7f)throw new Exception("Overlapping grid reservations");
                    closest=Mathf.Min(closest,gap);
                }
                // A blocked front row must produce another full grid, with the same deterministic ordering.
                bool Clear(Pose pose)=>Vector3.Distance(pose.position,slots[0].position)>6;
                if(!StartingGrid.Plan(route,8,2.35f,Ground,Clear,out var moved) || moved.Any(p=>!Clear(p)))
                    throw new Exception("Grid ignored an obstruction");
                StartingGrid.Plan(route,8,2.35f,Ground,Clear,out var repeat);
                if(!moved.Select(p=>p.position).SequenceEqual(repeat.Select(p=>p.position)))throw new Exception("Grid is not deterministic");
                layouts++;
            }
            var closed=new[]{new Vector3(0,0,0),new Vector3(0,0,100),new Vector3(100,0,100),new Vector3(100,0,0)};
            if(StartingGrid.Plan(closed,8,2.35f,Ground,_=>false,out _))throw new Exception("Blocked grid must fail closed");
            Directory.CreateDirectory("Builds");
            File.WriteAllText("Builds/grid-check.txt",$"{layouts} eight-car grids: no overlapping reservations, minimum center separation {closest:F2} m. Obstruction fallback, deterministic assignment and fully blocked-grid failure checked in edit mode. No gameplay simulation performed.");
        }
    }
}
