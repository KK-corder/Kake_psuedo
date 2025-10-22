using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoneJudgeNew_reset : MonoBehaviour
{
    [Header("Hand Skeleton Settings")]
    // 右手と左手のスケルトン（OVRHandPrefabに設定）
    public OVRSkeleton rightHandSkeleton;
    public OVRSkeleton leftHandSkeleton;
    


    [Header("Target Objects")]
    // 接触オブジェクト（CubeやSphereなど）をインスペクターでアタッチ
    public GameObject[] cubes = new GameObject[3];



    [Header("Contact Settings")]
    // 各オブジェクトに対する接触状態
    [HideInInspector] public bool[] isTouching;
    // 各オブジェクトに対応する固定された最も近いボーン
    [HideInInspector] public Transform[] fixedClosestBones;
    // 各オブジェクトに対応する接触点
    [HideInInspector] public Vector3[] touchPoints;



    [Header("Visual Feedback")]
    // Optional: change cube material on first contact
    public Material contactMaterial;
    public Material selectedMaterial; // 選択時のマテリアル
    private Material[] originalMaterials;

    [Header("External References")]
    public TaskManage taskManage; // TaskManageの参照
    public CSVloader csvLoader; // CSVLoaderの参照
    public ContactProgressController contactProgressController; // ProgressRateを取得するための参照
    public BoneJudgeNew_2afc boneJudgeNew2afc; // 2AFC用スクリプトの参照（マテリアル変化判定用）

    [Header("Trial Settings")]
    public string userID = "User01"; // ユーザーID

    [Header("Debug")]
    // For debug visibility
    public bool logContactEvents = true;

    // 選択状態管理
    private int selectedObjectIndex = -1; // 現在選択されているオブジェクトのインデックス（-1は未選択）
    private int trialCount = 0; // トライアル回数
    
    // CSV出力用の一時データ保存
    private string pendingWeight = ""; // 選択結果の一時保存
    private string pendingSession1ProgressRate = ""; // Session1のProgressRateの一時保存
    private string pendingSession2ProgressRate = ""; // Session2のProgressRateの一時保存
    private string pending2afcResult = ""; // 2AFC結果の一時保存（1または2）
    
    // リセットボタン重複実行防止用
    private bool isResetProcessing = false; // リセット処理中フラグ
    private float resetCooldownTime = 0.5f; // リセット後のクールダウン時間（秒）
    private float lastResetTime = 0f; // 最後のリセット実行時刻

    void Start()
    {
        // 配列の初期化
        int count = Mathf.Max(1, cubes != null ? cubes.Length : 1);
        isTouching = new bool[count];
        fixedClosestBones = new Transform[count];
        touchPoints = new Vector3[count];

        // cache original materials
        originalMaterials = new Material[count];
        for (int i = 0; i < count; i++)
        {
            var r = (cubes != null && i < cubes.Length && cubes[i] != null) ? cubes[i].GetComponent<Renderer>() : null;
            originalMaterials[i] = (r != null) ? r.sharedMaterial : null;
        }

        // 初期選択状態
        selectedObjectIndex = -1;

        if (logContactEvents)
        {
            Debug.Log($"BoneJudgeNew_2afc: Initialized with {count} objects");
        }
    }

    void Update()
    {
        if ((rightHandSkeleton == null || rightHandSkeleton.Bones == null) && 
            (leftHandSkeleton == null || leftHandSkeleton.Bones == null))
            return;
            
        if (cubes == null)
            return;

        HandleObjectCollisions();
    }

    private void HandleObjectCollisions()
    {
        if (cubes == null || cubes.Length == 0)
            return;

        // 両手のボーンを統合したリストを作成
        List<OVRBone> allBones = new List<OVRBone>();

        if (rightHandSkeleton != null && rightHandSkeleton.Bones != null)
        {
            allBones.AddRange(rightHandSkeleton.Bones);
        }

        if (leftHandSkeleton != null && leftHandSkeleton.Bones != null)
        {
            allBones.AddRange(leftHandSkeleton.Bones);
        }

        if (allBones.Count == 0) return;

        // Cubeの当たり判定を拡張する倍率
        float enlargementFactor = 2.0f;

        for (int cubeIndex = 0; cubeIndex < cubes.Length; cubeIndex++)
        {
            if (cubes[cubeIndex] == null) continue;

            bool currentlyTouching = false;
            Transform closestBone = null;
            float minDistance = float.MaxValue;
            Vector3 closestTouchPoint = Vector3.zero;

            // Cube の Collider を取得
            Collider cubeCollider = cubes[cubeIndex].GetComponent<Collider>();
            if (cubeCollider == null) continue;

            // 両手の各ボーンとの距離をチェック
            foreach (var bone in allBones)
            {
                if (bone.Transform == null) continue;

                Vector3 bonePosition = bone.Transform.position;
                Vector3 closestPoint = cubeCollider.ClosestPoint(bonePosition);
                float distance = Vector3.Distance(bonePosition, closestPoint);

                // 拡張された当たり判定範囲内かチェック
                Bounds enlargedBounds = cubeCollider.bounds;
                enlargedBounds.Expand(enlargedBounds.size * (enlargementFactor - 1f));

                if (enlargedBounds.Contains(bonePosition))
                {
                    currentlyTouching = true;
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestBone = bone.Transform;
                        closestTouchPoint = closestPoint;
                    }
                }
            }

            // 接触状態の更新
            bool wasNotTouching = !isTouching[cubeIndex];
            isTouching[cubeIndex] = currentlyTouching;

            if (currentlyTouching)
            {
                // 初回接触時または最も近いボーンが変わった場合
                if (wasNotTouching || fixedClosestBones[cubeIndex] != closestBone)
                {
                    fixedClosestBones[cubeIndex] = closestBone;
                    touchPoints[cubeIndex] = closestTouchPoint;

                    if (logContactEvents)
                    {
                        string handType = GetHandTypeFromBone(closestBone);
                        Debug.Log($"Contact started with Cube {cubeIndex} using {handType} bone: {closestBone.name}");
                    }

                    // 接触開始時に即座に選択
                    if (wasNotTouching)
                    {
                        SelectObject(cubeIndex);
                    }
                }
            }
            else if (wasNotTouching == false) // 接触が終了した場合
            {
                fixedClosestBones[cubeIndex] = null;

                if (logContactEvents)
                {
                    Debug.Log($"Contact ended with Cube {cubeIndex}");
                }

                // 選択されていないオブジェクトの場合は元のマテリアルに戻す
                if (selectedObjectIndex != cubeIndex)
                {
                    RestoreOriginalMaterial(cubeIndex);
                }
            }
        }
    }
    
    
    /// 指定されたインデックスの接触しているボーンの位置を取得
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
    
    /// <summary>
    /// いずれかのオブジェクトと接触しているかチェック
    /// </summary>
    /// <returns>接触している場合true</returns>
    public bool IsAnyContact()
    {
        if (isTouching == null) return false;
        
        foreach (bool touching in isTouching)
        {
            if (touching) return true;
        }
        return false;
    }
    
    /// <summary>
    /// 指定されたボーンがどちらの手に属するかを判定
    /// </summary>
    /// <param name="boneTransform">判定するボーン</param>
    /// <returns>右手、左手、または不明</returns>
    private string GetHandTypeFromBone(Transform boneTransform)
    {
        if (rightHandSkeleton != null && rightHandSkeleton.Bones != null)
        {
            foreach (var bone in rightHandSkeleton.Bones)
            {
                if (bone.Transform == boneTransform)
                {
                    return "Right Hand";
                }
            }
        }
        
        if (leftHandSkeleton != null && leftHandSkeleton.Bones != null)
        {
            foreach (var bone in leftHandSkeleton.Bones)
            {
                if (bone.Transform == boneTransform)
                {
                    return "Left Hand";
                }
            }
        }
        
        return "Unknown Hand";
    }
    
    /// <summary>
    /// 右手のスケルトンを取得
    /// </summary>
    /// <returns>右手のOVRSkeleton</returns>
    public OVRSkeleton GetRightHandSkeleton()
    {
        return rightHandSkeleton;
    }
    
    /// <summary>
    /// 左手のスケルトンを取得
    /// </summary>
    /// <returns>左手のOVRSkeleton</returns>
    public OVRSkeleton GetLeftHandSkeleton()
    {
        return leftHandSkeleton;
    }
    
    /// <summary>
    /// 指定されたボーンがどちらの手に属するかを判定（public版）
    /// </summary>
    /// <param name="cubeIndex">Cubeのインデックス</param>
    /// <returns>右手、左手、または不明</returns>
    public string GetContactingHandType(int cubeIndex)
    {
        if (cubeIndex < 0 || cubeIndex >= fixedClosestBones.Length)
            return "Invalid Index";
            
        if (isTouching != null && cubeIndex < isTouching.Length && isTouching[cubeIndex])
        {
            if (fixedClosestBones[cubeIndex] != null)
            {
                return GetHandTypeFromBone(fixedClosestBones[cubeIndex]);
            }
        }
        
        return "No Contact";
    }

    /// <summary>
    /// オブジェクトを選択する
    /// </summary>
    /// <param name="objectIndex">選択するオブジェクトのインデックス</param>
    private void SelectObject(int objectIndex)
    {
        if (objectIndex < 0 || objectIndex >= cubes.Length || cubes[objectIndex] == null)
            return;

        // 既に選択されているオブジェクトがある場合は、そのマテリアルを元に戻す
        if (selectedObjectIndex >= 0 && selectedObjectIndex != objectIndex)
        {
            RestoreOriginalMaterial(selectedObjectIndex);
            
            if (logContactEvents)
            {
                Debug.Log($"BoneJudgeNew_reset: Deselected object {selectedObjectIndex}");
            }
        }

        // 新しいオブジェクトを選択
        selectedObjectIndex = objectIndex;

        // 選択マテリアルを適用
        if (selectedMaterial != null)
        {
            var renderer = cubes[objectIndex].GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = selectedMaterial;
            }
        }

        if (logContactEvents)
        {
            Debug.Log($"BoneJudgeNew_reset: Selected object {objectIndex}");
        }

        // 選択が確定したら一連の処理を実行
        OnSelectionConfirmed(objectIndex);
    }

    /// <summary>
    /// 選択確定時の処理
    /// </summary>
    /// <param name="selectedIndex">選択されたオブジェクトのインデックス</param>
    private void OnSelectionConfirmed(int selectedIndex)
    {
        // 2AFCの結果を一時保存（CSV出力はリセット時に行う）
        // インデックスを1ベースに変換（0→1, 1→2, 2→3）
        pendingWeight = (selectedIndex + 1).ToString();

        // 2AFC結果を一時保存（初期値：未選択）
        pending2afcResult = "0";

        // Session1とSession2のProgressRateを取得
        pendingSession1ProgressRate = "0.0";
        pendingSession2ProgressRate = "0.0";
        
        if (taskManage != null)
        {
            // TaskManageから現在のタスクのSession1とSession2のprogressrate値を取得
            float session1ProgressRate = taskManage.GetCurrentSession1ProgressRate();
            float session2ProgressRate = taskManage.GetCurrentSession2ProgressRate();
            
            pendingSession1ProgressRate = session1ProgressRate.ToString();
            pendingSession2ProgressRate = session2ProgressRate.ToString();
        }
        else if (contactProgressController != null && contactProgressController.progressIncreaseRate != null)
        {
            // フォールバック: 従来の方法
            if (selectedIndex < contactProgressController.progressIncreaseRate.Length)
            {
                pendingSession1ProgressRate = contactProgressController.progressIncreaseRate[selectedIndex].ToString();
            }
        }

        if (logContactEvents)
        {
            Debug.Log($"BoneJudgeNew_reset: Selection confirmed. Weight={pendingWeight}, Session1Rate={pendingSession1ProgressRate}, Session2Rate={pendingSession2ProgressRate}. Waiting for reset to save CSV.");
        }

        // TaskManageに選択完了を通知
        if (taskManage != null)
        {
            // 全実験完了済みの場合は何もしない
            if (taskManage.IsAllExperimentsCompleted())
            {
                if (logContactEvents)
                {
                    Debug.Log($"BoneJudgeNew_reset: All experiments completed. No further action.");
                }
                return;
            }

            // セッション2完了後の場合は次のタスクに進む
            if (taskManage.IsSession2Completed())
            {
                taskManage.OnSelectionCompleted();
                
                if (logContactEvents)
                {
                    Debug.Log($"BoneJudgeNew_reset: Session 2 completed. Moving to next task. Trial {trialCount} completed.");
                }
            }
            else
            {
                // 通常の試行完了処理
                taskManage.OnTrialCompleted();
                taskManage.ResetAllCubeTasks();
                
                if (logContactEvents)
                {
                    Debug.Log($"BoneJudgeNew_reset: TaskManage reset triggered. Trial {trialCount} completed.");
                }
            }
        }

        // 選択状態をリセット
        ResetSelection();
    }

    /// <summary>
    /// CSVにデータを保存
    /// </summary>
    /// <param name="resetChoice">リセット時の選択結果</param>
    /// <param name="session1Rate">Session1のProgressRate値</param>
    /// <param name="session2Rate">Session2のProgressRate値</param>
    /// <param name="afc2Result">2AFC結果（マテリアル変化したオブジェクト）</param>
    private void SaveDataToCSV(string resetChoice, string session1Rate, string session2Rate, string afc2Result = "0")
    {
        if (csvLoader == null)
        {
            Debug.LogWarning("BoneJudgeNew_reset: CSVLoader reference is null. Cannot save data.");
            return;
        }
        
        try
        {
            // CSVLoaderのSaveDataメソッドを呼び出し
            csvLoader.SaveData(
                userID,                    // UserID
                trialCount.ToString(),     // TrialCount
                session1Rate,              // Session1ProgressRate
                session2Rate,              // Session2ProgressRate
                afc2Result                 // Weight (2AFCでマテリアル変化したオブジェクト)
            );

            if (logContactEvents)
            {
                Debug.Log($"BoneJudgeNew_reset: Data saved to CSV - Trial: {trialCount}, Weight(2AFC): {afc2Result}, Session1Rate: {session1Rate}, Session2Rate: {session2Rate}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"BoneJudgeNew_reset: Failed to save data to CSV. Error: {e.Message}");
        }
    }

    /// <summary>
    /// 指定されたオブジェクトのマテリアルを元に戻す
    /// </summary>
    /// <param name="objectIndex">オブジェクトのインデックス</param>
    private void RestoreOriginalMaterial(int objectIndex)
    {
        if (objectIndex < 0 || objectIndex >= cubes.Length || cubes[objectIndex] == null)
            return;

        if (originalMaterials[objectIndex] != null)
        {
            var renderer = cubes[objectIndex].GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = originalMaterials[objectIndex];
            }
        }
    }

    /// <summary>
    /// 現在選択されているオブジェクトのインデックスを取得
    /// </summary>
    /// <returns>選択されているオブジェクトのインデックス（未選択の場合は-1）</returns>
    public int GetSelectedObjectIndex()
    {
        return selectedObjectIndex;
    }

    /// <summary>
    /// 指定されたオブジェクトが選択されているかチェック
    /// </summary>
    /// <param name="objectIndex">チェックするオブジェクトのインデックス</param>
    /// <returns>選択されている場合true</returns>
    public bool IsObjectSelected(int objectIndex)
    {
        return selectedObjectIndex == objectIndex;
    }

    /// <summary>
    /// 選択状態をリセット
    /// </summary>
    public void ResetSelection()
    {
        if (selectedObjectIndex >= 0)
        {
            RestoreOriginalMaterial(selectedObjectIndex);
            selectedObjectIndex = -1;
            
            if (logContactEvents)
            {
                Debug.Log("BoneJudgeNew_reset: Selection reset");
            }
        }
    }

    /// <summary>
    /// 指定されたオブジェクトを手動で選択
    /// </summary>
    /// <param name="objectIndex">選択するオブジェクトのインデックス</param>
    public void ManualSelectObject(int objectIndex)
    {
        SelectObject(objectIndex);
    }

    /// <summary>
    /// 現在のトライアル数を取得
    /// </summary>
    /// <returns>現在のトライアル数</returns>
    public int GetTrialCount()
    {
        return trialCount;
    }

    /// <summary>
    /// トライアル数を手動でリセット
    /// </summary>
    public void ResetTrialCount()
    {
        trialCount = 0;
        
        if (logContactEvents)
        {
            Debug.Log("BoneJudgeNew_reset: Trial count reset to 0");
        }
    }

    /// <summary>
    /// ユーザーIDを設定
    /// </summary>
    /// <param name="newUserID">新しいユーザーID</param>
    public void SetUserID(string newUserID)
    {
        userID = newUserID;
        
        if (logContactEvents)
        {
            Debug.Log($"BoneJudgeNew_reset: User ID set to {userID}");
        }
    }

    /// <summary>
    /// 実験をリセット（全ての状態をクリア）
    /// </summary>
    public void ResetExperiment()
    {
        ResetSelection();
        ResetTrialCount();
        
        if (taskManage != null)
        {
            taskManage.ResetAllCubeTasks();
        }
        
        if (logContactEvents)
        {
            Debug.Log("BoneJudgeNew_reset: Experiment fully reset");
        }
    }

    /// <summary>
    /// トライアルカウントを増加（リセット時に呼ばれる）
    /// </summary>
    public void IncrementTrialCount()
    {
        // リセット処理中またはクールダウン中の場合は実行しない
        if (isResetProcessing || Time.time - lastResetTime < resetCooldownTime)
        {
            if (logContactEvents)
            {
                Debug.Log($"BoneJudgeNew_reset: Reset ignored - processing: {isResetProcessing}, cooldown remaining: {resetCooldownTime - (Time.time - lastResetTime):F2}s");
            }
            return;
        }

        // リセット処理開始
        isResetProcessing = true;
        lastResetTime = Time.time;
        
        trialCount++;
        
        // リセット時に2AFCマテリアル変化状態を取得
        pending2afcResult = "0";
        if (boneJudgeNew2afc != null)
        {
            int materialChangedIndex = boneJudgeNew2afc.GetMaterialChangedObjectIndex();
            pending2afcResult = materialChangedIndex.ToString();
            
            if (logContactEvents)
            {
                Debug.Log($"BoneJudgeNew_reset: Material changed object index: {materialChangedIndex}");
            }
        }
        else
        {
            Debug.LogWarning("BoneJudgeNew_reset: boneJudgeNew2afc reference is null");
        }
        
        // リセット時にCSV出力を実行
        if (!string.IsNullOrEmpty(pendingWeight) && !string.IsNullOrEmpty(pendingSession1ProgressRate) && !string.IsNullOrEmpty(pendingSession2ProgressRate))
        {
            SaveDataToCSV(pendingWeight, pendingSession1ProgressRate, pendingSession2ProgressRate, pending2afcResult);
            
            if (logContactEvents)
            {
                Debug.Log($"BoneJudgeNew_reset: CSV saved on reset. Weight(Reset): {pendingWeight}, Session1Rate: {pendingSession1ProgressRate}, Session2Rate: {pendingSession2ProgressRate}, 2AFC: {pending2afcResult}");
            }
            
            // 一時データをクリア
            pendingWeight = "";
            pendingSession1ProgressRate = "";
            pendingSession2ProgressRate = "";
            pending2afcResult = "";
        }
        
        if (logContactEvents)
        {
            Debug.Log($"BoneJudgeNew_reset: Trial count incremented to {trialCount}");
        }

        // TaskManageには次のタスクへの切り替えは要求しない
        // （SelectObject内で既にOnSelectionCompleted()が呼ばれているため）
        if (logContactEvents)
        {
            Debug.Log($"BoneJudgeNew_reset: Trial count increment completed. Task transition handled by SelectObject.");
        }

        // 少し遅延してリセット処理完了フラグをクリア
        StartCoroutine(ResetProcessingFlag());
    }

    /// <summary>
    /// リセット処理完了後にフラグをクリアするコルーチン
    /// </summary>
    private System.Collections.IEnumerator ResetProcessingFlag()
    {
        yield return new WaitForSeconds(0.1f); // 短い遅延
        isResetProcessing = false;
        
        if (logContactEvents)
        {
            Debug.Log($"BoneJudgeNew_reset: Reset processing flag cleared");
        }
    }
}