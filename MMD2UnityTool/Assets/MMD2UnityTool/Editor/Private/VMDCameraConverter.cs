using System.Collections;
using System.Collections.Generic;
using MMD.VMD;
using UnityEditor;
using UnityEngine;

public class VMDCameraConverter
{
	public static AnimationClip CreateAnimationClip(VMDFormat format)
	{
		VMDCameraConverter converter = new VMDCameraConverter();
		return converter.CreateAnimationClip_(format);
	}

	private AnimationClip CreateAnimationClip_(MMD.VMD.VMDFormat format)
	{
		
		AnimationClip clip = new AnimationClip();
		clip.name = format.name;

		CreateKeysForCamera(format, clip);  

		return clip;
	}

	void CreateKeysForCamera(MMD.VMD.VMDFormat format, AnimationClip clip)
	{
		const float tick_time = 1f / 30f;
		const float mmd4unity_unit = 0.085f;

		Keyframe[] posX_keyframes = new Keyframe[format.camera_list.camera_count];
		Keyframe[] posY_keyframes = new Keyframe[format.camera_list.camera_count];
		Keyframe[] posZ_keyframes = new Keyframe[format.camera_list.camera_count];

		Keyframe[] rotX_keyframes = new Keyframe[format.camera_list.camera_count];
		Keyframe[] rotY_keyframes = new Keyframe[format.camera_list.camera_count];
		Keyframe[] rotZ_keyframes = new Keyframe[format.camera_list.camera_count];

		Keyframe[] fov_keyframes = new Keyframe[format.camera_list.camera_count];

		//模拟一个相机的变换,用矩阵变换也可以,从世界坐标转局部坐标会很麻烦
		var cameraWorldObj = new GameObject();
		var cameraLocalObj = new GameObject();
		var cameraWorldTrans = cameraWorldObj.transform;
		var cameraLocalTrans = cameraLocalObj.transform;
		cameraLocalTrans.SetParent(cameraWorldTrans);

		for (int i = 0; i < format.camera_list.camera_count; i++)
		{
			MMD.VMD.VMDFormat.CameraData cameraData = format.camera_list.camera[i];

			//本身为欧拉角,这里是弧度值
			cameraWorldTrans.localEulerAngles = new Vector3(
				(-cameraData.rotation.x) * Mathf.Rad2Deg,                   //X相反轴
				(cameraData.rotation.y - Mathf.PI) * Mathf.Rad2Deg,
				(-cameraData.rotation.z) * Mathf.Rad2Deg);

			//位置
			//Unity 正数往屏幕里 ,location.z 越大越往屏幕里 ,属于右手坐标系,重点*Length 属于本地坐标,更旋转有关,需要矩阵变换
			cameraWorldTrans.localPosition = new Vector3(
				cameraData.location.x * -mmd4unity_unit,    //X相反轴
				cameraData.location.y * mmd4unity_unit,
				cameraData.location.z * -mmd4unity_unit);

			//posZ与length相反等效,因为这里是沿摄像机方向的,受旋转矩阵的影响
			var cameraObjChildTransLocalPosition = cameraLocalTrans.localPosition;
			cameraObjChildTransLocalPosition.z = cameraData.length * mmd4unity_unit;
			cameraLocalTrans.localPosition = cameraObjChildTransLocalPosition;

			posX_keyframes[i] = new Keyframe(cameraData.frame_no * tick_time, cameraLocalTrans.position.x);
			posY_keyframes[i] = new Keyframe(cameraData.frame_no * tick_time, cameraLocalTrans.position.y);
			posZ_keyframes[i] = new Keyframe(cameraData.frame_no * tick_time, cameraLocalTrans.position.z);

			rotX_keyframes[i] = new Keyframe(cameraData.frame_no * tick_time, cameraLocalTrans.eulerAngles.x);
			rotY_keyframes[i] = new Keyframe(cameraData.frame_no * tick_time, cameraLocalTrans.eulerAngles.y);
			rotZ_keyframes[i] = new Keyframe(cameraData.frame_no * tick_time, cameraLocalTrans.eulerAngles.z);

			//fov
			fov_keyframes[i] = new Keyframe(cameraData.frame_no * tick_time, cameraData.viewing_angle);


			//TODO插值:贝塞尔曲线插值(四个点)
			//https://blog.csdn.net/seizeF/article/details/96368503
			if (i > 0)
			{
				MMD.VMD.VMDFormat.CameraData lastCameraData = format.camera_list.camera[i - 1];

				float dFrame = cameraData.frame_no - lastCameraData.frame_no;

				//插值计算[0~127]
				//参考https://www.jianshu.com/p/ae312fb53fc3
				//经过观察得知interpolation前四位分别是(x1,x2)(y1,y2),后面都是重复的
				Vector2 p1 = new Vector2(lastCameraData.interpolation[0], lastCameraData.interpolation[2]);
				Vector2 p2 = new Vector2(lastCameraData.interpolation[1], lastCameraData.interpolation[3]);
				float p1Angle = Vector2.Angle(Vector2.right, p1);
				float p2Angle = Vector2.Angle(Vector2.right, p2);
				float outWeight = p1.x / dFrame;
				float inWeight = p2.x / dFrame;

			}

		}
		Object.DestroyImmediate(cameraWorldObj);

		AddDummyKeyframe(ref posX_keyframes);
		AddDummyKeyframe(ref posY_keyframes);
		AddDummyKeyframe(ref posZ_keyframes);
		AnimationCurve posX_curve = new AnimationCurve(posX_keyframes);
		AnimationCurve posY_curve = new AnimationCurve(posY_keyframes);
		AnimationCurve posZ_curve = new AnimationCurve(posZ_keyframes);

		AddDummyKeyframe(ref rotX_keyframes);
		AddDummyKeyframe(ref rotY_keyframes);
		AddDummyKeyframe(ref rotZ_keyframes);
		AnimationCurve rotX_curve = new AnimationCurve(rotX_keyframes);
		AnimationCurve rotY_curve = new AnimationCurve(rotY_keyframes);
		AnimationCurve rotZ_curve = new AnimationCurve(rotZ_keyframes);

		AddDummyKeyframe(ref fov_keyframes);
		AnimationCurve distance_curve = new AnimationCurve(fov_keyframes);

		AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"), posX_curve);
		AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.y"), posY_curve);
		AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.z"), posZ_curve);

		AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "localEulerAngles.x"), rotX_curve);
		AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "localEulerAngles.y"), rotY_curve);
		AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "localEulerAngles.z"), rotZ_curve);

		AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Camera), "field of view"), distance_curve);
	}

	void AddDummyKeyframe(ref Keyframe[] keyframes)
	{
		if (keyframes.Length == 1)
		{
			Keyframe[] newKeyframes = new Keyframe[2];
			newKeyframes[0] = keyframes[0];
			newKeyframes[1] = keyframes[0];
			newKeyframes[1].time += 0.001f / 60f;//1[ms]
			newKeyframes[0].outTangent = 0f;
			newKeyframes[1].inTangent = 0f;
			keyframes = newKeyframes;
		}
	}
}
