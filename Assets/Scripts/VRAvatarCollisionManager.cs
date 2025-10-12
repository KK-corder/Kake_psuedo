using UnityEngine;

public class VRAvatarCollisionManager : MonoBehaviour
{
    [Header("Hand Settings")]
    public OVRSkeleton rightHandSkeleton;
    public OVRSkeleton leftHandSkeleton;
    public float handColliderRadius = 0.02f;
    
    [Header("Collision Settings")]
    public LayerMask obstacleLayer = -1;
    public float pushBackForce = 5f;
    
    private Rigidbody avatarRigidbody;
    
    void Start()
    {
        SetupHandColliders();
    }
    
    void SetupHandColliders()
    {
        // メインのRigidbodyを取得または作成
        avatarRigidbody = GetComponent<Rigidbody>();
        if (avatarRigidbody == null)
        {
            avatarRigidbody = gameObject.AddComponent<Rigidbody>();
            avatarRigidbody.isKinematic = true; // VRアバターは通常キネマティック
        }
        
        // 右手のColliderを設定
        if (rightHandSkeleton != null)
        {
            SetupHandCollider(rightHandSkeleton, "Right");
        }
        
        // 左手のColliderを設定
        if (leftHandSkeleton != null)
        {
            SetupHandCollider(leftHandSkeleton, "Left");
        }
    }
    
    void SetupHandCollider(OVRSkeleton handSkeleton, string handName)
    {
        if (handSkeleton.Bones == null) return;
        
        int fingerCount = 0;
        foreach (var bone in handSkeleton.Bones)
        {
            // 指先のボーンにColliderを追加
            if (bone.Transform != null && 
                (bone.Transform.name.Contains("Tip") || 
                 bone.Transform.name.Contains("IndexTip") ||
                 bone.Transform.name.Contains("MiddleTip") ||
                 bone.Transform.name.Contains("RingTip") ||
                 bone.Transform.name.Contains("PinkyTip") ||
                 bone.Transform.name.Contains("ThumbTip")))
            {
                SetupFingerCollider(bone.Transform);
                fingerCount++;
            }
        }
        
        Debug.Log($"VRAvatarCollisionManager: Setup {fingerCount} finger colliders for {handName} hand");
    }
    
    void SetupFingerCollider(Transform fingerTransform)
    {
        // 既存のColliderを削除
        SphereCollider existingCollider = fingerTransform.GetComponent<SphereCollider>();
        if (existingCollider != null)
        {
            DestroyImmediate(existingCollider);
        }
        
        // 新しいSphereColliderを追加
        SphereCollider fingerCollider = fingerTransform.gameObject.AddComponent<SphereCollider>();
        fingerCollider.radius = handColliderRadius;
        fingerCollider.isTrigger = false; // 物理的な衝突を有効
        
        // CollisionHandlerを追加
        HandCollisionHandler handler = fingerTransform.gameObject.AddComponent<HandCollisionHandler>();
        handler.Initialize(this, fingerTransform);
    }
    
    /// <summary>
    /// 衝突時の押し戻し処理
    /// </summary>
    public void HandleCollision(Transform collisionTransform, Vector3 collisionPoint, Vector3 normal)
    {
        // 押し戻し方向を計算
        Vector3 pushDirection = -normal;
        
        // VRアバターの場合、直接Transform位置を調整
        Vector3 pushBack = pushDirection * pushBackForce * Time.fixedDeltaTime;
        collisionTransform.position += pushBack;
        
        Debug.Log($"Hand collision detected: Pushing back {collisionTransform.name} by {pushBack}");
    }
}

// 手の衝突ハンドラー
public class HandCollisionHandler : MonoBehaviour
{
    private VRAvatarCollisionManager manager;
    private Transform handTransform;
    
    public void Initialize(VRAvatarCollisionManager collisionManager, Transform hand)
    {
        manager = collisionManager;
        handTransform = hand;
    }
    
    void OnCollisionEnter(Collision collision)
    {
        // 障害物レイヤーとの衝突のみ処理
        if (((1 << collision.gameObject.layer) & manager.obstacleLayer) != 0)
        {
            ContactPoint contact = collision.contacts[0];
            manager.HandleCollision(handTransform, contact.point, contact.normal);
        }
    }
}