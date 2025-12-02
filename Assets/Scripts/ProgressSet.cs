using UnityEngine;

public class ProgressSet : MonoBehaviour
{
    [Header("References")]
    public BoneJudgeNew bonejudgeNew; // 接触判定の参照
    
    [Header("Start Point Settings")]
    public Transform leftHandStartPointObject;  // 左手用の始点オブジェクト
    public Transform rightHandStartPointObject; // 右手用の始点オブジェクト
    public bool useCustomStartPoint = false; // カスタムstart pointを使用するかどうか
    
    [Header("Hand Detection")]
    public GrabJudge grabJudge; // 掴み判定の参照
    
    [Header("Progress Points")]
    // プログレスの始点と終点の座標
    [HideInInspector] public Vector3 progressStart;
    [HideInInspector] public Vector3 progressEnd;
    
    [Header("Debug")]
    public bool logProgressEvents = true;
    
    [Header("Point Visualization")]
    public GameObject startPointSphere; // StartPointを表示するSphere
    public GameObject endPointSphere;   // EndPointを表示するSphere
    public bool enableVisualization = true; // 可視化を有効にするか

    // 内部変数
    private float[] fixedZs; // 各オブジェクトに対応する固定z座標
    private float fixedForearmX; // 始点用のHand_ForearmStubの x 座標を固定するための変数（Y軸のみ変化用）
    private float fixedForearmZ; // 始点用のHand_ForearmStubの z 座標を固定するための変数（Y軸のみ変化用）
    private Vector3 fixedStartPoint; // 固定されたスタートポイント
    private bool forearmZFrozen = false;

    void Start()
    {
        // 初期化
        if (bonejudgeNew == null)
        {
            Debug.LogError("ProgressSet: bonejudgeNew reference is required!");
            return;
        }
        
        // BoneJudgeNewから両手のスケルトンが設定されているかチェック
        if (bonejudgeNew.GetRightHandSkeleton() == null && bonejudgeNew.GetLeftHandSkeleton() == null)
        {
            Debug.LogError("ProgressSet: At least one hand skeleton must be set in BoneJudgeNew!");
            return;
        }

        int count = Mathf.Max(1, bonejudgeNew.cubes != null ? bonejudgeNew.cubes.Length : 1);
        fixedZs = new float[count];
        
        // カスタムstart pointの検証
        if (useCustomStartPoint)
        {
            bool leftValid = leftHandStartPointObject != null;
            bool rightValid = rightHandStartPointObject != null;
            
            if (!leftValid && !rightValid)
            {
                Debug.LogWarning("ProgressSet: useCustomStartPoint is enabled but no start point objects are assigned. Falling back to Hand_ForearmStub.");
                useCustomStartPoint = false;
            }
            else
            {
                if (leftValid && rightValid)
                {
                    Debug.Log($"ProgressSet: Using custom start point objects - Left: {leftHandStartPointObject.name}, Right: {rightHandStartPointObject.name}");
                }
                else if (leftValid)
                {
                    Debug.Log($"ProgressSet: Using left hand start point object: {leftHandStartPointObject.name}");
                }
                else if (rightValid)
                {
                    Debug.Log($"ProgressSet: Using right hand start point object: {rightHandStartPointObject.name}");
                }
            }
        }
        
        // GrabJudgeの検証
        if (grabJudge == null)
        {
            Debug.LogWarning("ProgressSet: grabJudge reference is not assigned. Hand-specific start point selection may not work properly.");
        }
    }

    void Update()
    {
        if (bonejudgeNew == null)
            return;

        UpdateProgressPoints();
    }

