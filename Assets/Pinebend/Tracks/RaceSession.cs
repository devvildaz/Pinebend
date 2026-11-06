using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pinebend
{
    [DefaultExecutionOrder(-1000)]
    public sealed class RaceSession : MonoBehaviour
    {
        public const int Laps=3;
        public static TrackRecipe Selected = TrackRecipe.Original();
        public static int PlayerPlace {get; private set;}
        static int finishers;

        public static int Finish(bool player)
        {
            int place=++finishers;
            if(player) { PlayerPlace=place; Time.timeScale=0; }
            return place;
        }

        void Awake()
        {
            Time.timeScale=1;
            PlayerPlace=finishers=0;
            if(!Selected.pinebend) ApplyLayout(Selected.Sample());
            var racers=FindObjectsByType<TestCar>();
            GameObject.Find("Apricot gravel").AddComponent<RallyRoad>().Initialize(racers[0].route);
            PeerRace.ConfigureGrid(racers);
            bool placed=StartingGrid.Place(racers);
            gameObject.AddComponent<RaceStartSequence>().Initialize(racers,placed);
            PeerRace.GridReady(placed);
        }

        static void ApplyLayout(Vector3[] points)
        {
            bool junctionActive=Selected.version>=3 && CircuitGenerator.Style(Selected.seed,Selected.style)==CircuitStyle.Intersection && CircuitGenerator.TryCrossing(points,out _);
            CircuitGenerator.TryCrossing(points,out var junction);
            var cars=FindObjectsByType<TestCar>();
            var route=cars[0].route;
            var old=new Vector3[route.Length];
            var facing=new Quaternion[route.Length];
            for(int i=0;i<route.Length;i++)
            {
                old[i]=route[i].position;
                facing[i]=route[i].rotation;
                var forward=Vector3.ProjectOnPlane(points[(i+1)%128]-points[(i+127)%128],Vector3.up);
                route[i].SetPositionAndRotation(points[i],Quaternion.LookRotation(forward));
            }
            int Closest(Vector3 p)
            {
                int best=0;
                for(int i=1;i<old.Length;i++) if((old[i]-p).sqrMagnitude<(old[best]-p).sqrMagnitude) best=i;
                return best;
            }
            var grounds=GameObject.Find("Pinebend - grounds").transform;
            foreach(Transform tree in grounds)
            {
                if(tree.name!="Fir") continue;
                int index=Closest(tree.position);
                tree.position=points[index]+route[index].rotation*(Quaternion.Inverse(facing[index])*(tree.position-old[index]));
                if(Selected.version>=2)
                {
                    Vector3 position=tree.position;
                    position.y=-6.02f;
                    tree.position=position;
                    if(System.Array.Exists(points,p=>Vector3.ProjectOnPlane(p-position,Vector3.up).sqrMagnitude<180))
                        tree.gameObject.SetActive(false);
                }
            }
            foreach(Transform part in GameObject.Find("D oval - sand circuit").transform)
            {
                if(!part.name.Contains("curb")) continue;
                var mesh=part.GetComponent<MeshFilter>().mesh;
                var vertices=mesh.vertices;
                for(int i=0;i<vertices.Length;i++)
                {
                    int index=Closest(vertices[i]);
                    vertices[i]=points[index]+route[index].rotation*(Quaternion.Inverse(facing[index])*(vertices[i]-old[index]));
                }
                if(junctionActive)
                {
                    var clipped=new System.Collections.Generic.List<Vector3>(vertices);
                    var triangles=new System.Collections.Generic.List<int>(mesh.triangles);
                    CircuitJunction.CutOpening(clipped,triangles,junction);
                    mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
                    mesh.vertices=clipped.ToArray();mesh.triangles=triangles.ToArray();
                    var collider=part.GetComponent<MeshCollider>();if(collider)collider.sharedMesh=mesh;
                }
                else mesh.vertices=vertices;
                mesh.RecalculateBounds(); mesh.RecalculateNormals();
            }
        }

        public static void Menu(){PeerRace.Close();Time.timeScale=1; SceneManager.LoadScene("MainMenu");}
        void OnGUI()
        {
            if(PlayerPlace==0) return;
            GUILayout.BeginArea(new Rect(Screen.width/2-160,Screen.height/2-60,320,120),GUI.skin.box);
            GUILayout.Label("Race complete    •    Position "+PlayerPlace+" / 8");
            if(GUILayout.Button("Race again")) {Time.timeScale=1; SceneManager.LoadScene("Pinebend");}
            if(GUILayout.Button("Track selection")) Menu();
            GUILayout.EndArea();
        }
    }
}




