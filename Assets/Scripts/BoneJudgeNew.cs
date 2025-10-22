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
    // 接触オブジェクト（CubeやSphereなど）をインスペクターでアタッチ
    public GameObject[] cubes = new GameObject[3];



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

        // cache original materials
        originalMaterials = new Material[count];
        for (int i = 0; i < count; i++)
        {
            var r = (cubes != null && i < cubes.Length && cubes[i] != null) ? cubes[i].GetComponent<Renderer>() : null;
            originalMaterials[i] = (r != null) ? r.sharedMaterial : null;
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

        // Cubeの当たり判定を拡張する倍率
        float enlargementFactor = 1.005f;

        for (int cubeIndex = 0; cubeIndex < cubes.Length; cubeIndex++)
        {
            if (cubes[cubeIndex] == null) continue;

            bool currentlyTouching = false;
            Transform closestBone = null;
            float minDistance = float.MaxValue;
            Vector3 closestTouchPoint = Vector3.zero;

            // Cube の Collider を取得
            Collider cubeCollider = cubes[cubeIndex].GetComponent<Collider>();
            if (cubeCollider == null) continue;

            // 両手の各ボーンとの距離をチェック
            foreach (var bone in allBones)
            {
                if (bone.Transform == null) continue;

                Vector3 bonePosition = bone.Transform.position;
                Vector3 closestPoint = cubeCollider.ClosestPoint(bonePosition);
                float distance = Vector3.Distance(bonePosition, closestPoint);

                // 拡張された当たり判定範囲内かチェック
                Bounds enlargedBounds = cubeCollider.bounds;
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

            // 接触状態の更新
            bool wasNotTouching = !isTouching[cubeIndex];
            isTouching[cubeIndex] = currentlyTouching;

            if (currentlyTouching)
            {
                // 初回接触時または最も近いボーンが変わった場合
                if (wasNotTouching || fixedClosestBones[cubeIndex] != closestBone)
                {
                    fixedClosestBones[cubeIndex] = closestBone;
                    touchPoints[cubeIndex] = closestTouchPoint;

                    if (logContactEvents)
                    {
                        string handType = GetHandTypeFromBone(closestBone);
                        Debug.Log($"Contact started with Cube {cubeIndex} using {handType} bone: {closestBone.name}");
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
                    Debug.Log($"Contact ended with Cube {cubeIndex}");
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