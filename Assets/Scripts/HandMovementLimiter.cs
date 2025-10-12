using UnityEngine;

public class HandMovementLimiter : MonoBehaviour
{
    [Header("Hand Settings")]
    public OVRSkeleton handSkeleton;
    public BoneJudge boneJudge;
    
    [Header("Movement Limitation")]
    public bool enableZPositionLimit = true;
    public float limitOffset = 0.01f; // endpoint から少し手前で制限をかける
    
    [Header("Debug")]
    public bool showDebugLogs = true;
    
    private Vector3[] originalBonePositions;
    private bool isContactActive = false;
    private float limitZPosition;
    private bool limitInitialized = false;
    
    void Start()
    {
        if (handSkeleton == null)
        {
            Debug.LogError("HandMovementLimiter: handSkeleton is not assigned!");
            return;
        }
        
        if (boneJudge == null)
        {
            Debug.LogError("HandMovementLimiter: boneJudge is not assigned!");
            return;
        }
        
        // 初期化を少し遅らせる（OVRSkeletonの初期化を待つ）
        Invoke("InitializeBonePositions", 0.1f);
    }
    
    void InitializeBonePositions()
    {
        if (handSkeleton.Bones != null && handSkeleton.Bones.Count > 0)
        {
            originalBonePositions = new Vector3[handSkeleton.Bones.Count];
            for (int i = 0; i < handSkeleton.Bones.Count; i++)
            {
                if (handSkeleton.Bones[i].Transform != null)
                {
                    originalBonePositions[i] = handSkeleton.Bones[i].Transform.position;
                }
            }
            
            if (showDebugLogs)
            {
                Debug.Log($"HandMovementLimiter: Initialized {handSkeleton.Bones.Count} bone positions");
            }
        }
    }
    
    void Update()
    {
        if (handSkeleton == null || boneJudge == null || !enableZPositionLimit)
            return;
            
        CheckContactStatus();
        
        if (isContactActive)
        {
            LimitHandMovement();
        }
    }
    
    void CheckContactStatus()
    {
        bool anyContact = false;
        
        if (boneJudge.isTouching != null)
        {
            foreach (bool touching in boneJudge.isTouching)
            {
                if (touching)
                {
                    anyContact = true;
                    break;
                }
            }
        }
        
        // 接触状態が変化した時の処理
        if (anyContact && !isContactActive)
        {
            // 接触開始：制限位置を設定
            OnContactStart();
        }
        else if (!anyContact && isContactActive)
        {
            // 接触終了：制限解除
            OnContactEnd();
        }
        
        isContactActive = anyContact;
    }
    
    void OnContactStart()
    {
        // endpointのZ座標を取得して制限位置を設定
        limitZPosition = boneJudge.progressEnd.z - limitOffset;
        limitInitialized = true;
        
        if (showDebugLogs)
        {
            Debug.Log($"HandMovementLimiter: Contact started. Limit Z position set to {limitZPosition:F4} (endpoint: {boneJudge.progressEnd.z:F4})");
        }
    }
    
    void OnContactEnd()
    {
        limitInitialized = false;
        
        if (showDebugLogs)
        {
            Debug.Log("HandMovementLimiter: Contact ended. Movement limitation removed.");
        }
    }
    
    void LimitHandMovement()
    {
        if (!limitInitialized || handSkeleton.Bones == null)
            return;
            
        foreach (var bone in handSkeleton.Bones)
        {
            if (bone.Transform == null) continue;
            
            Vector3 currentPos = bone.Transform.position;
            
            // Z座標がlimitZPositionを超えないように制限
            if (currentPos.z > limitZPosition)
            {
                Vector3 limitedPos = new Vector3(currentPos.x, currentPos.y, limitZPosition);
                bone.Transform.position = limitedPos;
                
                if (showDebugLogs)
                {
                    Debug.Log($"HandMovementLimiter: Limited {bone.Transform.name} Z position from {currentPos.z:F4} to {limitZPosition:F4}");
                }
            }
        }
    }
    
    /// <summary>
    /// 手動で制限位置を設定
    /// </summary>
    public void SetLimitPosition(float zPosition)
    {
        limitZPosition = zPosition - limitOffset;
        limitInitialized = true;
        
        if (showDebugLogs)
        {
            Debug.Log($"HandMovementLimiter: Manual limit position set to {limitZPosition:F4}");
        }
    }
    
    /// <summary>
    /// 制限を有効/無効化
    /// </summary>
    public void SetLimitEnabled(bool enabled)
    {
        enableZPositionLimit = enabled;
        
        if (!enabled)
        {
            limitInitialized = false;
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"HandMovementLimiter: Limit {(enabled ? "enabled" : "disabled")}");
        }
    }
    
    /// <summary>
    /// 現在の制限状態を取得
    /// </summary>
    public bool IsLimitActive()
    {
        return isContactActive && limitInitialized && enableZPositionLimit;
    }
    
    /// <summary>
    /// 現在の制限Z座標を取得
    /// </summary>
    public float GetCurrentLimitZ()
    {
        return limitInitialized ? limitZPosition : float.MaxValue;
    }
    
    void OnDrawGizmos()
    {
        // Scene ビューで制限位置を可視化
        if (limitInitialized && enableZPositionLimit)
        {
            Gizmos.color = Color.red;
            Vector3 center = new Vector3(transform.position.x, transform.position.y, limitZPosition);
            Gizmos.DrawWireCube(center, new Vector3(0.2f, 0.2f, 0.01f));
            
            // 制限線を描画
            Gizmos.color = Color.yellow;
            Vector3 lineStart = center + Vector3.up * 0.1f;
            Vector3 lineEnd = center + Vector3.down * 0.1f;
            Gizmos.DrawLine(lineStart, lineEnd);
        }
    }
}