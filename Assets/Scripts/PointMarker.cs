using System;
using UnityEngine;

public class PointMarker : MonoBehaviour
{
    [Header("Reference Settings")]
    public BoneJudgeNew boneJudge; // BoneJudgeの参照
    public ProgressSet progressset;
    
    [Header("Point Settings")]
    public Vector3 startPoint = new Vector3(0, 0, 0);
    public Vector3 endPoint = new Vector3(1, 1, 1);
    public bool useReferencePoints = true; // BoneJudgeから座標を取得するかどうか
    
    [Header("Sphere Settings")]
    public float sphereRadius = 0.1f;
    public Material startPointMaterial;
    public Material endPointMaterial;
    public bool showOnStart = true;
    
    [Header("Runtime Control")]
    public bool updateInRealtime = true; // デフォルトをtrueに変更
    
    private GameObject startSphere;
    private GameObject endSphere;
    
    void Start()
    {
        // BoneJudgeの参照チェック
        if (boneJudge == null && useReferencePoints)
        {
            Debug.LogWarning("PointMarker: BoneJudge reference is not set. Using manual coordinates.");
            useReferencePoints = false;
        }
        
        if (showOnStart)
        {
            CreateSpheres();
        }
    }
    
    void Update()
    {
        // BoneJudgeから座標を取得
        if (useReferencePoints && boneJudge != null)
        {
            Vector3 newStartPoint = progressset.progressStart;
            Vector3 newEndPoint = progressset.progressEnd;

            // 座標が変化した場合のみ更新（パフォーマンス向上）
            if (newStartPoint != startPoint || newEndPoint != endPoint)
            {
                startPoint = newStartPoint;
                endPoint = newEndPoint;
                
                // リアルタイム更新が有効な場合、またはBoneJudge参照時は常に更新
                if (updateInRealtime || useReferencePoints)
                {
                    UpdateSpherePositions();
                }
                
                // デバッグログで座標変化を確認
                Debug.Log($"PointMarker: Points updated - Start: {startPoint}, End: {endPoint}");
            }
        }
        else if (updateInRealtime)
        {
            // 手動座標でリアルタイム更新
            UpdateSpherePositions();
        }

        DebugBoneJudgeValues();
          }
    
    /// <summary>
    /// Start pointとEnd pointにSphereオブジェクトを作成します
    /// </summary>
    public void CreateSpheres()
    {
        // 既存のSphereがあれば削除
        DestroySpheres();
        
        // Start Point Sphere
        startSphere = CreateSphere("StartPoint", startPoint, startPointMaterial);
        
        // End Point Sphere
        endSphere = CreateSphere("EndPoint", endPoint, endPointMaterial);
        
        Debug.Log($"PointMarker: Created spheres at Start({startPoint}) and End({endPoint})");
    }
    
    /// <summary>
    /// Sphereオブジェクトを作成する内部メソッド
    /// </summary>
    private GameObject CreateSphere(string name, Vector3 position, Material material)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.position = position;
        sphere.transform.localScale = Vector3.one * sphereRadius * 2f; // radiusは半径なので直径に変換
        
        // マテリアルを設定
        if (material != null)
        {
            Renderer renderer = sphere.GetComponent<Renderer>();
            renderer.material = material;
        }
        
        // このオブジェクトの子にする
        sphere.transform.SetParent(this.transform);
        
