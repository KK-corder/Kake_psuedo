using UnityEngine;

public class ContactProgressController : MonoBehaviour
{
    [Header("References")]
    // BoneJudgeNew.cs の参照（接触判定用）
    public BoneJudgeNew bonejudgeNew;
    public GrabJudge grabJudge; // 握り判定用
    public ProgressSet progressSet;
    public TaskManage taskmanage;
    public OVRSkeleton handModel;

    [Header("Material Settings")]
    // handtransition.shader を使用しているマテリアルの参照
    public Material handTransitionMaterial;

    [Header("Progress Settings")]
    // progress の基礎増加率（Inspectorから調整可能）
    [SerializeField] public float[] progressIncreaseRate = new float[3]; // publicに変更
    
    [Header("Displacement Sensitivity")]
    // 手の変位量に対する係数（感度調整）
    [Range(0.1f, 50.0f)]
    public float displacementMultiplier = 20.0f; // 変位量に乗算する係数（Y軸用に高感度）
    
    [Header("Advanced Sensitivity")]
    // Y軸専用の高感度設定（通常の感度では不十分な場合）
    [Range(1.0f, 100.0f)]
    public float yAxisBoostMultiplier = 1.0f; // Y軸のみに追加で適用される係数
    
    // 使用する軸の選択
    public enum DisplacementAxis { Y_Axis, Z_Axis, X_Axis }
    [Header("Axis Selection")]
    public DisplacementAxis useAxis = DisplacementAxis.Y_Axis;
    
    [Header("Activation Conditions")]
    // Progress発動条件の設定
    public bool requireGrabAndContact = true; // 握り+接触の両方を必要とする
    public bool requireContactOnly = false; // 接触のみで発動（従来動作）

    [Header("Contact Stability")]
    [SerializeField] private bool enableContactFiltering = true; // 接触フィルタリングを有効にする
    [SerializeField] private float minContactDuration = 0.1f; // 最小接触継続時間（秒）
    
    // 接触継続時間の追跡
    private float[] contactDuration;

    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = true;

    // 内部で接触時間および現在の progress を保持
    private float[] contactTime;
    private float[] currentProgress;

    // 接触開始時の手モデルの座標（各キューブごと）
    private Vector3[] contactStartPosition;
    // 接触状態の追跡（各キューブごと）
    private bool[] wasContactingLastFrame;
    // 前フレームの手の座標（速度計算用）
    private Vector3[] previousHandPosition;

    // 最大配列サイズの制限（StackOverflow防止）
    private const int MAX_ARRAY_SIZE = 10;




    void Start()
    {
        // Basic validation and debug
        if (bonejudgeNew == null)
        {
            Debug.LogError("ContactProgressController: bonejudgeNew is not assigned in the Inspector.");
        }
        
        if (requireGrabAndContact && grabJudge == null)
        {
            Debug.LogError("ContactProgressController: grabJudge is not assigned but requireGrabAndContact is enabled.");
            return;
        }

        // 安全な配列サイズの決定
        int cubeCount = 0;
        if (bonejudgeNew.cubes != null)
        {
            cubeCount = Mathf.Min(bonejudgeNew.cubes.Length, MAX_ARRAY_SIZE);
        }
        
        if (cubeCount == 0)
        {
            cubeCount = 3; // デフォルト値
            Debug.LogWarning("ContactProgressController: No cubes found, using default count of 3");
        }

        // 配列の安全な初期化
        try
        {
            contactTime = new float[cubeCount];
            currentProgress = new float[cubeCount];
            contactStartPosition = new Vector3[cubeCount];
            wasContactingLastFrame = new bool[cubeCount];
            contactDuration = new float[cubeCount]; // 接触継続時間の初期化
            previousHandPosition = new Vector3[cubeCount]; // 前フレーム座標の初期化

            // progressIncreaseRateのサイズ調整
            if (progressIncreaseRate == null || progressIncreaseRate.Length != cubeCount)
            {
                System.Array.Resize(ref progressIncreaseRate, cubeCount);
                for (int i = 0; i < cubeCount; i++)
                {
                    if (progressIncreaseRate[i] == 0f)
                        progressIncreaseRate[i] = 0.5f; // デフォルト値
                }
            }

            for (int i = 0; i < cubeCount; i++)
            {
                contactTime[i] = 0f;
                currentProgress[i] = 1.0f;
                contactStartPosition[i] = Vector3.zero;
                wasContactingLastFrame[i] = false;
                contactDuration[i] = 0f; // 接触継続時間の初期化
                previousHandPosition[i] = Vector3.zero; // 前フレーム座標の初期化
            }

            if (enableDebugLogs)
            {
                Debug.Log($"ContactProgressController: Initialized with {cubeCount} cubes");
                Debug.Log($"Initial progressIncreaseRate: [{string.Join(", ", progressIncreaseRate)}]");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"ContactProgressController: Error during initialization: {e.Message}");
        }
    }

