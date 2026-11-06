using System;
using UnityEngine;

namespace Pinebend
{
    // Compose the entire map in one bottom-left-origin pixel space. GUI only scales the finished image.
    public sealed class CircuitMap
    {
        const int Size = 320;
        static readonly Color32 Gravel = new Color32(200, 213, 213, 255);
        static readonly Color32 Rival = new Color32(232, 71, 48, 255);
        static readonly Color32 Driver = new Color32(255, 185, 35, 255);
        static readonly Color32 Ink = new Color32(25, 38, 47, 255);
        readonly Color32[] pixels = new Color32[Size * Size];
        readonly Vector3[] left, right;
        readonly Bounds bounds;
        readonly Vector3? junction;
        Vector2 origin;
        float scale;
        Vector2 drivingHeading=Vector2.up;
        Vector2 mapUp, mapRight;
        public readonly Texture2D Texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {wrapMode=TextureWrapMode.Clamp, filterMode=FilterMode.Bilinear};

        public CircuitMap(Vector3[] leftEdge, Vector3[] rightEdge,Vector3? intersection=null)
        {
            junction=intersection;
            left = leftEdge;
            right = rightEdge;
            var extents = new Bounds(left[0], Vector3.zero);
            for (int i=0;i<left.Length;i++) {extents.Encapsulate(left[i]); extents.Encapsulate(right[i]);}
            bounds = extents;
        }

        public void Begin(Vector3 player, Vector3 forward, bool overview)
        {
            var flatForward=new Vector2(forward.x,forward.z);
            if(flatForward.sqrMagnitude>.001f) drivingHeading=flatForward.normalized;
            mapUp=overview ? Vector2.up : drivingHeading;
            mapRight=new Vector2(mapUp.y,-mapUp.x);
            Vector3 focus = overview ? bounds.center : player;
            origin = new Vector2(focus.x, focus.z);
            scale = overview ? (Size-30)/Mathf.Max(1,bounds.size.x,bounds.size.z) : Size/95f;
            Array.Clear(pixels,0,pixels.Length);
            for(int i=0;i<left.Length;i++)
            {
                int next=(i+1)%left.Length;
                Vector2 a=Pixel(left[i]), b=Pixel(right[i]), c=Pixel(right[next]), d=Pixel(left[next]);
                Triangle(a,b,c,Gravel);
                Triangle(a,c,d,Gravel);
            }
            if(junction.HasValue)
            {
                var center=junction.Value;float half=CircuitJunction.HalfSize;
                Vector2 a=Pixel(center+new Vector3(-half,0,-half)),b=Pixel(center+new Vector3(-half,0,half)),c=Pixel(center+new Vector3(half,0,half)),d=Pixel(center+new Vector3(half,0,-half));
                Triangle(a,b,c,Gravel);Triangle(a,c,d,Gravel);
            }
            Vector2 l=Pixel(left[0]), r=Pixel(right[0]);
            for(int i=0;i<8;i++)
            {
                Vector2 a=Vector2.Lerp(l,r,i/8f), b=Vector2.Lerp(l,r,(i+1)/8f);
                Vector2 offset=(Pixel((left[1]+right[1])*.5f)-(l+r)*.5f).normalized*3;
                Color32 color=i%2==0 ? Ink : (Color32)Color.white;
                Triangle(a,b,a+offset,color); Triangle(b,b+offset,a+offset,color);
            }
        }

        Vector2 MapAxes(Vector2 vector) => new Vector2(Vector2.Dot(vector,mapRight),Vector2.Dot(vector,mapUp));
        Vector2 Pixel(Vector3 point) => Vector2.one*((Size-1)*.5f)+MapAxes(new Vector2(point.x,point.z)-origin)*scale;

        public void Mark(Vector3 position, Vector3 forward, bool player)
        {
            var p=Pixel(position);
            var flat=new Vector2(forward.x,forward.z);
            var f=MapAxes(flat.sqrMagnitude>.001f ? flat.normalized : drivingHeading);
            var side=new Vector2(f.y,-f.x);
            if(player)
            {
                Triangle(p+f*13,p-f*8+side*8,p-f*8-side*8,Ink);
                Triangle(p+f*10,p-f*6+side*6,p-f*6-side*6,Driver);
            }
            else
            {
                for(int y=-5;y<=5;y++)
                for(int x=-5;x<=5;x++)
                {
                    int px=Mathf.RoundToInt(p.x)+x, py=Mathf.RoundToInt(p.y)+y;
                    if(px>=0 && px<Size && py>=0 && py<Size && x*x+y*y<=25)
                        pixels[py*Size+px]=x*x+y*y<=14 ? Rival : Ink;
                }
            }
        }

        public void Upload(){Texture.SetPixels32(pixels);Texture.Apply(false,false);}

        void Triangle(Vector2 a,Vector2 b,Vector2 c,Color32 color)
        {
            if(a.y>b.y) (a,b)=(b,a);
            if(b.y>c.y) (b,c)=(c,b);
            if(a.y>b.y) (a,b)=(b,a);
            if(c.y-a.y<.001f)return;
            int first=Mathf.Max(0,Mathf.CeilToInt(a.y)), last=Mathf.Min(Size-1,Mathf.FloorToInt(c.y));
            for(int y=first;y<=last;y++)
            {
                float x1=Mathf.Lerp(a.x,c.x,(y-a.y)/(c.y-a.y));
                Vector2 lo=y<b.y?a:b, hi=y<b.y?b:c;
                float x2=Mathf.Lerp(lo.x,hi.x,Mathf.InverseLerp(lo.y,hi.y,y));
                int start=Mathf.Max(0,Mathf.CeilToInt(Mathf.Min(x1,x2)));
                int end=Mathf.Min(Size-1,Mathf.FloorToInt(Mathf.Max(x1,x2)));
                for(int x=start;x<=end;x++)pixels[y*Size+x]=color;
            }
        }
    }
}

