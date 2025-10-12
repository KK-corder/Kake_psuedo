using UnityEngine;

public class AvatarColliderSetup : MonoBehaviour
{
    [Header("Collider Settings")]
    public bool enablePhysics = true;
    public bool useKinematicRigidbody = false;
    public LayerMask collisionLayers = -1;
    
    //[Header("Body Part Settings")]
    [System.Serializable]
    public class BodyPartCollider
    {
        public Transform bodyPart;
        public ColliderType colliderType;
        public Vector3 size = Vector3.one;
        public Vector3 center = Vector3.zero;
        public float radius = 0.5f;
        public bool isTrigger = false;
    }
    
    public enum ColliderType
    {
        Box,
        Sphere,
        Capsule
    }
    
    public BodyPartCollider[] bodyParts;
    
    void Start()
    {
        SetupColliders();
    }
    
    void SetupColliders()
    {
        // メインのRigidbodyを追加（まだない場合）
        Rigidbody mainRigidbody = GetComponent<Rigidbody>();
        if (mainRigidbody == null && enablePhysics)
        {
            mainRigidbody = gameObject.AddComponent<Rigidbody>();
            mainRigidbody.isKinematic = useKinematicRigidbody;
            mainRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }
        
        // 各ボディパーツにColliderを設定
        foreach (var bodyPart in bodyParts)
        {
            if (bodyPart.bodyPart == null) continue;
            
            SetupBodyPartCollider(bodyPart);
        }
        
        Debug.Log($"AvatarColliderSetup: Setup completed for {bodyParts.Length} body parts");
    }
    
    void SetupBodyPartCollider(BodyPartCollider bodyPart)
    {
        GameObject bodyPartObj = bodyPart.bodyPart.gameObject;
        
        // 既存のColliderを削除
        Collider existingCollider = bodyPartObj.GetComponent<Collider>();
        if (existingCollider != null)
        {
            DestroyImmediate(existingCollider);
        }
        
        // 新しいColliderを追加
        Collider newCollider = null;
        switch (bodyPart.colliderType)
        {
            case ColliderType.Box:
                BoxCollider boxCollider = bodyPartObj.AddComponent<BoxCollider>();
                boxCollider.size = bodyPart.size;
                boxCollider.center = bodyPart.center;
                newCollider = boxCollider;
                break;
                
            case ColliderType.Sphere:
                SphereCollider sphereCollider = bodyPartObj.AddComponent<SphereCollider>();
                sphereCollider.radius = bodyPart.radius;
                sphereCollider.center = bodyPart.center;
                newCollider = sphereCollider;
                break;
                
            case ColliderType.Capsule:
                CapsuleCollider capsuleCollider = bodyPartObj.AddComponent<CapsuleCollider>();
                capsuleCollider.radius = bodyPart.radius;
                capsuleCollider.height = bodyPart.size.y;
                capsuleCollider.center = bodyPart.center;
                newCollider = capsuleCollider;
                break;
        }
        
        if (newCollider != null)
        {
            newCollider.isTrigger = bodyPart.isTrigger;
            
            // Rigidbodyを追加（物理演算が必要な場合）
            if (enablePhysics && bodyPartObj != this.gameObject)
            {
                Rigidbody rb = bodyPartObj.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = bodyPartObj.AddComponent<Rigidbody>();
                    rb.isKinematic = true; // ボディパーツは通常キネマティック
                }
            }
        }
    }
    
    /// <summary>
    /// 特定のレイヤーとの衝突を無効化
    /// </summary>
    public void IgnoreCollisionWithLayer(int layer)
    {
        foreach (var bodyPart in bodyParts)
        {
            if (bodyPart.bodyPart != null)
            {
                Collider collider = bodyPart.bodyPart.GetComponent<Collider>();
                if (collider != null)
                {
                    // 特定レイヤーとの衝突を無視
                    Physics.IgnoreLayerCollision(bodyPart.bodyPart.gameObject.layer, layer);
                }
            }
        }
    }
    
    /// <summary>
    /// すべてのColliderを有効/無効化
    /// </summary>
    public void SetCollidersEnabled(bool enabled)
    {
        foreach (var bodyPart in bodyParts)
        {
            if (bodyPart.bodyPart != null)
            {
                Collider collider = bodyPart.bodyPart.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.enabled = enabled;
                }
            }
        }
    }
}