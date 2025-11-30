using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// OVRSkeletonを使用して手を握っているかを判定するクラス
/// </summary>
public class GrabJudge : MonoBehaviour
{
    [Header("Hand Skeleton Settings")]
    public OVRSkeleton rightHandSkeleton;
    public OVRSkeleton leftHandSkeleton;

    [Header("Grab Detection Settings")]
    [Range(0.1f, 1.0f)]
    public float grabThreshold = 0.3f; // 握り判定の閾値（0.3 = 30%握った状態）
    
    [Range(0.05f, 0.5f)]
    public float releaseThreshold = 0.4f; // 離し判定の閾値（ヒステリシス効果）
    
    public bool enableGrabStabilization = true; // 握り状態の安定化
    public int grabStabilityFrames = 5; // 安定化に必要なフレーム数

    [Header("Debug Settings")]
    public bool logGrabEvents = true;
    public bool showDetailedDebug = false;
    
    [Header("Current Grab Status - 現在の握り状態")]
    [SerializeField] private string rightHandStatus = "Not Grabbing";
    [SerializeField] private string leftHandStatus = "Not Grabbing";
    [SerializeField] private string grabSummary = "None";

    // 握り状態の管理
    [HideInInspector] public bool isRightHandGrabbing = false;
    [HideInInspector] public bool isLeftHandGrabbing = false;
    
    // 安定化用の内部変数
    private int rightGrabFrameCount = 0;
    private int rightReleaseFrameCount = 0;
    private int leftGrabFrameCount = 0;
    private int leftReleaseFrameCount = 0;

    // 手のボーン参照（キャッシュ用）
    private Transform rightThumb, rightIndex, rightMiddle, rightRing, rightPinky;
    private Transform leftThumb, leftIndex, leftMiddle, leftRing, leftPinky;

    void Start()
    {
        // 手のボーンを事前にキャッシュ
        CacheHandBones();
    }

    void Update()
    {
        // 右手の握り判定
        if (rightHandSkeleton != null && rightHandSkeleton.Bones != null)
        {
            UpdateHandGrabState(rightHandSkeleton, ref isRightHandGrabbing, 
                              ref rightGrabFrameCount, ref rightReleaseFrameCount, "Right");
        }

        // 左手の握り判定
        if (leftHandSkeleton != null && leftHandSkeleton.Bones != null)
        {
            UpdateHandGrabState(leftHandSkeleton, ref isLeftHandGrabbing, 
                              ref leftGrabFrameCount, ref leftReleaseFrameCount, "Left");
        }
        
        // インスペクター表示用の状態更新
        UpdateDebugStatus();
    }

    /// <summary>
    /// 手のボーンを事前にキャッシュして処理を高速化
    /// </summary>
    private void CacheHandBones()
    {
        if (rightHandSkeleton != null && rightHandSkeleton.Bones != null)
        {
            foreach (var bone in rightHandSkeleton.Bones)
            {
                if (bone.Transform == null) continue;
                
                string boneName = bone.Transform.name.ToLower();
                if (boneName.Contains("thumb") && boneName.Contains("tip"))
                    rightThumb = bone.Transform;
                else if (boneName.Contains("index") && boneName.Contains("tip"))
                    rightIndex = bone.Transform;
                else if (boneName.Contains("middle") && boneName.Contains("tip"))
                    rightMiddle = bone.Transform;
                else if (boneName.Contains("ring") && boneName.Contains("tip"))
                    rightRing = bone.Transform;
                else if (boneName.Contains("pinky") && boneName.Contains("tip"))
                    rightPinky = bone.Transform;
            }
        }

        if (leftHandSkeleton != null && leftHandSkeleton.Bones != null)
        {
            foreach (var bone in leftHandSkeleton.Bones)
            {
                if (bone.Transform == null) continue;
                
                string boneName = bone.Transform.name.ToLower();
                if (boneName.Contains("thumb") && boneName.Contains("tip"))
                    leftThumb = bone.Transform;
                else if (boneName.Contains("index") && boneName.Contains("tip"))
                    leftIndex = bone.Transform;
                else if (boneName.Contains("middle") && boneName.Contains("tip"))
                    leftMiddle = bone.Transform;
                else if (boneName.Contains("ring") && boneName.Contains("tip"))
                    leftRing = bone.Transform;
                else if (boneName.Contains("pinky") && boneName.Contains("tip"))
                    leftPinky = bone.Transform;
            }
        }
    }

