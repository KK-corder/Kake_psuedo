using UnityEngine;

public class ContactProgressController : MonoBehaviour
{
    [Header("References")]
    // BoneJudgeNew.cs の参照（接触判定用）
    public BoneJudgeNew bonejudgeNew;
    public ProgressSet progressSet;
    public TaskManage taskmanage;
    public OVRSkeleton handModel;

    [Header("Material Settings")]
    // handtransition.shader を使用しているマテリアルの参照
    public Material handTransitionMaterial;

    [Header("Progress Settings")]
    // progress の基礎増加率（Inspectorから調整可能）
    [SerializeField] public float[] progressIncreaseRate = new float[3]; // publicに変更

    [Header("Contact Stability")]
    [SerializeField] private bool enableContactFiltering = true; // 接触フィルタリングを有効にする
    [SerializeField] private float minContactDuration = 0.1f; // 最小接触継続時間（秒）
    
    // 接触継続時間の追跡
    private float[] contactDuration;

    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = false;

    // 内部で接触時間および現在の progress を保持
    private float[] contactTime;
    private float[] currentProgress;

    // 接触開始時の手モデルの座標（各キューブごと）
    private Vector3[] contactStartPosition;
    // 接触状態の追跡（各キューブごと）
    private bool[] wasContactingLastFrame;

    // 最大配列サイズの制限（StackOverflow防止）
    private const int MAX_ARRAY_SIZE = 10;




    void Start()
    {
        // Basic validation and debug
        if (bonejudgeNew == null)
        {
            Debug.LogError("ContactProgressController: bonejudgeNew is not assigned in the Inspector.");
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
            }

            if (enableDebugLogs)
            {
                Debug.Log($"ContactProgressController: Initialized with {cubeCount} cubes");
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

        // bonejudgeNew の isTouching 配列のうち、いずれかが true なら接触中とする
        for (int i = 0; i < Mathf.Min(bonejudgeNew.isTouching.Length, cubeCount); i++)
        {
            if (bonejudgeNew.isTouching[i])
            {
                contactActive = true;
                break;
            }
        }

        if (contactActive)
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
                        wasContactingLastFrame[i] = true;
                        
                        if (enableDebugLogs)
                        {
                            Debug.Log($"Stable contact started for cube[{i}] at position: {contactStartPosition[i]} after {contactDuration[i]:F3}s");
                        }
                    }

                    contactTime[i] += Time.deltaTime;

                    // 現在の手の座標を取得
                    Vector3 currentHandPosition = GetHandPosition();
                    
                    // 接触開始時からのY軸方向の変位のみを計算
                    float yDisplacement = currentHandPosition.y - contactStartPosition[i].y;

                    // protect against progressIncreaseRate missing or too short
                    float pRate = 0.5f;
                    if (progressIncreaseRate != null && i < progressIncreaseRate.Length)
                        pRate = progressIncreaseRate[i];

                    float oldProgress = currentProgress[i];
                    
                    // Y軸方向の変位に基づいてprogressを直接計算（Time.deltaTimeは不要）
                    // 変位量に基づいた進行度計算
                    float progressChange = pRate * yDisplacement;
                    
                    if (yDisplacement > 0)
                    {
                        // Y軸正方向への移動：currentProgress を減少
                        currentProgress[i] = 1.0f - progressChange;
                    }
                    else if (yDisplacement < 0)
                    {
                        // Y軸負方向への移動：currentProgress を増加（上限は1.0）
                        currentProgress[i] = 1.0f - progressChange; // 負の変位でも同じ計算
                    }
                    else
                    {
                        // 変位がない場合は初期値
                        currentProgress[i] = 1.0f;
                    }
                    
                    // デバッグ：変化量を確認
                    if (enableDebugLogs && Mathf.Abs(oldProgress - currentProgress[i]) > 0.0001f)
                    {
                        Debug.Log($"Progress changed for cube[{i}]: {oldProgress:F6} -> {currentProgress[i]:F6}, Y displacement: {yDisplacement:F6}, Progress change: {progressChange:F6}");
                    }

                    if (enableDebugLogs)
                        Debug.Log($"ContactProgressController: cube[{i}] touching, pRate={pRate:F3}, currentProgress={currentProgress[i]:F4}, Y displacement={yDisplacement:F4}");
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
                        if (progressIncreaseRate != null && i < progressIncreaseRate.Length)
                        {
                            Debug.Log($"Cube {i}: Shader _Progress = {progress:F6}, Current Progress = {currentProgress[i]:F6}, pRate={progressIncreaseRate[i]:F6}");
                        }
                        else
                        {
                            Debug.Log($"Cube {i}: Shader _Progress = {progress:F6}, Current Progress = {currentProgress[i]:F6}, pRate=default(0.5)");
                        }
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
        }
    }

    private bool logShouldShow()
    {
        // enable some logs only in development/editor to avoid spam
        return true;
    }
}
