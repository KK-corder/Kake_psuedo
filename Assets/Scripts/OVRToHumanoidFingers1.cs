using System.Collections.Generic;
using UnityEngine;

public class OVRToHumanoidFingers1 : MonoBehaviour {
    [Header("Refs")]
    public Animator animator;
    public OVRSkeleton leftSkeleton;   // OVRHandPrefab_Left の OVRSkeleton
    public OVRSkeleton rightSkeleton;  // OVRHandPrefab_Right の OVRSkeleton

    [Header("Options")]
    public bool autoCalibrateOnReady = true;
    public float lerp = 25f; // 回転追従スムージング

struct Map {
    public HumanBodyBones human;
    public OVRSkeleton.BoneId[] candidates;
    public Transform dst, src;
    public Quaternion offsetLocal;
    public Quaternion dstRest;          // 追加：dstの基準姿勢
    public Vector3 curlAxisLocal;       // 追加：曲げ軸（ローカル）。とりあえず Vector3.right or Vector3.forward を試す
    public float twistGain;             // 追加：曲げの強さ(0..1)
    public float swingGain;             // 追加：横開きの強さ(0..1)
}

    readonly List<Map> mapsL = new();
    readonly List<Map> mapsR = new();
    bool leftReady, rightReady, calibrated;

    void Reset(){ if(!animator) animator = GetComponent<Animator>(); }

    void Awake(){
        if(!animator) animator = GetComponent<Animator>();
        BuildMaps(mapsL, true);
        BuildMaps(mapsR, false);
    }

    // 変更版 BuildMaps（差分）:
void BuildMaps(List<Map> list, bool left){
    HumanBodyBones HB(string l, string r)
        => left ? (HumanBodyBones)System.Enum.Parse(typeof(HumanBodyBones), l)
                : (HumanBodyBones)System.Enum.Parse(typeof(HumanBodyBones), r);
    Map Mk(HumanBodyBones h, params OVRSkeleton.BoneId[] ids)
        => new Map{ human=h, candidates=ids };

    // Thumb: 0 をフォールバック候補に追加
    list.Add(Mk(HB(nameof(HumanBodyBones.LeftThumbProximal),    nameof(HumanBodyBones.RightThumbProximal)),
                OVRSkeleton.BoneId.Hand_Thumb1));
    list.Add(Mk(HB(nameof(HumanBodyBones.LeftThumbIntermediate), nameof(HumanBodyBones.RightThumbIntermediate)),
                OVRSkeleton.BoneId.Hand_Thumb2));
    list.Add(Mk(HB(nameof(HumanBodyBones.LeftThumbDistal),       nameof(HumanBodyBones.RightThumbDistal)),
                OVRSkeleton.BoneId.Hand_Thumb3));


    // Index（そのまま）
    list.Add(Mk(HB(nameof(HumanBodyBones.LeftIndexProximal),     nameof(HumanBodyBones.RightIndexProximal)),
                OVRSkeleton.BoneId.Hand_Index1));
    list.Add(Mk(HB(nameof(HumanBodyBones.LeftIndexIntermediate),  nameof(HumanBodyBones.RightIndexIntermediate)),
                OVRSkeleton.BoneId.Hand_Index2));
    list.Add(Mk(HB(nameof(HumanBodyBones.LeftIndexDistal),        nameof(HumanBodyBones.RightIndexDistal)),
                OVRSkeleton.BoneId.Hand_Index3));

    // Middle（そのまま）
    list.Add(Mk(HB(nameof(HumanBodyBones.LeftMiddleProximal),     nameof(HumanBodyBones.RightMiddleProximal)),
                OVRSkeleton.BoneId.Hand_Middle1));
    list.Add(Mk(HB(nameof(HumanBodyBones.LeftMiddleIntermediate),  nameof(HumanBodyBones.RightMiddleIntermediate)),
                OVRSkeleton.BoneId.Hand_Middle2));
    list.Add(Mk(HB(nameof(HumanBodyBones.LeftMiddleDistal),        nameof(HumanBodyBones.RightMiddleDistal)),
                OVRSkeleton.BoneId.Hand_Middle3));

    // Ring（そのまま）
    list.Add(Mk(HB(nameof(HumanBodyBones.LeftRingProximal),       nameof(HumanBodyBones.RightRingProximal)),
                OVRSkeleton.BoneId.Hand_Ring1));
    list.Add(Mk(HB(nameof(HumanBodyBones.LeftRingIntermediate),    nameof(HumanBodyBones.RightRingIntermediate)),
                OVRSkeleton.BoneId.Hand_Ring2));
    list.Add(Mk(HB(nameof(HumanBodyBones.LeftRingDistal),         nameof(HumanBodyBones.RightRingDistal)),
                OVRSkeleton.BoneId.Hand_Ring3));

    // Little/Pinky: 0 をフォールバック候補に追加（重要！）
    list.Add(Mk(HB(nameof(HumanBodyBones.LeftLittleProximal),     nameof(HumanBodyBones.RightLittleProximal)),
                OVRSkeleton.BoneId.Hand_Pinky1));
    list.Add(Mk(HB(nameof(HumanBodyBones.LeftLittleIntermediate),  nameof(HumanBodyBones.RightLittleIntermediate)),
                OVRSkeleton.BoneId.Hand_Pinky2));
    list.Add(Mk(HB(nameof(HumanBodyBones.LeftLittleDistal),        nameof(HumanBodyBones.RightLittleDistal)),
                OVRSkeleton.BoneId.Hand_Pinky3));
}

