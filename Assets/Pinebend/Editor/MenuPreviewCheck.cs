using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Pinebend.Editor
{
    // Edit-mode layout inspection only: never loads or drives a race.
    public sealed class MenuPreviewCheck : EditorWindow
    {
        MainMenu menu;
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        public static void Open(int width,int height)
        {
            var window=GetWindow<MenuPreviewCheck>();
            window.titleContent=new GUIContent("Menu layout check");
            window.position=new Rect(40,40,width,height);
            window.Show();
        }
        void OnEnable()
        {
            var obj=new GameObject("Menu inspection"){hideFlags=HideFlags.HideAndDontSave};
            menu=obj.AddComponent<MainMenu>();
            typeof(MainMenu).GetMethod("Select",Private).Invoke(menu,new object[]{new TrackRecipe{version=3,seed=104729,style=CircuitStyle.Intersection,title="Crossroads preview"}});
            var page=typeof(MainMenu).GetField("page",Private);
            page.SetValue(menu,System.Enum.ToObject(page.FieldType,2));
        }
        void OnGUI(){if(menu)menu.DrawMenu(position.width,position.height);}
        void OnDisable()
        {
            if(!menu)return;
            var field=typeof(MainMenu).GetField("previewMap",Private);
            var map=field.GetValue(menu) as CircuitMap;
            if(map!=null)DestroyImmediate(map.Texture);
            field.SetValue(menu,null);
            DestroyImmediate(menu.gameObject);
        }
    }
}
