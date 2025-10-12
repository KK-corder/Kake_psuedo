// Assets/Scripts/ElbowHintAutoFollow.cs
using UnityEngine;

public class ElbowHintAutoFollow : MonoBehaviour {
    [Header("Refs")]
    public Animator avatarAnimator;         // アバターのAnimator
    public Transform leftHandTarget;        // VRIKのLeftHandTarget
    public Transform rightHandTarget;       // VRIKのRightHandTarget
    public Transform leftElbowHint;         // VRIKのLeftElbowHint
    public Transform rightElbowHint;        // VRIKのRightElbowHint

    [Header("Tuning")]
    public float along = 0.55f;             // 肩→手の線上にどれだけ寄せる（0.5前後）
    public float outward = 0.12f;           // 体の外側オフセット量
    public float forward = 0.06f;           // 手方向へ少し押し出す量
    public float liftGain = 0.6f;           // 手が肩より上に行った時の“肘の持ち上がり”倍率
    public float smooth = 20f;              // 追従スムージング

    Transform lShoulder, rShoulder;

    void Start() {
        if (!avatarAnimator) {
            Debug.LogError("ElbowHintAutoFollow: Animator未設定"); enabled = false; return;
        }
        lShoulder = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftShoulder)
                 ?? avatarAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm)?.parent;
        rShoulder = avatarAnimator.GetBoneTransform(HumanBodyBones.RightShoulder)
                 ?? avatarAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm)?.parent;
    }

    void LateUpdate() {
        if (lShoulder && leftHandTarget && leftElbowHint)
            DriveElbow(leftElbowHint, lShoulder.position, leftHandTarget.position, +1);
        if (rShoulder && rightHandTarget && rightElbowHint)
            DriveElbow(rightElbowHint, rShoulder.position, rightHandTarget.position, -1);
    }

    void DriveElbow(Transform hint, Vector3 shoulder, Vector3 hand, int sideSign) {
        Vector3 sh = hand - shoulder;
        float dist = sh.magnitude;
        if (dist < 1e-4f) return;
        Vector3 dir = sh / dist;

        // 体の外側方向（手方向×上方向）
        Vector3 side = Vector3.Cross(dir, Vector3.up).normalized * sideSign;

        // 手が肩より高いほど、肘を上げる
        float extraLift = Mathf.Max(0f, hand.y - shoulder.y) * liftGain;

        // 基本位置：肩→手の線上 + 外側 + 少し前 + 持ち上げ
        Vector3 target =
            shoulder
          + dir * (dist * along)
          + side   * outward
          + dir    * forward
          + Vector3.up * extraLift;

        hint.position = Vector3.Lerp(hint.position, target, Time.deltaTime * smooth);
    }
}
