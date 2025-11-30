using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// WISS Demo用のCube管理システム
/// BoneJudge.csにアタッチしている3つのCubeオブジェクトを管理し、
/// それぞれに異なるprogressrateを割り当てます
/// </summary>
public class WISSdemo : MonoBehaviour
{
    [Header("References")]
    // BoneJudge関連の参照
    public BoneJudgeNew boneJudgeNew;
    public ContactProgressController contactProgressController;

    [Header("Cube Management")]
    // 3つのCubeオブジェクトの直接参照
    [SerializeField] private GameObject cube1;
    [SerializeField] private GameObject cube2;
    [SerializeField] private GameObject cube3;

    [Header("Progress Rate Settings")]
    // 各Cubeの個別progressrate設定（インスペクターで調整可能）
    public float cube1ProgressRate = 0.5f;
    
    public float cube2ProgressRate = 1.0f;
    
    public float cube3ProgressRate = 1.5f;

    [Header("Auto Assignment")]
    // 自動でBoneJudgeからCubeを取得するか
    public bool autoAssignFromBoneJudge = true;

    [Header("Demo Settings")]
    // デモ用の設定
    public bool enableProgressRateSync = true; // progressrateの同期を有効にする
    public bool showDebugInfo = true; // デバッグ情報を表示
    
    [Header("Cube Information Display")]
    // 各Cubeの状態表示（読み取り専用）
    [SerializeField] private string cube1Status = "Not Assigned";
    [SerializeField] private string cube2Status = "Not Assigned";
    [SerializeField] private string cube3Status = "Not Assigned";

    // 内部管理用
    private GameObject[] managedCubes = new GameObject[3];
    private float[] currentProgressRates = new float[3];
    private bool isInitialized = false;

    void Start()
    {
        InitializeCubeManagement();
    }

    void Update()
    {
        if (!isInitialized)
            return;

        // progressrateの変更を監視して同期
        if (enableProgressRateSync)
        {
            UpdateProgressRates();
        }

        // デバッグ情報の更新
        if (showDebugInfo)
        {
            UpdateDebugInfo();
        }
    }

    /// <summary>
    /// Cube管理システムの初期化
    /// </summary>
    private void InitializeCubeManagement()
    {
        try
        {
            // BoneJudgeNewの参照チェック
            if (boneJudgeNew == null)
            {
                Debug.LogError("WISSdemo: BoneJudgeNew reference is not assigned!");
                return;
            }

            // 自動割り当てが有効な場合、BoneJudgeからCubeを取得
            if (autoAssignFromBoneJudge)
            {
                AssignCubesFromBoneJudge();
            }
            else
            {
                // 手動で設定されたCubeを使用
                managedCubes[0] = cube1;
                managedCubes[1] = cube2;
                managedCubes[2] = cube3;
            }

            // 初期progressrateの設定
            currentProgressRates[0] = cube1ProgressRate;
            currentProgressRates[1] = cube2ProgressRate;
            currentProgressRates[2] = cube3ProgressRate;

            // ContactProgressControllerに初期値を適用
            ApplyProgressRatesToController();

            isInitialized = true;

            if (showDebugInfo)
            {
                Debug.Log("WISSdemo: Initialization completed successfully");
                LogCubeAssignments();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"WISSdemo: Error during initialization: {e.Message}");
        }
    }

    /// <summary>
    /// BoneJudgeNewからCubeオブジェクトを自動取得
    /// </summary>
    private void AssignCubesFromBoneJudge()
    {
        if (boneJudgeNew.cubes == null || boneJudgeNew.cubes.Length == 0)
        {
            Debug.LogWarning("WISSdemo: No cubes found in BoneJudgeNew!");
            return;
        }

        // BoneJudgeのcubes配列から最大3つまで取得
        for (int i = 0; i < Mathf.Min(3, boneJudgeNew.cubes.Length); i++)
        {
            managedCubes[i] = boneJudgeNew.cubes[i];
            
            // インスペクター表示用の参照も更新
            if (i == 0) cube1 = managedCubes[i];
            else if (i == 1) cube2 = managedCubes[i];
            else if (i == 2) cube3 = managedCubes[i];
        }

        if (showDebugInfo)
        {
            Debug.Log($"WISSdemo: Auto-assigned {Mathf.Min(3, boneJudgeNew.cubes.Length)} cubes from BoneJudgeNew");
        }
    }

