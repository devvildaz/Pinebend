using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Pinebend.Editor
{
    public static class CircuitGenerationCheck
    {
        static float Cross(Vector3 a,Vector3 b)=>a.x*b.z-a.z*b.x;
        public static void CheckAndBuild()
        {
            Check();
            CircuitBuilder.Build();
        }

        public static void Check()
        {
            int tested=0,junctions=0;
            float maxGap=0;
            foreach(CircuitStyle style in new[]{CircuitStyle.Speedway,CircuitStyle.Switchbacks,CircuitStyle.Canyon,CircuitStyle.Intersection})
            for(int k=0;k<64;k++)
            {
                var recipe=new TrackRecipe{version=3,seed=unchecked(104729*k-275015),style=style,title="Test"};
                var p=recipe.Sample();
                if(!p.SequenceEqual(JsonUtility.FromJson<TrackRecipe>(JsonUtility.ToJson(recipe)).Sample()))throw new Exception("Recipe round trip failed");
                int crossings=0;
                for(int i=0;i<128;i++)
                {
                    Vector3 u=p[(i+1)%128]-p[i];
                    if(u.magnitude<1 || u.magnitude>17)throw new Exception("Invalid spacing: "+style);
                    for(int j=i+2;j<128;j++)
                    {
                        if(i==0&&j==127)continue;
                        Vector3 v=p[(j+1)%128]-p[j],delta=p[j]-p[i];
                        float det=Cross(u,v);
                        if(Mathf.Abs(det)<.001f)continue;
                        float a=Cross(delta,v)/det,b=Cross(delta,u)/det;
                        if(a<0||a>1||b<0||b>1)continue;
                        float gap=Mathf.Abs((p[i]+a*u).y-(p[j]+b*v).y);
                        if(gap>.001f)throw new Exception("Uneven junction: "+gap);
                        maxGap=Mathf.Max(maxGap,gap);crossings++;
                    }
                }
                if(crossings!=(style==CircuitStyle.Intersection?1:0))throw new Exception("Unexpected crossing count: "+style);
                if(crossings>0)
                {
                    CircuitGenerator.TryCrossing(p,out var center);
                    foreach(var point in p)
                        if(Vector3.ProjectOnPlane(point-center,Vector3.up).magnitude<40 && Mathf.Abs(point.y-center.y)>.001f)
                            throw new Exception("Junction approach is not level");
                }
                tested++;junctions+=crossings;
            }
            CheckJunctionMesh();
            Directory.CreateDirectory("Builds");
            File.WriteAllText("Builds/layout-check.txt",$"{tested} loops, {junctions} level intersections; maximum height mismatch {maxGap:F5} m. Junction clipping/area/winding checked. Recipe JSON round trips deterministic.");
        }

        static void CheckJunctionMesh()
        {
            var vertices=new System.Collections.Generic.List<Vector3>{new(-30,0,-30),new(-30,0,30),new(30,0,30),new(30,0,-30)};
            var triangles=new System.Collections.Generic.List<int>{0,1,2,0,2,3};
            CircuitJunction.CutOpening(vertices,triangles,Vector3.zero);
            float area=0;
            for(int i=0;i<triangles.Count;i+=3)
            {
                var a=vertices[triangles[i]];var b=vertices[triangles[i+1]];var c=vertices[triangles[i+2]];
                area+=Vector3.Cross(b-a,c-a).magnitude*.5f;
                var middle=(a+b+c)/3;
                if(Mathf.Abs(middle.x)<17.999f && Mathf.Abs(middle.z)<17.999f)throw new Exception("Geometry blocks junction");
            }
            if(Mathf.Abs(area-(3600-1296))>.01f)throw new Exception("Junction clipping lost area");
            int start=triangles.Count;
            CircuitJunction.AddSurface(vertices,triangles,Vector3.zero);
            area=0;
            for(int i=start;i<triangles.Count;i+=3)
            {
                var normal=Vector3.Cross(vertices[triangles[i+1]]-vertices[triangles[i]],vertices[triangles[i+2]]-vertices[triangles[i]]);
                if(normal.y<=0)throw new Exception("Inverted junction surface");
                area+=normal.magnitude*.5f;
            }
            if(Mathf.Abs(area-1296)>.01f)throw new Exception("Junction patch has holes");
        }
    }
}