    void UpdateProgressPoints()
    {
        // 接触状態をチェック
        bool anyContact = bonejudgeNew.IsAnyContact();

        // 始点(progressStart)
        // VRIK時は肩の座標
        // 通常のハントラ時は手首の座標
        Vector3 baseStartPos = Vector3.zero;
        
        if (useCustomStartPoint && (leftHandStartPointObject != null || rightHandStartPointObject != null))
        {
            // 接触かつ掴み状態に基づいて手種別を判定
            HandType activeHand = DetermineActiveHand();
            
            if (activeHand == HandType.LeftHand && leftHandStartPointObject != null)
            {
                baseStartPos = leftHandStartPointObject.position;
                if (logProgressEvents && anyContact)
                {
                    Debug.Log($"ProgressSet: Using left hand start point object: {leftHandStartPointObject.name}");
                }
            }
            else if (activeHand == HandType.RightHand && rightHandStartPointObject != null)
            {
                baseStartPos = rightHandStartPointObject.position;
                if (logProgressEvents && anyContact)
                {
                    Debug.Log($"ProgressSet: Using right hand start point object: {rightHandStartPointObject.name}");
                }
            }
            else
            {
                // フォールバック: 利用可能なオブジェクトを使用
                if (leftHandStartPointObject != null)
                {
                    baseStartPos = leftHandStartPointObject.position;
                }
                else if (rightHandStartPointObject != null)
                {
                    baseStartPos = rightHandStartPointObject.position;
                }
                else
                {
                    baseStartPos = GetContactingHandForearmPosition();
                }
                
                if (logProgressEvents && anyContact)
                {
                    Debug.Log($"ProgressSet: Using fallback start point, active hand: {activeHand}");
                }
            }
        }
        else
        {
            // 接触している手のHand_ForearmStubを探す
            baseStartPos = GetContactingHandForearmPosition();
            
            // 接触している手が見つからない場合は、利用可能な手を使用
            if (baseStartPos == Vector3.zero)
            {
                baseStartPos = GetAnyAvailableHandForearmPosition();
            }
        }
        
        // StartPointは常に動的に更新（固定しない）
        progressStart = baseStartPos;
        
        if (anyContact)
        {
            if (!forearmZFrozen)
            {
                forearmZFrozen = true;
                if (logProgressEvents)
                {
                    Debug.Log($"ProgressSet: Contact started. StartPoint following hand movement dynamically.");
                }
            }
        }
        else
        {
            forearmZFrozen = false; // 接触がなくなったら解除
            
            if (logProgressEvents && anyContact != bonejudgeNew.IsAnyContact())
            {
                Debug.Log("ProgressSet: Contact ended. StartPoint remains dynamic.");
            }
        }

        // 終点(progressEnd)は、接触があれば固定されたboneの座標、
        // 接触がなければ接触している手のHand_IndexTipの座標を使用する
        bool foundContact = false;
        for (int i = 0; i < bonejudgeNew.fixedClosestBones.Length; i++)
        {
            if (bonejudgeNew.isTouching[i] && bonejudgeNew.fixedClosestBones[i] != null)
            {
                // EndPointは手の実際の位置を使用（手の方向ベクトルを保持するため）
                progressEnd = new Vector3(bonejudgeNew.fixedClosestBones[i].position.x, 
                                        bonejudgeNew.fixedClosestBones[i].position.y, 
                                        bonejudgeNew.fixedClosestBones[i].position.z);
                foundContact = true;
                break;
            }
        }
        
        if (!foundContact)
        {
            // 接触している手のIndexTipを探す
            progressEnd = GetContactingHandIndexTipPosition();
            
            // 接触している手が見つからない場合は、利用可能な手を使用
            if (progressEnd == Vector3.zero)
            {
                progressEnd = GetAnyAvailableHandIndexTipPosition();
            }
        }
        
        // デバッグログ
        if (logProgressEvents && anyContact)
        {
            Debug.Log($"ProgressSet: Start={progressStart}, End={progressEnd}");
        }
        
        // Pointの可視化更新
        UpdatePointVisualization();
    }
    
    /// <summary>
    /// StartPointとEndPointのSphereの位置を更新
    /// </summary>
    private void UpdatePointVisualization()
    {
        if (!enableVisualization)
            return;
            
        // StartPoint Sphereの位置更新
        if (startPointSphere != null)
        {
            startPointSphere.transform.position = progressStart;
            
            // 接触中は表示、非接触時は非表示
            bool anyContact = bonejudgeNew.IsAnyContact();
            startPointSphere.SetActive(anyContact);
        }
        
        // EndPoint Sphereの位置更新
        if (endPointSphere != null)
        {
            endPointSphere.transform.position = progressEnd;
            
            // 接触中は表示、非接触時は非表示
            bool anyContact = bonejudgeNew.IsAnyContact();
            endPointSphere.SetActive(anyContact);
        }
    }
    
