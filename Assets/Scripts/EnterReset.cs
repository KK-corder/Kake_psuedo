using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enterキーを押すことで指定されたオブジェクトを初期位置にリセットするスクリプト
/// </summary>
public class EnterReset : MonoBehaviour
{
    [Header("Reset Target Objects")]
    [SerializeField] private GameObject object1;
    [SerializeField] private GameObject object2;
    [SerializeField] private GameObject object3;

    [Header("Reset Settings")]
    [SerializeField] private bool resetPosition = true;    // 位置をリセットするか
    [SerializeField] private bool resetRotation = false;   // 回転もリセットするか
    [SerializeField] private bool resetScale = false;      // スケールもリセットするか

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // 初期座標を保存する配列
    private Vector3[] initialPositions = new Vector3[3];
    private Quaternion[] initialRotations = new Quaternion[3];
    private Vector3[] initialScales = new Vector3[3];
    
    // オブジェクトの配列（内部管理用）
    private GameObject[] targetObjects = new GameObject[3];

    void Start()
    {
        InitializeObjects();
        RecordInitialTransforms();
    }

    void Update()
    {
        // Enterキーの入力を検知
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            ResetObjectsToInitialPosition();
        }
    }

    /// <summary>
    /// オブジェクトの初期化
    /// </summary>
    private void InitializeObjects()
    {
        targetObjects[0] = object1;
        targetObjects[1] = object2;
        targetObjects[2] = object3;

        if (showDebugLogs)
        {
            Debug.Log("EnterReset: Objects initialized");
            for (int i = 0; i < targetObjects.Length; i++)
            {
                if (targetObjects[i] != null)
                {
                    Debug.Log($"Object {i + 1}: {targetObjects[i].name}");
                }
                else
                {
                    Debug.Log($"Object {i + 1}: Not assigned");
                }
            }
        }
    }

    /// <summary>
    /// 各オブジェクトの初期Transform情報を記録
    /// </summary>
    private void RecordInitialTransforms()
    {
        for (int i = 0; i < targetObjects.Length; i++)
        {
            if (targetObjects[i] != null)
            {
                // 初期位置を記録
                initialPositions[i] = targetObjects[i].transform.position;
                initialRotations[i] = targetObjects[i].transform.rotation;
                initialScales[i] = targetObjects[i].transform.localScale;

                if (showDebugLogs)
                {
                    Debug.Log($"EnterReset: Recorded initial transform for {targetObjects[i].name}");
                    Debug.Log($"  Position: {initialPositions[i]}");
                    Debug.Log($"  Rotation: {initialRotations[i].eulerAngles}");
                    Debug.Log($"  Scale: {initialScales[i]}");
                }
            }
        }

        if (showDebugLogs)
        {
            Debug.Log("EnterReset: All initial transforms recorded. Press Enter to reset objects.");
        }
    }

    /// <summary>
    /// すべてのオブジェクトを初期位置にリセット
    /// </summary>
    private void ResetObjectsToInitialPosition()
    {
        int resetCount = 0;

        for (int i = 0; i < targetObjects.Length; i++)
        {
            if (targetObjects[i] != null)
            {
                // 位置をリセット
                if (resetPosition)
                {
                    targetObjects[i].transform.position = initialPositions[i];
                }

                // 回転をリセット
                if (resetRotation)
                {
                    targetObjects[i].transform.rotation = initialRotations[i];
                }

                // スケールをリセット
                if (resetScale)
                {
                    targetObjects[i].transform.localScale = initialScales[i];
                }

                // Rigidbodyがある場合は物理的な速度もリセット
                Rigidbody rb = targetObjects[i].GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                resetCount++;

                if (showDebugLogs)
                {
                    Debug.Log($"EnterReset: Reset {targetObjects[i].name} to initial transform");
                }
            }
        }

        if (showDebugLogs)
        {
            Debug.Log($"EnterReset: Reset completed for {resetCount} objects");
        }
    }

    /// <summary>
    /// 初期位置を再記録（ランタイム中に呼び出し可能）
    /// </summary>
    public void UpdateInitialTransforms()
    {
        RecordInitialTransforms();
        
        if (showDebugLogs)
        {
            Debug.Log("EnterReset: Initial transforms updated");
        }
    }

    /// <summary>
    /// 手動でリセットを実行（外部から呼び出し可能）
    /// </summary>
    public void ManualReset()
    {
        ResetObjectsToInitialPosition();
    }

    /// <summary>
    /// 特定のオブジェクトのみをリセット
    /// </summary>
    /// <param name="objectIndex">オブジェクトのインデックス（0-2）</param>
    public void ResetSpecificObject(int objectIndex)
    {
        if (objectIndex < 0 || objectIndex >= targetObjects.Length)
        {
            Debug.LogWarning($"EnterReset: Invalid object index {objectIndex}");
            return;
        }

        if (targetObjects[objectIndex] != null)
        {
            if (resetPosition)
            {
                targetObjects[objectIndex].transform.position = initialPositions[objectIndex];
            }

            if (resetRotation)
            {
                targetObjects[objectIndex].transform.rotation = initialRotations[objectIndex];
            }

            if (resetScale)
            {
                targetObjects[objectIndex].transform.localScale = initialScales[objectIndex];
            }

            // Rigidbodyがある場合は物理的な速度もリセット
            Rigidbody rb = targetObjects[objectIndex].GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            if (showDebugLogs)
            {
                Debug.Log($"EnterReset: Reset {targetObjects[objectIndex].name} to initial transform");
            }
        }
    }

    /// <summary>
    /// リセット設定を変更
    /// </summary>
    public void SetResetSettings(bool position, bool rotation, bool scale)
    {
        resetPosition = position;
        resetRotation = rotation;
        resetScale = scale;

        if (showDebugLogs)
        {
            Debug.Log($"EnterReset: Settings updated - Position: {position}, Rotation: {rotation}, Scale: {scale}");
        }
    }
}