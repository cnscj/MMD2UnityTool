using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;



[InitializeOnLoad]
public class MotionVmdAgent
{
    public class DataCacheObject : ScriptableObject
    {
        public GameObject fbxIns;
        public int recordStatus;
        public string recordClipsOutPath;
    }

    string _fbxFilePath;
    public MotionVmdAgent(string filePath)
    {
        _fbxFilePath = filePath;

    }
    public void Export()
    {
        var isImportSuccess = ImportModelFbx(_fbxFilePath);
        if (!isImportSuccess) return;

        string fbxRoot = Path.GetDirectoryName(_fbxFilePath);
        var spliteClips = SplitFbxAnim(_fbxFilePath);

        var fbxIns = InstanceMotionFbx(_fbxFilePath);

        GetOrLoadCache().fbxIns = fbxIns;

        var timeline = CreateTimeline(Path.Combine(fbxRoot, string.Format("{0}.playable", Path.GetFileNameWithoutExtension(_fbxFilePath))), spliteClips);
        SetupPlayable(fbxIns, timeline);

        RecordMotionClips(fbxIns);
    }

    ////////////////////////////////////////////////
    static DataCacheObject _cache;
    static string dataCacheFilePath = "Assets/Resources/RecordTmplCache.asset";
    static DataCacheObject LoadCache()
    {
        if (_cache == null)
        {
            if (File.Exists(dataCacheFilePath))
            {
                _cache = AssetDatabase.LoadAssetAtPath<DataCacheObject>(dataCacheFilePath);
            }
        }

        return _cache;
    }
    static DataCacheObject GetOrLoadCache()
    {
        _cache = LoadCache();
        if (_cache == null)
        {
            _cache = AssetDatabase.LoadAssetAtPath<DataCacheObject>(dataCacheFilePath);
        }
        return _cache;
    }

