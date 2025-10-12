using System.Collections.Generic;
using UnityEngine;

// OVRのネームスペースは SDK 版により異なります。通常は OVR, Oculus など。
// using OVR; // あなたの環境に合わせて

public class OVRToHumanoidFingers : MonoBehaviour
{
    [Header("Targets")]
    public Animator avatarAnimator;          // Humanoid アバター
    public OVRSkeleton leftSkeleton;         // Left OVRHand の OVRSkeleton
    public OVRSkeleton rightSkeleton;        // Right OVRHand の OVRSkeleton
    public bool useOffsets = true;           // 基準姿勢からの差分で適用

    // Humanoid 指ボーン列挙（左手）
    private readonly HumanBodyBones[] LThumb  = { HumanBodyBones.LeftThumbProximal,  HumanBodyBones.LeftThumbIntermediate,  HumanBodyBones.LeftThumbDistal };
    private readonly HumanBodyBones[] LIndex  = { HumanBodyBones.LeftIndexProximal,  HumanBodyBones.LeftIndexIntermediate,  HumanBodyBones.LeftIndexDistal };
    private readonly HumanBodyBones[] LMiddle = { HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal };
    private readonly HumanBodyBones[] LRing   = { HumanBodyBones.LeftRingProximal,   HumanBodyBones.LeftRingIntermediate,   HumanBodyBones.LeftRingDistal };
    private readonly HumanBodyBones[] LPinky  = { HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal };

    // 右手
    private readonly HumanBodyBones[] RThumb  = { HumanBodyBones.RightThumbProximal,  HumanBodyBones.RightThumbIntermediate,  HumanBodyBones.RightThumbDistal };
    private readonly HumanBodyBones[] RIndex  = { HumanBodyBones.RightIndexProximal,  HumanBodyBones.RightIndexIntermediate,  HumanBodyBones.RightIndexDistal };
    private readonly HumanBodyBones[] RMiddle = { HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal };
    private readonly HumanBodyBones[] RRing   = { HumanBodyBones.RightRingProximal,   HumanBodyBones.RightRingIntermediate,   HumanBodyBones.RightRingDistal };
    private readonly HumanBodyBones[] RPinky  = { HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal };

    // OVR 側のボーン名に “Thumb0..3 / Index1..3 / Middle1..3 / Ring1..3 / Pinky0..3 / *Tip” などがあり、SDK で微妙に差があります。
    // 下のヘルパで種類ごとに並べ替えます。
    private enum Finger { Thumb, Index, Middle, Ring, Pinky }

    // 基準姿勢保持（オフセット用）
    private readonly Dictionary<Transform, Quaternion> avatarBindLocalRot = new();
    private readonly Dictionary<Transform, Quaternion> ovrBindLocalRot    = new();

    void Start()
    {
        if (avatarAnimator == null) avatarAnimator = GetComponentInChildren<Animator>();

        // Humanoid 指ボーンの基準回転を保存
        CacheBindPoseFor(CollectAvatarFingerBones(true));
        CacheBindPoseFor(CollectAvatarFingerBones(false));

        // OVR 側の基準回転を保存（OVRHand を開いた状態＝静止時に一度だけ）
        if (leftSkeleton != null)  CacheOVRBindPose(leftSkeleton);
        if (rightSkeleton != null) CacheOVRBindPose(rightSkeleton);
    }

    void LateUpdate()
    {
        if (leftSkeleton != null)  ApplyHand(leftSkeleton,  true);
        if (rightSkeleton != null) ApplyHand(rightSkeleton, false);
    }

    // ==== 基本処理 ====

    private void ApplyHand(OVRSkeleton skel, bool isLeft)
    {
        // OVR の指ごと配列を構成
        var ovrThumb  = GetOvrChain(skel, Finger.Thumb);
        var ovrIndex  = GetOvrChain(skel, Finger.Index);
        var ovrMiddle = GetOvrChain(skel, Finger.Middle);
        var ovrRing   = GetOvrChain(skel, Finger.Ring);
        var ovrPinky  = GetOvrChain(skel, Finger.Pinky);

        var avThumb  = GetAvatarChain(isLeft ? LThumb  : RThumb);
        var avIndex  = GetAvatarChain(isLeft ? LIndex  : RIndex);
        var avMiddle = GetAvatarChain(isLeft ? LMiddle : RMiddle);
        var avRing   = GetAvatarChain(isLeft ? LRing   : RRing);
        var avPinky  = GetAvatarChain(isLeft ? LPinky  : RPinky);

        ApplyChain(ovrThumb,  avThumb);
        ApplyChain(ovrIndex,  avIndex);
        ApplyChain(ovrMiddle, avMiddle);
        ApplyChain(ovrRing,   avRing);
        ApplyChain(ovrPinky,  avPinky);
    }

