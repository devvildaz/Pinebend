using System.Collections.Generic;
using UnityEngine;

namespace Pinebend
{
    // A single flat, destructible patch replaces both road decks at a junction.
    // Clip roadside geometry to the same boundary so curbs cannot block the crossing.
    public static class CircuitJunction
    {
        public const float HalfSize = 18;

        public static void CutOpening(List<Vector3> vertices, List<int> triangles, Vector3 center)
        {
            var result = new List<int>(triangles.Count);
            for (int i=0;i<triangles.Count;i+=3)
            {
                var a=vertices[triangles[i]]; var b=vertices[triangles[i+1]]; var c=vertices[triangles[i+2]];
                if (Mathf.Max(a.x,b.x,c.x)<=center.x-HalfSize || Mathf.Min(a.x,b.x,c.x)>=center.x+HalfSize ||
                    Mathf.Max(a.z,b.z,c.z)<=center.z-HalfSize || Mathf.Min(a.z,b.z,c.z)>=center.z+HalfSize)
                { result.AddRange(new[]{triangles[i],triangles[i+1],triangles[i+2]}); continue; }
                var remaining=new List<Vector3>{a,b,c};
                for(int edge=0;edge<4 && remaining.Count>=3;edge++)
                {
                    Add(Clip(remaining,center,edge,false),vertices,result);
                    remaining=Clip(remaining,center,edge,true);
                }
            }
            triangles.Clear(); triangles.AddRange(result);
        }

        static float Distance(Vector3 p,Vector3 center,int edge) => edge switch {
            0=>p.x-center.x+HalfSize, 1=>center.x+HalfSize-p.x,
            2=>p.z-center.z+HalfSize, _=>center.z+HalfSize-p.z };

        static List<Vector3> Clip(List<Vector3> polygon,Vector3 center,int edge,bool inside)
        {
            var output=new List<Vector3>();
            for(int i=0;i<polygon.Count;i++)
            {
                Vector3 a=polygon[i], b=polygon[(i+1)%polygon.Count];
                float da=Distance(a,center,edge), db=Distance(b,center,edge);
                bool keepA=inside ? da>=0 : da<0, keepB=inside ? db>=0 : db<0;
                if(keepA)output.Add(a);
                if(keepA!=keepB) output.Add(Vector3.Lerp(a,b,da/(da-db)));
            }
            return output;
        }

        static void Add(List<Vector3> polygon,List<Vector3> vertices,List<int> triangles)
        {
            if(polygon.Count<3)return;
            int first=vertices.Count;vertices.AddRange(polygon);
            for(int i=1;i<polygon.Count-1;i++)
            {
                if(Vector3.Cross(polygon[i]-polygon[0],polygon[i+1]-polygon[0]).sqrMagnitude<.0000001f)continue;
                triangles.Add(first);triangles.Add(first+i);triangles.Add(first+i+1);
            }
        }

        public static void AddSurface(List<Vector3> vertices,List<int> triangles,Vector3 center)
        {
            const int count=48;
            int first=vertices.Count;
            for(int z=0;z<=count;z++)
            for(int x=0;x<=count;x++)
                vertices.Add(center+new Vector3(-HalfSize+x*2*HalfSize/count,.002f,-HalfSize+z*2*HalfSize/count));
            for(int z=0;z<count;z++)
            for(int x=0;x<count;x++)
            {
                int a=first+z*(count+1)+x,b=a+count+1;
                triangles.AddRange(new[]{a,b,b+1,a,b+1,a+1});
            }
        }
    }
}