    /// <summary>
    /// 手動でstart pointを設定
    /// </summary>
    /// <param name="startPoint">設定する座標</param>
    public void SetStartPoint(Vector3 startPoint)
    {
        progressStart = startPoint;
        if (logProgressEvents)
        {
            Debug.Log($"ProgressSet: Manual start point set to {startPoint}");
        }
    }
    
    /// <summary>
    /// 手動でend pointを設定
    /// </summary>
    /// <param name="endPoint">設定する座標</param>
    public void SetEndPoint(Vector3 endPoint)
    {
        progressEnd = endPoint;
        if (logProgressEvents)
        {
            Debug.Log($"ProgressSet: Manual end point set to {endPoint}");
        }
    }
    
    /// <summary>
    /// 左手用カスタムstart pointオブジェクトを設定
    /// </summary>
    /// <param name="customObject">使用するTransform</param>
    public void SetLeftHandStartPointObject(Transform customObject)
    {
        leftHandStartPointObject = customObject;
        useCustomStartPoint = (leftHandStartPointObject != null || rightHandStartPointObject != null);
        
        if (logProgressEvents)
        {
            Debug.Log($"ProgressSet: Left hand start point object set to {(customObject != null ? customObject.name : "null")}");
        }
    }
    
    /// <summary>
    /// 右手用カスタムstart pointオブジェクトを設定
    /// </summary>
    /// <param name="customObject">使用するTransform</param>
    public void SetRightHandStartPointObject(Transform customObject)
    {
        rightHandStartPointObject = customObject;
        useCustomStartPoint = (leftHandStartPointObject != null || rightHandStartPointObject != null);
        
        if (logProgressEvents)
        {
            Debug.Log($"ProgressSet: Right hand start point object set to {(customObject != null ? customObject.name : "null")}");
        }
    }
    
    /// <summary>
    /// 両手のカスタムstart pointオブジェクトを設定
    /// </summary>
    /// <param name="leftHandObject">左手用Transform</param>
    /// <param name="rightHandObject">右手用Transform</param>
    public void SetBothHandStartPointObjects(Transform leftHandObject, Transform rightHandObject)
    {
        leftHandStartPointObject = leftHandObject;
        rightHandStartPointObject = rightHandObject;
        useCustomStartPoint = (leftHandObject != null || rightHandObject != null);
        
        if (logProgressEvents)
        {
            Debug.Log($"ProgressSet: Start point objects set - Left: {(leftHandObject != null ? leftHandObject.name : "null")}, Right: {(rightHandObject != null ? rightHandObject.name : "null")}");
        }
    }
    
    /// <summary>
    /// 現在のプログレス距離を取得
    /// </summary>
    /// <returns>start pointとend pointの距離</returns>
    public float GetProgressDistance()
    {
        return Vector3.Distance(progressStart, progressEnd);
    }
    
    /// <summary>
    /// 可視化用Sphereを設定
    /// </summary>
    /// <param name="startSphere">StartPoint用のSphere</param>
    /// <param name="endSphere">EndPoint用のSphere</param>
    public void SetVisualizationSpheres(GameObject startSphere, GameObject endSphere)
    {
        startPointSphere = startSphere;
        endPointSphere = endSphere;
        
        if (logProgressEvents)
        {
            Debug.Log($"ProgressSet: Visualization spheres set. Start: {(startSphere != null ? startSphere.name : "null")}, End: {(endSphere != null ? endSphere.name : "null")}");
        }
    }
    
    /// <summary>
    /// 可視化の有効/無効を切り替え
    /// </summary>
    /// <param name="enabled">有効にするか</param>
    public void SetVisualizationEnabled(bool enabled)
    {
        enableVisualization = enabled;
        
        // 無効にした場合はSphereを非表示
        if (!enabled)
        {
            if (startPointSphere != null) startPointSphere.SetActive(false);
            if (endPointSphere != null) endPointSphere.SetActive(false);
        }
        
        if (logProgressEvents)
        {
            Debug.Log($"ProgressSet: Visualization {(enabled ? "enabled" : "disabled")}");
        }
    }
    
