using System.Collections.Generic;
using UnityEngine;

namespace Pinebend
{
    // One continuous road mesh supplies both the rendered surface and the wheel contacts.
    public class RallyRoad : MonoBehaviour
    {
        public static RallyRoad Instance { get; private set; }
        const int Across = 16;
        Mesh road, grass;
        MeshCollider contacts;
        Material burnt;
        Vector3[] surface;
        int[] faces;
        int routeCount, stripVertices;
        bool junctionActive;
        Vector3 junction;
        float[] originalHeight;
        readonly List<int> clean = new(), charred = new();

        public CircuitMap CreateMap()
        {
            int count=routeCount;
            var left=new Vector3[count];
            var right=new Vector3[count];
            for(int i=0;i<count;i++)
            {
                int first=i*6*(Across+1);
                left[i]=transform.TransformPoint(surface[first]);
                right[i]=transform.TransformPoint(surface[first+Across]);
            }
            return new CircuitMap(left,right,junctionActive ? junction : (Vector3?)null);
        }

        public void Initialize(Transform[] route)
        {
            Instance = this;
            routeCount=route.Length;
            var points=System.Array.ConvertAll(route,t=>t.position);
            junctionActive=RaceSession.Selected.version>=3 && CircuitGenerator.Style(RaceSession.Selected.seed,RaceSession.Selected.style)==CircuitStyle.Intersection && CircuitGenerator.TryCrossing(points,out junction);
            var ground = GameObject.Find("Grass and driving surface");
            var groundBounds = ground.GetComponent<Collider>().bounds;
            var grassMaterial = ground.GetComponent<Renderer>().sharedMaterial;
            var filter = GetComponent<MeshFilter>();
            var renderer = GetComponent<MeshRenderer>();
            var gravel = renderer.sharedMaterial;
            burnt = new Material(gravel) { color = new Color(.39f,.27f,.17f) };
            renderer.sharedMaterials = new[] { gravel, burnt };

            float[] station = new float[route.Length+1];
            for (int i=0;i<route.Length;i++)
                station[i+1] = station[i]+Vector3.Distance(route[i].position, route[(i+1)%route.Length].position);
            int rows = route.Length * 6;
            surface = new Vector3[rows*(Across+1)];
            faces = new int[rows*Across*6];
            stripVertices=surface.Length;
            originalHeight = new float[surface.Length];
            var grassVertices = new List<Vector3>();
            var grassFaces = new List<int>();
            Vector3 middle = Vector3.zero;
            foreach (var marker in route) middle += marker.position;
            middle /= route.Length;

            for (int r=0; r<rows; r++)
            {
                Transform a = route[r/6], b = route[(r/6+1)%route.Length];
                float t = r%6/6f;
                Vector3 left = Vector3.Lerp(a.position-a.right*6,b.position-b.right*6,t);
                Vector3 right = Vector3.Lerp(a.position+a.right*6,b.position+b.right*6,t);
                float meters = Mathf.Lerp(station[r/6],station[r/6+1],t);
                float crest = Jump(meters-station[8],14,5,26,2.6f)
                            + Jump(meters-station[78],11,3,21,1.7f);
                if(junctionActive) crest*=Mathf.SmoothStep(0,1,Mathf.InverseLerp(42,60,Vector3.ProjectOnPlane((left+right)*.5f-junction,Vector3.up).magnitude));
                for (int j=0;j<=Across;j++)
                {
                    int vertex = r*(Across+1)+j;
                    float offset = j/(float)Across;
                    float shoulder = Mathf.SmoothStep(0,1,Mathf.Min(offset,1-offset)*8);
                    originalHeight[vertex] = Mathf.Lerp(left.y,right.y,offset)+.002f+crest*shoulder;
                    surface[vertex] = Vector3.Lerp(left,right,offset)+Vector3.up*(.002f+crest*shoulder);
                }
                for (int j=0;j<Across;j++)
                {
                    int k=(r*Across+j)*6, i=r*(Across+1)+j, n=((r+1)%rows)*(Across+1)+j;
                    faces[k]=i; faces[k+1]=n; faces[k+2]=n+1;
                    faces[k+3]=i; faces[k+4]=n+1; faces[k+5]=i+1;
                }
                // The clockwise oval has its infield on the right.
                Vector3 outer = left-middle;
                outer.Normalize();
                float distance = Mathf.Max(groundBounds.size.x,groundBounds.size.z);
                int g=grassVertices.Count, next=((r+1)%rows)*4;
                if(RaceSession.Selected.version>=2)
                {
                    // Roadside shoulders follow each deck independently at a crossing.
                    Vector3 across=(right-left).normalized;
                    grassVertices.Add(left-Vector3.up*.02f);
                    grassVertices.Add(left-across*3.8f-Vector3.up*.65f);
                    grassVertices.Add(right-Vector3.up*.02f);
                    grassVertices.Add(right+across*3.8f-Vector3.up*.65f);
                    grassFaces.AddRange(new[]{g+1,next+1,next,g+1,next,g,g+2,next+2,next+3,g+2,next+3,g+3});
                    continue;
                }
                grassVertices.Add(left-Vector3.up*.02f);
                grassVertices.Add(middle+outer*distance-Vector3.up*.02f);
                grassVertices.Add(right-Vector3.up*.02f);
                grassVertices.Add(middle-Vector3.up*.02f);
                grassFaces.AddRange(new[]{g+1,next+1,next,g+1,next,g,g+2,next+2,g+3});
            }
            if(RaceSession.Selected.version>=2) AddBanks(grassVertices,grassFaces,route,groundBounds.max.y-6);
            if(junctionActive)
            {
                CircuitJunction.CutOpening(grassVertices,grassFaces,junction);
                var vertices=new List<Vector3>(surface); var triangles=new List<int>(faces);
                CircuitJunction.CutOpening(vertices,triangles,junction);
                CircuitJunction.AddSurface(vertices,triangles,junction);
                surface=vertices.ToArray();faces=triangles.ToArray();
                originalHeight=System.Array.ConvertAll(surface,v=>v.y);
            }
            grass = new Mesh {name="Circuit runoff",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32,vertices=grassVertices.ToArray(),triangles=grassFaces.ToArray()};
            grass.RecalculateNormals();
            var runoff = new GameObject("Circuit runoff");
            runoff.transform.SetParent(transform,false);
            runoff.AddComponent<MeshFilter>().sharedMesh=grass;
            runoff.AddComponent<MeshRenderer>().sharedMaterial=grassMaterial;
            runoff.AddComponent<MeshCollider>().sharedMesh=grass;
            if(RaceSession.Selected.version>=2) ground.transform.position+=Vector3.down*6;
            else ground.SetActive(false);
            road = new Mesh {name="Breakable gravel",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            road.MarkDynamic();
            contacts=gameObject.AddComponent<MeshCollider>();
            filter.sharedMesh=road;
            Rebuild();
        }

        // Connect the runoff to the valley floor. Near a lower road, end the upper
        // shoulder as a thin bridge fascia so the passage beneath stays open.
        static void AddBanks(List<Vector3> vertices,List<int> faces,Transform[] route,float floor)
        {
            int rows=vertices.Count/4;
            int bankStart=vertices.Count;
            for(int i=0;i<rows;i++)
            {
                Vector3 left=vertices[i*4+1],right=vertices[i*4+3];
                Vector3 middle=(left+right)*.5f;
                float drop=middle.y-floor;
                float nearest=float.PositiveInfinity;
                for(int j=0;j<route.Length;j++)
                {
                    if(route[j].position.y>middle.y-5)continue;
                    Vector3 delta=route[j].position-middle;
                    nearest=Mathf.Min(nearest,delta.x*delta.x+delta.z*delta.z);
                }
                float bank=Mathf.SmoothStep(0,1,Mathf.InverseLerp(23,39,Mathf.Sqrt(nearest)));
                Vector3 side=(right-left).normalized;
                Vector3 slope=Vector3.down*Mathf.Lerp(.6f,drop,bank);
                vertices.Add(left+slope-side*(drop*.65f*bank));
                vertices.Add(right+slope+side*(drop*.65f*bank));
            }
            for(int i=0;i<rows;i++)
            {
                int j=(i+1)%rows;
                int l=i*4+1,r=i*4+3,ln=j*4+1,rn=j*4+3;
                int baseL=bankStart+2*i,nextL=bankStart+2*j;
                faces.AddRange(new[]{baseL,nextL,ln,baseL,ln,l,r,rn,nextL+1,r,nextL+1,baseL+1});
            }
        }

        // A rising face ends at a short tabletop. The long downslope catches the landing.
        static float Jump(float meters,float approach,float top,float landing,float height)
        {
            if (meters<0 || meters>=approach+top+landing) return 0;
            if (meters<approach) return height*Mathf.Pow(meters/approach,1.55f);
            return height*(1-Mathf.SmoothStep(0,1,(meters-approach-top)/landing));
        }

        // Probe the centre and four corners against the current, possibly deformed mesh.
        public bool TrySpawn(Vector3 point, Quaternion rotation, out Vector3 spawn)
        {
            spawn=point;
            var bounds=contacts.bounds;
            float low=float.PositiveInfinity, high=float.NegativeInfinity;
            for(int i=0;i<5;i++)
            {
                Vector3 offset=i==4 ? Vector3.zero : new Vector3(i%2==0 ? -.97f : .97f,0,i<2 ? -1.85f : 1.85f);
                Vector3 top=point+rotation*offset;
                top.y=RaceSession.Selected.version>=2 ? point.y+4 : bounds.max.y+3;
                if(!contacts.Raycast(new Ray(top,Vector3.down),out var hit,bounds.size.y+6) || hit.normal.y<.88f) return false;
                high=Mathf.Max(high,hit.point.y); low=Mathf.Min(low,hit.point.y);
            }
            if(high-low>1.15f || low<point.y-.65f) return false;
            spawn.y=high+.90f;
            return true;
        }
        Transform[] finishTiles;

        public void Crater(Vector3 impact)
        {
            if (!contacts.Raycast(new Ray(impact + Vector3.up*3,Vector3.down),out var hit,6)) return;
            bool changed=false;
            for(int i=0;i<surface.Length;i++)
            {
                int side=i%(Across+1);
                if(i<stripVertices && (side==0 || side==Across) && (!junctionActive || Mathf.Abs(surface[i].x-junction.x)>20 || Mathf.Abs(surface[i].z-junction.z)>20)) continue;
                Vector3 v=surface[i];
                if(RaceSession.Selected.version>=2 && Mathf.Abs(v.y-hit.point.y)>4)continue;
                float dx=v.x-impact.x, dz=v.z-impact.z;
                float bowl=1-(dx*dx+dz*dz)/10.24f;
                if(bowl<=0) continue;
                float cut=.42f*bowl*bowl;
                if(cut<=0) continue;
                surface[i].y-=cut;
                changed=true;
            }
            if (!changed) return;
            PeerRace.Publish(new RaceProtocol.Event{kind="crater",position=impact});
            Rebuild();
            if (Mathf.Abs(impact.x)>10 || Mathf.Abs(impact.z)>5) return;
            finishTiles ??= System.Array.FindAll(transform.parent.GetComponentsInChildren<Transform>(), t => t.name=="Finish tile");
            foreach (var tile in finishTiles)
            {
                Vector3 origin=tile.position;
                origin.y=2;
                if (!contacts.Raycast(new Ray(origin,Vector3.down),out var paint,float.MaxValue)) continue;
                tile.position=paint.point+paint.normal*.021f;
                tile.rotation=Quaternion.FromToRotation(Vector3.up,paint.normal);
            }
        }

        void Rebuild()
        {
            clean.Clear(); charred.Clear();
            for(int i=0;i<faces.Length;i+=3)
            {
                bool scorched=originalHeight[faces[i]]-surface[faces[i]].y + originalHeight[faces[i+1]]-surface[faces[i+1]].y + originalHeight[faces[i+2]]-surface[faces[i+2]].y > .12f;
                var group=scorched ? charred : clean;
                group.Add(faces[i]); group.Add(faces[i+1]); group.Add(faces[i+2]);
            }
            road.vertices=surface;
            road.subMeshCount=2;
            road.SetTriangles(clean,0);
            road.SetTriangles(charred,1);
            road.RecalculateNormals();
            road.RecalculateBounds();
            contacts.sharedMesh=null;
            contacts.sharedMesh=road;
        }

        void OnDestroy()
        {
            if(Instance==this) Instance=null;
            Destroy(road); Destroy(grass); Destroy(burnt);
        }
    }
}







