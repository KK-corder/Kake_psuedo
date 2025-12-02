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
    // 接触開始位置が設定済みかのフラグ（累積変位計算のため）
    private bool[] contactStartPositionSet;
    // 接触状態の追跡（各キューブごと）
    private bool[] wasContactingLastFrame;
    // 前フレームの手の座標（速度計算用）
    private Vector3[] previousHandPosition;

    // 最大配列サイズの制限（StackOverflow防止）
    private const int MAX_ARRAY_SIZE = 10;
    
    [Header("Stability Control")]
    [SerializeField] private bool enableStabilityChecks = true; // 安定性チェックを有効にする
    [SerializeField] private bool enableErrorRecovery = true; // エラー回復機能を有効にする
    [SerializeField] private float progressSmoothingFactor = 0.1f; // Progress値のスムージング係数
    [SerializeField] private int maxProgressUpdatesPerFrame = 3; // フレーム当たりの最大Progress更新数
    
    // 安定性管理用の内部変数
    private bool isInitialized = false;
    private float[] lastValidProgress; // 最後の有効なProgress値
    private int[] progressUpdateCount; // フレーム当たりのProgress更新回数
    private float lastUpdateTime = 0f;




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
            contactStartPositionSet = new bool[cubeCount];
            wasContactingLastFrame = new bool[cubeCount];
            contactDuration = new float[cubeCount]; // 接触継続時間の初期化
            previousHandPosition = new Vector3[cubeCount]; // 前フレーム座標の初期化
            
            // 安定性管理用配列の初期化
            lastValidProgress = new float[cubeCount];
            progressUpdateCount = new int[cubeCount];

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
                contactStartPositionSet[i] = false;
                wasContactingLastFrame[i] = false;
                contactDuration[i] = 0f; // 接触継続時間の初期化
                previousHandPosition[i] = Vector3.zero; // 前フレーム座標の初期化
                
                // 安定性管理用の初期化
                lastValidProgress[i] = 1.0f;
                progressUpdateCount[i] = 0;
            }
            
            isInitialized = true;
            lastUpdateTime = Time.time;

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
        // 包括的な安定性チェック
        if (!ValidateSystemState())
        {
            if (enableErrorRecovery)
            {
                AttemptErrorRecovery();
            }
            return;
        }
        
        // フレーム制限チェック
        if (enableStabilityChecks && Time.time - lastUpdateTime < 0.016f) // ~60FPS制限
        {
            return;
        }
        
        // Progress更新カウンターをリセット
        if (progressUpdateCount != null)
        {
            for (int i = 0; i < progressUpdateCount.Length; i++)
            {
                progressUpdateCount[i] = 0;
            }
        }
        
        lastUpdateTime = Time.time;

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

                    // 接触開始時の座標を記録（累積変位計算のため一度だけ設定）
                    if (!contactStartPositionSet[i])
                    {
                        Vector3 handPos = GetHandPosition();
                        contactStartPosition[i] = handPos;
                        contactStartPositionSet[i] = true;
                        previousHandPosition[i] = handPos; // 前フレーム座標も初期化
                        
                        if (enableDebugLogs)
                        {
                            Debug.Log($"Initial contact start position set for cube[{i}] at position: {contactStartPosition[i]} (cumulative displacement base)");
                        }
                    }
                    
                    if (!wasContactingLastFrame[i])
                    {
                        wasContactingLastFrame[i] = true;
                        
                        if (enableDebugLogs)
                        {
                            Debug.Log($"Stable contact resumed for cube[{i}] after {contactDuration[i]:F3}s (using existing start position: {contactStartPosition[i]})");
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
                    float newProgress = 1.0f - (adjustedDisplacement * pRate);
                    
                    // Progress値の安定化処理
                    newProgress = ValidateAndStabilizeProgress(i, newProgress, oldProgress);
                    
                    // 更新制限チェック
                    if (enableStabilityChecks && progressUpdateCount[i] >= maxProgressUpdatesPerFrame)
                    {
                        if (enableDebugLogs)
                        {
                            Debug.Log($"Progress update limit reached for cube[{i}] this frame");
                        }
                        continue;
                    }
                    
                    currentProgress[i] = newProgress;
                    progressUpdateCount[i]++;
                    
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
                
                // 累積変位計算のベース位置もリセット（重要！）
                if (i < contactStartPositionSet.Length)
                {
                    contactStartPositionSet[i] = false;
                    if (enableDebugLogs)
                    {
                        Debug.Log($"Cube {i}: Reset contact start position flag for fresh cumulative displacement calculation");
                    }
                }

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
                // シェーダーの _Progress プロパティを安全に更新
                bool shaderUpdateSuccess = SafeUpdateShader(i, currentProgress[i]);
                
                if (!shaderUpdateSuccess && enableErrorRecovery)
                {
                    // シェーダー更新失敗時の回復処理
                    if (enableDebugLogs)
                    {
                        Debug.LogWarning($"Shader update failed for cube[{i}], attempting recovery");
                    }
                    
                    // 最後の有効値で再試行
                    SafeUpdateShader(i, lastValidProgress[i]);
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

    /// <summary>
    /// 累積変位計算をリセット（新しいセッション開始時に呼び出し）
    /// </summary>
    public void ResetCumulativeDisplacement()
    {
        if (contactStartPositionSet != null)
        {
            for (int i = 0; i < contactStartPositionSet.Length; i++)
            {
                contactStartPositionSet[i] = false;
                contactStartPosition[i] = Vector3.zero;
                currentProgress[i] = 1.0f;
            }
        }

        if (enableDebugLogs)
        {
            Debug.Log("ContactProgressController: Cumulative displacement calculation reset - fresh start positions will be recorded");
        }
    }

    /// <summary>
    /// 特定のCubeの累積変位計算をリセット
    /// </summary>
    public void ResetCumulativeDisplacement(int cubeIndex)
    {
        if (contactStartPositionSet != null && cubeIndex >= 0 && cubeIndex < contactStartPositionSet.Length)
        {
            contactStartPositionSet[cubeIndex] = false;
            contactStartPosition[cubeIndex] = Vector3.zero;
            currentProgress[cubeIndex] = 1.0f;

            if (enableDebugLogs)
            {
                Debug.Log($"ContactProgressController: Cumulative displacement reset for cube[{cubeIndex}]");
            }
        }
    }

    #region Stability and Validation Methods

    /// <summary>
    /// システム状態の包括的な検証
    /// </summary>
    private bool ValidateSystemState()
    {
        // 基本的なnullチェック
        if (bonejudgeNew == null || bonejudgeNew.isTouching == null || 
            contactTime == null || currentProgress == null || !isInitialized)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning("ContactProgressController: Critical system state validation failed");
            }
            return false;
        }

        // 配列サイズの一貫性チェック
        int expectedSize = bonejudgeNew.cubes != null ? 
            Mathf.Min(bonejudgeNew.cubes.Length, MAX_ARRAY_SIZE) : 3;

        if (currentProgress.Length != expectedSize || 
            contactTime.Length != expectedSize ||
            (contactStartPosition != null && contactStartPosition.Length != expectedSize))
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning("ContactProgressController: Array size inconsistency detected");
            }
            return false;
        }

        // 握り判定の必要性チェック
        if (requireGrabAndContact && grabJudge == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning("ContactProgressController: GrabJudge required but not assigned");
            }
            return false;
        }

        return true;
    }

    /// <summary>
    /// Progress値の検証と安定化
    /// </summary>
    private float ValidateAndStabilizeProgress(int cubeIndex, float newProgress, float oldProgress)
    {
        if (cubeIndex < 0 || cubeIndex >= currentProgress.Length)
        {
            return 1.0f; // デフォルト値
        }

        // NaN や無限大の値をチェック
        if (float.IsNaN(newProgress) || float.IsInfinity(newProgress))
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"Invalid progress value detected for cube[{cubeIndex}]: {newProgress}, using last valid value");
            }
            return lastValidProgress[cubeIndex];
        }

        // 値を0-1の範囲にクランプ
        newProgress = Mathf.Clamp01(newProgress);

        // スムージング処理（急激な変化を抑制）
        if (enableStabilityChecks && Mathf.Abs(newProgress - oldProgress) > 0.5f)
        {
            if (progressSmoothingFactor > 0f)
            {
                newProgress = Mathf.Lerp(oldProgress, newProgress, progressSmoothingFactor);
                if (enableDebugLogs)
                {
                    Debug.Log($"Progress smoothing applied for cube[{cubeIndex}]: {oldProgress:F3} -> {newProgress:F3}");
                }
            }
        }

        // 有効値として記録
        lastValidProgress[cubeIndex] = newProgress;
        return newProgress;
    }

    /// <summary>
    /// 安全なシェーダー更新
    /// </summary>
    private bool SafeUpdateShader(int cubeIndex, float progressValue)
    {
        if (handTransitionMaterial == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning("handTransitionMaterial is null! Cannot update shader");
            }
            return false;
        }

        try
        {
            // 値の最終検証
            if (float.IsNaN(progressValue) || float.IsInfinity(progressValue))
            {
                progressValue = lastValidProgress[cubeIndex];
            }

            progressValue = Mathf.Clamp01(progressValue);
            handTransitionMaterial.SetFloat("_Progress", progressValue);

            // 設定値の確認
            if (enableDebugLogs)
            {
                float actualValue = handTransitionMaterial.GetFloat("_Progress");
                if (Mathf.Abs(actualValue - progressValue) > 0.001f)
                {
                    Debug.LogWarning($"Shader value mismatch for cube[{cubeIndex}]: Set {progressValue:F6}, Got {actualValue:F6}");
                }
                else
                {
                    Debug.Log($"Shader updated successfully for cube[{cubeIndex}]: _Progress = {actualValue:F6}");
                }
            }

            return true;
        }
        catch (System.Exception e)
        {
            if (enableDebugLogs)
            {
                Debug.LogError($"Shader update exception for cube[{cubeIndex}]: {e.Message}");
            }
            return false;
        }
    }

    /// <summary>
    /// エラー回復処理
    /// </summary>
    private void AttemptErrorRecovery()
    {
        if (enableDebugLogs)
        {
            Debug.Log("ContactProgressController: Attempting error recovery");
        }

        try
        {
            // 基本的なnullチェックと再初期化
            if (bonejudgeNew == null)
            {
                bonejudgeNew = FindObjectOfType<BoneJudgeNew>();
                if (bonejudgeNew == null)
                {
                    Debug.LogError("ContactProgressController: Could not find BoneJudgeNew component");
                    return;
                }
            }

            if (requireGrabAndContact && grabJudge == null)
            {
                grabJudge = FindObjectOfType<GrabJudge>();
                if (grabJudge == null)
                {
                    Debug.LogError("ContactProgressController: Could not find GrabJudge component");
                    return;
                }
            }

            if (handTransitionMaterial == null)
            {
                // マテリアルの再検索を試行
                Renderer[] renderers = FindObjectsOfType<Renderer>();
                foreach (var renderer in renderers)
                {
                    if (renderer.material != null && renderer.material.HasProperty("_Progress"))
                    {
                        handTransitionMaterial = renderer.material;
                        Debug.Log("ContactProgressController: Found material with _Progress property");
                        break;
                    }
                }
            }

            // 配列の再初期化
            if (currentProgress == null || contactTime == null)
            {
                Start(); // 再初期化
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"ContactProgressController: Error recovery failed: {e.Message}");
        }
    }

    /// <summary>
    /// システム状態のデバッグ出力
    /// </summary>
    public void DebugSystemState()
    {
        Debug.Log("=== ContactProgressController System State ===");
        Debug.Log($"Initialized: {isInitialized}");
        Debug.Log($"BoneJudgeNew: {(bonejudgeNew != null ? "OK" : "NULL")}");
        Debug.Log($"GrabJudge: {(grabJudge != null ? "OK" : "NULL")}");
        Debug.Log($"HandTransitionMaterial: {(handTransitionMaterial != null ? "OK" : "NULL")}");
        
        if (currentProgress != null)
        {
            Debug.Log($"Current Progress Values: [{string.Join(", ", System.Array.ConvertAll(currentProgress, x => x.ToString("F3")))}]");
        }
        
        if (contactStartPositionSet != null)
        {
            Debug.Log($"Contact Start Position Set: [{string.Join(", ", System.Array.ConvertAll(contactStartPositionSet, x => x.ToString()))}]");
        }
    }

    #endregion
}
