using System;
using UnityEngine;

namespace Pinebend
{
    public enum CircuitStyle { Surprise, Speedway, Switchbacks, Canyon, Overpass, Intersection }

    public static class CircuitGenerator
    {
        // Control polygons describe the order of corners; sampling and start-grid orientation
        // are shared by all families. The overpass visits the centre on two separate diagonals.
        static readonly Vector2[][] outlines = {
            new[]{P(0,0),P(0,80),P(24,133),P(110,145),P(183,115),P(188,55),P(162,20),P(160,-90),P(124,-122),P(44,-110),P(0,-69)},
            new[]{P(0,0),P(0,88),P(28,122),P(61,88),P(55,4),P(84,-20),P(112,12),P(112,96),P(151,120),P(186,84),P(190,-67),P(145,-106),P(77,-82),P(24,-103),P(0,-65)},
            new[]{P(0,0),P(0,60),P(37,111),P(82,120),P(115,89),P(165,94),P(204,53),P(172,20),P(100,39),P(77,12),P(103,-27),P(162,-53),P(141,-106),P(69,-126),P(10,-76)},
            new[]{P(0,0),P(0,64),P(36,97),P(78,74),P(122,14),P(166,-44),P(209,-60),P(242,-29),P(240,31),P(206,70),P(165,65),P(120,9),P(79,-50),P(42,-70),P(4,-52)}
        };
        static Vector2 P(float x,float z)=>new Vector2(x,z);
        static float Cross(Vector3 a,Vector3 b)=>a.x*b.z-a.z*b.x;

        public static CircuitStyle Style(int seed,CircuitStyle requested)
            => requested==CircuitStyle.Surprise ? new[]{CircuitStyle.Speedway,CircuitStyle.Switchbacks,CircuitStyle.Canyon,CircuitStyle.Intersection}[new System.Random(seed).Next(4)] : requested;

        public static CircuitStyle LegacyStyle(int seed,CircuitStyle requested)
            => requested==CircuitStyle.Surprise ? (CircuitStyle)(1+new System.Random(seed).Next(4)) : requested;

        public static Vector3[] Build(int seed,CircuitStyle requested)
        {
            var random=new System.Random(seed);
            float Range(float min,float max)=>Mathf.Lerp(min,max,(float)random.NextDouble());
            var family=Style(seed,requested);
            var source=outlines[family==CircuitStyle.Intersection ? 3 : (int)family-1];
            var knots=new Vector3[source.Length];
            float width=Range(.86f,1.35f),length=Range(.9f,1.38f),shear=Range(-.23f,.23f);
            float mirror=random.Next(2)==0 ? -1 : 1;
            for(int i=0;i<knots.Length;i++)
            {
                var p=source[i];
                knots[i]=new Vector3(mirror*((p.x+Range(-7,7))*width+p.y*shear),0,(p.y+Range(-8,8))*length);
            }
            if(random.Next(2)==0) Array.Reverse(knots,1,knots.Length-1);
            Vector3 K(int index)=>knots[(index+knots.Length)%knots.Length];
            int steps=knots.Length*32;
            var dense=new Vector3[steps+1];
            var station=new float[steps+1];
            for(int i=0;i<=steps;i++)
            {
                int k=i/32;float t=i%32/32f;
                Vector3 a=K(k-1),b=K(k),c=K(k+1),d=K(k+2);
                dense[i]=.5f*(2*b+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t);
                if(i>0) station[i]=station[i-1]+Vector3.Distance(dense[i-1],dense[i]);
            }
            float lap=station[steps],height=Range(2,6),phase=Range(0,6);
            var points=new Vector3[128];
            int sample=1;
            for(int i=0;i<128;i++)
            {
                float d=lap*i/128;
                while(station[sample]<d)sample++;
                points[i]=Vector3.Lerp(dense[sample-1],dense[sample],Mathf.InverseLerp(station[sample-1],station[sample],d));
                float angle=2*Mathf.PI*i/128;
                points[i].y=height*(1-Mathf.Cos(angle*2))*(1+.35f*Mathf.Sin(angle*3+phase));
            }
            if(family==CircuitStyle.Overpass) RaiseCrossing(points,lap);
            if(family==CircuitStyle.Intersection && TryCrossing(points,out var junction))
                for(int i=0;i<points.Length;i++)
                {
                    float distance=Vector3.ProjectOnPlane(points[i]-junction,Vector3.up).magnitude;
                    points[i].y=Mathf.Lerp(junction.y,points[i].y,Mathf.SmoothStep(0,1,Mathf.InverseLerp(42,85,distance)));
                }
            Vector3 origin=points[0];
            var rotation=Quaternion.Inverse(Quaternion.LookRotation(Vector3.ProjectOnPlane(points[1]-points[127],Vector3.up)));
            for(int i=0;i<points.Length;i++) points[i]=rotation*(points[i]-origin);
            return points;
        }

        public static bool TryCrossing(Vector3[] p,out Vector3 junction)
        {
            for(int i=0;i<p.Length;i++)
            for(int j=i+2;j<p.Length;j++)
            {
                if(i==0 && j==p.Length-1)continue;
                Vector3 u=p[(i+1)%p.Length]-p[i],v=p[(j+1)%p.Length]-p[j],delta=p[j]-p[i];
                float det=Cross(u,v);
                if(Mathf.Abs(det)<.001f)continue;
                float a=Cross(delta,v)/det,b=Cross(delta,u)/det;
                if(a<0 || a>1 || b<0 || b>1)continue;
                junction=(p[i]+u*a+p[j]+v*b)*.5f;return true;
            }
            junction=default;return false;
        }

        static void RaiseCrossing(Vector3[] p,float lap)
        {
            for(int i=0;i<p.Length;i++)
            for(int j=i+3;j<p.Length-1;j++)
            {
                Vector3 u=p[(i+1)%128]-p[i],v=p[j+1]-p[j],delta=p[j]-p[i];
                float det=Cross(u,v);
                if(Mathf.Abs(det)<.001f)continue;
                float a=Cross(delta,v)/det,b=Cross(delta,u)/det;
                if(a<0 || a>1 || b<0 || b>1)continue;
                float up=Mathf.Max(0,Mathf.Lerp(p[j].y,p[j+1].y,b)-Mathf.Lerp(p[i].y,p[(i+1)%128].y,a))+14;
                for(int n=0;n<p.Length;n++)
                {
                    float metres=Mathf.Abs(Mathf.DeltaAngle((i+a)*360/128,n*360f/128))*lap/360f;
                    p[n].y+=up*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(22,105,metres)));
                }
                return;
            }
            throw new InvalidOperationException("Overpass route has no crossing.");
        }
    }
}

