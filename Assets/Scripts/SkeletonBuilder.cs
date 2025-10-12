using System.Collections.Generic;
using UnityEngine;

// Minimal SkeletonBuilder shim to satisfy OculusHandPoseTransfer usage.
// This is intentionally minimal — if you have an existing SkeletonBuilder library, prefer using that instead.

public class SkeletonBuilderParams { }

public class SkeletonBuilder {
    public Dictionary<HumanBodyBones, Transform> Skeleton { get; private set; } = new Dictionary<HumanBodyBones, Transform>();

    Transform _root;
    List<HumanBone> _humanBones = new List<HumanBone>();

    public SkeletonBuilder(Transform root) {
        _root = new GameObject("_SkeletonRoot").transform;
        _root.SetParent(root, false);
        _root.localPosition = Vector3.zero;
        _root.localRotation = Quaternion.identity;
    }

    public void AddBasicSkeleton(SkeletonBuilderParams p) {
        // Create a minimal set of root/body bones required by AvatarBuilder
        EnsureBoneExists(HumanBodyBones.Hips, "Hips", Vector3.zero, Quaternion.identity, parent: null);
        EnsureBoneExists(HumanBodyBones.Spine, "Spine", new Vector3(0, 0.1f, 0), Quaternion.identity, HumanBodyBones.Hips);
        EnsureBoneExists(HumanBodyBones.Chest, "Chest", new Vector3(0, 0.2f, 0), Quaternion.identity, HumanBodyBones.Spine);
        EnsureBoneExists(HumanBodyBones.Neck, "Neck", new Vector3(0, 0.35f, 0), Quaternion.identity, HumanBodyBones.Chest);
        EnsureBoneExists(HumanBodyBones.Head, "Head", new Vector3(0, 0.5f, 0), Quaternion.identity, HumanBodyBones.Neck);
        EnsureBoneExists(HumanBodyBones.LeftUpperLeg, "LeftUpperLeg", new Vector3(-0.1f, -0.2f, 0), Quaternion.identity, HumanBodyBones.Hips);
        EnsureBoneExists(HumanBodyBones.RightUpperLeg, "RightUpperLeg", new Vector3(0.1f, -0.2f, 0), Quaternion.identity, HumanBodyBones.Hips);
        EnsureBoneExists(HumanBodyBones.LeftUpperArm, "LeftUpperArm", new Vector3(-0.15f, 0.25f, 0), Quaternion.identity, HumanBodyBones.Chest);
        EnsureBoneExists(HumanBodyBones.RightUpperArm, "RightUpperArm", new Vector3(0.15f, 0.25f, 0), Quaternion.identity, HumanBodyBones.Chest);
        EnsureBoneExists(HumanBodyBones.LeftHand, "LeftHand", new Vector3(-0.3f, 0.1f, 0), Quaternion.identity, HumanBodyBones.LeftUpperArm);
        EnsureBoneExists(HumanBodyBones.RightHand, "RightHand", new Vector3(0.3f, 0.1f, 0), Quaternion.identity, HumanBodyBones.RightUpperArm);

        // build skeleton bones array
        _skeletonBones = new List<SkeletonBone>();
        foreach (var kv in Skeleton) {
            var sb = new SkeletonBone();
            sb.name = kv.Value.name;
            sb.position = kv.Value.localPosition;
            sb.rotation = kv.Value.localRotation;
            sb.scale = kv.Value.localScale;
            _skeletonBones.Add(sb);
        }
    }

    List<SkeletonBone> _skeletonBones = new List<SkeletonBone>();

    void EnsureBoneExists(HumanBodyBones boneId, string boneName, Vector3 pos, Quaternion rot, HumanBodyBones? parent) {
        if (Skeleton.ContainsKey(boneId)) return;
        var go = new GameObject(boneName);
        go.transform.SetParent(_root, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        Skeleton[boneId] = go.transform;

        HumanBone hb = new HumanBone(); hb.humanName = boneId.ToString(); hb.boneName = go.name; hb.limit = new HumanLimit(); _humanBones.Add(hb);
    }

    public void Add(HumanBodyBones bone, HumanBodyBones parent, Vector3 localPosition, Quaternion localRotation) {
        var go = new GameObject(bone.ToString());
        // Parent to the specified parent bone if it exists, otherwise attach to root
        if (Skeleton.TryGetValue(parent, out var parentT))
            go.transform.SetParent(parentT, false);
        else
            go.transform.SetParent(_root, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        Skeleton[bone] = go.transform;

        // track human bone mapping for HumanDescription
        HumanBone hb = new HumanBone();
        hb.humanName = bone.ToString();
        hb.boneName = go.name;
        hb.limit = new HumanLimit();
        _humanBones.Add(hb);
    }

    public void UpdateRotation(HumanBodyBones bone, Quaternion rot) {
        if (Skeleton.TryGetValue(bone, out var t)) t.localRotation = rot;
    }

    public HumanDescription GetHumanDescription() {
        var hd = new HumanDescription();
        hd.human = _humanBones.ToArray();
        hd.skeleton = _skeletonBones.ToArray();
        return hd;
    }

    public void Add(HumanBodyBones humanBone, System.Enum parent, Vector3 localPosition, Quaternion localRotation) {
        // fallback overload used by the other script; try to cast parent to HumanBodyBones and use it
        if (parent is HumanBodyBones hbParent)
            Add(humanBone, hbParent, localPosition, localRotation);
        else
            Add(humanBone, HumanBodyBones.Hips, localPosition, localRotation);
    }
}
