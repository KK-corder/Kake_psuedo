using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoneJudgeNew : MonoBehaviour
{
    [Header("Hand Skeleton Settings")]
    // 右手と左手のスケルトン（OVRHandPrefabに設定）
    public OVRSkeleton rightHandSkeleton;
    public OVRSkeleton leftHandSkeleton;
    


    [Header("Target Objects")]
    // 接触オブジェクトをインスペクターでアタッチ
    // 対応Collider: SphereCollider, CapsuleCollider（円柱）, BoxCollider, MeshCollider
    // 各形状に最適化された精密判定を自動選択
    public GameObject[] cubes = new GameObject[3];

    [Header("Contact Range Settings")]
    // 各形状用の接触判定距離設定
    public float sphereContactThreshold = 0.02f; // Sphere表面からの接触判定距離（拡大）
    

    // Capsule（円柱）専用設定 - 体積内接触判定
    [HideInInspector] public bool useVolumeBasedDetection = true; // 円柱体積内での接触判定を使用
    [HideInInspector]public bool showDebugInfo = true; // デバッグ情報を表示
    
    [Header("Contact Sensitivity Settings")]
    // 接触判定の甘さ調整
    [Range(1.0f, 2.0f)]
    public float radiusMultiplier = 1.1f; // 半径の拡大倍率（1.1 = 10%拡大）
    [Range(1.0f, 2.0f)]
    public float heightMultiplier = 1.05f; // 高さの拡大倍率（1.05 = 5%拡大）
    [Range(0.0f, 0.05f)]
    public float contactMargin = 0.01f; // 追加の接触マージン（1cm）
    
    [HideInInspector] // Other threshold settings - Hidden
    public float boxContactThreshold = 0.02f; // Box（立方体）表面からの接触判定距離
    public float genericContactThreshold = 0.02f; // その他のCollider表面からの接触判定距離
    
    [HideInInspector] // Contact Stability Settings - Hidden
    // 接触安定化のための設定
    public float contactHysteresis = 0.005f; // ヒステリシス効果（接触継続のための追加距離）
    public int contactStabilityFrames = 3; // 接触状態を安定化するフレーム数
    public bool enableContactStabilization = true; // 接触安定化を有効にするか

    // 接触安定化用の内部変数
    private int[] contactFrameCount; // 各オブジェクトの連続接触フレーム数
    private int[] nonContactFrameCount; // 各オブジェクトの連続非接触フレーム数



    [Header("Contact Settings")]
    // 各オブジェクトに対する接触状態
    [HideInInspector] public bool[] isTouching;
    // 各オブジェクトに対応する固定された最も近いボーン
    [HideInInspector] public Transform[] fixedClosestBones;
    // 各オブジェクトに対応する接触点
    [HideInInspector] public Vector3[] touchPoints;



    [Header("Visual Feedback")]
    // Optional: change cube material on first contact
    public Material contactMaterial;
    private Material[] originalMaterials;

    [Header("Debug")]
    // For debug visibility
    public bool logContactEvents = true;

    void Start()
    {
        // 配列の初期化
        int count = Mathf.Max(1, cubes != null ? cubes.Length : 1);
        isTouching = new bool[count];
        fixedClosestBones = new Transform[count];
        touchPoints = new Vector3[count];
        
        // 接触安定化用配列の初期化
        contactFrameCount = new int[count];
        nonContactFrameCount = new int[count];

        // cache original materials
        originalMaterials = new Material[count];
        for (int i = 0; i < count; i++)
        {
            var r = (cubes != null && i < cubes.Length && cubes[i] != null) ? cubes[i].GetComponent<Renderer>() : null;
            originalMaterials[i] = (r != null) ? r.sharedMaterial : null;
            
            // 安定化カウンターの初期化
            contactFrameCount[i] = 0;
            nonContactFrameCount[i] = 0;
        }
    }

    void Update()
    {
        if ((rightHandSkeleton == null || rightHandSkeleton.Bones == null) && 
            (leftHandSkeleton == null || leftHandSkeleton.Bones == null))
            return;
            
        if (cubes == null)
            return;

        HandleObjectCollisions();
    }

    private void HandleObjectCollisions()
    {
        if (cubes == null || cubes.Length == 0)
            return;

        // 両手のボーンを統合したリストを作成
        List<OVRBone> allBones = new List<OVRBone>();

        if (rightHandSkeleton != null && rightHandSkeleton.Bones != null)
        {
            allBones.AddRange(rightHandSkeleton.Bones);
        }

        if (leftHandSkeleton != null && leftHandSkeleton.Bones != null)
        {
            allBones.AddRange(leftHandSkeleton.Bones);
        }

        if (allBones.Count == 0) return;

        for (int cubeIndex = 0; cubeIndex < cubes.Length; cubeIndex++)
        {
            if (cubes[cubeIndex] == null) continue;

            bool currentlyTouching = false;
            Transform closestBone = null;
            float minDistance = float.MaxValue;
            Vector3 closestTouchPoint = Vector3.zero;

            // Sphere Collider を取得
            SphereCollider sphereCollider = cubes[cubeIndex].GetComponent<SphereCollider>();
            if (sphereCollider != null)
            {
                // Sphere用の精密な接触判定
                Vector3 sphereCenter = sphereCollider.transform.TransformPoint(sphereCollider.center);
                float sphereRadius = sphereCollider.radius * Mathf.Max(
                    sphereCollider.transform.lossyScale.x, 
                    sphereCollider.transform.lossyScale.y, 
                    sphereCollider.transform.lossyScale.z);

                // 各ボーンとSphereの距離をチェック
                foreach (var bone in allBones)
                {
                    if (bone.Transform == null) continue;

                    Vector3 bonePosition = bone.Transform.position;
                    float distanceToCenter = Vector3.Distance(bonePosition, sphereCenter);
                    
                    // Sphere表面からの距離を計算
                    float distanceToSurface = distanceToCenter - sphereRadius;

                    // 動的な閾値設定（ヒステリシス効果）
                    float effectiveThreshold = sphereContactThreshold;
                    if (enableContactStabilization && isTouching[cubeIndex])
                    {
                        // 既に接触している場合は、より離れるまで接触を維持
                        effectiveThreshold += contactHysteresis;
                    }

                    // 接触判定：Sphere表面に近いか内部にある場合
                    if (distanceToSurface <= effectiveThreshold)
                    {
                        currentlyTouching = true;
                        if (distanceToCenter < minDistance)
                        {
                            minDistance = distanceToCenter;
                            closestBone = bone.Transform;
                            // 接触点はSphere表面の最も近い点
                            closestTouchPoint = sphereCenter + (bonePosition - sphereCenter).normalized * sphereRadius;
                        }
                    }
                }

                if (logContactEvents && currentlyTouching)
                {
                    Debug.Log($"Sphere contact detected: Distance to surface = {minDistance - sphereRadius:F3}, Radius = {sphereRadius:F3}, Threshold = {sphereContactThreshold:F3}");
                }
            }
            else
            {
                // Sphere以外のCollider処理
                Collider objectCollider = cubes[cubeIndex].GetComponent<Collider>();
                if (objectCollider == null) continue;

                // CapsuleCollider（円柱形）の特別処理
                CapsuleCollider capsuleCollider = objectCollider as CapsuleCollider;
                if (capsuleCollider != null)
                {
                    ProcessCapsuleCollision(capsuleCollider, cubeIndex, allBones, ref currentlyTouching, ref closestBone, ref minDistance, ref closestTouchPoint);
                }
                // BoxCollider（立方体）の特別処理
                else if (objectCollider is BoxCollider)
                {
                    ProcessBoxCollision(objectCollider, cubeIndex, allBones, ref currentlyTouching, ref closestBone, ref minDistance, ref closestTouchPoint);
                }
                // その他のCollider（MeshCollider等）の汎用処理
                else
                {
                    ProcessGenericCollision(objectCollider, cubeIndex, allBones, ref currentlyTouching, ref closestBone, ref minDistance, ref closestTouchPoint);
                }
            }

            // 接触安定化処理
            bool finalTouchingState = currentlyTouching;
            
            if (enableContactStabilization)
            {
                if (currentlyTouching)
                {
                    // 接触検出時のカウンター更新
                    contactFrameCount[cubeIndex]++;
                    nonContactFrameCount[cubeIndex] = 0;
                    
                    // 一定フレーム以上接触していれば確定
                    if (contactFrameCount[cubeIndex] >= contactStabilityFrames || isTouching[cubeIndex])
                    {
                        finalTouchingState = true;
                    }
                    else
                    {
                        // まだ安定していない場合は前の状態を維持
                        finalTouchingState = isTouching[cubeIndex];
                    }
                }
                else
                {
                    // 非接触検出時のカウンター更新
                    nonContactFrameCount[cubeIndex]++;
                    contactFrameCount[cubeIndex] = 0;
                    
                    // 一定フレーム以上非接触でなければ接触状態を維持
                    if (nonContactFrameCount[cubeIndex] >= contactStabilityFrames)
                    {
                        finalTouchingState = false;
                    }
                    else
                    {
                        // まだ安定していない場合は前の状態を維持
                        finalTouchingState = isTouching[cubeIndex];
                    }
                }
            }

            // 接触状態の更新
            bool wasNotTouching = !isTouching[cubeIndex];
            isTouching[cubeIndex] = finalTouchingState;

            if (finalTouchingState)
            {
                // 初回接触時または最も近いボーンが変わった場合
                if (wasNotTouching || (closestBone != null && fixedClosestBones[cubeIndex] != closestBone))
                {
                    fixedClosestBones[cubeIndex] = closestBone;
                    touchPoints[cubeIndex] = closestTouchPoint;

                    if (logContactEvents)
                    {
                        string handType = GetHandTypeFromBone(closestBone);
                        string objectType = sphereCollider != null ? "Sphere" : "Object";
                        Debug.Log($"Contact started with {objectType} {cubeIndex} using {handType} bone: {closestBone.name}");
                        
                        if (enableContactStabilization)
                        {
                            Debug.Log($"Contact stability: frames={contactFrameCount[cubeIndex]}, threshold={contactStabilityFrames}");
                        }
                    }

                    // Change material on first contact
                    if (contactMaterial != null && wasNotTouching)
                    {
                        var renderer = cubes[cubeIndex].GetComponent<Renderer>();
                        if (renderer != null)
                        {
                            renderer.sharedMaterial = contactMaterial;
                            
                            // 体積ベース接触の場合は追加ログ
                            CapsuleCollider capsuleCol = cubes[cubeIndex].GetComponent<CapsuleCollider>();
                            if (capsuleCol != null && useVolumeBasedDetection)
                            {
                                Debug.Log($"VOLUME-BASED CONTACT: Material changed for Capsule {cubeIndex} - Hand entered cylinder volume!");
                            }
                        }
                    }
                }
            }
            else if (wasNotTouching == false) // 接触が終了した場合
            {
                fixedClosestBones[cubeIndex] = null;

                if (logContactEvents)
                {
                    string objectType = cubes[cubeIndex].GetComponent<SphereCollider>() != null ? "Sphere" : "Object";
                    Debug.Log($"Contact ended with {objectType} {cubeIndex}");
                    
                    if (enableContactStabilization)
                    {
                        Debug.Log($"Contact lost stability: non-contact frames={nonContactFrameCount[cubeIndex]}, threshold={contactStabilityFrames}");
                    }
                }

                // Restore original material
                if (originalMaterials[cubeIndex] != null)
                {
                    var rend = cubes[cubeIndex].GetComponent<Renderer>();
                    if (rend != null)
                    {
                        rend.sharedMaterial = originalMaterials[cubeIndex];
                    }
                }
            }
        }
    }
    
    
    /// 指定されたインデックスの接触しているボーンの位置を取得
    public Vector3 GetContactingBonePosition(int cubeIndex)
    {
        if (cubeIndex < 0 || cubeIndex >= fixedClosestBones.Length)
            return Vector3.zero;
            
        if (isTouching != null && cubeIndex < isTouching.Length && isTouching[cubeIndex])
        {
            if (fixedClosestBones[cubeIndex] != null)
            {
                return fixedClosestBones[cubeIndex].position;
            }
        }
        
        return Vector3.zero;
    }
    
    /// 指定されたインデックスの接触しているボーンの回転を取得
    public Quaternion GetContactingBoneRotation(int cubeIndex)
    {
        if (cubeIndex < 0 || cubeIndex >= fixedClosestBones.Length)
            return Quaternion.identity;
            
        if (isTouching != null && cubeIndex < isTouching.Length && isTouching[cubeIndex])
        {
            if (fixedClosestBones[cubeIndex] != null)
            {
                return fixedClosestBones[cubeIndex].rotation;
            }
        }
        
        return Quaternion.identity;
    }
    
    /// <summary>
    /// 指定されたインデックスの接触しているボーンのTransformを取得
    /// </summary>
    /// <param name="cubeIndex">Cubeのインデックス</param>
    /// <returns>接触しているボーンのTransform、接触していない場合はnull</returns>
    public Transform GetContactingBoneTransform(int cubeIndex)
    {
        if (cubeIndex < 0 || cubeIndex >= fixedClosestBones.Length)
            return null;
            
        if (isTouching != null && cubeIndex < isTouching.Length && isTouching[cubeIndex])
        {
            return fixedClosestBones[cubeIndex];
        }
        
        return null;
    }
    
    /// <summary>
    /// いずれかのオブジェクトと接触しているかチェック
    /// </summary>
    /// <returns>接触している場合true</returns>
    public bool IsAnyContact()
    {
        if (isTouching == null) return false;
        
        foreach (bool touching in isTouching)
        {
            if (touching) return true;
        }
        return false;
    }
    
    /// <summary>
    /// 指定されたボーンがどちらの手に属するかを判定
    /// </summary>
    /// <param name="boneTransform">判定するボーン</param>
    /// <returns>右手、左手、または不明</returns>
    private string GetHandTypeFromBone(Transform boneTransform)
    {
        if (rightHandSkeleton != null && rightHandSkeleton.Bones != null)
        {
            foreach (var bone in rightHandSkeleton.Bones)
            {
                if (bone.Transform == boneTransform)
                {
                    return "Right Hand";
                }
            }
        }
        
        if (leftHandSkeleton != null && leftHandSkeleton.Bones != null)
        {
            foreach (var bone in leftHandSkeleton.Bones)
            {
                if (bone.Transform == boneTransform)
                {
                    return "Left Hand";
                }
            }
        }
        
        return "Unknown Hand";
    }
    
    /// <summary>
    /// 右手のスケルトンを取得
    /// </summary>
    /// <returns>右手のOVRSkeleton</returns>
    public OVRSkeleton GetRightHandSkeleton()
    {
        return rightHandSkeleton;
    }
    
    /// <summary>
    /// 左手のスケルトンを取得
    /// </summary>
    /// <returns>左手のOVRSkeleton</returns>
    public OVRSkeleton GetLeftHandSkeleton()
    {
        return leftHandSkeleton;
    }
    
    /// <summary>
    /// 指定されたボーンがどちらの手に属するかを判定（public版）
    /// </summary>
    /// <param name="cubeIndex">Cubeのインデックス</param>
    /// <returns>右手、左手、または不明</returns>
    public string GetContactingHandType(int cubeIndex)
    {
        if (cubeIndex < 0 || cubeIndex >= fixedClosestBones.Length)
            return "Invalid Index";
            
        if (isTouching != null && cubeIndex < isTouching.Length && isTouching[cubeIndex])
        {
            if (fixedClosestBones[cubeIndex] != null)
            {
                return GetHandTypeFromBone(fixedClosestBones[cubeIndex]);
            }
        }
        
        return "No Contact";
    }

    /// <summary>
    /// CapsuleCollider（円柱形）専用の接触判定処理
    /// アタッチしているオブジェクトと全く同一サイズの円柱体積内での接触判定
    /// </summary>
    private void ProcessCapsuleCollision(CapsuleCollider capsuleCollider, int cubeIndex, List<OVRBone> allBones, 
        ref bool currentlyTouching, ref Transform closestBone, ref float minDistance, ref Vector3 closestTouchPoint)
    {
        // Capsuleの基本情報を取得
        Transform capsuleTransform = capsuleCollider.transform;
        Vector3 capsuleCenter = capsuleTransform.TransformPoint(capsuleCollider.center);
        Vector3 scale = capsuleTransform.lossyScale;
        
        // 軸方向に応じた正確なサイズ計算
        float capsuleRadius, capsuleHeight;
        Vector3 axisDirection;
        
        if (capsuleCollider.direction == 0) // X軸方向
        {
            axisDirection = capsuleTransform.right;
            capsuleRadius = capsuleCollider.radius * Mathf.Max(scale.y, scale.z);
            capsuleHeight = capsuleCollider.height * scale.x;
        }
        else if (capsuleCollider.direction == 1) // Y軸方向
        {
            axisDirection = capsuleTransform.up;
            capsuleRadius = capsuleCollider.radius * Mathf.Max(scale.x, scale.z);
            capsuleHeight = capsuleCollider.height * scale.y;
        }
        else // Z軸方向
        {
            axisDirection = capsuleTransform.forward;
            capsuleRadius = capsuleCollider.radius * Mathf.Max(scale.x, scale.y);
            capsuleHeight = capsuleCollider.height * scale.z;
        }

        // デバッグ情報の出力
        if (showDebugInfo && logContactEvents)
        {
            Debug.Log($"Capsule Analysis - Center: {capsuleCenter}, Original Radius: {capsuleRadius:F3} → Expanded: {capsuleRadius * radiusMultiplier + contactMargin:F3}, Original Height: {capsuleHeight:F3} → Expanded: {capsuleHeight * heightMultiplier:F3}, Direction: {capsuleCollider.direction}");
        }

        // 接触判定用の拡大された円柱サイズを計算
        float expandedRadius = capsuleRadius * radiusMultiplier + contactMargin;
        float expandedHeight = capsuleHeight * heightMultiplier;
        float cylinderHalfHeight = Mathf.Max(0f, (expandedHeight * 0.5f) - expandedRadius);
        
        foreach (var bone in allBones)
        {
            if (bone.Transform == null) continue;

            Vector3 bonePosition = bone.Transform.position;
            
            // 拡大された円柱体積内判定を行う
            bool isInsideVolume = IsInsideCapsuleVolume(bonePosition, capsuleCenter, axisDirection, 
                                                       expandedRadius, expandedHeight, cylinderHalfHeight);
            
            if (isInsideVolume)
            {
                // 体積内にある場合は接触とする
                Vector3 toBone = bonePosition - capsuleCenter;
                float projectionLength = Vector3.Dot(toBone, axisDirection);
                
                // 接触点を計算（元の円柱サイズでの表面点）
                float originalCylinderHalfHeight = Mathf.Max(0f, (capsuleHeight * 0.5f) - capsuleRadius);
                Vector3 surfacePoint = CalculateCapsuleSurfacePoint(bonePosition, capsuleCenter, axisDirection,
                                                                   capsuleRadius, originalCylinderHalfHeight);
                
                // 距離は0とする（体積内なので）
                float distanceToSurface = 0f;
                
                currentlyTouching = true;
                if (distanceToSurface <= minDistance)
                {
                    minDistance = distanceToSurface;
                    closestBone = bone.Transform;
                    closestTouchPoint = surfacePoint;
                }
                
                if (showDebugInfo && logContactEvents)
                {
                    Debug.Log($"Bone {bone.Transform.name} is INSIDE capsule volume at distance {Vector3.Distance(bonePosition, capsuleCenter):F3} from center");
                }
            }
        }

        if (logContactEvents && currentlyTouching)
        {
            Debug.Log($"Capsule VOLUME contact detected: Original Radius = {capsuleRadius:F3}, Expanded Radius = {expandedRadius:F3}, Original Height = {capsuleHeight:F3}, Expanded Height = {expandedHeight:F3}, Direction = {capsuleCollider.direction}");
        }
    }

    /// <summary>
    /// 指定された点がCapsule（円柱）の体積内にあるかを判定
    /// </summary>
    private bool IsInsideCapsuleVolume(Vector3 point, Vector3 capsuleCenter, Vector3 axisDirection, 
                                      float capsuleRadius, float capsuleHeight, float cylinderHalfHeight)
    {
        // 円柱中心軸からの距離を計算
        Vector3 toPoint = point - capsuleCenter;
        float projectionLength = Vector3.Dot(toPoint, axisDirection);
        
        if (Mathf.Abs(projectionLength) <= cylinderHalfHeight)
        {
            // 円柱部分（側面）の体積内判定
            Vector3 closestPointOnAxis = capsuleCenter + axisDirection * projectionLength;
            float distanceToAxis = Vector3.Distance(point, closestPointOnAxis);
            return distanceToAxis <= capsuleRadius;
        }
        else
        {
            // キャップ部分（底面の半球）の体積内判定
            float capDirection = projectionLength > 0 ? 1f : -1f;
            Vector3 capCenter = capsuleCenter + axisDirection * (capDirection * cylinderHalfHeight);
            float distanceToCapCenter = Vector3.Distance(point, capCenter);
            return distanceToCapCenter <= capsuleRadius;
        }
    }

    /// <summary>
    /// Capsule表面の最も近い点を計算
    /// </summary>
    private Vector3 CalculateCapsuleSurfacePoint(Vector3 point, Vector3 capsuleCenter, Vector3 axisDirection,
                                               float capsuleRadius, float cylinderHalfHeight)
    {
        Vector3 toPoint = point - capsuleCenter;
        float projectionLength = Vector3.Dot(toPoint, axisDirection);
        
        if (Mathf.Abs(projectionLength) <= cylinderHalfHeight)
        {
            // 円柱部分の表面点
            Vector3 closestPointOnAxis = capsuleCenter + axisDirection * projectionLength;
            Vector3 radialDirection = (point - closestPointOnAxis).normalized;
            return closestPointOnAxis + radialDirection * capsuleRadius;
        }
        else
        {
            // キャップ部分の表面点
            float capDirection = projectionLength > 0 ? 1f : -1f;
            Vector3 capCenter = capsuleCenter + axisDirection * (capDirection * cylinderHalfHeight);
            Vector3 directionToSurface = (point - capCenter).normalized;
            return capCenter + directionToSurface * capsuleRadius;
        }
    }

    /// <summary>
    /// BoxCollider（立方体）専用の接触判定処理
    /// </summary>
    private void ProcessBoxCollision(Collider boxCollider, int cubeIndex, List<OVRBone> allBones,
        ref bool currentlyTouching, ref Transform closestBone, ref float minDistance, ref Vector3 closestTouchPoint)
    {
        // 動的な閾値設定（ヒステリシス効果） - Box専用閾値を使用
        float effectiveThreshold = boxContactThreshold;
        if (enableContactStabilization && isTouching[cubeIndex])
        {
            effectiveThreshold += contactHysteresis;
        }

        foreach (var bone in allBones)
        {
            if (bone.Transform == null) continue;

            Vector3 bonePosition = bone.Transform.position;
            Vector3 closestPoint = boxCollider.ClosestPoint(bonePosition);
            float distanceToSurface = Vector3.Distance(bonePosition, closestPoint);

            // 接触判定
            if (distanceToSurface <= effectiveThreshold)
            {
                currentlyTouching = true;
                if (distanceToSurface < minDistance)
                {
                    minDistance = distanceToSurface;
                    closestBone = bone.Transform;
                    closestTouchPoint = closestPoint;
                }
            }
        }

        if (logContactEvents && currentlyTouching)
        {
            Debug.Log($"Box contact detected: Distance to surface = {minDistance:F3}, Threshold = {effectiveThreshold:F3}");
        }
    }

    /// <summary>
    /// 汎用Collider（MeshCollider等）の接触判定処理
    /// </summary>
    private void ProcessGenericCollision(Collider objectCollider, int cubeIndex, List<OVRBone> allBones,
        ref bool currentlyTouching, ref Transform closestBone, ref float minDistance, ref Vector3 closestTouchPoint)
    {
        // 動的な閾値設定（ヒステリシス効果） - 汎用Collider専用閾値を使用
        float effectiveThreshold = genericContactThreshold;
        if (enableContactStabilization && isTouching[cubeIndex])
        {
            effectiveThreshold += contactHysteresis;
        }

        // 従来の拡張Bounds判定をフォールバックとして使用
        float enlargementFactor = 1.005f;
        
        foreach (var bone in allBones)
        {
            if (bone.Transform == null) continue;

            Vector3 bonePosition = bone.Transform.position;
            Vector3 closestPoint = objectCollider.ClosestPoint(bonePosition);
            float distance = Vector3.Distance(bonePosition, closestPoint);

            // 精密な距離ベース判定とBounds判定の組み合わせ
            bool withinDistanceThreshold = distance <= effectiveThreshold;
            
            // 拡張Boundsチェック（フォールバック用）
            Bounds enlargedBounds = objectCollider.bounds;
            enlargedBounds.Expand(enlargedBounds.size * (enlargementFactor - 1f));
            bool withinBounds = enlargedBounds.Contains(bonePosition);

            if (withinDistanceThreshold || withinBounds)
            {
                currentlyTouching = true;
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestBone = bone.Transform;
                    closestTouchPoint = closestPoint;
                }
            }
        }

        if (logContactEvents && currentlyTouching)
        {
            string colliderType = objectCollider.GetType().Name;
            Debug.Log($"{colliderType} contact detected: Distance to surface = {minDistance:F3}, Threshold = {effectiveThreshold:F3}");
        }
    }

    /// <summary>
    /// 指定されたCubeに右手が接触しているかを判定
    /// </summary>
    /// <param name="cubeIndex">Cubeのインデックス</param>
    /// <returns>右手が接触している場合true</returns>
    public bool IsRightHandTouching(int cubeIndex)
    {
        string handType = GetContactingHandType(cubeIndex);
        return handType == "Right Hand";
    }

    /// <summary>
    /// 指定されたCubeに左手が接触しているかを判定
    /// </summary>
    /// <param name="cubeIndex">Cubeのインデックス</param>
    /// <returns>左手が接触している場合true</returns>
    public bool IsLeftHandTouching(int cubeIndex)
    {
        string handType = GetContactingHandType(cubeIndex);
        return handType == "Left Hand";
    }
}