    /// <summary>
    /// 指定された手の握り状態を更新
    /// </summary>
    private void UpdateHandGrabState(OVRSkeleton handSkeleton, ref bool isGrabbing, 
                                   ref int grabFrameCount, ref int releaseFrameCount, string handName)
    {
        // 握り強度を計算
        float grabStrength = CalculateGrabStrength(handSkeleton);
        
        if (showDetailedDebug)
        {
            Debug.Log($"{handName} Hand Grab Strength: {grabStrength:F3}");
        }

        // 現在の握り状態を判定
        bool currentlyGrabbing = false;
        
        if (isGrabbing)
        {
            // 既に握っている場合はreleaseThresholdを使用（ヒステリシス効果）
            currentlyGrabbing = grabStrength >= releaseThreshold;
        }
        else
        {
            // 握っていない場合はgrabThresholdを使用
            currentlyGrabbing = grabStrength >= grabThreshold;
        }

        // 安定化処理
        if (enableGrabStabilization)
        {
            if (currentlyGrabbing)
            {
                grabFrameCount++;
                releaseFrameCount = 0;
                
                if (grabFrameCount >= grabStabilityFrames || isGrabbing)
                {
                    if (!isGrabbing && logGrabEvents)
                    {
                        Debug.Log($"{handName} Hand GRAB Started - Strength: {grabStrength:F3}");
                    }
                    isGrabbing = true;
                }
            }
            else
            {
                releaseFrameCount++;
                grabFrameCount = 0;
                
                if (releaseFrameCount >= grabStabilityFrames)
                {
                    if (isGrabbing && logGrabEvents)
                    {
                        Debug.Log($"{handName} Hand GRAB Released - Strength: {grabStrength:F3}");
                    }
                    isGrabbing = false;
                }
            }
        }
        else
        {
            // 安定化なしの場合
            if (currentlyGrabbing != isGrabbing)
            {
                if (logGrabEvents)
                {
                    string action = currentlyGrabbing ? "GRAB Started" : "GRAB Released";
                    Debug.Log($"{handName} Hand {action} - Strength: {grabStrength:F3}");
                }
                isGrabbing = currentlyGrabbing;
            }
        }
    }

    /// <summary>
    /// 手の握り強度を計算（0.0～1.0）
    /// </summary>
    private float CalculateGrabStrength(OVRSkeleton handSkeleton)
    {
        if (handSkeleton == null || handSkeleton.Bones == null || handSkeleton.Bones.Count == 0)
            return 0f;

        float totalCloseness = 0f;
        int fingerCount = 0;

        // 手のひらの中心位置を推定
        Vector3 palmCenter = GetPalmCenter(handSkeleton);

        // 各指先と手のひら中心の距離を計算
        foreach (var bone in handSkeleton.Bones)
        {
            if (bone.Transform == null) continue;
            
            string boneName = bone.Transform.name.ToLower();
            
            // 指先のボーンのみを対象とする
            if (boneName.Contains("tip"))
            {
                float distance = Vector3.Distance(bone.Transform.position, palmCenter);
                
                // 距離を0-1の範囲に正規化（近いほど1に近い）
                float closeness = Mathf.Clamp01(1f - (distance / 0.15f)); // 15cm基準
                totalCloseness += closeness;
                fingerCount++;

                if (showDetailedDebug)
                {
                    Debug.Log($"Finger {boneName}: Distance = {distance:F3}, Closeness = {closeness:F3}");
                }
            }
        }

        return fingerCount > 0 ? totalCloseness / fingerCount : 0f;
    }

    /// <summary>
    /// 手のひらの中心位置を推定
    /// </summary>
    private Vector3 GetPalmCenter(OVRSkeleton handSkeleton)
    {
        Vector3 palmCenter = Vector3.zero;
        int palmBoneCount = 0;

        foreach (var bone in handSkeleton.Bones)
        {
            if (bone.Transform == null) continue;
            
            string boneName = bone.Transform.name.ToLower();
            
            // 手のひら周辺のボーンを使用
            if (boneName.Contains("wrist") || boneName.Contains("palm") || 
                (boneName.Contains("metacarpal") && !boneName.Contains("thumb")))
            {
                palmCenter += bone.Transform.position;
                palmBoneCount++;
            }
        }

        return palmBoneCount > 0 ? palmCenter / palmBoneCount : handSkeleton.transform.position;
    }

