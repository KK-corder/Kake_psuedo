using System.Collections;
using UnityEngine;

public class CubeInterval : MonoBehaviour
{
    [Header("Interval Settings")]
    public float intervalDuration = 3.0f; // インターバル時間（秒）
    
    [Header("External References")]
    public BoneJudgeNew boneJudgeNew; // 接触判定を制御するスクリプト
    public TaskManage taskManage; // キューブのリセットを監視するスクリプト
    
    [Header("Debug")]
    public bool showDebugLogs = true;

    // 内部状態管理
    private bool isIntervalActive = false; // インターバル中かどうか
    private bool[] previousTaskCompletedState; // 前フレームのタスク完了状態
    private Coroutine intervalCoroutine; // インターバルのコルーチン

    void Start()
    {
        InitializeInterval();
    }

    void Update()
    {
        CheckForCubeReset();

        ShowDebugInfo();
    }

    /// <summary>
    /// インターバル機能の初期化
    /// </summary>
    private void InitializeInterval()
    {
        if (taskManage == null)
        {
            Debug.LogError("CubeInterval: TaskManage reference is required!");
            return;
        }

        if (boneJudgeNew == null)
        {
            Debug.LogError("CubeInterval: BoneJudgeNew reference is required!");
            return;
        }

        // タスク状態の監視用配列を初期化
        if (taskManage.cubes != null)
        {
            previousTaskCompletedState = new bool[taskManage.cubes.Length];
            for (int i = 0; i < previousTaskCompletedState.Length; i++)
            {
                previousTaskCompletedState[i] = taskManage.IsCubeTaskCompleted(i);
            }
        }

        if (showDebugLogs)
        {
            Debug.Log($"CubeInterval: Initialized with interval duration: {intervalDuration}s");
        }
    }

    /// <summary>
    /// キューブのリセットをチェック
    /// </summary>
    private void CheckForCubeReset()
    {
        if (taskManage == null || previousTaskCompletedState == null)
            return;

        // 各キューブのタスク完了状態をチェック
        for (int i = 0; i < taskManage.cubes.Length; i++)
        {
            bool currentCompleted = taskManage.IsCubeTaskCompleted(i);
            bool previousCompleted = previousTaskCompletedState[i];

            // タスクが完了状態から未完了状態に変わった場合（リセットされた場合）
            if (previousCompleted && !currentCompleted)
            {
                OnCubeReset(i);
            }

            // 前フレームの状態を更新
            previousTaskCompletedState[i] = currentCompleted;
        }
    }

    /// <summary>
    /// キューブがリセットされた時の処理
    /// </summary>
    /// <param name="cubeIndex">リセットされたキューブのインデックス</param>
    private void OnCubeReset(int cubeIndex)
    {
        if (showDebugLogs)
        {
            Debug.Log($"CubeInterval: Cube[{cubeIndex}] reset detected. Starting interval...");
        }

        StartInterval();
    }

    /// <summary>
    /// インターバルを開始
    /// </summary>
    public void StartInterval()
    {
        // 既にインターバル中の場合は前のコルーチンを停止
        if (intervalCoroutine != null)
        {
            StopCoroutine(intervalCoroutine);
        }

        intervalCoroutine = StartCoroutine(IntervalCoroutine());
    }

    /// <summary>
    /// インターバル処理のコルーチン
    /// </summary>
    /// <returns>IEnumerator</returns>
    private IEnumerator IntervalCoroutine()
    {
        isIntervalActive = true;
        
        // 接触判定を無効化
        SetContactDetectionEnabled(false);

        if (showDebugLogs)
        {
            Debug.Log($"CubeInterval: Interval started for {intervalDuration} seconds. Contact detection disabled.");
        }

        // 指定時間待機
        yield return new WaitForSeconds(intervalDuration);

        // 接触判定を再有効化
        SetContactDetectionEnabled(true);
        
        isIntervalActive = false;
        intervalCoroutine = null;

        if (showDebugLogs)
        {
            Debug.Log("CubeInterval: Interval ended. Contact detection re-enabled.");
        }
    }

    /// <summary>
    /// 接触判定の有効/無効を切り替え
    /// </summary>
    /// <param name="enabled">true=有効, false=無効</param>
    private void SetContactDetectionEnabled(bool enabled)
    {
        if (boneJudgeNew != null)
        {
            boneJudgeNew.enabled = enabled;
            
            // 無効化時は選択状態もリセット（BoneJudgeNewにResetSelectionメソッドがある場合）
            if (!enabled)
            {
                // BoneJudgeNewには選択状態リセットメソッドがないため、コメントアウト
                // boneJudgeNew.ResetSelection();
            }
        }
    }

    /// <summary>
    /// 手動でインターバルを開始
    /// </summary>
    public void ManualStartInterval()
    {
        StartInterval();
        
        if (showDebugLogs)
        {
            Debug.Log("CubeInterval: Manual interval start triggered.");
        }
    }

    /// <summary>
    /// インターバルを強制終了
    /// </summary>
    public void ForceStopInterval()
    {
        if (intervalCoroutine != null)
        {
            StopCoroutine(intervalCoroutine);
            intervalCoroutine = null;
        }

        SetContactDetectionEnabled(true);
        isIntervalActive = false;

        if (showDebugLogs)
        {
            Debug.Log("CubeInterval: Interval force stopped. Contact detection re-enabled.");
        }
    }

    /// <summary>
    /// 現在インターバル中かどうかを取得
    /// </summary>
    /// <returns>インターバル中の場合true</returns>
    public bool IsIntervalActive()
    {
        return isIntervalActive;
    }

    /// <summary>
    /// インターバル時間を動的に変更
    /// </summary>
    /// <param name="newDuration">新しいインターバル時間（秒）</param>
    public void SetIntervalDuration(float newDuration)
    {
        intervalDuration = newDuration;
        
        if (showDebugLogs)
        {
            Debug.Log($"CubeInterval: Interval duration changed to {intervalDuration} seconds.");
        }
    }

    /// <summary>
    /// 残りインターバル時間を取得
    /// </summary>
    /// <returns>残り時間（秒）、インターバル中でない場合は0</returns>
    public float GetRemainingIntervalTime()
    {
        // この実装では正確な残り時間は計算できないため、近似値を返す
        return isIntervalActive ? intervalDuration : 0f;
    }

    /// <summary>
    /// デバッグ情報を表示
    /// </summary>
    public void ShowDebugInfo()
    {
        if (showDebugLogs)
        {
            Debug.Log("=== CubeInterval Debug Info ===");
            Debug.Log($"Interval Active: {isIntervalActive}");
            Debug.Log($"Interval Duration: {intervalDuration}s");
            Debug.Log($"TaskManage Reference: {(taskManage != null ? "OK" : "NULL")}");
            Debug.Log($"BoneJudgeNew Reference: {(boneJudgeNew != null ? "OK" : "NULL")}");
            
            if (taskManage != null && taskManage.cubes != null)
            {
                for (int i = 0; i < taskManage.cubes.Length; i++)
                {
                    bool completed = taskManage.IsCubeTaskCompleted(i);
                    Debug.Log($"Cube[{i}] Task Completed: {completed}");
                }
            }
        }
    }

    void OnDestroy()
    {
        // コンポーネント破棄時にコルーチンを停止
        if (intervalCoroutine != null)
        {
            StopCoroutine(intervalCoroutine);
        }
    }
}