using UnityEngine;

namespace Pinebend
{
    public class RallyHud : MonoBehaviour
    {
        float redrawAt;
        bool overview;
        void Update()
        {
            if(UnityEngine.InputSystem.Keyboard.current?.mKey.wasPressedThisFrame==true) {overview=!overview;redrawAt=0;}
        }
        static readonly Color Navy = new Color(.038f, .10f, .15f, .91f);
        static readonly Color Aqua = new Color(.32f, .92f, .92f);
        static readonly Color Gold = new Color(1, .70f, .17f);
        TestCar car;
        RallyWeapons weapons;
        RallyWeaponInput weaponInput;
        CircuitMap map;
        GUIStyle type;

        public void Initialize(TestCar driver)
        {
            car = driver;
            weapons = driver.GetComponent<RallyWeapons>();
            weaponInput = driver.GetComponent<RallyWeaponInput>();
            map=RallyRoad.Instance.CreateMap();
        }

        void OnDestroy(){if(map!=null) Destroy(map.Texture);}

        void OnGUI()
        {
            if (RaceStartSequence.Introducing || map==null || Event.current.type != EventType.Repaint) return;
            type ??= new GUIStyle(GUI.skin.label) {fontStyle=FontStyle.Bold, alignment=TextAnchor.MiddleLeft, padding=new RectOffset()};
            float scale = Mathf.Min(Screen.height / 760f, Screen.width / 900f);
            var saved = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            float height = Screen.height / scale;
            float width = Screen.width / scale;
            float smoke = Mathf.Max(WreckSmoke.Obscurity(car.chase.transform.position),car.Respawning ? 0 : WreckSmoke.Obscurity(car.transform.position));
            if (smoke > 0) Bar(0,0,width,height,new Color(.20f,.21f,.22f,.94f*smoke));
            float size=overview ? Mathf.Min(width*.58f,height*.66f) : 178;
            var area=overview ? new Rect((width-size)*.5f,(height-size)*.5f,size,size) : new Rect(20,20,size,size);
            Panel(area.x,area.y,size,size+21);
            if(Time.unscaledTime >= redrawAt)
            {
                map.Begin(car.transform.position,car.transform.forward,overview);
                foreach(var rival in TestCar.Racers)
                    if(rival!=car && !rival.Respawning) map.Mark(rival.transform.position,rival.transform.forward,false);
                map.Mark(car.transform.position,car.transform.forward,true);
                map.Upload();
                redrawAt=Time.unscaledTime+.033f;
            }
            GUI.DrawTexture(area,map.Texture);
            Label(area.x+8,area.yMax,12,overview ? "M   CLOSE MAP" : "M   FULL CIRCUIT",Aqua);
            float y = height - 165;
            Panel(20,y,242,145);
            Label(34,y+3,45,Mathf.RoundToInt(car.Speed).ToString(),Color.white);
            Label(36,y+54,15,"KM/H",Aqua);
            Label(165,y+9,14,"GEAR",Aqua);
            Label(165,y+27,32,car.Gear<0 ? "R" : car.Gear.ToString(),Color.white);
            Bar(35,y+80,212,1,new Color(.4f,.55f,.61f));
            Label(35,y+87,14,"LAP",Aqua);
            Label(35,y+102,27,car.Lap+"/"+RaceSession.Laps,Color.white);
            Label(165,y+87,14,"POSITION",Aqua);
            Label(165,y+102,27,car.Position+"/"+TestCar.Racers.Count,Color.white);

            Panel(width-211,20,191,car.LastLap>0?110:83);
            Label(width-197,26,13,"LAP TIME",Aqua);
            Label(width-197,43,25,TimeText(car.LapTime),Color.white);
            Label(width-197,76,14,car.Drifting ? "DRIFT" : car.Handbrake ? "HANDBRAKE" : "SPACE   HANDBRAKE",car.Drifting || car.Handbrake ? Gold : Color.white);
            if (car.LastLap>0) Label(width-197,102,14,"LAST   "+TimeText(car.LastLap),Aqua);
            float ammoY = car.LastLap > 0 ? 141 : 114;
            Panel(20,y-32,242,29);
            Label(32,y-29,14,"HP  " + car.Health,Color.white);
            Bar(100,y-20,147,7,Color.gray);
            Bar(100,y-20,147f*car.Health/TestCar.MaxHealth,7,car.Health <= 40 ? Gold : Aqua);
            if (car.Respawning)
            {
                Panel(width/2-94,height/2-20,188,40);
                Label(width/2-81,height/2-14,19,"RESPAWN   "+car.RespawnSeconds.ToString("0.0")+"s",Color.white);
            }
            if (weapons.Active != null)
            {
                var item = weapons.Active;
                Panel(width-211,ammoY,191,weapons.Queued == null ? 61 : 86);
                Label(width-197,ammoY+5,14,item.Preview,item.Type == RallyWeapons.Power.Missile && !item.Rolling ? Gold : Aqua);
                if (!item.Rolling)
                    Label(width-197,ammoY+27,15,(item.Type == RallyWeapons.Power.Minigun ? "RMB / RT    " : "LMB / LT    ")+item.Ammo,Color.white);
                if (weapons.Queued != null)
                    Label(width-197,ammoY+58,14,"NEXT    "+weapons.Queued.Preview,Color.white);
            }
            if (!car.Respawning && weapons.Missiles + weapons.Guided > 0)
            {
                Vector2 sight = weaponInput.Sight / scale;
                float aimY = height - sight.y;
                Color sightColor = weapons.LockedTarget ? Aqua : Gold;
                Bar(sight.x-14,aimY-2,10,4,Navy);
                Bar(sight.x+4,aimY-2,10,4,Navy);
                Bar(sight.x-2,aimY-14,4,10,Navy);
                Bar(sight.x-2,aimY+4,4,10,Navy);
                Bar(sight.x-1,aimY-1,3,3,Color.white);
                if (weapons.Guided > 0)
                {
                    Label(width-197,ammoY+(weapons.Queued == null ? 64 : 88),14,"RMB / RT   LOCK",Aqua);
                    if (weapons.LockedTarget)
                    {
                        var point=car.chase.WorldToScreenPoint(weapons.LockedTarget.transform.position);
                        if (point.z>0) Label(point.x/scale-28,height-point.y/scale-28,15,"LOCKED",Aqua);
                    }
                }
                Bar(sight.x-11,aimY-1,6,2,sightColor);
                Bar(sight.x+5,aimY-1,6,2,sightColor);
                Bar(sight.x-1,aimY-11,2,6,sightColor);
                Bar(sight.x-1,aimY+5,2,6,sightColor);
            }
            GUI.matrix = saved;
        }

        static string TimeText(float seconds) => $"{(int)seconds/60:00}:{seconds%60:00.00}";
        static void Panel(float x,float y,float w,float h) => Bar(x,y,w,h,Navy);
        static void Bar(float x,float y,float w,float h,Color color)
        {
            GUI.color=color;
            GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture);
            GUI.color=Color.white;
        }

        void Label(float x,float y,int size,string value,Color color)
        {
            type.fontSize=size;
            type.normal.textColor=color;
            GUI.Label(new Rect(x,y,200,size*1.35f),value,type);
        }
    }
}









