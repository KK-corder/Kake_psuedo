using UnityEngine;
using UnityEngine.UI;

public class TaskManage : MonoBehaviour
{
    [Header("Cube Settings")]
    public GameObject[] cubes; // 管理対象のキューブ配列
    public float yThreshold = 2.0f; // Y座標の閾値
    
    [Header("Default Positions")]
    [SerializeField] private Vector3[] defaultPositions; // 各キューブのデフォルト位置（自動取得）
    
    [Header("Question Objects")]
    public GameObject[] questionObjects; // 質問用GameObjectの配列
    
    [Header("UI Text")]
    public Text instructionText; // 指示テキスト表示用
    
    [Header("Text Messages")]
    public string initialMessage = "push the cube";
    public string secondMessage = "push the cube again";
    public string finalMessage = "どちらのcubeをより重く感じましたか";
    
    [Header("Debug")]
    public bool showDebugLogs = true;

    // 内部管理用変数
    private bool[] hasReachedThresholdOnce; // 各キューブが一度閾値に達したかどうか

    private bool[] isCompleted; // 各キューブのタスクが完了したかどうか
    private Vector3[] originalPositions; // 元の位置を記録
    private bool anyTaskCompleted = false; // いずれかのタスクが完了したかどうか

    void Start()
    {
        InitializeTaskManager();
    }

    void Update()
    {
        CheckCubePositions();
    }

    /// <summary>
    /// タスクマネージャーの初期化
    /// </summary>
    private void InitializeTaskManager()
    {
        if (cubes == null || cubes.Length == 0)
        {
            Debug.LogError("TaskManage: No cubes assigned!");
            return;
        }

        int cubeCount = cubes.Length;
        hasReachedThresholdOnce = new bool[cubeCount];
        isCompleted = new bool[cubeCount];
        originalPositions = new Vector3[cubeCount];

        // デフォルト位置の自動設定（再生開始時の座標を取得）
        defaultPositions = new Vector3[cubeCount];
        for (int i = 0; i < cubeCount; i++)
        {
            if (cubes[i] != null)
            {
                // 現在の位置をデフォルト位置として記録
                defaultPositions[i] = cubes[i].transform.position;
                originalPositions[i] = cubes[i].transform.position;
                
                if (showDebugLogs)
                {
                    Debug.Log($"TaskManage: Cube[{i}] default position auto-set to: {defaultPositions[i]}");
                }
            }
        }

        // 質問オブジェクトの初期化（最初は非表示）
        if (questionObjects != null)
        {
            for (int i = 0; i < questionObjects.Length; i++)
            {
                if (questionObjects[i] != null)
                {
                    questionObjects[i].SetActive(false);
                }
            }
        }

        // 初期状態の設定
        for (int i = 0; i < cubeCount; i++)
        {
            hasReachedThresholdOnce[i] = false;
            isCompleted[i] = false;
        }

        // 初期テキストを設定
        UpdateInstructionText();

        if (showDebugLogs)
        {
            Debug.Log($"TaskManage: Initialized with {cubeCount} cubes, Y threshold: {yThreshold}");
        }
    }

    /// <summary>
    /// キューブの位置をチェックして必要なアクションを実行
    /// </summary>
    private void CheckCubePositions()
    {
        if (cubes == null) return;

        for (int i = 0; i < cubes.Length; i++)
        {
            if (cubes[i] == null || isCompleted[i]) continue;

            float currentY = cubes[i].transform.position.y;

            // Y座標が閾値以上に達した場合
            if (currentY >= yThreshold)
            {
                if (!hasReachedThresholdOnce[i])
                {
                    // 初回到達：デフォルト位置に戻す
                    ResetCubeToDefaultPosition(i);
                    hasReachedThresholdOnce[i] = true;
                    
                    // テキストを更新
                    UpdateInstructionText();
                    
                    if (showDebugLogs)
                    {
                        Debug.Log($"TaskManage: Cube[{i}] reached threshold (Y={currentY:F2}) for first time. Reset to default position.");
                    }
                }
                else
                {
                    // 二回目到達：キューブを非表示にして質問オブジェクトを表示
                    CompleteCubeTask(i);
                    
                    // テキストを更新
                    UpdateInstructionText();
                    
                    if (showDebugLogs)
                    {
                        Debug.Log($"TaskManage: Cube[{i}] reached threshold (Y={currentY:F2}) for second time. Task completed.");
                    }
                }
            }
        }
    }

