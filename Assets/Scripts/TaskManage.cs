using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class TaskManage : MonoBehaviour
{
    [Header("Cube Settings")]
    public GameObject[] cubes; // 管理対象のキューブ配列
    [HideInInspector] public float yThreshold = 2.0f; // Y座標の閾値（手動設定用、target_limitがある場合は自動更新）
    
    [Header("Target Limit Object")]
    public GameObject target_limit; // Y threshold自動取得用のターゲットオブジェクト
    public bool autoUpdateYThreshold = true; // Y thresholdの自動更新を有効にするか
    
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
    public string sessionTransitionMessage = "session2にうつります";
    public string session2Message = "session2";
    
    [Header("Progress Rate Settings")]
    public ContactProgressController contactProgressController; // ProgressRateを制御するための参照
    public BoneJudgeNew_reset boneJudgeNewReset; // trialCount管理のための参照
    public float[] progressRateValues = {2.0f, 6.0f, 10.0f, 14.0f, 18.0f}; // ProgressRate候補値
    
    [Header("Experiment Settings")]
    public int totalTasks = 25; // 総タスク数
    public int trialsPerProgressRate = 5; // 各ProgressRateパターンの実行回数
    
    [Header("Session Settings")]
    public int trialsPerPattern = 5; // 各パターンの試行数
    public bool autoStartExperiment = true; // 実験の自動開始
    public float sessionTransitionDuration = 3.0f; // セッション移行時間（秒）
    
    [Header("Debug")]
    public bool showDebugLogs = true;

    // 内部管理用変数
    private bool[] hasReachedThresholdOnce; // 各キューブが一度閾値に達したかどうか

    private bool[] isCompleted; // 各キューブのタスクが完了したかどうか
    private Vector3[] originalPositions; // 元の位置を記録
    private bool anyTaskCompleted = false; // いずれかのタスクが完了したかどうか
    
    // Session Management Variables
    private int currentSession = 0; // 0: first session, 1: second session
    private int currentTrialInSession = 0;
    private bool isSessionZero; // true if current session has progressRate=0
    private bool isSessionOne; // true if current session has non-zero progressRate
    private float currentProgressRateValue = 0f;
    private int totalTrialsCompleted = 0;
    private bool experimentInitialized = false;
    
    // Experiment Pattern Management
    private int currentTaskIndex = 0; // 現在のタスクインデックス（0-24）
    private List<ExperimentPattern> experimentPatterns; // 実験パターンのリスト
    private bool experimentCompleted = false; // 全実験完了フラグ
    
    // Session Transition Variables
    private bool isInSessionTransition = false; // セッション移行中かどうか

    /// <summary>
    /// 実験パターンのデータ構造
    /// </summary>
    [System.Serializable]
    public class ExperimentPattern
    {
        public float progressRateValue; // 非ゼロのProgressRate値
        public bool zeroInSession1; // trueならSession1が0、falseならSession2が0
        
        public ExperimentPattern(float progressRate, bool zeroFirst)
        {
            progressRateValue = progressRate;
            zeroInSession1 = zeroFirst;
        }
    }

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

        // target_limitからY thresholdを自動取得
        UpdateYThresholdFromTargetLimit();

        // 初期テキストを設定
        UpdateInstructionText();

        // セッション管理の初期化
        InitializeExperimentSessions();

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
        if (cubes == null || isInSessionTransition) return;

        for (int i = 0; i < cubes.Length; i++)
        {
            if (cubes[i] == null || isCompleted[i]) continue;

            float currentY = cubes[i].transform.position.y;

            // Y座標が閾値以上に達した場合
            if (currentY >= yThreshold)
            {
                if (!hasReachedThresholdOnce[i])
                {
                    if (currentSession == 0)
                    {
                        // セッション1: 初回到達時はデフォルト位置に戻して3秒間非表示
                        ResetCubeToDefaultPosition(i);
                        hasReachedThresholdOnce[i] = true;
                        
                        if (showDebugLogs)
                        {
                            Debug.Log($"TaskManage: Session 1 - Cube[{i}] reached threshold (Y={currentY:F2}) for first time. Starting session transition.");
                        }
                    }
                    else
                    {
                        // セッション2: 初回到達時はキューブを非表示にして質問オブジェクトを表示
                        CompleteCubeTaskWithQuestion(i);
                        hasReachedThresholdOnce[i] = true;
                        
                        if (showDebugLogs)
                        {
                            Debug.Log($"TaskManage: Session 2 - Cube[{i}] reached threshold (Y={currentY:F2}). Showing question objects.");
                        }
                    }
                    
                    // テキストを更新
                    UpdateInstructionText();
                }
                else
                {
                    // 二回目到達（このケースは現在の仕様では使用しない）
                    if (showDebugLogs)
                    {
                        Debug.Log($"TaskManage: Cube[{i}] reached threshold (Y={currentY:F2}) for second time (ignored in current implementation).");
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

        // 3秒間キューブを非表示にする
        StartCoroutine(HideCubeTemporarily(cubeIndex, 3.0f));

        if (showDebugLogs)
        {
            Debug.Log($"TaskManage: Cube[{cubeIndex}] reset to default position: {defaultPositions[cubeIndex]} and hidden for 3 seconds");
        }
    }

    /// <summary>
    /// 指定されたキューブを一時的に非表示にし、セッション2を開始
    /// </summary>
    private System.Collections.IEnumerator HideCubeTemporarily(int cubeIndex, float duration)
    {
        if (cubeIndex < 0 || cubeIndex >= cubes.Length || cubes[cubeIndex] == null)
            yield break;

        // キューブを非表示にする
        cubes[cubeIndex].SetActive(false);
        
        // セッション移行メッセージを表示
        if (instructionText != null)
        {
            int displayTaskNumber = currentTaskIndex + 1;
            int totalTasksDisplay = (experimentPatterns != null) ? experimentPatterns.Count : totalTasks;
            string taskInfo = $"Task {displayTaskNumber}/{totalTasksDisplay} - ";
            instructionText.text = taskInfo + sessionTransitionMessage;
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"TaskManage: Cube[{cubeIndex}] hidden for {duration} seconds. Starting session transition...");
        }

        // 指定時間待機（3秒）
        yield return new WaitForSeconds(duration);

        // セッション2を開始
        StartSession2(cubeIndex);
    }

    /// <summary>
    /// セッション2を開始
    /// </summary>
    private void StartSession2(int cubeIndex)
    {
        // セッション設定を変更
        currentSession = 1;
        currentTrialInSession = 0;
        
        // 新しいprogressRate設定を適用
        ApplyCurrentTaskSettings();
        
        // キューブの状態をリセット
        hasReachedThresholdOnce[cubeIndex] = false;
        isCompleted[cubeIndex] = false;
        anyTaskCompleted = false;
        
        // キューブを再表示
        cubes[cubeIndex].SetActive(true);
        
        // Session2のテキストを表示
        if (instructionText != null)
        {
            int displayTaskNumber = currentTaskIndex + 1;
            int totalTasksDisplay = (experimentPatterns != null) ? experimentPatterns.Count : totalTasks;
            string taskInfo = $"Task {displayTaskNumber}/{totalTasksDisplay} - ";
            instructionText.text = taskInfo + session2Message;
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"TaskManage: ===== SESSION 2 STARTED =====");
            Debug.Log($"TaskManage: Cube[{cubeIndex}] shown again. New progress rate: {currentProgressRateValue}");
            Debug.Log($"TaskManage: Text updated to session 2 message: '{session2Message}'");
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

        // 質問オブジェクトの表示は実験全体完了時のみ行う
        // 各試行完了時は質問オブジェクトを表示しない

        if (showDebugLogs)
        {
            Debug.Log($"TaskManage: Cube[{cubeIndex}] task completed and hidden.");
        }
    }

    /// <summary>
    /// セッション2でキューブのタスクを完了（非表示化＋質問オブジェクト表示）
    /// </summary>
    private void CompleteCubeTaskWithQuestion(int cubeIndex)
    {
        if (cubeIndex < 0 || cubeIndex >= cubes.Length || cubes[cubeIndex] == null)
            return;

        // キューブを非表示にする
        cubes[cubeIndex].SetActive(false);
        isCompleted[cubeIndex] = true;
        anyTaskCompleted = true;

        // 質問オブジェクトを表示
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
            Debug.Log($"TaskManage: Session 2 - Cube[{cubeIndex}] task completed, hidden, and question object displayed.");
        }
    }

    /// <summary>
    /// 実験セッションの初期化（25タスクのパターンを生成）
    /// </summary>
    private void InitializeExperimentSessions()
    {
        // ContactProgressControllerの参照確認
        if (contactProgressController == null)
        {
            Debug.LogError("TaskManage: ContactProgressController reference is not set!");
            return;
        }

        // 実験パターンを生成
        GenerateExperimentPatterns();

        // 現在のタスク設定を適用
        ApplyCurrentTaskSettings();

        experimentInitialized = true;

        if (showDebugLogs)
        {
            Debug.Log($"TaskManage: Experiment initialized with {experimentPatterns.Count} tasks");
            Debug.Log($"TaskManage: Current task {currentTaskIndex + 1}/{totalTasks}");
            Debug.Log($"TaskManage: Current session progress rates - Session1: {(GetCurrentPattern().zeroInSession1 ? 0 : GetCurrentPattern().progressRateValue)}, Session2: {(GetCurrentPattern().zeroInSession1 ? GetCurrentPattern().progressRateValue : 0)}");
        }
    }

    /// <summary>
    /// 25タスクの実験パターンを生成
    /// </summary>
    private void GenerateExperimentPatterns()
    {
        experimentPatterns = new List<ExperimentPattern>();

        if (showDebugLogs)
        {
            Debug.Log($"TaskManage: GenerateExperimentPatterns - progressRateValues.Length: {progressRateValues.Length}, trialsPerProgressRate: {trialsPerProgressRate}");
        }

        // 各ProgressRate値に対して5回ずつ、Session1/Session2の0配置もランダム化
        for (int i = 0; i < progressRateValues.Length; i++)
        {
            float progressRate = progressRateValues[i];
            
            for (int j = 0; j < trialsPerProgressRate; j++)
            {
                bool zeroInSession1 = Random.Range(0, 2) == 0; // ランダムに決定
                experimentPatterns.Add(new ExperimentPattern(progressRate, zeroInSession1));
            }
        }

        if (showDebugLogs)
        {
            Debug.Log($"TaskManage: Before shuffle - experimentPatterns.Count: {experimentPatterns.Count}");
        }

        // リストをシャッフルしてランダムな順序にする
        for (int i = 0; i < experimentPatterns.Count; i++)
        {
            ExperimentPattern temp = experimentPatterns[i];
            int randomIndex = Random.Range(i, experimentPatterns.Count);
            experimentPatterns[i] = experimentPatterns[randomIndex];
            experimentPatterns[randomIndex] = temp;
        }

        if (showDebugLogs)
        {
            Debug.Log($"TaskManage: Generated {experimentPatterns.Count} experiment patterns (expected: {totalTasks})");
            for (int i = 0; i < experimentPatterns.Count; i++)
            {
                var pattern = experimentPatterns[i];
                Debug.Log($"Task {i + 1}: ProgressRate={pattern.progressRateValue}, ZeroInSession1={pattern.zeroInSession1}");
            }
        }
    }

    /// <summary>
    /// 現在のパターンを取得
    /// </summary>
    private ExperimentPattern GetCurrentPattern()
    {
        if (experimentPatterns == null)
        {
            if (showDebugLogs)
            {
                Debug.LogError("TaskManage: GetCurrentPattern - experimentPatterns is null!");
            }
            return null;
        }

        if (currentTaskIndex >= 0 && currentTaskIndex < experimentPatterns.Count)
        {
            return experimentPatterns[currentTaskIndex];
        }

        if (showDebugLogs)
        {
            Debug.LogError($"TaskManage: GetCurrentPattern - currentTaskIndex ({currentTaskIndex}) is out of range (0-{experimentPatterns.Count - 1})");
        }
        return null;
    }

    /// <summary>
    /// 現在のタスク設定を適用
    /// </summary>
    private void ApplyCurrentTaskSettings()
    {
        if (contactProgressController == null) return;

        var currentPattern = GetCurrentPattern();
        if (currentPattern == null)
        {
            Debug.LogError("TaskManage: No current pattern available!");
            return;
        }

        if (currentSession == 0) // セッション1
        {
            currentProgressRateValue = currentPattern.zeroInSession1 ? 0f : currentPattern.progressRateValue;
        }
        else // セッション2
        {
            currentProgressRateValue = currentPattern.zeroInSession1 ? currentPattern.progressRateValue : 0f;
        }

        // ContactProgressControllerのprogressIncreaseRateを更新
        for (int i = 0; i < contactProgressController.progressIncreaseRate.Length; i++)
        {
            contactProgressController.progressIncreaseRate[i] = currentProgressRateValue;
        }

        if (showDebugLogs)
        {
            Debug.Log($"TaskManage: Applied task {currentTaskIndex + 1} session {currentSession + 1} settings. Progress rate: {currentProgressRateValue}");
        }
    }

    /// <summary>
    /// 試行完了時に呼び出される（BoneJudgeNew_resetから呼ばれる）
    /// </summary>
    public void OnTrialCompleted()
    {
        totalTrialsCompleted++;

        if (showDebugLogs)
        {
            Debug.Log($"TaskManage: Trial completed. Session {currentSession + 1}, Total: {totalTrialsCompleted}");
        }

        // 現在の実装では各試行後に特別な処理は不要
        // セッション移行は各キューブの閾値到達時に自動的に行われる
    }

    /// <summary>
    /// セッション移行処理を開始
    /// </summary>
    private void StartSessionTransition()
    {
        isInSessionTransition = true;
        
        if (showDebugLogs)
        {
            Debug.Log("TaskManage: ===== SESSION TRANSITION STARTED =====");
            Debug.Log($"TaskManage: Session 1 completed with {currentTrialInSession} trials");
        }
        
        // 全てのキューブを非表示にする
        HideAllCubes();
        
        // セッション移行メッセージを表示
        DisplaySessionTransitionMessage();
        
        // 3秒後にセッション2を開始
        StartCoroutine(SessionTransitionCoroutine());
        
        if (showDebugLogs)
        {
            Debug.Log($"TaskManage: Cubes hidden for {sessionTransitionDuration} seconds. Waiting for session 2...");
        }
    }
    
    /// <summary>
    /// セッション移行のコルーチン
    /// </summary>
    private System.Collections.IEnumerator SessionTransitionCoroutine()
    {
        if (showDebugLogs)
        {
            Debug.Log($"TaskManage: Starting {sessionTransitionDuration} second wait...");
        }
        
        yield return new WaitForSeconds(sessionTransitionDuration);
        
        if (showDebugLogs)
        {
            Debug.Log("TaskManage: Wait completed. Starting session 2 setup...");
        }
        
        // セッション2への移行を完了
        currentSession = 1;
        currentTrialInSession = 0;
        isInSessionTransition = false;
        
        // 設定を適用
        ApplyCurrentTaskSettings();
        
        // キューブの状態をリセットしてから再表示
        ResetAllCubeStates();
        ShowAllCubes();
        
        // 通常のテキストに戻す
        UpdateInstructionText();
        
        if (showDebugLogs)
        {
            Debug.Log($"TaskManage: ===== SESSION 2 STARTED =====");
            Debug.Log($"TaskManage: New progress rate: {currentProgressRateValue}");
        }
    }
    
    /// <summary>
    /// 全てのキューブを非表示にする
    /// </summary>
    private void HideAllCubes()
    {
        if (cubes == null) return;
        
        for (int i = 0; i < cubes.Length; i++)
        {
            if (cubes[i] != null)
            {
                cubes[i].SetActive(false);
            }
        }
    }
    
    /// <summary>
    /// 全てのキューブを再表示する
    /// </summary>
    private void ShowAllCubes()
    {
        if (cubes == null) return;
        
        for (int i = 0; i < cubes.Length; i++)
        {
            if (cubes[i] != null)
            {
                cubes[i].SetActive(true);
                // デフォルト位置にリセット
                cubes[i].transform.position = defaultPositions[i];
            }
        }
    }
    
    /// <summary>
    /// 全てのキューブの状態をリセット（セッション移行時用）
    /// </summary>
    private void ResetAllCubeStates()
    {
        if (cubes == null) return;
        
        for (int i = 0; i < cubes.Length; i++)
        {
            hasReachedThresholdOnce[i] = false;
            isCompleted[i] = false;
        }
        
        anyTaskCompleted = false;
        
        if (showDebugLogs)
        {
            Debug.Log("TaskManage: All cube states reset for new session.");
        }
    }
    
    /// <summary>
    /// セッション移行メッセージを表示
    /// </summary>
    private void DisplaySessionTransitionMessage()
    {
        if (instructionText != null)
        {
            int displayTaskNumber = currentTaskIndex + 1;
            int totalTasksDisplay = (experimentPatterns != null) ? experimentPatterns.Count : totalTasks;
            string taskInfo = $"Task {displayTaskNumber}/{totalTasksDisplay} - ";
            instructionText.text = taskInfo + sessionTransitionMessage;
        }
    }

    /// <summary>
    /// セッション2完了処理（質問オブジェクト表示）
    /// </summary>
    private void OnSession2Completed()
    {
        if (showDebugLogs)
        {
            Debug.Log("TaskManage: ===== SESSION 2 COMPLETED =====");
            Debug.Log("TaskManage: Total trials: " + totalTrialsCompleted);
        }

        // 全てのキューブを非表示にする
        HideAllCubes();
        
        // 質問オブジェクトを表示する
        ShowQuestionObjects();
        
        // 最終メッセージを表示
        if (instructionText != null)
        {
            instructionText.text = finalMessage;
        }

        if (showDebugLogs)
        {
            Debug.Log("TaskManage: Question objects displayed for selection. Waiting for reset...");
        }
    }

    /// <summary>
    /// 質問オブジェクトを表示する
    /// </summary>
    private void ShowQuestionObjects()
    {
        if (questionObjects == null) return;
        
        for (int i = 0; i < questionObjects.Length; i++)
        {
            if (questionObjects[i] != null)
            {
                questionObjects[i].SetActive(true);
                
                if (showDebugLogs)
                {
                    Debug.Log($"TaskManage: Question object[{i}] activated: {questionObjects[i].name}");
                }
            }
        }
    }

    /// <summary>
    /// 次のタスクに進むか実験を終了
    /// </summary>
    public void RestartExperiment()
    {
        currentTaskIndex++;

        // 実際の実験パターン数を使用して終了判定
        int actualTotalTasks = (experimentPatterns != null) ? experimentPatterns.Count : totalTasks;
        
        if (showDebugLogs)
        {
            Debug.Log($"TaskManage: RestartExperiment - currentTaskIndex: {currentTaskIndex}, actualTotalTasks: {actualTotalTasks}, totalTasks setting: {totalTasks}");
        }

        if (currentTaskIndex >= actualTotalTasks)
        {
            // 全実験終了
            OnAllExperimentsCompleted();
            return;
        }

        if (showDebugLogs)
        {
            Debug.Log($"TaskManage: ===== STARTING TASK {currentTaskIndex + 1}/{totalTasks} =====");
        }

        // セッション状態をリセット
        currentSession = 0;
        currentTrialInSession = 0;
        isInSessionTransition = false;

        // 新しいタスク設定を適用
        ApplyCurrentTaskSettings();

        // キューブ状態をリセット
        ResetAllCubeStates();

        // 質問オブジェクトを非表示
        HideQuestionObjects();

        // キューブを再表示
        ShowAllCubes();

        // 初期テキストを表示
        UpdateInstructionText();

        if (showDebugLogs)
        {
            Debug.Log($"TaskManage: Task {currentTaskIndex + 1} started. Progress rates - Session1: {(GetCurrentPattern().zeroInSession1 ? 0 : GetCurrentPattern().progressRateValue)}, Session2: {(GetCurrentPattern().zeroInSession1 ? GetCurrentPattern().progressRateValue : 0)}");
        }
    }

    /// <summary>
    /// 全実験完了処理
    /// </summary>
    private void OnAllExperimentsCompleted()
    {
        experimentCompleted = true;
        
        if (showDebugLogs)
        {
            Debug.Log("TaskManage: ===== ALL EXPERIMENTS COMPLETED =====");
            Debug.Log($"TaskManage: Total tasks completed: {currentTaskIndex} (expected: {totalTasks})");
            Debug.Log($"TaskManage: Experiment patterns count: {(experimentPatterns != null ? experimentPatterns.Count : 0)}");
        }

        // 実験終了メッセージを表示
        if (instructionText != null)
        {
            int totalTasksDisplay = (experimentPatterns != null) ? experimentPatterns.Count : totalTasks;
            instructionText.text = $"実験が完了しました ({totalTasksDisplay}/{totalTasksDisplay}) - お疲れ様でした。";
        }

        // 全てのオブジェクトを非表示
        HideAllCubes();
        HideQuestionObjects();
    }

    /// <summary>
    /// 選択完了後の自動リスタート（BoneJudgeNew_resetから呼ばれる）
    /// </summary>
    public void OnSelectionCompleted()
    {
        // trialCountを増加（質問フェーズでの選択完了時のみ）
        if (boneJudgeNewReset != null)
        {
            boneJudgeNewReset.IncrementTrialCount();
            
            if (showDebugLogs)
            {
                Debug.Log($"TaskManage: Trial count increased to {boneJudgeNewReset.GetTrialCount()}");
            }
        }

        if (showDebugLogs)
        {
            Debug.Log("TaskManage: Selection completed. Auto-restarting experiment...");
        }
        
        // 少し待ってからリスタート（選択の視覚的フィードバックのため）
        StartCoroutine(DelayedRestart());
    }

    /// <summary>
    /// 遅延リスタートのコルーチン
    /// </summary>
    private System.Collections.IEnumerator DelayedRestart()
    {
        yield return new WaitForSeconds(1.0f); // 1秒待機
        RestartExperiment();
    }

    /// <summary>
    /// 質問オブジェクトを非表示にする
    /// </summary>
    private void HideQuestionObjects()
    {
        if (questionObjects == null) return;
        
        for (int i = 0; i < questionObjects.Length; i++)
        {
            if (questionObjects[i] != null)
            {
                questionObjects[i].SetActive(false);
                
                if (showDebugLogs)
                {
                    Debug.Log($"TaskManage: Question object[{i}] deactivated: {questionObjects[i].name}");
                }
            }
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
        // セッション移行中は処理をスキップ
        if (isInSessionTransition) return;
        
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
    /// Y座標の閾値を動的に変更（レガシーメソッド - 新しいコードではUpdateYThresholdAndTargetを使用推奨）
    /// </summary>
    public void SetYThreshold(float newThreshold)
    {
        float previousThreshold = yThreshold;
        yThreshold = newThreshold;
        
        // target_limitオブジェクトも更新（自動更新が有効な場合）
        if (target_limit != null && autoUpdateYThreshold)
        {
            Vector3 currentPos = target_limit.transform.position;
            target_limit.transform.position = new Vector3(currentPos.x, newThreshold, currentPos.z);
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"TaskManage: Y threshold changed from {previousThreshold:F3} to {newThreshold:F3}");
            if (target_limit != null && autoUpdateYThreshold)
            {
                Debug.Log($"TaskManage: target_limit position also updated to Y={newThreshold:F3}");
            }
        }
    }

    /// <summary>
    /// 指定されたキューブがタスク完了状態かどうかを取得
    /// </summary>
    public bool IsCubeTaskCompleted(int cubeIndex)
    {
        if (isCompleted == null || cubeIndex < 0 || cubeIndex >= isCompleted.Length)
            return false;
            
        return isCompleted[cubeIndex];
    }

    /// <summary>
    /// 指定されたキューブが一度閾値に達したかどうかを取得
    /// </summary>
    public bool HasCubeReachedThresholdOnce(int cubeIndex)
    {
        if (hasReachedThresholdOnce == null || cubeIndex < 0 || cubeIndex >= hasReachedThresholdOnce.Length)
            return false;
            
        return hasReachedThresholdOnce[cubeIndex];
    }

    /// <summary>
    /// 指示テキストを現在の状態に応じて更新
    /// </summary>
    private void UpdateInstructionText()
    {
        if (instructionText == null) return;

        // 現在のタスク番号を表示用に計算（1ベース）
        int displayTaskNumber = currentTaskIndex + 1;
        int totalTasksDisplay = (experimentPatterns != null) ? experimentPatterns.Count : totalTasks;
        string taskInfo = $"Task {displayTaskNumber}/{totalTasksDisplay} - ";

        // いずれかのタスクが完了している場合
        if (anyTaskCompleted)
        {
            instructionText.text = taskInfo + finalMessage;
            
            if (showDebugLogs)
            {
                Debug.Log($"TaskManage: Text updated to final message: '{taskInfo + finalMessage}'");
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
            instructionText.text = taskInfo + secondMessage;
            
            if (showDebugLogs)
            {
                Debug.Log($"TaskManage: Text updated to second message: '{taskInfo + secondMessage}'");
            }
        }
        else
        {
            instructionText.text = taskInfo + initialMessage;
            
            if (showDebugLogs)
            {
                Debug.Log($"TaskManage: Text updated to initial message: '{taskInfo + initialMessage}'");
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
    /// セッション2が完了したかどうかを取得
    /// </summary>
    public bool IsSession2Completed()
    {
        return currentSession == 1; // セッション2でキューブが閾値に達した場合
    }

    /// <summary>
    /// 全実験が完了したかどうかを取得
    /// </summary>
    public bool IsAllExperimentsCompleted()
    {
        return experimentCompleted;
    }

    /// <summary>
    /// 現在のタスク情報を取得
    /// </summary>
    public string GetCurrentTaskInfo()
    {
        if (experimentCompleted)
        {
            return $"All experiments completed ({totalTasks}/{totalTasks})";
        }
        return $"Task {currentTaskIndex + 1}/{totalTasks}, Session {currentSession + 1}";
    }

    /// <summary>
    /// 現在のタスクの非ゼロprogressrate値を取得
    /// </summary>
    public float GetCurrentNonZeroProgressRate()
    {
        var currentPattern = GetCurrentPattern();
        if (currentPattern == null)
        {
            return 0f;
        }
        return currentPattern.progressRateValue;
    }

    /// <summary>
    /// 現在のタスクのSession1のProgressRate値を取得
    /// </summary>
    public float GetCurrentSession1ProgressRate()
    {
        var currentPattern = GetCurrentPattern();
        if (currentPattern == null)
        {
            return 0f;
        }
        // Session1が0の場合は0、そうでなければ非ゼロ値
        return currentPattern.zeroInSession1 ? 0f : currentPattern.progressRateValue;
    }

    /// <summary>
    /// 現在のタスクのSession2のProgressRate値を取得
    /// </summary>
    public float GetCurrentSession2ProgressRate()
    {
        var currentPattern = GetCurrentPattern();
        if (currentPattern == null)
        {
            return 0f;
        }
        // Session2が0の場合は0、そうでなければ非ゼロ値
        return currentPattern.zeroInSession1 ? currentPattern.progressRateValue : 0f;
    }

    /// <summary>
    /// 全キューブのタスク状態を取得
    /// </summary>
    public void GetAllTaskStatus()
    {
        if (showDebugLogs)
        {
            Debug.Log("=== TaskManage Status ===");
            
            if (cubes == null || hasReachedThresholdOnce == null || isCompleted == null || defaultPositions == null)
            {
                Debug.Log("TaskManage not yet initialized");
                return;
            }
            
            for (int i = 0; i < cubes.Length; i++)
            {
                if (cubes[i] != null)
                {
                    Debug.Log($"Cube[{i}]: ReachedOnce={hasReachedThresholdOnce[i]}, Completed={isCompleted[i]}, CurrentY={cubes[i].transform.position.y:F2}, DefaultPos={defaultPositions[i]}");
                }
            }
        }
    }
    
    /// <summary>
    /// target_limitオブジェクトのY座標からY thresholdを自動更新
    /// </summary>
    private void UpdateYThresholdFromTargetLimit()
    {
        if (!autoUpdateYThreshold)
        {
            if (showDebugLogs)
            {
                Debug.Log("TaskManage: Auto Y threshold update is disabled");
            }
            return;
        }

        if (target_limit == null)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning("TaskManage: target_limit object is not assigned. Using manual Y threshold value.");
            }
            return;
        }

        float previousThreshold = yThreshold;
        yThreshold = target_limit.transform.position.y;

        if (showDebugLogs)
        {
            Debug.Log($"TaskManage: Y threshold updated from target_limit. Previous: {previousThreshold:F3}, New: {yThreshold:F3}");
            Debug.Log($"TaskManage: target_limit position: {target_limit.transform.position}");
        }
    }
    
    /// <summary>
    /// Y thresholdを手動で更新し、target_limitオブジェクトも更新（必要に応じて）
    /// </summary>
    public void UpdateYThresholdAndTarget(float newThreshold)
    {
        float previousThreshold = yThreshold;
        yThreshold = newThreshold;
        
        // target_limitがある場合は、その位置も更新
        if (target_limit != null && autoUpdateYThreshold)
        {
            Vector3 currentPos = target_limit.transform.position;
            target_limit.transform.position = new Vector3(currentPos.x, newThreshold, currentPos.z);
            
            if (showDebugLogs)
            {
                Debug.Log($"TaskManage: Updated both Y threshold ({previousThreshold:F3} -> {newThreshold:F3}) and target_limit position");
            }
        }
        else
        {
            if (showDebugLogs)
            {
                Debug.Log($"TaskManage: Updated Y threshold only ({previousThreshold:F3} -> {newThreshold:F3})");
            }
        }
    }
    
    /// <summary>
    /// target_limitオブジェクトからY thresholdを強制再取得
    /// </summary>
    public void RefreshYThresholdFromTarget()
    {
        if (target_limit != null)
        {
            UpdateYThresholdFromTargetLimit();
        }
        else
        {
            Debug.LogWarning("TaskManage: Cannot refresh Y threshold - target_limit object is not assigned");
        }
    }
}