    void Update()
    {
        // 必要なコンポーネントのnullチェック
        if (bonejudgeNew == null || bonejudgeNew.isTouching == null || 
            contactTime == null || currentProgress == null)
        {
            return;
        }

        // 配列サイズの整合性チェック
        if (bonejudgeNew.cubes == null || bonejudgeNew.cubes.Length == 0)
        {
            return;
        }

        int cubeCount = Mathf.Min(bonejudgeNew.cubes.Length, contactTime.Length);
        cubeCount = Mathf.Min(cubeCount, MAX_ARRAY_SIZE);

        bool contactActive = false;
        bool grabActive = false;

        // 接触とそれに対応する握り状態をチェック
        // 右手が触れているときは右手の握り判定、左手が触れているときは左手の握り判定のみを使用
        for (int i = 0; i < Mathf.Min(bonejudgeNew.isTouching.Length, cubeCount); i++)
        {
            if (bonejudgeNew.isTouching[i])
            {
                contactActive = true;
                
                // 接触している手に応じた握り判定を行う
                if (grabJudge != null)
                {
                    bool isRightHandTouching = bonejudgeNew.IsRightHandTouching(i);
                    bool isLeftHandTouching = bonejudgeNew.IsLeftHandTouching(i);
                    
                    if (isRightHandTouching)
                    {
                        // 右手が接触している場合は右手の握り判定のみ
                        grabActive = grabJudge.IsHandGrabbing(true); // true = 右手
                        
                        if (enableDebugLogs)
                        {
                            Debug.Log($"Right hand touching cube[{i}] - Right hand grabbing: {grabActive}");
                        }
                    }
                    else if (isLeftHandTouching)
                    {
                        // 左手が接触している場合は左手の握り判定のみ
                        grabActive = grabJudge.IsHandGrabbing(false); // false = 左手
                        
                        if (enableDebugLogs)
                        {
                            Debug.Log($"Left hand touching cube[{i}] - Left hand grabbing: {grabActive}");
                        }
                    }
                    else
                    {
                        // どちらの手か不明な場合はfalse
                        grabActive = false;
                        
                        if (enableDebugLogs)
                        {
                            Debug.Log($"Unknown hand touching cube[{i}] - grabbing: {grabActive}");
                        }
                    }
                }
                
                break; // 最初に接触しているCubeで判定を確定
            }
        }

        // 発動条件をチェック
        bool shouldActivateProgress = CheckActivationConditions(contactActive, grabActive);
        
        // デバッグ: 発動条件の詳細をログ出力
        if (enableDebugLogs)
        {
            Debug.Log($"=== Frame Update Debug ===");
            Debug.Log($"Contact Active: {contactActive}, Grab Active: {grabActive}, Should Activate: {shouldActivateProgress}");
            Debug.Log($"Activation Mode - RequireGrabAndContact: {requireGrabAndContact}, RequireContactOnly: {requireContactOnly}");
        }

        if (shouldActivateProgress)
        {
            // 接触しているCubeに基づいてcurrentProgressを更新
            for (int i = 0; i < cubeCount; i++)
            {
                // 配列境界チェック
                if (i >= bonejudgeNew.isTouching.Length || i >= contactTime.Length)
                    break;

                if (bonejudgeNew.isTouching[i])
                {
                    // 接触継続時間を更新
                    contactDuration[i] += Time.deltaTime;
                    
                    // 接触フィルタリング：最小継続時間をチェック
                    bool isStableContact = !enableContactFiltering || contactDuration[i] >= minContactDuration;
                    
                    if (!isStableContact)
                    {
                        // まだ安定していない接触はスキップ
                        if (enableDebugLogs)
                        {
                            Debug.Log($"Contact for cube[{i}] not yet stable: duration={contactDuration[i]:F3}s, required={minContactDuration:F3}s");
                        }
                        continue;
                    }

                    // 接触開始時の座標を記録
                    if (!wasContactingLastFrame[i])
                    {
                        Vector3 handPos = GetHandPosition();
                        contactStartPosition[i] = handPos;
                        previousHandPosition[i] = handPos; // 前フレーム座標も初期化
                        wasContactingLastFrame[i] = true;
                        
                        if (enableDebugLogs)
                        {
                            Debug.Log($"Stable contact started for cube[{i}] at position: {contactStartPosition[i]} after {contactDuration[i]:F3}s");
                        }
                    }

                    contactTime[i] += Time.deltaTime;

                    // 現在の手の座標を取得
                    Vector3 currentHandPosition = GetHandPosition();
                    
                    // デバッグ: 全軸の変化量を確認
                    if (enableDebugLogs && previousHandPosition[i] != Vector3.zero)
                    {
                        float xChange = currentHandPosition.x - previousHandPosition[i].x;
                        float yChange = currentHandPosition.y - previousHandPosition[i].y;
                        float zChange = currentHandPosition.z - previousHandPosition[i].z;
                        Debug.Log($"Hand Movement - X: {xChange:F6}, Y: {yChange:F6}, Z: {zChange:F6}");
                        Debug.Log($"Current Position: {currentHandPosition}, Previous: {previousHandPosition[i]}");
                    }
                    
                    // 接触開始位置からの絶対変位を計算（絶対位置ベース）
                    float absoluteDisplacement = 0f;
                    switch (useAxis)
                    {
                        case DisplacementAxis.Y_Axis:
                            absoluteDisplacement = currentHandPosition.y - contactStartPosition[i].y;
                            break;
                        case DisplacementAxis.Z_Axis:
                            absoluteDisplacement = currentHandPosition.z - contactStartPosition[i].z;
                            break;
                        case DisplacementAxis.X_Axis:
                            absoluteDisplacement = currentHandPosition.x - contactStartPosition[i].x;
                            break;
                    }
                    
                    // デバッグ: 絶対変位の詳細ログ
                    if (enableDebugLogs)
                    {
                        Debug.Log($"=== Absolute Displacement Debug for Cube[{i}] ===");
                        Debug.Log($"Contact Start Position: {contactStartPosition[i]}");
                        Debug.Log($"Current Hand Position: {currentHandPosition}");
                        Debug.Log($"Selected Axis: {useAxis}, Absolute Displacement: {absoluteDisplacement:F6}");
                        Debug.Log($"Displacement Multiplier: {displacementMultiplier}, Y Boost Multiplier: {yAxisBoostMultiplier}");
                    }
                    
                    // 変位量に係数を適用
                    float adjustedDisplacement = absoluteDisplacement * displacementMultiplier;
                    
                    // Y軸の場合は追加ブーストを適用
                    if (useAxis == DisplacementAxis.Y_Axis)
                    {
                        adjustedDisplacement *= yAxisBoostMultiplier;
                        if (enableDebugLogs)
                        {
                            Debug.Log($"Y-Axis boost applied: {adjustedDisplacement:F6}");
                        }
                    }
                    
                    // progressIncreaseRate を取得（変化率として使用）
                    float pRate = 0.5f;
                    if (progressIncreaseRate != null && i < progressIncreaseRate.Length)
                        pRate = progressIncreaseRate[i];

                    float oldProgress = currentProgress[i];
                    
                    // 絶対変位に基づいてProgress値を直接設定
                    // Y座標増加（正の変位） → progress減少、Y座標減少（負の変位） → progress増加
                    currentProgress[i] = 1.0f - (adjustedDisplacement * pRate);
                    
                    // currentProgressを0.0-1.0の範囲にクランプ
                    currentProgress[i] = Mathf.Clamp01(currentProgress[i]);
                    
                    // デバッグ: Progress計算の詳細ログ
                    if (enableDebugLogs)
                    {
                        Debug.Log($"=== Absolute Position Progress Calculation Debug for Cube[{i}] ===");
                        Debug.Log($"Progress Rate: {pRate:F3}, Adjusted Displacement: {adjustedDisplacement:F6}");
                        Debug.Log($"Progress Formula: 1.0 - ({adjustedDisplacement:F6} * {pRate:F3}) = {(1.0f - (adjustedDisplacement * pRate)):F6}");
                        Debug.Log($"Old Progress: {oldProgress:F6}");
                        Debug.Log($"New Progress (after clamp): {currentProgress[i]:F6}");
                        Debug.Log($"Progress Change: {oldProgress:F6} -> {currentProgress[i]:F6} (Delta: {(currentProgress[i] - oldProgress):F6})");
                    }
                    
                    // デバッグ：変化量を確認
                    if (enableDebugLogs)
                    {
                        if (Mathf.Abs(oldProgress - currentProgress[i]) > 0.0001f)
                        {
                            Debug.Log($"*** PROGRESS CHANGED *** for cube[{i}]: {oldProgress:F6} -> {currentProgress[i]:F6}, Absolute {useAxis} displacement: {absoluteDisplacement:F6}, Adjusted displacement: {adjustedDisplacement:F6}");
                        }
                        else
                        {
                            Debug.Log($"*** NO PROGRESS CHANGE *** for cube[{i}]: Progress remains {currentProgress[i]:F6}, Absolute {useAxis} displacement: {absoluteDisplacement:F6} (too small or zero)");
                        }
                    }

                    if (enableDebugLogs)
                    {
                        string statusInfo = GetActivationStatus();
                        Debug.Log($"ContactProgressController: cube[{i}] touching, pRate={pRate:F3}, currentProgress={currentProgress[i]:F4}, Absolute {useAxis} displacement={absoluteDisplacement:F6}, displacementMultiplier={displacementMultiplier:F2}");
                        Debug.Log($"Activation Status: {statusInfo}");
                    }
                }
                else
                {
                    // 接触が終了した場合
                    if (i < wasContactingLastFrame.Length && wasContactingLastFrame[i])
                    {
                        wasContactingLastFrame[i] = false;
                        if (enableDebugLogs)
                        {
                            Debug.Log($"Contact ended for cube[{i}] after {contactDuration[i]:F3}s");
                        }
                    }
                    
                    // 接触継続時間をリセット
                    contactDuration[i] = 0f;
                    // 前フレーム座標もリセット
                    if (i < previousHandPosition.Length)
                        previousHandPosition[i] = Vector3.zero;
                }
            }
        }
        else
        {
            for (int i = 0; i < cubeCount; i++)
            {
                // 配列境界チェック
                if (i >= contactTime.Length || i >= currentProgress.Length || i >= wasContactingLastFrame.Length)
                    break;

                // 接触がなくなった場合はリセット
                contactTime[i] = 0f;
                currentProgress[i] = 1.0f; // リセット時も1.0に変更
                wasContactingLastFrame[i] = false;
                contactDuration[i] = 0f; // 接触継続時間もリセット
                if (i < previousHandPosition.Length)
                    previousHandPosition[i] = Vector3.zero; // 前フレーム座標もリセット

                // デバッグログでリセットを確認
                if (enableDebugLogs)
                {
                    Debug.Log($"Cube {i}: Reset currentProgress to {currentProgress[i]}");
                }
            }

            // シェーダーの _Progress プロパティを初期値に戻す
            if (handTransitionMaterial != null)
            {
                handTransitionMaterial.SetFloat("_Progress", 1.0f); // シェーダーも1.0に変更
                if (enableDebugLogs)
                {
                    Debug.Log("Reset _Progress to 1.0f");
                }
            }
        }

        for (int i = 0; i < cubeCount; i++)
        {
            // 配列境界チェック
            if (i >= currentProgress.Length || i >= bonejudgeNew.isTouching.Length)
                break;

            // currentProgress を 0～1.0 の範囲にクランプ
            currentProgress[i] = Mathf.Clamp(currentProgress[i], 0f, 1.0f);

            if (bonejudgeNew.isTouching[i])
            {
                // シェーダーの _Progress プロパティを更新
                if (handTransitionMaterial != null)
                {
                    handTransitionMaterial.SetFloat("_Progress", currentProgress[i]);

                    // デバッグログで _Progress の値を確認
                    if (enableDebugLogs)
                    {
                        float progress = handTransitionMaterial.GetFloat("_Progress");
                        Debug.Log($"=== Shader Update Debug for Cube[{i}] ===");
                        Debug.Log($"Shader _Progress set to: {progress:F6}, Current Progress: {currentProgress[i]:F6}");
                        if (progressIncreaseRate != null && i < progressIncreaseRate.Length)
                        {
                            Debug.Log($"Progress Rate: {progressIncreaseRate[i]:F6}");
                        }
                        else
                        {
                            Debug.Log($"Progress Rate: default(0.5)");
                        }
                    }
                }
                else
                {
                    if (enableDebugLogs)
                    {
                        Debug.LogWarning($"handTransitionMaterial is null! Cannot update shader for cube[{i}]");
                    }
                }
            }
        }
    }