    /// <summary>
    /// 接触している手のForearmStub位置を取得
    /// </summary>
    /// <returns>接触している手のForearmStub位置</returns>
    private Vector3 GetContactingHandForearmPosition()
    {
        // 接触しているボーンがどちらの手に属するかを確認
        for (int i = 0; i < bonejudgeNew.fixedClosestBones.Length; i++)
        {
            if (bonejudgeNew.isTouching[i] && bonejudgeNew.fixedClosestBones[i] != null)
            {
                string handType = bonejudgeNew.GetContactingHandType(i);
                
                if (handType == "Right Hand")
                {
                    return FindBonePosition(bonejudgeNew.GetRightHandSkeleton(), "Hand_ForearmStub");
                }
                else if (handType == "Left Hand")
                {
                    return FindBonePosition(bonejudgeNew.GetLeftHandSkeleton(), "Hand_ForearmStub");
                }
            }
        }
        return Vector3.zero;
    }
    
    /// <summary>
    /// 利用可能な手のForearmStub位置を取得
    /// </summary>
    /// <returns>利用可能な手のForearmStub位置</returns>
    private Vector3 GetAnyAvailableHandForearmPosition()
    {
        // 右手を優先して試す
        Vector3 rightHandPos = FindBonePosition(bonejudgeNew.GetRightHandSkeleton(), "Hand_ForearmStub");
        if (rightHandPos != Vector3.zero)
            return rightHandPos;
            
        // 左手を試す
        Vector3 leftHandPos = FindBonePosition(bonejudgeNew.GetLeftHandSkeleton(), "Hand_ForearmStub");
        return leftHandPos;
    }
    
    /// <summary>
    /// 接触している手のIndexTip位置を取得
    /// </summary>
    /// <returns>接触している手のIndexTip位置</returns>
    private Vector3 GetContactingHandIndexTipPosition()
    {
        // 接触しているボーンがどちらの手に属するかを確認
        for (int i = 0; i < bonejudgeNew.fixedClosestBones.Length; i++)
        {
            if (bonejudgeNew.isTouching[i] && bonejudgeNew.fixedClosestBones[i] != null)
            {
                string handType = bonejudgeNew.GetContactingHandType(i);
                
                if (handType == "Right Hand")
                {
                    return FindBonePosition(bonejudgeNew.GetRightHandSkeleton(), "Hand_IndexTip");
                }
                else if (handType == "Left Hand")
                {
                    return FindBonePosition(bonejudgeNew.GetLeftHandSkeleton(), "Hand_IndexTip");
                }
            }
        }
        return Vector3.zero;
    }
    
    /// <summary>
    /// 利用可能な手のIndexTip位置を取得
    /// </summary>
    /// <returns>利用可能な手のIndexTip位置</returns>
    private Vector3 GetAnyAvailableHandIndexTipPosition()
    {
        // 右手を優先して試す
        Vector3 rightHandPos = FindBonePosition(bonejudgeNew.GetRightHandSkeleton(), "Hand_IndexTip");
        if (rightHandPos != Vector3.zero)
            return rightHandPos;
            
        // 左手を試す
        Vector3 leftHandPos = FindBonePosition(bonejudgeNew.GetLeftHandSkeleton(), "Hand_IndexTip");
        return leftHandPos;
    }
    
    /// <summary>
    /// 指定したスケルトンから指定した名前のボーンの位置を検索
    /// </summary>
    /// <param name="skeleton">検索対象のスケルトン</param>
    /// <param name="boneName">検索するボーン名</param>
    /// <returns>ボーンの位置、見つからない場合はVector3.zero</returns>
    private Vector3 FindBonePosition(OVRSkeleton skeleton, string boneName)
    {
        if (skeleton == null || skeleton.Bones == null)
            return Vector3.zero;
            
        foreach (var bone in skeleton.Bones)
        {
            if (bone.Transform != null && bone.Transform.name == boneName)
            {
                return bone.Transform.position;
            }
        }
        return Vector3.zero;
    }

    #region Hand Detection Methods

    /// <summary>
    /// 手の種別を表す列挙型
    /// </summary>
    public enum HandType
    {
        None,
        LeftHand,
        RightHand,
        BothHands
    }

