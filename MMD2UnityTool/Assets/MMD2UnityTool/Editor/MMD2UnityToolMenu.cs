using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class MMD2UnityToolMenu
{
    [MenuItem("Assets/MMD2UnityTool/Export Camera Vmd To Fbx")]
    public static void GetAssetBunleResList()
    {
        var selected = Selection.activeObject;
        string selectPath = AssetDatabase.GetAssetPath(selected);
        if (!string.IsNullOrEmpty(selectPath))
        {
            CameraVmdAgent camera_agent = new CameraVmdAgent(selectPath);
            camera_agent.CreateAnimationClip();
            Debug.LogFormat("[{0}]:Export Camera Vmd Success!", System.DateTime.Now);
        }
        else
        {
            Debug.LogError("没有选中文件或文件夹");
        }
    }
}