    // bonejudgeNew の progressStart の z 座標を取得する関数
    private float GetHandZ()
    {
        if (bonejudgeNew != null)
        {
            // bonejudgeNewのprogressStartを使用（より一貫性がある）
            return progressSet.progressStart.z;
        }
        
        // フォールバック: 従来のOVRSkeletonからの取得
        Vector3 handPos = Vector3.zero;
        if (handModel == null || handModel.Bones == null)
            return 0f;

        foreach (var bone in handModel.Bones)
        {
            if (bone.Transform != null && bone.Transform.name == "Hand_ForearmStub")
            {
                handPos = bone.Transform.position;
                break;
            }
        }
        return handPos.z;
    }

    // 手の3D座標を取得する関数（progress計算ではY軸のみ使用）
    private Vector3 GetHandPosition()
    {
        if (progressSet != null)
        {
            // progressSetのprogressStartを使用（より一貫性がある）
            return progressSet.progressStart;
        }
        
        // フォールバック: 従来のOVRSkeletonからの取得
        Vector3 handPos = Vector3.zero;
        if (handModel == null || handModel.Bones == null)
            return Vector3.zero;

        foreach (var bone in handModel.Bones)
        {
            if (bone.Transform != null && bone.Transform.name == "Hand_ForearmStub")
            {
                handPos = bone.Transform.position;
                break;
            }
        }
        return handPos;
    }

