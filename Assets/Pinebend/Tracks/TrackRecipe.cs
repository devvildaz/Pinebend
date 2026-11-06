using System;
using System.IO;
using UnityEngine;

namespace Pinebend
{
    // The file stores a recipe. Version 1 fixes the generator, jump profiles and marker order.
    [Serializable]
    public sealed class TrackRecipe
    {
        public int version;
        public string title;
        public int seed;
        public CircuitStyle style;
        public bool Supported => version==1 || ((version==2 || version==3) && !pinebend && Enum.IsDefined(typeof(CircuitStyle),style));
        public bool pinebend;
        public static TrackRecipe Original() => new TrackRecipe { version=1, title="Pinebend", pinebend=true };
        public static TrackRecipe Generate(CircuitStyle style=CircuitStyle.Surprise)
        {
            int seed=Guid.NewGuid().GetHashCode();
            return new TrackRecipe{version=3,seed=seed,style=style,title=CircuitGenerator.Style(seed,style)+" "+seed.ToString("X8").Substring(0,4)};
        }

        public Vector3[] Sample()
        {
            if(version==2) return CircuitGenerator.Build(seed,CircuitGenerator.LegacyStyle(seed,style));
            if(version==3) return CircuitGenerator.Build(seed,style);
            var random = new System.Random(seed);
            float Radius(float min,float range) => min+(float)random.NextDouble()*range;
            float width=Radius(65,43), length=Radius(83,52), phase=Radius(0,6), twist=Radius(0,6);
            float hills=Radius(4,5);
            var points=new Vector3[128];
            for(int i=0;i<points.Length;i++)
            {
                float t=i*Mathf.PI*2/points.Length;
                float radius=1+.17f*Mathf.Sin(3*t+phase)+.085f*Mathf.Cos(5*t+twist);
                points[i]=new Vector3(-width*radius*Mathf.Cos(t),hills*(1-Mathf.Cos(2*t)) + 2*(1-Mathf.Cos(3*t)),length*radius*Mathf.Sin(t));
            }
            Vector3 origin=points[0];
            var tangent=Vector3.ProjectOnPlane(points[1]-points[127],Vector3.up);
            Quaternion align=Quaternion.Inverse(Quaternion.LookRotation(tangent));
            for(int i=0;i<points.Length;i++) points[i]=align*(points[i]-origin);
            return points;
        }
    }

    public static class TrackLibrary
    {
        public static string Folder => Path.Combine(Application.persistentDataPath,"Tracks");
        public static string[] Files() { Directory.CreateDirectory(Folder); return Directory.GetFiles(Folder,"*.pbrtrack"); }
        public static TrackRecipe Read(string path)
        {
            var file=new FileInfo(path.Trim().Trim('"'));
            if(!file.Exists || file.Length>8192 || !file.Extension.Equals(".pbrtrack",StringComparison.OrdinalIgnoreCase))
                throw new IOException("Choose a Pinebend .pbrtrack file (8 KB maximum).");
            var recipe=JsonUtility.FromJson<TrackRecipe>(File.ReadAllText(file.FullName));
            if(recipe==null || !recipe.Supported || string.IsNullOrWhiteSpace(recipe.title) || recipe.title.Length>48 || recipe.title.IndexOfAny(Path.GetInvalidFileNameChars())>=0)
                throw new InvalidDataException("Invalid track name or unsupported track version.");
            return recipe;
        }
        public static string Save(TrackRecipe recipe)
        {
            string name=recipe.title.Trim();
            if(name.Length==0 || name.Length>48 || name.IndexOfAny(Path.GetInvalidFileNameChars())>=0) throw new IOException("Enter a valid track name.");
            Directory.CreateDirectory(Folder);
            string path=Path.Combine(Folder,name+"-"+recipe.seed.ToString("X8")+".pbrtrack");
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                File.WriteAllText(temp,JsonUtility.ToJson(recipe,true));
                if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
            }
            finally { if(File.Exists(temp)) File.Delete(temp); }
            return path;
        }
    }
}


