using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class MMD2UnityToolMenu
{
    [MenuItem("Assets/MMD2UnityTool/Export Fbx(With Anim) To Humanoid Fbx")]
    public static void SpliteMMDFbxMotionToHumainMotionFbx()
    {
        var selected = Selection.activeObject;
        string selectPath = AssetDatabase.GetAssetPath(selected);
        if (!string.IsNullOrEmpty(selectPath))
        {
            MotionVmdAgent motion_agent = new MotionVmdAgent(selectPath);
            motion_agent.Export();
            //Debug.LogFormat("[{0}]:Export Motion Vmd Success!", System.DateTime.Now);

        }
        else
        {
            Debug.LogError("没有选中文件");
        }
    }

    [MenuItem("Assets/MMD2UnityTool/Export Camera Vmd To Anim")]
    public static void ExportCameraVmdToAnim()
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
            Debug.LogError("没有选中文件");
        }
    }

}