        return sphere;
    }
    
    /// <summary>
    /// Sphereの位置を更新します
    /// </summary>
    public void UpdateSpherePositions()
    {
        // BoneJudgeから最新の座標を取得
        if (useReferencePoints && boneJudge != null)
        {
            startPoint = progressset.progressStart;
            endPoint = progressset.progressEnd;
        }
        
        if (startSphere != null)
        {
            startSphere.transform.position = startPoint;
        }
        
        if (endSphere != null)
        {
            endSphere.transform.position = endPoint;
        }
    }
    
    /// <summary>
    /// Sphereオブジェクトを削除します
    /// </summary>
    public void DestroySpheres()
    {
        if (startSphere != null)
        {
            DestroyImmediate(startSphere);
            startSphere = null;
        }
        
        if (endSphere != null)
        {
            DestroyImmediate(endSphere);
            endSphere = null;
        }
    }
    
    /// <summary>
    /// Start pointの座標を設定します
    /// </summary>
    public void SetStartPoint(Vector3 point)
    {
        startPoint = point;
        if (startSphere != null)
        {
            startSphere.transform.position = startPoint;
        }
    }
    
    /// <summary>
    /// End pointの座標を設定します
    /// </summary>
    public void SetEndPoint(Vector3 point)
    {
        endPoint = point;
        if (endSphere != null)
        {
            endSphere.transform.position = endPoint;
        }
    }
    
    /// <summary>
    /// 両方の座標を同時に設定します
    /// </summary>
    public void SetPoints(Vector3 start, Vector3 end)
    {
        SetStartPoint(start);
        SetEndPoint(end);
    }
    
    /// <summary>
    /// Sphereの半径を変更します
    /// </summary>
    public void SetSphereRadius(float radius)
    {
        sphereRadius = radius;
        float scale = radius * 2f;
        
        if (startSphere != null)
        {
            startSphere.transform.localScale = Vector3.one * scale;
        }
        
        if (endSphere != null)
        {
            endSphere.transform.localScale = Vector3.one * scale;
        }
    }
    
    /// <summary>
    /// Sphereの表示/非表示を切り替えます
    /// </summary>
    public void SetSpheresVisible(bool visible)
    {
        if (startSphere != null)
        {
            startSphere.SetActive(visible);
        }
        
        if (endSphere != null)
        {
            endSphere.SetActive(visible);
        }
    }
    
    // Inspector上でのギズモ表示
    void OnDrawGizmos()
    {
        // BoneJudgeから座標を取得
        Vector3 displayStartPoint = startPoint;
        Vector3 displayEndPoint = endPoint;
        
        if (useReferencePoints && boneJudge != null)
        {
            displayStartPoint = progressset.progressStart;
            displayEndPoint = progressset.progressEnd;
        }
        
        // Start Point
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(displayStartPoint, sphereRadius);
        
        // End Point
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(displayEndPoint, sphereRadius);
        
        // 線で結ぶ
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(displayStartPoint, displayEndPoint);
    }
    
    /// <summary>
    /// BoneJudgeから現在の座標を強制的に取得します
    /// </summary>
    public void RefreshPointsFromBoneJudge()
    {
        if (progressset != null)
        {
            startPoint = progressset.progressStart;
            endPoint = progressset.progressEnd;
            UpdateSpherePositions();
            Debug.Log($"PointMarker: Refreshed points from BoneJudge - Start: {startPoint}, End: {endPoint}");
        }
        else
        {
            Debug.LogWarning("PointMarker: BoneJudge reference is null.");
        }
    }
    
    /// <summary>
    /// BoneJudgeの参照を設定し、座標を取得します
    /// </summary>
    public void SetBoneJudgeReference(BoneJudgeNew boneJudgeRef)
    {
        boneJudge = boneJudgeRef;
        useReferencePoints = (boneJudge != null);
        if (useReferencePoints)
        {
            RefreshPointsFromBoneJudge();
        }
    }
    
    /// <summary>
    /// BoneJudgeの現在の値をデバッグ出力します
    /// </summary>
    public void DebugBoneJudgeValues()
    {
        if (boneJudge != null)
        {
            Debug.Log($"BoneJudge ProgressStart: {progressset.progressStart}");
            Debug.Log($"BoneJudge ProgressEnd: {progressset.progressEnd}");
            Debug.Log($"PointMarker StartPoint: {startPoint}");
            Debug.Log($"PointMarker EndPoint: {endPoint}");
            
            if (startSphere != null)
                Debug.Log($"StartSphere Position: {startSphere.transform.position}");
            if (endSphere != null)
                Debug.Log($"EndSphere Position: {endSphere.transform.position}");
        }
        else
        {
            Debug.LogWarning("BoneJudge reference is null");
        }
    }
    
    void OnDestroy()
    {
        // オブジェクト破棄時にSphereも削除
        DestroySpheres();
    }
}