    static MotionVmdAgent()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange mode)
    {
        if (mode == PlayModeStateChange.ExitingPlayMode)
        {
            var movationCache = LoadCache();
            if (movationCache == null)
                return;

            var motionRecord = movationCache.recordStatus;
            if (motionRecord == 1)
            {
                //判断是否已经执行过了
                //关闭窗口
                GetOrLoadCache().recordStatus = 0;

                var recorderWindow = (UnityEditor.Recorder.RecorderWindow)UnityEditor.Recorder.RecorderWindow.GetWindow(typeof(UnityEditor.Recorder.RecorderWindow), false, "TestRecord");
                recorderWindow.Close();

                AssetDatabase.Refresh();

                var outputFile = GetOrLoadCache().recordClipsOutPath;
                var outRoot = Path.GetDirectoryName(outputFile);
                var clipFiles = Directory.EnumerateFiles(outRoot, "*.anim", SearchOption.TopDirectoryOnly);

                List<AnimationClip> animationClips = new List<AnimationClip>();
                foreach (var clipPath in clipFiles)
                {
                    var realPath = clipPath.Substring(clipPath.IndexOf("Assets"));
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(realPath);
                    animationClips.Add(clip);
                }

                var fbxIns = GetOrLoadCache().fbxIns;
                var animatorController = CreateAnimatorController(animationClips.ToArray());
                SetupAnimatorController(fbxIns, animatorController);

                ExportMotionFbx(fbxIns);

                GameObject.Destroy(fbxIns);
                GetOrLoadCache().fbxIns = null;
            }

        }
    }


    private static bool ImportModelFbx(string fbxPath)
    {
        //设置为Humain导出
        ModelImporter modelImporter = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (modelImporter != null)
        {
            if (modelImporter.animationType != ModelImporterAnimationType.Human || !modelImporter.importAnimation)
            {
                modelImporter.animationType = ModelImporterAnimationType.Human;
                modelImporter.importAnimation = true;
                modelImporter.SaveAndReimport();
            }
            return true;
        }
        else
        {
            Debug.Log("当前选择的文件不是带有AnimationClip的FBX文件");
        }

        return false;
    }

    private static AnimationClip[] SplitFbxAnim(string fbxPath)
    {
        UnityEngine.Object[] objects = AssetDatabase.LoadAllAssetsAtPath(fbxPath);        //加载FBX里所有物体
        if (objects != null && objects.Length > 0)
        {
            string fbxRoot = Path.GetDirectoryName(fbxPath);
            string fbxName = Path.GetFileNameWithoutExtension(fbxPath);
            List<AnimationClip> animationClips = new List<AnimationClip>();
            foreach (UnityEngine.Object obj in objects)     //遍历选择的物体
            {
                AnimationClip fbxClip = obj as AnimationClip;
                if (fbxClip != null)
                {
                    if (fbxClip.name.IndexOf("__preview__", System.StringComparison.Ordinal) == -1)
                    {
                        AnimationClip clip = new AnimationClip();       //new一个AnimationClip存放生成的AnimationClip
                        EditorUtility.CopySerialized(fbxClip, clip);    //复制
                        AssetDatabase.CreateAsset(clip, Path.Combine(fbxRoot, string.Format("{0}@{1}.anim", fbxName, fbxClip.name)));    //生成文件

                        animationClips.Add(clip);
                    }
                }
            }
            return animationClips.ToArray();
        }
        return null;
    }

    private static GameObject InstanceMotionFbx(string fbxPath)
    {
        GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        var goInst = GameObject.Instantiate(fbxPrefab);
        goInst.name = fbxPrefab.name;
        return goInst;
    }

    private static TimelineAsset CreateTimeline(string targetFile, AnimationClip[] animationClips = null)
    {
        if (animationClips != null && animationClips.Length > 0)
        {
            var asset = TimelineAsset.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(asset, targetFile);

            foreach (var clip in animationClips)
            {
                var track = asset.CreateTrack<AnimationTrack>();
                var timeClip = track.CreateClip(clip);
                timeClip.displayName = clip.name;
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            return asset;
        }

        return null;
    }

    private static AnimatorController CreateAnimatorController(AnimationClip[] animations)
    {
        var firstClis = animations[0];
        var clipPath = AssetDatabase.GetAssetPath(firstClis);
        var savePath = Path.Combine(Path.GetDirectoryName(clipPath), string.Format("{0}.controller", "clipCtrl"));
        var asset = AnimatorController.CreateAnimatorControllerAtPath(savePath);
        foreach(var clip in animations)
        {
            asset.AddMotion(clip);
        }

        AssetDatabase.SaveAssets();

        return asset;
    }

    private static void SetupPlayable(GameObject modelIns, TimelineAsset timelineAsset)
    {
        var director = modelIns.GetComponent<PlayableDirector>();
        if (director == null)
        {
            director = modelIns.AddComponent<PlayableDirector>();
        }
        var animator = modelIns.GetComponent<Animator>();
        if (animator == null)
        {
            animator = modelIns.AddComponent<Animator>();
        }

        if (director != null && animator != null)
        {
            director.playableAsset = timelineAsset;
            foreach (PlayableBinding item in director.playableAsset.outputs)
            {
                director.SetGenericBinding(item.sourceObject, animator);
            }
        }
    }

    private static void SetupAnimatorController(GameObject modelIns, AnimatorController animatorController)
    {
        var animator = modelIns.GetComponent<Animator>() ?? modelIns.AddComponent<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.runtimeAnimatorController = animatorController;
    }

    private static void RecordMotionClips(GameObject modelIns, Action<AnimationClip[]> callback = null)
    {
        var recorderWindow = (UnityEditor.Recorder.RecorderWindow)UnityEditor.Recorder.RecorderWindow.GetWindow(typeof(UnityEditor.Recorder.RecorderWindow), false, "TestRecord");

        if (modelIns == null)
            return;

        var endTime = GetTimelineLength(modelIns);
        var settings = new UnityEditor.Recorder.Input.AnimationInputSettings();
        settings.gameObject = modelIns;
        settings.AddComponentToRecord(typeof(Transform));

        var recoredr = new UnityEditor.Recorder.AnimationRecorderSettings();
        recoredr.name = "Animation Clip";
        recoredr.AnimationInputSettings = settings;

        var controller = UnityEditor.Recorder.RecorderControllerSettings.GetGlobalSettings();
        List<UnityEditor.Recorder.RecorderSettings> delSettingList = new List<UnityEditor.Recorder.RecorderSettings>(controller.RecorderSettings);
        foreach (var oldSetting in delSettingList)
        {
            controller.RemoveRecorder(oldSetting);
        }
        controller.AddRecorderSettings(recoredr);
        controller.SetRecordModeToTimeInterval(0, endTime);
        controller.Save();

        recorderWindow.SetRecorderControllerSettings(controller);
        recorderWindow.StartRecording();
        recorderWindow.Show();

        //运行态下无法保存,修改任何数据,这里利用其他方式存储
        GetOrLoadCache().recordStatus = 1;
        GetOrLoadCache().recordClipsOutPath = recoredr.OutputFile;
    }

    private static void ExportMotionFbx(GameObject modelIns)
    {
        Assembly asm = Assembly.Load("Unity.Formats.Fbx.Editor");
        if (asm == null)
            return;

        Type exportModelEditorWindowType = asm.GetType("UnityEditor.Formats.Fbx.Exporter.ExportModelEditorWindow");
        MethodInfo initMethod = exportModelEditorWindowType.GetMethod("Init");
        MethodInfo exportMethod = exportModelEditorWindowType.GetMethod("Export", BindingFlags.Instance | BindingFlags.NonPublic);  //保护成员
        MethodInfo closeMethod = exportModelEditorWindowType.GetMethod("Close");

        HashSet<GameObject> toExport = new HashSet<GameObject>();
        toExport.Add(modelIns);

        var windwodInstance = initMethod.Invoke(exportModelEditorWindowType, new object[] {
            System.Linq.Enumerable.Cast<UnityEngine.Object>(toExport),
            GetClipNameByFbxIns(modelIns),
            false
        });

        MethodInfo exportModelSettingsInstanceMethod = exportModelEditorWindowType.GetMethod("get_ExportModelSettingsInstance");
        var exportModelSettingsInstance = exportModelSettingsInstanceMethod.Invoke(windwodInstance, null);

        Type exportModelSettingsType = asm.GetType("UnityEditor.Formats.Fbx.Exporter.ExportModelSettings");
        MethodInfo infoMethod = exportModelSettingsType.GetMethod("get_info");
        var infoInstance = infoMethod.Invoke(exportModelSettingsInstance, null);

        Type exportModelSettingsSerializeType = asm.GetType("UnityEditor.Formats.Fbx.Exporter.ExportModelSettingsSerialize");
        MethodInfo setExportFormatMethod = exportModelSettingsSerializeType.GetMethod("SetExportFormat");
        setExportFormatMethod.Invoke(infoInstance, new object[] { 1 });
        MethodInfo setModelAnimIncludeOptionMethod = exportModelSettingsSerializeType.GetMethod("SetModelAnimIncludeOption");
        setModelAnimIncludeOptionMethod.Invoke(infoInstance, new object[] { 1 });

        exportMethod.Invoke(windwodInstance,null);
        closeMethod.Invoke(windwodInstance, null);

    }

    private static float GetTimelineLength(GameObject fbxIns)
    {
        float recordTime = 0f;
        var playableDirector = fbxIns.GetComponent<PlayableDirector>();
        var timelineAsset = playableDirector.playableAsset;

        foreach (PlayableBinding pb in timelineAsset.outputs)
        {
            var track = pb.sourceObject as TrackAsset;
            if (track != null)
            {
                if (track.end > recordTime)
                {
                    recordTime = (float)track.end;
                }
            }
        }

        return recordTime;
    }

    private static string GetClipNameByFbxIns(GameObject fbxIns)
    {
        string retName = fbxIns.name;

        var animator = fbxIns.GetComponent<Animator>();
        if (animator != null)
        {
            var runtimeAnimatorController = animator.runtimeAnimatorController;
            if (runtimeAnimatorController != null && runtimeAnimatorController.animationClips != null)
            {
                foreach (var clip in runtimeAnimatorController.animationClips)
                {
                    retName = clip.name;
                    break;
                }
            }
        }
        return retName;
    }


}