    /// <summary>
    /// progressrateの変更を監視して同期
    /// </summary>
    private void UpdateProgressRates()
    {
        bool changed = false;

        // 各Cubeのprogressrateの変更をチェック
        if (currentProgressRates[0] != cube1ProgressRate)
        {
            currentProgressRates[0] = cube1ProgressRate;
            changed = true;
        }

        if (currentProgressRates[1] != cube2ProgressRate)
        {
            currentProgressRates[1] = cube2ProgressRate;
            changed = true;
        }

        if (currentProgressRates[2] != cube3ProgressRate)
        {
            currentProgressRates[2] = cube3ProgressRate;
            changed = true;
        }

        // 変更があった場合はContactProgressControllerに反映
        if (changed)
        {
            ApplyProgressRatesToController();
            
            if (showDebugInfo)
            {
                Debug.Log($"WISSdemo: ProgressRates updated - Cube1: {cube1ProgressRate:F2}, Cube2: {cube2ProgressRate:F2}, Cube3: {cube3ProgressRate:F2}");
            }
        }
    }

    /// <summary>
    /// progressrateをContactProgressControllerに適用
    /// </summary>
    private void ApplyProgressRatesToController()
    {
        if (contactProgressController == null)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("WISSdemo: ContactProgressController is not assigned, cannot apply progress rates");
            }
            return;
        }

        // ContactProgressControllerのSetAllProgressIncreaseRatesメソッドを呼び出し
        float[] rates = { cube1ProgressRate, cube2ProgressRate, cube3ProgressRate };
        contactProgressController.SetAllProgressIncreaseRates(rates);

        if (showDebugInfo)
        {
            Debug.Log($"WISSdemo: Applied progress rates to ContactProgressController");
        }
    }

    /// <summary>
    /// デバッグ情報の更新
    /// </summary>
    private void UpdateDebugInfo()
    {
        // 各Cubeの状態情報を更新
        cube1Status = GetCubeStatusString(0);
        cube2Status = GetCubeStatusString(1);
        cube3Status = GetCubeStatusString(2);
    }

    /// <summary>
    /// 指定されたインデックスのCubeの状態文字列を取得
    /// </summary>
    /// <param name="index">Cubeのインデックス</param>
    /// <returns>状態文字列</returns>
    private string GetCubeStatusString(int index)
    {
        if (index < 0 || index >= managedCubes.Length)
            return "Invalid Index";

        if (managedCubes[index] == null)
            return "Not Assigned";

        bool isContacting = false;
        if (boneJudgeNew != null && boneJudgeNew.isTouching != null && 
            index < boneJudgeNew.isTouching.Length)
        {
            isContacting = boneJudgeNew.isTouching[index];
        }

        return $"{managedCubes[index].name} - Rate: {currentProgressRates[index]:F2} - Contact: {isContacting}";
    }

    /// <summary>
    /// Cube割り当て情報をログに出力
    /// </summary>
    private void LogCubeAssignments()
    {
        for (int i = 0; i < managedCubes.Length; i++)
        {
            if (managedCubes[i] != null)
            {
                Debug.Log($"WISSdemo: Cube{i + 1} = {managedCubes[i].name}, ProgressRate = {currentProgressRates[i]:F2}");
            }
            else
            {
                Debug.Log($"WISSdemo: Cube{i + 1} = Not Assigned");
            }
        }
    }

    /// <summary>
    /// progressrateを手動で設定（外部から呼び出し可能）
    /// </summary>
    /// <param name="cubeIndex">Cubeのインデックス (0-2)</param>
    /// <param name="newRate">新しいprogressrate</param>
    public void SetProgressRate(int cubeIndex, float newRate)
    {
        if (cubeIndex < 0 || cubeIndex >= 3)
        {
            Debug.LogWarning($"WISSdemo: Invalid cube index {cubeIndex}. Must be 0-2.");
            return;
        }

        newRate = Mathf.Clamp(newRate, 0.1f, 5.0f);

        if (cubeIndex == 0) cube1ProgressRate = newRate;
        else if (cubeIndex == 1) cube2ProgressRate = newRate;
        else if (cubeIndex == 2) cube3ProgressRate = newRate;

        if (showDebugInfo)
        {
            Debug.Log($"WISSdemo: Cube{cubeIndex + 1} progress rate set to {newRate:F2}");
        }
    }

    /// <summary>
    /// 全てのprogressrateを一括設定
    /// </summary>
    /// <param name="rates">3つのprogressrateの配列</param>
    public void SetAllProgressRates(float[] rates)
    {
        if (rates == null || rates.Length < 3)
        {
            Debug.LogWarning("WISSdemo: Invalid rates array. Must contain at least 3 elements.");
            return;
        }

        cube1ProgressRate = Mathf.Clamp(rates[0], 0.1f, 5.0f);
        cube2ProgressRate = Mathf.Clamp(rates[1], 0.1f, 5.0f);
        cube3ProgressRate = Mathf.Clamp(rates[2], 0.1f, 5.0f);

        if (showDebugInfo)
        {
            Debug.Log($"WISSdemo: All progress rates updated - [{cube1ProgressRate:F2}, {cube2ProgressRate:F2}, {cube3ProgressRate:F2}]");
        }
    }

    /// <summary>
    /// 現在のprogressrateを取得
    /// </summary>
    /// <param name="cubeIndex">Cubeのインデックス (0-2)</param>
    /// <returns>現在のprogressrate</returns>
    public float GetProgressRate(int cubeIndex)
    {
        if (cubeIndex < 0 || cubeIndex >= 3)
        {
            Debug.LogWarning($"WISSdemo: Invalid cube index {cubeIndex}. Must be 0-2.");
            return 0f;
        }

        if (cubeIndex == 0) return cube1ProgressRate;
        else if (cubeIndex == 1) return cube2ProgressRate;
        else return cube3ProgressRate;
    }

    /// <summary>
    /// 管理しているCubeオブジェクトを取得
    /// </summary>
    /// <param name="cubeIndex">Cubeのインデックス (0-2)</param>
    /// <returns>Cubeオブジェクト</returns>
    public GameObject GetManagedCube(int cubeIndex)
    {
        if (cubeIndex < 0 || cubeIndex >= managedCubes.Length)
            return null;

        return managedCubes[cubeIndex];
    }

    /// <summary>
    /// 初期化状態を取得
    /// </summary>
    /// <returns>初期化済みの場合true</returns>
    public bool IsInitialized()
    {
        return isInitialized;
    }

    /// <summary>
    /// デモシステム全体の状態情報を取得
    /// </summary>
    /// <returns>状態情報の文字列</returns>
    public string GetSystemStatus()
    {
        if (!isInitialized)
            return "System not initialized";

        return $"WISSdemo Status:\n" +
               $"Cube1: {cube1Status}\n" +
               $"Cube2: {cube2Status}\n" +
               $"Cube3: {cube3Status}\n" +
               $"ProgressRate Sync: {enableProgressRateSync}\n" +
               $"Debug Info: {showDebugInfo}";
    }

    // Inspector上でのボタン実装（エディター拡張が必要）
    [ContextMenu("Apply Progress Rates Now")]
    private void ForceApplyProgressRates()
    {
        ApplyProgressRatesToController();
        Debug.Log("WISSdemo: Progress rates applied manually");
    }

    [ContextMenu("Reset Progress Rates to Default")]
    private void ResetProgressRatesToDefault()
    {
        cube1ProgressRate = 0.5f;
        cube2ProgressRate = 1.0f;
        cube3ProgressRate = 1.5f;
        Debug.Log("WISSdemo: Progress rates reset to default values");
    }

    [ContextMenu("Log Current System Status")]
    private void LogSystemStatus()
    {
        Debug.Log(GetSystemStatus());
    }
}