using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CubeMovementController : MonoBehaviour
{
    [Header("References")]
    public BoneJudge boneJudge;

    [Header("Settings")]
    public int cubeIndex = 0;
    public float moveSpeed = 1.0f; // 移動の倍率（1.0で手と同じ移動量）
    
    [Header("Movement Constraints")]
    public bool enableZMovement = true;
    public float minZPosition = -5f;
    public float maxZPosition = 5f;
    
    [Header("Debug")]
    public bool showDebugLogs = false;

    private float previousHandZ = 0f;
    private bool firstFrame = true;

    // 初期位置を保持する変数
    private Vector3 initialPosition;

    // 接触しているボーンのZ座標を取得する関数
    private float GetContactingBoneZ()
    {
        if (boneJudge != null)
        {
            Vector3 bonePosition = boneJudge.GetContactingBonePosition(cubeIndex);
            return bonePosition.z;
        }
        return 0f;
    }

    void Start()
    {
        // 初期位置を記録
        initialPosition = transform.position;
        
        if (showDebugLogs)
        {
            Debug.Log($"CubeMovementController: Cube {cubeIndex} initialized at position {initialPosition}");
        }
    }

    void Update()
    {
        if (!enableZMovement || boneJudge == null || boneJudge.isTouching == null)
            return;
            
        if (cubeIndex < 0 || cubeIndex >= boneJudge.isTouching.Length)
            return;

        if (boneJudge.isTouching[cubeIndex])
        {
            float currentBoneZ = GetContactingBoneZ();
            if (firstFrame)
            {
                previousHandZ = currentBoneZ;
                firstFrame = false;
                return;
            }

            // 接触しているボーンのZ軸変化量を計算
            float delta = currentBoneZ - previousHandZ;
            previousHandZ = currentBoneZ;

            // シンプルにボーンの移動量に移動倍率をかけてCubeを移動
            float moveAmount = delta * moveSpeed;
            Vector3 newPosition = transform.position + Vector3.forward * moveAmount;
            
            // Z座標の制限を適用
            newPosition.z = Mathf.Clamp(newPosition.z, minZPosition, maxZPosition);
            
            // 位置を更新
            transform.position = newPosition;
            
            // デバッグログ
            if (showDebugLogs && Mathf.Abs(moveAmount) > 0.0001f)
            {
                Debug.Log($"Cube {cubeIndex}: Bone Z delta = {delta:F4}, Move amount = {moveAmount:F4}, New Z = {newPosition.z:F4}");
            }
        }
        else
        {
            firstFrame = true;
        }
    }

    /// <summary>
    /// Cubeを初期位置にリセット
    /// </summary>
    public void ResetToInitialPosition()
    {
        transform.position = initialPosition;
        firstFrame = true;
        
        if (showDebugLogs)
        {
            Debug.Log($"Cube {cubeIndex}: Reset to initial position {initialPosition}");
        }
    }
    
    /// <summary>
    /// Z軸移動を有効/無効化
    /// </summary>
    public void SetZMovementEnabled(bool enabled)
    {
        enableZMovement = enabled;
        if (!enabled)
        {
            firstFrame = true;
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"Cube {cubeIndex}: Z movement {(enabled ? "enabled" : "disabled")}");
        }
    }
    
    /// <summary>
    /// 初期位置からのZ軸移動距離を取得
    /// </summary>
    public float GetZMovementDistance()
    {
        return transform.position.z - initialPosition.z;
    }
    
    /// <summary>
    /// 現在接触中かどうかを取得
    /// </summary>
    public bool IsCurrentlyTouching()
    {
        if (boneJudge == null || boneJudge.isTouching == null)
            return false;
            
        if (cubeIndex < 0 || cubeIndex >= boneJudge.isTouching.Length)
            return false;
            
        return boneJudge.isTouching[cubeIndex];
    }

    // 初期位置からのz座標の移動距離を計算するメソッド
    // public float GetMovedDistance()
    // {
    //     //return Mathf.Abs(transform.position.z - cubeResetButton.defaultPositions[0].z);
    // }
}