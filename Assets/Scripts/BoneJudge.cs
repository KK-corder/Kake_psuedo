using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoneJudge : MonoBehaviour
{
    // スケルトン（OVRHandPrefabに設定）
    public OVRSkeleton HandSkeleton;
    // 接触オブジェクト（CubeやSphereなど）をインスペクターでアタッチ
    public GameObject[] cubes = new GameObject[3];

    // 各オブジェクトに対する接触状態
    public bool[] isTouching;
    // 各オブジェクトに対応する固定された最も近いボーン
    public Transform[] fixedClosestBones; // privateからpublicに変更

    public Vector3[] touchPoints; // 各オブジェクトに対応する接触点
    private float[] fixedZs;       // 各オブジェクトに対応する固定z座標
    private float[] zDifferences;

    // プログレスの始点と終点の座標
    [HideInInspector] public Vector3 progressStart;
    [HideInInspector] public Vector3 progressEnd;

    // Start Point用のオブジェクト参照（Inspectorで設定）
    [Header("Progress Point Settings")]
    public Transform startPointObject; // 始点として使用するオブジェクト
    public bool useCustomStartPoint = false; // カスタムstart pointを使用するかどうか

    // 追加: 始点用のHand_ForearmStubの z 座標を固定するための変数
    private float fixedForearmZ;
    private bool forearmZFrozen = false;
    // Optional: change cube material on first contact
    public Material contactMaterial;
    private Material[] originalMaterials;

    // For debug visibility
    public bool logContactEvents = true;

    void Start()
    {
        // 配列の初期化
        int count = Mathf.Max(1, cubes != null ? cubes.Length : 1);
        isTouching = new bool[count];
        fixedClosestBones = new Transform[count];
        touchPoints = new Vector3[count];
        fixedZs = new float[count];
        zDifferences = new float[count];

        // cache original materials
        originalMaterials = new Material[count];
        for (int i = 0; i < count; i++)
        {
            var r = (cubes != null && i < cubes.Length && cubes[i] != null) ? cubes[i].GetComponent<Renderer>() : null;
            originalMaterials[i] = (r != null) ? r.sharedMaterial : null;
        }
        
        // カスタムstart pointの検証
        if (useCustomStartPoint && startPointObject == null)
        {
            Debug.LogWarning("BoneJudge: useCustomStartPoint is enabled but startPointObject is not assigned. Falling back to Hand_ForearmStub.");
            useCustomStartPoint = false;
        }
        
        if (useCustomStartPoint && startPointObject != null)
        {
            Debug.Log($"BoneJudge: Using custom start point object: {startPointObject.name}");
        }
    }

    void Update()
    {
        if (HandSkeleton == null || HandSkeleton.Bones == null)
            return;

        // オブジェクトとの衝突判定を実施
        HandleObjectCollisions();

        // まず、任意のオブジェクトに接触しているかをチェック
        bool anyContact = false;
        for (int i = 0; i < isTouching.Length; i++)
        {
            if (isTouching[i])
            {
                anyContact = true;
                break;
            }
        }

        // 始点(progressStart)の決定
        Vector3 baseStartPos = Vector3.zero;
        
        if (useCustomStartPoint && startPointObject != null)
        {
            // カスタムオブジェクトの座標を使用
            baseStartPos = startPointObject.position;
        }
        else
        {
            // デフォルト: Hand_ForearmStub の座標を使用する
            foreach (var bone in HandSkeleton.Bones)
            {
                if (bone.Transform != null && bone.Transform.name == "Hand_ForearmStub")
                {
                    baseStartPos = bone.Transform.position;
                    break;
                }
            }
        }
        
        if (anyContact)
        {
            // 接触中なら、初回の接触時に固定した z 座標を使う
            if (!forearmZFrozen)
            {
                fixedForearmZ = baseStartPos.z;
                forearmZFrozen = true;
            }
            progressStart = new Vector3(baseStartPos.x, baseStartPos.y, fixedForearmZ);
        }
        else
        {
            forearmZFrozen = false; // 接触がなくなったら解除
            progressStart = baseStartPos;
        }

        // 終点(progressEnd)は、接触があれば固定されたboneの座標、
        // 接触がなければ Hand_IndexTip の座標を使用する
        bool foundContact = false;
        for (int i = 0; i < fixedClosestBones.Length; i++)
        {
            if (isTouching[i] && fixedClosestBones[i] != null)
            {
                // 接触中の場合は、接触時に記録した z 座標を使用
                progressEnd = new Vector3(fixedClosestBones[i].position.x, fixedClosestBones[i].position.y, fixedZs[i]);
                foundContact = true;
                break;
            }
        }
        if (!foundContact)
        {
            foreach (var bone in HandSkeleton.Bones)
            {
                if (bone.Transform != null && bone.Transform.name == "Hand_IndexTip")
                {
                    progressEnd = bone.Transform.position;
                    break;
                }
            }
        }
    }

    private void HandleObjectCollisions()
    {
        if (cubes == null || cubes.Length == 0 || HandSkeleton == null || HandSkeleton.Bones == null)
            return;

        // Cubeの当たり判定を拡張する倍率
        const float cubeHitboxScale = 1.05f; // 30%大きく
        for (int i = 0; i < cubes.Length; i++)
        {
            GameObject obj = cubes[i];
            if (obj == null)
                continue;

            Transform objTransform = obj.transform;
            var bones = HandSkeleton.Bones;

            // 接触中の場合、固定されたboneの位置を使用
            if (isTouching != null && i < isTouching.Length && isTouching[i] && fixedClosestBones != null && i < fixedClosestBones.Length && fixedClosestBones[i] != null)
            {
                Transform fixedBone = fixedClosestBones[i];
                touchPoints[i] = new Vector3(fixedBone.position.x, fixedBone.position.y, fixedZs[i]);
                zDifferences[i] = Mathf.Max(0, Mathf.Abs(fixedBone.position.z - fixedZs[i]));
            }
            else
            {
                // 接触していない場合は、暫定的に人差し指先端 (Hand_IndexTip) の位置を使用
                int indexTipId = (int)OVRSkeleton.BoneId.Hand_IndexTip;
                if (indexTipId >= 0 && HandSkeleton.Bones.Count > indexTipId && HandSkeleton.Bones[indexTipId].Transform != null)
                {
                    touchPoints[i] = HandSkeleton.Bones[indexTipId].Transform.position;
                }
            }

            // オブジェクトの形状によって判定方法を切り替え
            bool isSphere = obj.GetComponent<SphereCollider>() != null;
            bool isCube = obj.GetComponent<BoxCollider>() != null;

            Transform closestBone = null;

            if (isCube)
            {
                // --- AABB（立方体）判定（本来の大きさより少し大きく） ---
                foreach (var bone in bones)
                {
                    if (bone.Transform == null)
                        continue;

                    Vector3 bonePos = bone.Transform.position;
                    Vector3 cubeCenter = objTransform.position;
                    Vector3 cubeScale = objTransform.lossyScale * 0.5f * cubeHitboxScale; // 10%拡大

                    bool inside =
                        Mathf.Abs(bonePos.x - cubeCenter.x) <= cubeScale.x &&
                        Mathf.Abs(bonePos.y - cubeCenter.y) <= cubeScale.y &&
                        Mathf.Abs(bonePos.z - cubeCenter.z) <= cubeScale.z;

                    if (inside)
                    {
                        closestBone = bone.Transform;
                        break; // 1つでも入っていればOK
                    }
                }
            }
            else if (isSphere)
            {
                // --- 球体判定（本来の大きさそのまま） ---
                float minDist = float.MaxValue;
                foreach (var bone in bones)
                {
                    if (bone.Transform == null)
                        continue;

                    Vector3 bonePos = bone.Transform.position;
                    Vector3 sphereCenter = objTransform.position;
                    float radius = objTransform.lossyScale.x * 0.5f * cubeHitboxScale * 1.2f; //ドスケール半径
                    float dist = Vector3.Distance(bonePos, sphereCenter);

                    if (dist <= radius)
                    {
                        if (dist < minDist)
                        {
                            closestBone = bone.Transform;
                            minDist = dist;
                        }
                    }
                }
            }
            else
            {
                // 形状がCube/Sphere以外の場合はAABBで判定
                foreach (var bone in bones)
                {
                    if (bone.Transform == null)
                        continue;

                    Vector3 bonePos = bone.Transform.position;
                    Vector3 center = objTransform.position;
                    Vector3 scale = objTransform.lossyScale * 0.5f;

                    bool inside =
                        Mathf.Abs(bonePos.x - center.x) <= scale.x &&
                        Mathf.Abs(bonePos.y - center.y) <= scale.y &&
                        Mathf.Abs(bonePos.z - center.z) <= scale.z;

                    if (inside)
                    {
                        closestBone = bone.Transform;
                        break;
                    }
                }
            }

            if (closestBone != null)
            {
                if (!isTouching[i])
                {
                    fixedClosestBones[i] = closestBone;
                    fixedZs[i] = closestBone.position.z;
                    isTouching[i] = true;
                    // First frame of contact: optional material swap + log
                    if (contactMaterial != null)
                    {
                        var rend = cubes[i].GetComponent<Renderer>();
                        if (rend != null)
                            rend.sharedMaterial = contactMaterial;
                    }
                    if (logContactEvents)
                        Debug.Log($"BoneJudge: Cube[{i}] contact START (closest bone={closestBone.name})");
                }
            }
            else
            {
                if (isTouching != null && i < isTouching.Length)
                {
                    if (isTouching[i] && logContactEvents)
                        Debug.Log($"BoneJudge: Cube[{i}] contact END");
                    isTouching[i] = false;
                }
                if (fixedClosestBones != null && i < fixedClosestBones.Length)
                    fixedClosestBones[i] = null;
                if (zDifferences != null && i < zDifferences.Length)
                    zDifferences[i] = 0;
                // restore original material when contact ends
                if (originalMaterials != null && i < originalMaterials.Length && originalMaterials[i] != null)
                {
                    var rend = cubes[i].GetComponent<Renderer>();
                    if (rend != null)
                        rend.sharedMaterial = originalMaterials[i];
                }
            }
        }
    }
    
    /// <summary>
    /// 指定されたインデックスの接触しているボーンの位置を取得
    /// </summary>
    /// <param name="cubeIndex">Cubeのインデックス</param>
    /// <returns>接触しているボーンの位置、接触していない場合はVector3.zero</returns>
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
}