    /// <summary>
    /// 指定されたインデックスのprogressIncreaseRateを安全に取得
    /// </summary>
    public float GetProgressIncreaseRate(int index)
    {
        if (progressIncreaseRate == null || index < 0 || index >= progressIncreaseRate.Length)
        {
            return 0.5f; // デフォルト値
        }
        return progressIncreaseRate[index];
    }

    /// <summary>
    /// 指定されたインデックスのprogressIncreaseRateを安全に設定
    /// </summary>
    public void SetProgressIncreaseRate(int index, float rate)
    {
        if (progressIncreaseRate == null)
        {
            progressIncreaseRate = new float[MAX_ARRAY_SIZE];
        }
        
        if (index >= 0 && index < progressIncreaseRate.Length)
        {
            progressIncreaseRate[index] = rate;
            
            if (enableDebugLogs)
            {
                Debug.Log($"ContactProgressController: Set progressIncreaseRate[{index}] = {rate}");
            }
        }
        else
        {
            Debug.LogWarning($"ContactProgressController: Invalid index {index} for progressIncreaseRate");
        }
    }

    /// <summary>
    /// 全てのprogressIncreaseRateを設定
    /// </summary>
    public void SetAllProgressIncreaseRates(float[] rates)
    {
        if (rates == null)
        {
            Debug.LogWarning("ContactProgressController: Cannot set null rates array");
            return;
        }

        int copyLength = Mathf.Min(rates.Length, MAX_ARRAY_SIZE);
        
        if (progressIncreaseRate == null || progressIncreaseRate.Length != copyLength)
        {
            progressIncreaseRate = new float[copyLength];
        }

        for (int i = 0; i < copyLength; i++)
        {
            progressIncreaseRate[i] = rates[i];
        }

        if (enableDebugLogs)
        {
            Debug.Log($"ContactProgressController: Set {copyLength} progressIncreaseRates");
            Debug.Log($"Updated progressIncreaseRate: [{string.Join(", ", progressIncreaseRate)}]");
        }
    }

