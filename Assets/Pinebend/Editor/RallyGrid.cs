using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pinebend.Editor
{
    public static class RallyGrid
    {
        public static void Install()
        {
            var player = GameObject.Find("01 - Rally hatch").GetComponent<TestCar>();
            if (Object.FindObjectsByType<RallyBot>().Length > 0) return;
            string[] colors = {"DD5242","FFF3D7","689942","FF9B24","185582","FA4F3D","F49320"};
            float[] lanes = {-2.7f, 2.7f, -2.5f, 2.5f, -2.3f, 2.3f, -2.6f};
            var rearRow = player.route[player.route.Length-4];
            player.transform.SetPositionAndRotation(rearRow.position + rearRow.right*2.6f + Vector3.up*.8f,rearRow.rotation);
            for (int i=0; i<colors.Length; i++)
            {
                var clone = Object.Instantiate(player.gameObject);
                clone.name = "Bot " + (i+1) + " - Rally hatch";
                var row = player.route[player.route.Length-1-i/2];
                clone.transform.SetPositionAndRotation(row.position + row.right*lanes[i] + Vector3.up*.8f,row.rotation);
                var driver = clone.GetComponent<TestCar>();
                driver.chase = null;
                var bot = clone.AddComponent<RallyBot>();
                bot.laneOffset = lanes[i];
                bot.cruisingSpeed = 34 + (i%4)*1.2f;
                clone.transform.Find("Rally blue body").GetComponent<MeshRenderer>().sharedMaterial =
                    AssetDatabase.LoadAssetAtPath<Material>("Assets/Pinebend/Art/"+colors[i]+".mat");
            }
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            EditorSceneManager.SaveScene(player.gameObject.scene);
        }
    }
}


