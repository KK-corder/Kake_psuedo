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
    // 接触オブジェクト（Sphereなど）をインスペクターでアタッチ
    // Sphere: 中心からの距離で精密判定を行います
    public GameObject[] cubes = new GameObject[3];

    [Header("Sphere Contact Settings")]
    // Sphere用の接触判定距離設定
    public float sphereContactThreshold = 0.02f; // Sphere表面からの接触判定距離（拡大）
    
    [Header("Contact Stability Settings")]
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
                // SphereColliderがない場合は汎用Colliderでフォールバック
                Collider objectCollider = cubes[cubeIndex].GetComponent<Collider>();
                if (objectCollider == null) continue;

                // 従来の拡張Bounds判定
                float enlargementFactor = 1.005f;
                
                foreach (var bone in allBones)
                {
                    if (bone.Transform == null) continue;

                    Vector3 bonePosition = bone.Transform.position;
                    Vector3 closestPoint = objectCollider.ClosestPoint(bonePosition);
                    float distance = Vector3.Distance(bonePosition, closestPoint);

                    // 拡張された当たり判定範囲内かチェック
                    Bounds enlargedBounds = objectCollider.bounds;
                    enlargedBounds.Expand(enlargedBounds.size * (enlargementFactor - 1f));

                    if (enlargedBounds.Contains(bonePosition))
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
}