    private bool logShouldShow()
    {
        // enable some logs only in development/editor to avoid spam
        return true;
    }

    /// <summary>
    /// Progress発動条件をチェック
    /// </summary>
    /// <param name="isContacting">接触している</param>
    /// <param name="isGrabbing">握っている</param>
    /// <returns>Progress処理を実行すべき場合true</returns>
    private bool CheckActivationConditions(bool isContacting, bool isGrabbing)
    {
        if (requireGrabAndContact)
        {
            // 握り+接触の両方が必要
            bool shouldActivate = isContacting && isGrabbing;
            
            if (enableDebugLogs)
            {
                Debug.Log($"ContactProgressController: RequireGrabAndContact mode - Contact: {isContacting}, Grab: {isGrabbing}, Activate: {shouldActivate}");
            }
            
            return shouldActivate;
        }
        else if (requireContactOnly)
        {
            // 接触のみで発動（従来動作）
            if (enableDebugLogs)
            {
                Debug.Log($"ContactProgressController: ContactOnly mode - Contact: {isContacting}, Activate: {isContacting}");
            }
            
            return isContacting;
        }
        else
        {
            // デフォルト: 握り+接触の両方が必要
            bool shouldActivate = isContacting && isGrabbing;
            
            if (enableDebugLogs)
            {
                Debug.Log($"ContactProgressController: Default mode (GrabAndContact) - Contact: {isContacting}, Grab: {isGrabbing}, Activate: {shouldActivate}");
            }
            
            return shouldActivate;
        }
    }

    /// <summary>
    /// 現在のProgress発動状態を取得
    /// </summary>
    /// <returns>発動状態の情報文字列</returns>
    public string GetActivationStatus()
    {
        if (bonejudgeNew == null)
            return "BoneJudgeNew not assigned";
        
        if (requireGrabAndContact && grabJudge == null)
            return "GrabJudge not assigned but required";

        bool contactActive = false;
        bool grabActive = false;

        // 接触状態をチェック
        if (bonejudgeNew.isTouching != null)
        {
            for (int i = 0; i < bonejudgeNew.isTouching.Length; i++)
            {
                if (bonejudgeNew.isTouching[i])
                {
                    contactActive = true;
                    break;
                }
            }
        }

        // 握り状態をチェック
        if (grabJudge != null)
        {
            grabActive = grabJudge.IsAnyHandGrabbing();
        }

        bool shouldActivate = CheckActivationConditions(contactActive, grabActive);
        
        string mode = requireGrabAndContact ? "GrabAndContact" : (requireContactOnly ? "ContactOnly" : "Default(GrabAndContact)");
        
        return $"Mode: {mode}, Contact: {contactActive}, Grab: {grabActive}, Progress Active: {shouldActivate}";
    }
}