    /// <summary>
    /// 接触かつ掴み状態の手を判定する
    /// </summary>
    /// <returns>アクティブな手の種別</returns>
    private HandType DetermineActiveHand()
    {
        if (bonejudgeNew == null)
            return HandType.None;

        bool anyContact = bonejudgeNew.IsAnyContact();
        if (!anyContact)
            return HandType.None;

        // 接触している手を確認
        bool isRightHandTouching = false;
        bool isLeftHandTouching = false;

        // 各Cubeについて接触している手をチェック
        for (int i = 0; i < bonejudgeNew.cubes.Length; i++)
        {
            if (bonejudgeNew.isTouching[i])
            {
                if (bonejudgeNew.IsRightHandTouching(i))
                {
                    isRightHandTouching = true;
                }
                if (bonejudgeNew.IsLeftHandTouching(i))
                {
                    isLeftHandTouching = true;
                }
            }
        }

        // 握り状態を確認
        bool isRightHandGrabbing = false;
        bool isLeftHandGrabbing = false;

        if (grabJudge != null)
        {
            isRightHandGrabbing = grabJudge.IsRightHandGrabbing();
            isLeftHandGrabbing = grabJudge.IsLeftHandGrabbing();
        }

        // 接触かつ掴みの条件をチェック
        bool rightHandActive = isRightHandTouching && isRightHandGrabbing;
        bool leftHandActive = isLeftHandTouching && isLeftHandGrabbing;

        if (logProgressEvents && (rightHandActive || leftHandActive))
        {
            Debug.Log($"ProgressSet: Hand Activity - Right: Touch={isRightHandTouching}, Grab={isRightHandGrabbing}, Active={rightHandActive} | Left: Touch={isLeftHandTouching}, Grab={isLeftHandGrabbing}, Active={leftHandActive}");
        }

        // アクティブな手を判定
        if (rightHandActive && leftHandActive)
        {
            return HandType.BothHands;
        }
        else if (rightHandActive)
        {
            return HandType.RightHand;
        }
        else if (leftHandActive)
        {
            return HandType.LeftHand;
        }
        else
        {
            // 接触はしているが掴んでいない場合は、接触している手を返す
            if (isRightHandTouching && isLeftHandTouching)
            {
                return HandType.BothHands;
            }
            else if (isRightHandTouching)
            {
                return HandType.RightHand;
            }
            else if (isLeftHandTouching)
            {
                return HandType.LeftHand;
            }
        }

        return HandType.None;
    }

    /// <summary>
    /// 現在アクティブな手の情報を取得（デバッグ用）
    /// </summary>
    /// <returns>アクティブな手の情報文字列</returns>
    public string GetActiveHandInfo()
    {
        HandType activeHand = DetermineActiveHand();
        string startPointUsed = "None";

        if (useCustomStartPoint)
        {
            switch (activeHand)
            {
                case HandType.LeftHand:
                    startPointUsed = leftHandStartPointObject != null ? leftHandStartPointObject.name : "Missing";
                    break;
                case HandType.RightHand:
                    startPointUsed = rightHandStartPointObject != null ? rightHandStartPointObject.name : "Missing";
                    break;
                case HandType.BothHands:
                    startPointUsed = "Both hands active";
                    break;
            }
        }
        else
        {
            startPointUsed = "Hand_ForearmStub";
        }

        return $"Active Hand: {activeHand}, Start Point Used: {startPointUsed}";
    }

    /// <summary>
    /// StartPoint設定の状態をデバッグ出力
    /// </summary>
    [ContextMenu("Debug Start Point Settings")]
    public void DebugStartPointSettings()
    {
        Debug.Log("=== ProgressSet Start Point Debug ===");
        Debug.Log($"Use Custom Start Point: {useCustomStartPoint}");
        Debug.Log($"Left Hand Start Point: {(leftHandStartPointObject != null ? leftHandStartPointObject.name : "Not Assigned")}");
        Debug.Log($"Right Hand Start Point: {(rightHandStartPointObject != null ? rightHandStartPointObject.name : "Not Assigned")}");
        Debug.Log($"GrabJudge: {(grabJudge != null ? "Assigned" : "Not Assigned")}");
        Debug.Log($"{GetActiveHandInfo()}");
        Debug.Log($"Current Progress Start: {progressStart}");
        Debug.Log($"Current Progress End: {progressEnd}");
    }

    #endregion
}