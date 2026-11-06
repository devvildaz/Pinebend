using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pinebend
{
    public sealed class MainMenu : MonoBehaviour
    {
        enum Page { Home, Solo, Multiplayer }
        Page page;
        public Vector3[] original;
        TrackRecipe selection;
        CircuitMap previewMap;
        string[] files=Array.Empty<string>();
        Vector2 scroll, pageScroll;
        string importPath="", message="";
        int style;
        static readonly string[] Styles={"Mix","Speed","Hairpins","Canyon","Crossroads"};
        bool host=true;
        string address="127.0.0.1";
        int port=7777;
        string portText="7777";

        void Start()
        {
            Select(RaceSession.Selected);
            if(!string.IsNullOrEmpty(PeerRace.Notice)){message=PeerRace.Notice;page=Page.Multiplayer;PeerRace.Notice="";}
            Run(Refresh);
            address=PlayerPrefs.GetString("PeerAddress",address);
            portText=PlayerPrefs.GetInt("PeerPort",port).ToString();
            host=PlayerPrefs.GetInt("PeerHost",1)==1;
        }
        void Refresh(){files=TrackLibrary.Files(); Array.Sort(files,StringComparer.OrdinalIgnoreCase);}
        void Select(TrackRecipe recipe)
        {
            selection=recipe;
            if(previewMap!=null)Destroy(previewMap.Texture);
            var points=recipe.pinebend?original:recipe.Sample();
            var left=new Vector3[points.Length];var right=new Vector3[points.Length];
            for(int i=0;i<points.Length;i++)
            {
                var tangent=(points[(i+1)%points.Length]-points[(i+points.Length-1)%points.Length]).normalized;
                var side=Vector3.Cross(Vector3.up,tangent).normalized*6;
                left[i]=points[i]-side;right[i]=points[i]+side;
            }
            Vector3? junction=recipe.version>=3 && CircuitGenerator.Style(recipe.seed,recipe.style)==CircuitStyle.Intersection && CircuitGenerator.TryCrossing(points,out var crossing) ? crossing : (Vector3?)null;
            previewMap=new CircuitMap(left,right,junction);
            previewMap.Begin(Vector3.zero,Vector3.forward,true);previewMap.Upload();
            message="";
        }
        void OnDestroy(){if(previewMap!=null)Destroy(previewMap.Texture);}
        void Run(Action action)
        {
            try { action(); } catch(Exception error){message=error.Message;}
        }
        void Save()
        {
            string path=TrackLibrary.Save(selection);
            Refresh(); message="Saved "+Path.GetFileName(path);
        }
        void OnGUI()=>DrawMenu(Screen.width,Screen.height);

        public void DrawMenu(float viewportWidth,float viewportHeight)
        {
            if(selection==null)return;
            float width=viewportWidth,height=viewportHeight;
            float panelWidth=Mathf.Min(1000,width-24),panelHeight=height-24;
            bool stacked=panelWidth<720;
            GUILayout.BeginArea(new Rect((width-panelWidth)/2,12,panelWidth,panelHeight),GUI.skin.box);
            pageScroll=GUILayout.BeginScrollView(pageScroll,GUILayout.ExpandHeight(true));
            GUILayout.Space(16);
            GUILayout.Label("P I N E B E N D    /    R A L L Y");
            GUILayout.Space(14);
            if(PeerRace.Lobby)
            {
                GUILayout.Label(PeerRace.Status);
                if(PeerRace.CanStart && GUILayout.Button("Start race",GUILayout.Height(46))) Run(()=>PeerRace.StartGrid(selection));
                if(GUILayout.Button("Leave lobby")) PeerRace.Close();
            }
            else if(page==Page.Home)
            {
                if(GUILayout.Button("Single player",GUILayout.Height(55))) page=Page.Solo;
                if(GUILayout.Button("Multiplayer",GUILayout.Height(55))) OpenMultiplayer();
                if(GUILayout.Button("Quit",GUILayout.Height(36))) Application.Quit();
            }
            else
            {
                GUILayout.Label(page==Page.Solo?"Single player    /    Track selection":"Multiplayer    /    Peer-hosted setup");
                if(!stacked)GUILayout.BeginHorizontal();
                float column=stacked ? panelWidth-42 : (panelWidth-48)*.5f;
                GUILayout.BeginVertical(GUILayout.Width(column));
                if(GUILayout.Button("Pinebend oval")) Select(TrackRecipe.Original());
                scroll=GUILayout.BeginScrollView(scroll,GUILayout.Height(105));
                foreach(string file in files)
                    if(GUILayout.Button(Path.GetFileNameWithoutExtension(file))) Run(()=>Select(TrackLibrary.Read(file)));
                GUILayout.EndScrollView();
                style=GUILayout.SelectionGrid(style,Styles,stacked ? 3 : 5);
                if(GUILayout.Button("Generate new circuit",GUILayout.Height(34))) Select(TrackRecipe.Generate(style==4 ? CircuitStyle.Intersection : (CircuitStyle)style));
                GUILayout.Label("Track name");
                selection.title=GUILayout.TextField(selection.title,48);
                if(GUILayout.Button("Save shareable track file")) Run(Save);
                if(GUILayout.Button("Open track folder")) Run(()=>{Refresh(); Application.OpenURL(new Uri(TrackLibrary.Folder+Path.DirectorySeparatorChar).AbsoluteUri);});
                GUILayout.Label("Import a .pbrtrack file by path");
                importPath=GUILayout.TextField(importPath);
                if(GUILayout.Button("Import into library")) Run(()=>{var recipe=TrackLibrary.Read(importPath); TrackLibrary.Save(recipe); Select(recipe); Refresh(); message="Imported "+recipe.title;});
                GUILayout.EndVertical();
                GUILayout.BeginVertical(GUILayout.Width(column));
                GUILayout.Label(selection.title+"    /    3 laps");
                DrawPreview(GUILayoutUtility.GetRect(0,Mathf.Min(240,column*.65f),GUILayout.ExpandWidth(true)));
                if(page==Page.Solo)
                {
                    if(GUILayout.Button("Start race",GUILayout.Height(44))) {RaceSession.Selected=selection; SceneManager.LoadScene("Pinebend");}
                }
                else
                {
                    host=GUILayout.Toolbar(host?0:1,new[]{"Host","Join"})==0;
                    if(!host){GUILayout.Label("Host address"); address=GUILayout.TextField(address,128);}
                    GUILayout.Label("Direct connection port");
                    portText=GUILayout.TextField(portText,5);
                    GUILayout.Label("Peer-hosted combat • 2–8 drivers • 3 laps");
                    if(GUILayout.Button(host ? "Open host lobby" : "Join host")) Run(()=>{
                        if(!int.TryParse(portText,out port)||port<1||port>65535||(!host&&Uri.CheckHostName(address)==UriHostNameType.Unknown))
                            throw new ArgumentException("Enter a valid host and port (1–65535).");
                        string path=TrackLibrary.Save(selection);
                        PlayerPrefs.SetString("PeerTrack",path); PlayerPrefs.SetString("PeerAddress",address);
                        PlayerPrefs.SetInt("PeerPort",port); PlayerPrefs.SetInt("PeerHost",host?1:0); PlayerPrefs.Save(); Refresh(); PeerRace.Open(host,address,port);
                    });
                }
                GUILayout.EndVertical(); if(!stacked)GUILayout.EndHorizontal();
                GUILayout.Label(string.IsNullOrEmpty(PeerRace.Notice)?message:PeerRace.Notice);
                if(GUILayout.Button("Back",GUILayout.Width(100))) {page=Page.Home;message="";}
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

        }

        void DrawPreview(Rect rect)
        {
            if(previewMap==null || Event.current.type!=EventType.Repaint)return;
            GUI.Box(rect,GUIContent.none);
            GUI.DrawTexture(new Rect(rect.x+6,rect.y+6,Mathf.Max(1,rect.width-12),Mathf.Max(1,rect.height-12)),previewMap.Texture,ScaleMode.ScaleToFit);
        }
        void OpenMultiplayer()
        {
            page=Page.Multiplayer;
            string saved=PlayerPrefs.GetString("PeerTrack","");
            if(File.Exists(saved)) Run(()=>Select(TrackLibrary.Read(saved)));
        }
    }
}