    /// <summary>
    /// どちらかの手が握っているかを判定
    /// </summary>
    /// <returns>どちらかの手が握っている場合true</returns>
    public bool IsAnyHandGrabbing()
    {
        return isRightHandGrabbing || isLeftHandGrabbing;
    }

    /// <summary>
    /// 両手が握っているかを判定
    /// </summary>
    /// <returns>両手が握っている場合true</returns>
    public bool AreBothHandsGrabbing()
    {
        return isRightHandGrabbing && isLeftHandGrabbing;
    }

    /// <summary>
    /// 指定された手が握っているかを判定
    /// </summary>
    /// <param name="isRightHand">右手の場合true、左手の場合false</param>
    /// <returns>指定された手が握っている場合true</returns>
    public bool IsHandGrabbing(bool isRightHand)
    {
        return isRightHand ? isRightHandGrabbing : isLeftHandGrabbing;
    }

    /// <summary>
    /// 現在の握り強度を取得
    /// </summary>
    /// <param name="isRightHand">右手の場合true、左手の場合false</param>
    /// <returns>握り強度（0.0～1.0）</returns>
    public float GetGrabStrength(bool isRightHand)
    {
        OVRSkeleton targetSkeleton = isRightHand ? rightHandSkeleton : leftHandSkeleton;
        return CalculateGrabStrength(targetSkeleton);
    }

    /// <summary>
    /// 握っている手を特定（右手、左手、両手、なし）
    /// </summary>
    /// <returns>握り状態の詳細情報</returns>
    public enum GrabbingHand { None, RightOnly, LeftOnly, BothHands }
    public GrabbingHand GetGrabbingHand()
    {
        if (isRightHandGrabbing && isLeftHandGrabbing)
            return GrabbingHand.BothHands;
        else if (isRightHandGrabbing)
            return GrabbingHand.RightOnly;
        else if (isLeftHandGrabbing)
            return GrabbingHand.LeftOnly;
        else
            return GrabbingHand.None;
    }

    /// <summary>
    /// 握っている手の名前を文字列で取得
    /// </summary>
    /// <returns>"Right", "Left", "Both", "None"</returns>
    public string GetGrabbingHandName()
    {
        switch (GetGrabbingHand())
        {
            case GrabbingHand.RightOnly: return "Right";
            case GrabbingHand.LeftOnly: return "Left";
            case GrabbingHand.BothHands: return "Both";
            default: return "None";
        }
    }

    /// <summary>
    /// 右手のみが握っているかを判定
    /// </summary>
    /// <returns>右手のみが握っている場合true</returns>
    public bool IsOnlyRightHandGrabbing()
    {
        return isRightHandGrabbing && !isLeftHandGrabbing;
    }

    /// <summary>
    /// 左手のみが握っているかを判定
    /// </summary>
    /// <returns>左手のみが握っている場合true</returns>
    public bool IsOnlyLeftHandGrabbing()
    {
        return isLeftHandGrabbing && !isRightHandGrabbing;
    }

    /// <summary>
    /// 右手が握っているかを判定（左手の状態は不問）
    /// </summary>
    /// <returns>右手が握っている場合true</returns>
    public bool IsRightHandGrabbing()
    {
        return isRightHandGrabbing;
    }

    /// <summary>
    /// 左手が握っているかを判定（右手の状態は不問）
    /// </summary>
    /// <returns>左手が握っている場合true</returns>
    public bool IsLeftHandGrabbing()
    {
        return isLeftHandGrabbing;
    }

    /// <summary>
    /// インスペクター表示用の状態情報を更新
    /// </summary>
    private void UpdateDebugStatus()
    {
        // 右手の状態
        if (isRightHandGrabbing)
        {
            float strength = GetGrabStrength(true);
            rightHandStatus = $"Grabbing (強度: {strength:F2})";
        }
        else
        {
            rightHandStatus = "Not Grabbing";
        }
        
        // 左手の状態
        if (isLeftHandGrabbing)
        {
            float strength = GetGrabStrength(false);
            leftHandStatus = $"Grabbing (強度: {strength:F2})";
        }
        else
        {
            leftHandStatus = "Not Grabbing";
        }
        
        // 全体の握り状態
        grabSummary = GetGrabbingHandName();
    }
}