    private void ApplyChain(List<Transform> ovr, List<Transform> avatar)
    {
        int n = Mathf.Min(ovr.Count, avatar.Count);
        for (int i = 0; i < n; i++)
        {
            if (ovr[i] == null || avatar[i] == null) continue;

            // OVR の現在ローカル回転
            var ovrLocal = ovr[i].localRotation;

            // オフセット方式： (avatarBind^-1 * current) を足し込む
            if (useOffsets
                && ovrBindLocalRot.TryGetValue(ovr[i], out var ovrBind)
                && avatarBindLocalRot.TryGetValue(avatar[i], out var avBind))
            {
                // OVR の「基準からの差分」
                var delta = Quaternion.Inverse(ovrBind) * ovrLocal;
                // アバター側の基準に差分を乗算
                avatar[i].localRotation = avBind * delta;
            }
            else
            {
                // 直写し（座標系差で歪む時は useOffsets を true に）
                avatar[i].localRotation = ovrLocal;
            }
        }
    }

    // ==== 収集系 ====

    private List<Transform> GetAvatarChain(HumanBodyBones[] bones)
    {
        var list = new List<Transform>(bones.Length);
        foreach (var b in bones)
        {
            list.Add(avatarAnimator ? avatarAnimator.GetBoneTransform(b) : null);
        }
        return list;
    }

    private void CacheBindPoseFor(List<Transform> bones)
    {
        foreach (var t in bones)
        {
            if (t == null) continue;
            if (!avatarBindLocalRot.ContainsKey(t))
                avatarBindLocalRot[t] = t.localRotation;
        }
    }

    private List<Transform> CollectAvatarFingerBones(bool left)
    {
        var all = new List<Transform>();
        var arrs = left
            ? new[] { LThumb, LIndex, LMiddle, LRing, LPinky }
            : new[] { RThumb, RIndex, RMiddle, RRing, RPinky };
        foreach (var arr in arrs)
            all.AddRange(GetAvatarChain(arr));
        return all;
    }

    private void CacheOVRBindPose(OVRSkeleton skel)
    {
        foreach (var b in skel.Bones)
        {
            if (!ovrBindLocalRot.ContainsKey(b.Transform))
                ovrBindLocalRot[b.Transform] = b.Transform.localRotation;
        }
    }

    private List<Transform> GetOvrChain(OVRSkeleton skel, Finger finger)
    {
        // SDK により BoneId 名が少し違います。安全のため “名前で指を推定”します。
        // Thumb: Thumb1..3（0は手首寄りのメタカルプ、環境により存在しないことも）
        var list = new List<Transform>();
        foreach (var b in skel.Bones)
        {
            string n = b.Id.ToString(); // 例: Hand_Index1, Hand_Thumb2, Hand_Pinky3, Hand_MiddleTip など
            if (finger == Finger.Thumb  && n.Contains("Thumb"))  AddIfJoint(n, b.Transform, list);
            if (finger == Finger.Index  && n.Contains("Index"))  AddIfJoint(n, b.Transform, list);
            if (finger == Finger.Middle && n.Contains("Middle")) AddIfJoint(n, b.Transform, list);
            if (finger == Finger.Ring   && n.Contains("Ring"))   AddIfJoint(n, b.Transform, list);
            if (finger == Finger.Pinky  && (n.Contains("Pinky") || n.Contains("Little"))) AddIfJoint(n, b.Transform, list);
        }

        // 近位→中位→遠位の順に並べ替え（1/2/3、Tipは無視）
        list.Sort((a, b) =>
        {
            int ai = Rank(a.name);
            int bi = Rank(b.name);
            return ai.CompareTo(bi);
        });

        return list;

        int Rank(string s)
        {
            // …1(近位)、2(中間)、3(遠位)、Tipは末尾扱い
            if (s.Contains("1")) return 1;
            if (s.Contains("2")) return 2;
            if (s.Contains("3")) return 3;
            if (s.Contains("0")) return 0; // ある場合はメタカルプ（親指の付け根側）
            return 99; // Tipなど
        }

        void AddIfJoint(string name, Transform t, List<Transform> dst)
        {
            // “Tip”は基本無視（Distalまでに対応）
            if (name.Contains("Tip")) return;
            dst.Add(t);
        }
    }
}
