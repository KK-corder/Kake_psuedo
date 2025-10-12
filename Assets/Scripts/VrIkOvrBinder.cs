// VrIkOvrBinder.cs
using UnityEngine;
using RootMotion.FinalIK;

public class VrIkOvrBinder : MonoBehaviour {
    public VRIK ik;
    public OVRCameraRig rig;
    public Transform headTarget, leftHandTarget, rightHandTarget;
    public Transform leftElbowHint, rightElbowHint;
    public OVRSkeleton leftSkeleton, rightSkeleton; // OVRHandPrefab 内の OVRSkeleton を割り当て

    public float posSmoothing = 20f, rotSmoothing = 20f;

    Transform lWrist, rWrist;

    void Start() {
        // 自動参照（Inspector未設定でも拾えるように）
        if (!rig) rig = FindObjectOfType<OVRCameraRig>();
        if (!ik) ik = FindObjectOfType<VRIK>();
        if (!headTarget || !leftHandTarget || !rightHandTarget) {
            Debug.LogError("Targets未割り当てです"); enabled = false; return;
        }
        InvokeRepeating(nameof(TryCacheWristBones), 0.2f, 0.5f);
    }

    void TryCacheWristBones() {
        if (leftSkeleton && leftSkeleton.IsInitialized && lWrist == null)
            lWrist = GetBone(leftSkeleton, OVRSkeleton.BoneId.Hand_WristRoot);
        if (rightSkeleton && rightSkeleton.IsInitialized && rWrist == null)
            rWrist = GetBone(rightSkeleton, OVRSkeleton.BoneId.Hand_WristRoot);
        if (lWrist && rWrist) CancelInvoke(nameof(TryCacheWristBones));
    }

    Transform GetBone(OVRSkeleton s, OVRSkeleton.BoneId id) {
        foreach (var b in s.Bones) if (b.Id == id) return b.Transform;
        return null;
    }

    void LateUpdate() {
        // 常にHMDを HeadTarget に反映
        SmoothFollow(headTarget, rig.centerEyeAnchor);

        bool controllers =
            (OVRInput.GetActiveController() & (OVRInput.Controller.LTouch | OVRInput.Controller.RTouch)) != 0;

        if (controllers) {
            // コントローラ使用時
            SmoothFollow(leftHandTarget, rig.leftHandAnchor);
            SmoothFollow(rightHandTarget, rig.rightHandAnchor);
        } else {
            // ハンドトラッキング使用時（手首ボーン）
            if (lWrist) SmoothFollow(leftHandTarget, lWrist);
            if (rWrist) SmoothFollow(rightHandTarget, rWrist);
        }
        // 肘ヒントは一度位置決めして固定でOK（必要なら追従ロジックを追加）
    }

    void SmoothFollow(Transform t, Transform src) {
        t.position = Vector3.Lerp(t.position, src.position, Time.deltaTime * posSmoothing);
        t.rotation = Quaternion.Slerp(t.rotation, src.rotation, Time.deltaTime * rotSmoothing);
    }
}