    /// <summary>
    /// キューブをデフォルト位置にリセット
    /// </summary>
    private void ResetCubeToDefaultPosition(int cubeIndex)
    {
        if (cubeIndex < 0 || cubeIndex >= cubes.Length || cubes[cubeIndex] == null)
            return;

        if (cubeIndex >= defaultPositions.Length)
        {
            Debug.LogError($"TaskManage: Default position not set for cube[{cubeIndex}]");
            return;
        }

        // 位置をリセット
        cubes[cubeIndex].transform.position = defaultPositions[cubeIndex];
        
        // 回転もリセット（必要に応じて）
        cubes[cubeIndex].transform.rotation = Quaternion.identity;
        
        // Rigidbodyがある場合は物理状態もリセット
        Rigidbody rb = cubes[cubeIndex].GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep(); // 物理計算を一時停止してから再開
            rb.WakeUp();
        }

        if (showDebugLogs)
        {
            Debug.Log($"TaskManage: Cube[{cubeIndex}] reset to default position: {defaultPositions[cubeIndex]}");
        }
    }

    /// <summary>
    /// キューブのタスクを完了（非表示化＋質問オブジェクト表示）
    /// </summary>
    private void CompleteCubeTask(int cubeIndex)
    {
        if (cubeIndex < 0 || cubeIndex >= cubes.Length || cubes[cubeIndex] == null)
            return;

        // キューブを非表示にする
        cubes[cubeIndex].SetActive(false);
        isCompleted[cubeIndex] = true;
        anyTaskCompleted = true;

        // 対応する質問オブジェクトを表示
        if (questionObjects != null && cubeIndex < questionObjects.Length && questionObjects[cubeIndex] != null)
        {
            questionObjects[cubeIndex].SetActive(true);
            
            if (showDebugLogs)
            {
                Debug.Log($"TaskManage: Question object[{cubeIndex}] activated: {questionObjects[cubeIndex].name}");
            }
        }

        if (showDebugLogs)
        {
            Debug.Log($"TaskManage: Cube[{cubeIndex}] task completed and hidden.");
        }
    }

    /// <summary>
    /// 指定されたキューブのタスクを手動でリセット
    /// </summary>
    public void ResetCubeTask(int cubeIndex)
    {
        if (cubeIndex < 0 || cubeIndex >= cubes.Length)
            return;

        // キューブを再表示
        if (cubes[cubeIndex] != null)
        {
            cubes[cubeIndex].SetActive(true);
            cubes[cubeIndex].transform.position = defaultPositions[cubeIndex];
        }

        // 質問オブジェクトを非表示
        if (questionObjects != null && cubeIndex < questionObjects.Length && questionObjects[cubeIndex] != null)
        {
            questionObjects[cubeIndex].SetActive(false);
        }

        // 状態をリセット
        hasReachedThresholdOnce[cubeIndex] = false;
        isCompleted[cubeIndex] = false;

        // 全体の完了状態もチェック
        anyTaskCompleted = false;
        for (int i = 0; i < isCompleted.Length; i++)
        {
            if (isCompleted[i])
            {
                anyTaskCompleted = true;
                break;
            }
        }

        // テキストを更新
        UpdateInstructionText();

        if (showDebugLogs)
        {
            Debug.Log($"TaskManage: Cube[{cubeIndex}] task manually reset.");
        }
    }

    /// <summary>
    /// すべてのキューブのタスクをリセット
    /// </summary>
    public void ResetAllCubeTasks()
    {
        for (int i = 0; i < cubes.Length; i++)
        {
            ResetCubeTask(i);
        }

        if (showDebugLogs)
        {
            Debug.Log("TaskManage: All cube tasks reset.");
        }
    }

    /// <summary>
    /// Y座標の閾値を動的に変更
    /// </summary>
    public void SetYThreshold(float newThreshold)
    {
        yThreshold = newThreshold;
        
        if (showDebugLogs)
        {
            Debug.Log($"TaskManage: Y threshold changed to {newThreshold}");
        }
    }

    /// <summary>
    /// 指定されたキューブがタスク完了状態かどうかを取得
    /// </summary>
    public bool IsCubeTaskCompleted(int cubeIndex)
    {
        if (cubeIndex < 0 || cubeIndex >= isCompleted.Length)
            return false;
            
        return isCompleted[cubeIndex];
    }

    /// <summary>
    /// 指定されたキューブが一度閾値に達したかどうかを取得
    /// </summary>
    public bool HasCubeReachedThresholdOnce(int cubeIndex)
    {
        if (cubeIndex < 0 || cubeIndex >= hasReachedThresholdOnce.Length)
            return false;
            
        return hasReachedThresholdOnce[cubeIndex];
    }

    /// <summary>
    /// 指示テキストを現在の状態に応じて更新
    /// </summary>
    private void UpdateInstructionText()
    {
        if (instructionText == null) return;

        // いずれかのタスクが完了している場合
        if (anyTaskCompleted)
        {
            instructionText.text = finalMessage;
            
            if (showDebugLogs)
            {
                Debug.Log($"TaskManage: Text updated to final message: '{finalMessage}'");
            }
            return;
        }

        // 一度でも閾値に達したキューブがあるかチェック
        bool anyReachedOnce = false;
        for (int i = 0; i < hasReachedThresholdOnce.Length; i++)
        {
            if (hasReachedThresholdOnce[i])
            {
                anyReachedOnce = true;
                break;
            }
        }

        if (anyReachedOnce)
        {
            instructionText.text = secondMessage;
            
            if (showDebugLogs)
            {
                Debug.Log($"TaskManage: Text updated to second message: '{secondMessage}'");
            }
        }
        else
        {
            instructionText.text = initialMessage;
            
            if (showDebugLogs)
            {
                Debug.Log($"TaskManage: Text updated to initial message: '{initialMessage}'");
            }
        }
    }

    /// <summary>
    /// テキストメッセージを手動で設定
    /// </summary>
    public void SetCustomMessage(string message)
    {
        if (instructionText != null)
        {
            instructionText.text = message;
            
            if (showDebugLogs)
            {
                Debug.Log($"TaskManage: Text manually set to: '{message}'");
            }
        }
    }

    /// <summary>
    /// 現在のテキストメッセージを取得
    /// </summary>
    public string GetCurrentMessage()
    {
        return instructionText != null ? instructionText.text : "";
    }

    /// <summary>
    /// デフォルト位置を現在の位置で再設定
    /// </summary>
    public void UpdateDefaultPositions()
    {
        if (cubes == null || defaultPositions == null) return;

        for (int i = 0; i < cubes.Length; i++)
        {
            if (cubes[i] != null)
            {
                defaultPositions[i] = cubes[i].transform.position;
                
                if (showDebugLogs)
                {
                    Debug.Log($"TaskManage: Cube[{i}] default position updated to: {defaultPositions[i]}");
                }
            }
        }

        if (showDebugLogs)
        {
            Debug.Log("TaskManage: All default positions updated to current positions.");
        }
    }

    /// <summary>
    /// 指定されたキューブのデフォルト位置を取得
    /// </summary>
    public Vector3 GetDefaultPosition(int cubeIndex)
    {
        if (cubeIndex < 0 || cubeIndex >= defaultPositions.Length)
            return Vector3.zero;
            
        return defaultPositions[cubeIndex];
    }

    /// <summary>
    /// すべてのデフォルト位置を取得
    /// </summary>
    public Vector3[] GetAllDefaultPositions()
    {
        return defaultPositions != null ? (Vector3[])defaultPositions.Clone() : null;
    }

    /// <summary>
    /// 全キューブのタスク状態を取得
    /// </summary>
    public void GetAllTaskStatus()
    {
        if (showDebugLogs)
        {
            Debug.Log("=== TaskManage Status ===");
            for (int i = 0; i < cubes.Length; i++)
            {
                if (cubes[i] != null)
                {
                    Debug.Log($"Cube[{i}]: ReachedOnce={hasReachedThresholdOnce[i]}, Completed={isCompleted[i]}, CurrentY={cubes[i].transform.position.y:F2}, DefaultPos={defaultPositions[i]}");
                }
            }
        }
    }
}