    void Update(){
        if(!leftReady && leftSkeleton && leftSkeleton.IsInitialized && leftSkeleton.Bones != null && leftSkeleton.Bones.Count>0){
            Cache(mapsL, leftSkeleton); leftReady = true;
        }
        if(!rightReady && rightSkeleton && rightSkeleton.IsInitialized && rightSkeleton.Bones != null && rightSkeleton.Bones.Count>0){
            Cache(mapsR, rightSkeleton); rightReady = true;
        }
        if(!calibrated && autoCalibrateOnReady && leftReady && rightReady){
            Calibrate(mapsL); Calibrate(mapsR); calibrated = true;
        }
        if(Input.GetKeyDown(KeyCode.C)){ Calibrate(mapsL); Calibrate(mapsR); calibrated = true; }
    }

    void LateUpdate(){
        if(leftReady)  Apply(mapsL);
        if(rightReady) Apply(mapsR);
    }

void Cache(List<Map> list, OVRSkeleton skel) {
    // ...既存処理...
    for (int i = 0; i < list.Count; i++) {
        var m = list[i];
        if (m.dst) {
            m.dstRest = m.dst.localRotation;
            // ★軸の仮定：まずは X を曲げ軸に（合わなければ Z に変える）
            m.curlAxisLocal = Vector3.right;
            m.twistGain = 1.0f;   // 曲げは全量
            m.swingGain = 0.25f;  // 横開きは弱め
        }
        list[i] = m;
    }
}

    void Calibrate(List<Map> list){
        for(int i=0;i<list.Count;i++){
            var m = list[i];
            if(m.dst && m.src){
                m.offsetLocal = Quaternion.Inverse(m.src.localRotation) * m.dst.localRotation;
                list[i] = m;
            }
        }
        Debug.Log("OVRToHumanoidFingers: Calibrated.");
    }

void Apply(List<Map> list){
    float t = Time.deltaTime * lerp;
    for(int i=0;i<list.Count;i++){
        var m = list[i];
        if(!m.dst || !m.src) continue;

        // OVR回転→オフセットでdst系に合わせた目標回転
        var target = m.src.localRotation * m.offsetLocal;

        // dst基準での“差”に分解（curlAxisLocal は dst ローカル空間）
        SwingTwist(target, m.curlAxisLocal, out var swing, out var twist);

        // 重みづけ（横開きは弱め）
        var twistW = Quaternion.Slerp(Quaternion.identity, twist, m.twistGain);
        var swingW = Quaternion.Slerp(Quaternion.identity, swing, m.swingGain);

        // 基準姿勢に合成して適用
        var newLocal = m.dstRest * (twistW * swingW);
        m.dst.localRotation = Quaternion.Slerp(m.dst.localRotation, newLocal, t);
    }
}

// 追加：スイング-ツイスト分解（軸はローカル）
void SwingTwist(Quaternion q, Vector3 axisLocal, out Quaternion swing, out Quaternion twist) {
    axisLocal = axisLocal.normalized;
    // “q が axis をどこへ回すか”を見てswingを作り、残りをtwistに
    Vector3 r = q * axisLocal;
    swing = Quaternion.FromToRotation(axisLocal, r);
    twist = q * Quaternion.Inverse(swing);
}
}
