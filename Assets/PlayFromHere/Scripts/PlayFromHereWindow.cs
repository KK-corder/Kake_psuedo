#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace LightShaft.PFH
{
    public class PlayFromHereWindow : EditorWindow
    {
        PlayFromHereConfig config;

        // Add menu item named "My Window" to the Window menu
        [MenuItem("Window/Play From Here")]
        public static void ShowWindow()
        {
            PlayFromHereConfig config = Resources.Load("Config/PFH_Config") as PlayFromHereConfig;
            AssetDatabase.OpenAsset(config.GetInstanceID());
        }
    }
}
    
#endif