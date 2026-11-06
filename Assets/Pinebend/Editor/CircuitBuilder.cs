using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Pinebend.Editor {
public static class CircuitBuilder {
 const string root="Assets/Pinebend/",scene=root+"Pinebend.unity";
 static readonly Dictionary<string,Material> mats=new();
 static Transform land,car,track; static Mesh pine;
 static Material Mat(string hex){if(mats.TryGetValue(hex,out var m))return m;var path=root+"Art/"+hex+".mat";m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));ColorUtility.TryParseHtmlString("#"+hex,out var c);m.color=c;m.SetFloat("_Smoothness",.17f);AssetDatabase.CreateAsset(m,path);}return mats[hex]=m;}
 static Transform Group(string name,Transform parent=null){var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
 static GameObject Shape(string n,PrimitiveType type,Transform par,Vector3 pos,Vector3 size,string color,bool solid=false){var g=GameObject.CreatePrimitive(type);g.name=n;g.transform.SetParent(par,false);g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=Mat(color);if(!solid)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
 static GameObject Box(string n,Transform t,Vector3 p,Vector3 s,string c,bool solid=false)=>Shape(n,PrimitiveType.Cube,t,p,s,c,solid);
 static Mesh MeshAsset(string n,Vector3[] vs,int[] ix){var m=new Mesh{name=n,vertices=vs,triangles=ix};m.RecalculateNormals();m.RecalculateBounds();var path=root+"Art/"+n+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(m,old);return old;}AssetDatabase.CreateAsset(m,path);return m;}
 static GameObject MeshObj(string n,Transform par,Mesh mesh,string c){var g=Group(n,par).gameObject;g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=Mat(c);return g;}
 static Vector3 V(float x,float z)=>new Vector3(x,0,z);
 static Vector3 Bezier(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t){float u=1-t;return u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d;}
 static Vector3 Point(int i) => 1.8f * OriginalPoint(i);
 static Vector3 OriginalPoint(int i){float a=((i%128)+128)%128/32f;float t=a%1;
 if(a<1)return Bezier(V(0,0),V(0,27),V(0,50),V(0,70),t);
 if(a<2)return Bezier(V(0,70),V(0,115),V(68,118),V(79,65),t);
 if(a<3)return Bezier(V(79,65),V(89,20),V(81,-32),V(47,-49),t);
 return Bezier(V(47,-49),V(14,-66),V(0,-42),V(0,0),t);}
 static Vector3 Dir(int i)=>(Point(i+1)-Point(i-1)).normalized;
 static Vector3 Edge(int i,float offset)=>Point(i)+Vector3.Cross(Vector3.up,Dir(i))*offset;
 static void Ribbon(string name,float a,float b,float y,string col,int parity=-1){
 var v=new List<Vector3>();var ix=new List<int>();for(int i=0;i<128;i++){if(parity>=0&&i%2!=parity)continue;int k=v.Count;foreach(var p in new[]{Edge(i,a),Edge(i+1,a),Edge(i+1,b),Edge(i,b)})v.Add(p+Vector3.up*y);ix.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});}MeshObj(name,track,MeshAsset(name,v.ToArray(),ix.ToArray()),col);}
 static Mesh Cone(){var v=new List<Vector3>();var ix=new List<int>();for(int i=0;i<7;i++){float a=i*Mathf.PI*2/7,b=(i+1)*Mathf.PI*2/7;int k=v.Count;v.Add(new Vector3(Mathf.Sin(a),0,Mathf.Cos(a)));v.Add(new Vector3(Mathf.Sin(b),0,Mathf.Cos(b)));v.Add(Vector3.up);ix.AddRange(new[]{k,k+1,k+2});}return MeshAsset("Seven sided fir",v.ToArray(),ix.ToArray());}
 static GameObject Shell(string n,Vector3 low,float w,float l,Vector3 top,float wt,float lt,string c){
 Vector3[] ring(Vector3 mid,float x,float z)=>new[]{mid+new Vector3(-x,0,-z*.75f),mid+new Vector3(-x*.8f,0,-z),mid+new Vector3(x*.8f,0,-z),mid+new Vector3(x,0,-z*.75f),mid+new Vector3(x,0,z*.8f),mid+new Vector3(x*.8f,0,z),mid+new Vector3(-x*.8f,0,z),mid+new Vector3(-x,0,z*.8f)};
 var a=ring(low,w,l);var b=ring(top,wt,lt);var vs=new List<Vector3>();var ix=new List<int>();for(int i=0;i<8;i++){int j=(i+1)%8,k=vs.Count;vs.AddRange(new[]{a[i],a[j],b[j],b[i],b[j],b[i],top});ix.AddRange(new[]{k,k+2,k+1,k,k+3,k+2,k+4,k+5,k+6});}return MeshObj(n,car,MeshAsset(n,vs.ToArray(),ix.ToArray()),c);}
 public static void AddUnderbody(Transform chassis)
 {
     if(chassis.Find("Chassis floor")) return;
     var lower = chassis.Find("Lower valance").GetComponent<MeshFilter>().sharedMesh.vertices;
     // Each of the eight valance panels starts with a corner on the open bottom rim.
     var rim = new Vector3[8];
     var triangles = new List<int>();
     for(int i=0;i<rim.Length;i++) rim[i]=lower[i*7];
     for(int i=1;i<rim.Length-1;i++) triangles.AddRange(new[]{0,i,i+1});
     MeshObj("Chassis floor",chassis,MeshAsset("Chassis floor",rim,triangles.ToArray()),"242D38");
     foreach(float x in new[]{-.49f,.49f})
         Box("Chassis rail",chassis,new Vector3(x,-.22f,0),new Vector3(.085f,.06f,2.8f),"213445");
     Box("Sump guard",chassis,new Vector3(0,-.235f,.98f),new Vector3(1.16f,.055f,.83f),"849297");
 }
 public static void Create(){
 Directory.CreateDirectory(root+"Art");AssetDatabase.Refresh();var s=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(s);
 land=Group("Pinebend - grounds");track=Group("D oval - sand circuit");pine=Cone();
 Box("Grass and driving surface",land,V(35,22)-Vector3.up*.52f,new Vector3(550,1,530),"9ABB53",true);
 Ribbon("Apricot gravel",-6,6,.002f,"E9AF73");foreach(int sign in new[]{-1,1}){float a=sign==1?6:-6.65f;Ribbon("Ivory curb "+sign,a,a+.65f,.015f,"FFF3D7",0);Ribbon("Vermilion curb "+sign,a,a+.65f,.015f,"DD5242",1);}
 for(int i=0;i<16;i++)for(int j=0;j<2;j++)Box("Finish tile",track,new Vector3(-5.625f+i*.75f,.023f,j*.75f),new Vector3(.75f,.015f,.75f),(i+j)%2==0?"213445":"FFF3D7");
 foreach(int sign in new[]{-1,1}){Box("Gate foot",track,new Vector3(sign*7,1,0),new Vector3(1.15f,2,1.45f),"DD5242",true);Box("Gate upright",track,new Vector3(sign*7,3.65f,0),new Vector3(.85f,3.35f,1.05f),"185582",true);}
 Box("Timing gantry",track,new Vector3(0,5.6f,0),new Vector3(14.8f,1.3f,1.06f),"185582");
 var text=Group("Start sign",track);text.localPosition=new Vector3(0,5.62f,-.56f);var tm=text.gameObject.AddComponent<TextMesh>();tm.text="◆   P I N E B E N D   /   S T A R T   ◆";tm.anchor=TextAnchor.MiddleCenter;tm.characterSize=.16f;tm.fontSize=44;tm.color=Color.white;
 var rng=new System.Random(17);for(int i=0;i<128;i+=2){foreach(int side in new[]{-1,1}){if(side==1&&i%4!=0)continue;float dist=11+(float)rng.NextDouble()*13;var p=Edge(i,side*dist);if(Array.Exists(System.Array.ConvertAll(System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Range(0,128)),Point), q=>(p-q).sqrMagnitude<110))continue;float h=3+(float)rng.NextDouble()*4;var t=Group("Fir",land);t.position=p;Box("Trunk",t,Vector3.up*h*.15f,new Vector3(.42f,h*.3f,.42f),"84583C");var crown=MeshObj("Crown",t,pine,i%3==0?"508941":"689942").transform;crown.localPosition=Vector3.up*h*.21f;crown.localScale=new Vector3(h*.34f,h*.83f,h*.34f);}}
 for(int i=0;i<19;i++){float a=i*.331f;var hill=Shape("Distant ridge",PrimitiveType.Sphere,land,new Vector3(65+Mathf.Cos(a)*335,-9,45+Mathf.Sin(a)*290),new Vector3(88,42+i%3*9,70),"84AC70");}
 car=Group("01 - Rally hatch");car.position=new Vector3(0,.8f,-11);var rb=car.gameObject.AddComponent<Rigidbody>();rb.mass=1080;rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
 var hull=car.gameObject.AddComponent<BoxCollider>();hull.center=new Vector3(0,.13f,0);hull.size=new Vector3(1.68f,.6f,3.55f);
 Shell("Lower valance",new Vector3(0,-.19f,0),.84f,1.76f,new Vector3(0,.05f,0),.95f,1.89f,"F49320");
 AddUnderbody(car);
 Shell("Rally blue body",new Vector3(0,.05f,0),.95f,1.89f,new Vector3(0,.46f,0),.90f,1.81f,"147DC3");
 Shell("Smoked glass",new Vector3(0,.46f,-.16f),.81f,1.10f,new Vector3(0,.97f,-.26f),.66f,.68f,"223C50");
 Shell("Tangerine roof",new Vector3(0,.96f,-.26f),.68f,.7f,new Vector3(0,1.03f,-.26f),.65f,.68f,"FF9B24");
 Box("Bonnet stripe",car,new Vector3(0,.47f,1.32f),new Vector3(.35f,.015f,.77f),"FF9B24");
 foreach(int side in new[]{-1,1}){Box("Tail lamp",car,new Vector3(side*.57f,.29f,-1.86f),new Vector3(.40f,.16f,.11f),"FA4F3D");Box("Headlamp",car,new Vector3(side*.59f,.27f,1.85f),new Vector3(.38f,.14f,.10f),"FFF3D7");Box("Wing support",car,new Vector3(side*.54f,.59f,-1.48f),new Vector3(.09f,.31f,.12f),"213445");}
 Box("Rear wing",car,new Vector3(0,.75f,-1.48f),new Vector3(1.88f,.12f,.34f),"213445");
 var ctrl=car.gameObject.AddComponent<TestCar>();ctrl.tires=new Transform[4];ctrl.wheels=new WheelCollider[4];
 for(int i=0;i<4;i++){int side=i%2==0?-1:1;var axle=Group("Suspension "+i,car);axle.localPosition=new Vector3(side*.86f,.0f,i<2?1.15f:-1.15f);var w=axle.gameObject.AddComponent<WheelCollider>();w.radius=.36f;w.suspensionDistance=.26f;w.mass=24;var spring=w.suspensionSpring;spring.spring=30000;spring.damper=4300;w.suspensionSpring=spring;ctrl.wheels[i]=w;
 var tire=ctrl.tires[i]=Group("Wheel "+i,car);tire.localPosition=axle.localPosition-Vector3.up*.13f;var rubber=Shape("Rubber",PrimitiveType.Cylinder,tire,Vector3.zero,new Vector3(.72f,.12f,.72f),"242D38");rubber.transform.localRotation=Quaternion.Euler(0,0,90);var hub=Shape("Alloy",PrimitiveType.Cylinder,tire,Vector3.right*side*.125f,new Vector3(.42f,.014f,.42f),"ADB9BD");hub.transform.localRotation=rubber.transform.localRotation;}
 ctrl.route=new Transform[128];var guides=Group("Circuit markers");guides.gameObject.AddComponent<RaceSession>();for(int i=0;i<128;i++){var t=ctrl.route[i]=Group("Marker "+i,guides);t.position=Point(i);t.rotation=Quaternion.LookRotation(Dir(i));if(i%32==0){var g=t.gameObject.AddComponent<BoxCollider>();g.isTrigger=true;g.center=Vector3.up;g.size=new Vector3(13,3,1);t.gameObject.AddComponent<TimingGate>().number=i/32;}}
 var cam=Group("Chase camera");cam.tag="MainCamera";ctrl.chase=cam.gameObject.AddComponent<Camera>();ctrl.chase.fieldOfView=58;ctrl.chase.clearFlags=CameraClearFlags.SolidColor;ctrl.chase.backgroundColor=new Color(.36f,.76f,.95f);cam.position=car.position+new Vector3(0,3.5f,-7.3f);cam.LookAt(car.position+new Vector3(0,.9f,3.8f));cam.gameObject.AddComponent<AudioListener>();
 var sun=Group("Afternoon sun").gameObject.AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(44,-33,0);sun.intensity=1.25f;sun.color=new Color(1,.96f,.87f);sun.shadows=LightShadows.Soft;
 RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.75f,.81f,.86f);RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=94;RenderSettings.fogEndDistance=245;RenderSettings.fogColor=ctrl.chase.backgroundColor;
 EditorSceneManager.SaveScene(s,scene);RallyGrid.Install();EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Pinebend/MainMenu.unity",true),new EditorBuildSettingsScene(scene,true)};AssetDatabase.SaveAssets();
 }
 [MenuItem("Pinebend/Build Windows player")]
 public static void Build(){Directory.CreateDirectory("Builds/Pinebend");var report=BuildPipeline.BuildPlayer(new[]{"Assets/Pinebend/MainMenu.unity",scene},"Builds/Pinebend/Pinebend.exe",BuildTarget.StandaloneWindows64,BuildOptions.None);File.WriteAllText("Builds/Pinebend-build.txt",report.summary.result+" | Errors: "+report.summary.totalErrors+" | Warnings: "+report.summary.totalWarnings+" | Bytes: "+report.summary.totalSize);if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Windows build failed");}
}
}




