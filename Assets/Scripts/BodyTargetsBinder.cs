// Assets/Scripts/BodyTargetsBinder.cs
// OVRBody(+OVRSkeleton) の胸/骨盤ボーンを「名前」で探索して、VRIKのChest/Pelvisターゲットへ追従。
using System.Collections.Generic;
using UnityEngine;

public class BodyTargetsBinder : MonoBehaviour {
    [Header("OVR Body Skeleton")]
    public OVRSkeleton bodySkeleton;    // BodyRig 上の OVRSkeleton（SkeletonType=Body or FullBody）

    [Header("VRIK Targets")]
    public Transform chestTarget;       // /TrackingSpace/ChestTarget
    public Transform pelvisTarget;      // /TrackingSpace/PelvisTarget

    [Header("Tuning")]
    public float posSmooth = 15f;
    public float rotSmooth = 15f;
    public Vector3 chestPosOffset = Vector3.zero;
    public Vector3 pelvisPosOffset = Vector3.zero;
    public Vector3 chestRotOffsetEuler = Vector3.zero;
    public Vector3 pelvisRotOffsetEuler = Vector3.zero;

    Transform chestBone, pelvisBone;

    // 優先順位の高い候補名（左ほど優先）。Id.ToString() と Transform.name の両方に対して照合します。
    static readonly string[] ChestNames = new string[] {
        "Body_Chest", "Body_SpineUpper", "Body_SpineMiddle",
        "FullBody_Chest", "FullBody_SpineUpper", "FullBody_SpineMiddle",
        "Chest", "SpineUpper", "Spine2", "Spine1"
    };

    static readonly string[] PelvisNames = new string[] {
        "Body_Hips", "Body_SpineLower",
        "FullBody_Hips", "FullBody_Pelvis",
        "Hips", "Pelvis", "SpineLower"
    };

    // キャッシュ： (idString, goNameLower) -> Transform
    List<(string idStr, string goName, Transform t)> _index;

    void LateUpdate() {
        if (!bodySkeleton) return;
        if (!IsSkeletonReady()) return;
        if (_index == null) BuildIndex();

        if (!chestBone)  chestBone  = FindFirstByNames(ChestNames);
        if (!pelvisBone) pelvisBone = FindFirstByNames(PelvisNames);

        if (chestBone && chestTarget) {
            var tPos = chestBone.position + chestTarget.TransformVector(chestPosOffset);
            var tRot = chestBone.rotation * Quaternion.Euler(chestRotOffsetEuler);
            chestTarget.position = Vector3.Lerp(chestTarget.position, tPos, Time.deltaTime * posSmooth);
            chestTarget.rotation = Quaternion.Slerp(chestTarget.rotation, tRot, Time.deltaTime * rotSmooth);
        }

        if (pelvisBone && pelvisTarget) {
            var tPos = pelvisBone.position + pelvisTarget.TransformVector(pelvisPosOffset);
            var tRot = pelvisBone.rotation * Quaternion.Euler(pelvisRotOffsetEuler);
            pelvisTarget.position = Vector3.Lerp(pelvisTarget.position, tPos, Time.deltaTime * posSmooth);
            pelvisTarget.rotation = Quaternion.Slerp(pelvisTarget.rotation, tRot, Time.deltaTime * rotSmooth);
        }
    }

    bool IsSkeletonReady() {
        return bodySkeleton.IsInitialized && bodySkeleton.Bones != null && bodySkeleton.Bones.Count > 0;
    }

    void BuildIndex() {
        _index = new List<(string, string, Transform)>(bodySkeleton.Bones.Count);
        foreach (var b in bodySkeleton.Bones) {
            string idStr = b.Id.ToString().ToLowerInvariant();
            string goName = (b.Transform ? b.Transform.name : "").ToLowerInvariant();
            _index.Add((idStr, goName, b.Transform));
        }
        // 一度だけ、見つかる候補をログしておくとデバッグに便利
        // Debug.Log(string.Join(", ", _index.Select(x => x.idStr)));
    }

    Transform FindFirstByNames(string[] candidates) {
        // 1) 完全一致（Id.ToString）
        foreach (var cand in candidates) {
            string key = cand.ToLowerInvariant();
            for (int i = 0; i < _index.Count; i++) {
                if (_index[i].idStr == key) return _index[i].t;
            }
        }
        // 2) 完全一致（Transform.name）
        foreach (var cand in candidates) {
            string key = cand.ToLowerInvariant();
            for (int i = 0; i < _index.Count; i++) {
                if (_index[i].goName == key) return _index[i].t;
            }
        }
        // 3) 部分一致（Id.ToString）
        foreach (var cand in candidates) {
            string key = cand.ToLowerInvariant();
            for (int i = 0; i < _index.Count; i++) {
                if (_index[i].idStr.Contains(key)) return _index[i].t;
            }
        }
        // 4) 部分一致（Transform.name）
        foreach (var cand in candidates) {
            string key = cand.ToLowerInvariant();
            for (int i = 0; i < _index.Count; i++) {
                if (_index[i].goName.Contains(key)) return _index[i].t;
            }
        }
        return null;
    }
}
