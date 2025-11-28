using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CubeMovementController : MonoBehaviour
{
    [Header("References")]
    public BoneJudgeNew bonejudgeNew;
    public GrabJudge grabJudge;

    [Header("Settings")]
    public int cubeIndex = 0;
    public float moveSpeed = 1.0f; // 移動の倍率（1.0で手と同じ移動量）
    
    [Header("Movement Constraints")]
    public bool enableMovement = true;
    public bool usePositionConstraints = false; // 位置制限を使用するかどうか
    public Vector3 minPosition = new Vector3(-10f, -10f, -10f);
    public Vector3 maxPosition = new Vector3(10f, 10f, 10f);
    
    [Header("Grab & Contact Control")]
    public bool requireGrabAndContact = true; // 握り+接触の両方を必要とするか
    public bool requireGrabOnly = false; // 握りのみで動作させるか
    public bool requireContactOnly = false; // 接触のみで動作させるか
    
    [Header("Axis Movement Control")]
    public bool enableXAxisMovement = false; // X軸移動を無効
    public bool enableYAxisMovement = true;  // Y軸移動のみ有効
    public bool enableZAxisMovement = false; // Z軸移動を無効
    
    [Header("Physics Settings")]
    public bool enableGravityControl = true; // 重力制御を有効にするかどうか
    public float normalGravity = -9.8f; // 通常時の重力加速度
    public float contactGravity = 0f; // 接触時の重力加速度
    
    [Header("Debug")]
    public bool showDebugLogs = false;

    private Vector3 previousHandPosition = Vector3.zero;
    private bool firstFrame = true;
    private bool wasContactingLastFrame = false; // 前フレームの接触状態
    private Rigidbody cubeRigidbody; // キューブのRigidbody参照
    private static bool gravityControllerExists = false; // 重力制御の重複防止

    // 初期位置を保持する変数
    private Vector3 initialPosition;

    // 接触しているボーンの3D座標を取得する関数
    private Vector3 GetContactingBonePosition()
    {
        if (bonejudgeNew != null)
        {
            return bonejudgeNew.GetContactingBonePosition(cubeIndex);
        }
        return Vector3.zero;
    }

    // 接触しているボーンのZ座標を取得する関数（後方互換性のため保持）
    private float GetContactingBoneZ()
    {
        Vector3 bonePosition = GetContactingBonePosition();
        return bonePosition.z;
    }

    void Start()
    {
        // 初期位置を記録
        initialPosition = transform.position;
        
        // Rigidbodyコンポーネントを取得
        cubeRigidbody = GetComponent<Rigidbody>();
        if (cubeRigidbody == null && enableGravityControl)
        {
            Debug.LogWarning($"CubeMovementController: Cube {cubeIndex} does not have a Rigidbody component. Gravity control will be disabled.");
            enableGravityControl = false;
        }
        
        // 初期状態では通常の重力を設定（接触していない状態）
        if (enableGravityControl && !gravityControllerExists)
        {
            Physics.gravity = new Vector3(0, normalGravity, 0);
            gravityControllerExists = true;
            
            if (showDebugLogs)
            {
                Debug.Log($"CubeMovementController: Cube {cubeIndex} set initial gravity to {normalGravity} m/s²");
            }
        }
        
        // Rigidbodyの重力使用フラグは常にtrueにしておく
        if (cubeRigidbody != null)
        {
            cubeRigidbody.useGravity = true;
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"CubeMovementController: Cube {cubeIndex} initialized at position {initialPosition}");
        }
    }

    void Update()
    {
        if (!enableMovement || bonejudgeNew == null || bonejudgeNew.isTouching == null)
            return;
            
        if (cubeIndex < 0 || cubeIndex >= bonejudgeNew.isTouching.Length)
            return;

        // 接触状態とつかみ状態をチェック
        bool isContacting = bonejudgeNew.isTouching[cubeIndex];
        bool isGrabbing = CheckGrabCondition();
        
        // 動作条件をチェック
        bool shouldMove = CheckMovementCondition(isContacting, isGrabbing);
        
        if (showDebugLogs)
        {
            Debug.Log($"Cube {cubeIndex}: Contact={isContacting}, Grab={isGrabbing}, ShouldMove={shouldMove}");
        }

        if (shouldMove)
        {
            // 接触開始時の処理
            if (!wasContactingLastFrame && enableGravityControl)
            {
                // 重力加速度を0に設定
                Physics.gravity = new Vector3(0, contactGravity, 0);
                
                // 物理的な速度をリセットして手動制御に移行
                if (cubeRigidbody != null)
                {
                    cubeRigidbody.velocity = Vector3.zero;
                    cubeRigidbody.angularVelocity = Vector3.zero;
                }
                
                if (showDebugLogs)
                {
                    Debug.Log($"Cube {cubeIndex}: Contact started - Gravity acceleration set to {contactGravity} m/s², velocity reset");
                }
            }
            
            wasContactingLastFrame = true;
            
            Vector3 currentHandPosition = GetContactingBonePosition();
            
            if (firstFrame)
            {
                previousHandPosition = currentHandPosition;
                firstFrame = false;
                return;
            }

            // 接触しているボーンの3D移動量を計算
            Vector3 deltaPosition = currentHandPosition - previousHandPosition;
            previousHandPosition = currentHandPosition;

            // ハンドの移動量に移動倍率をかけてCubeを移動
            Vector3 moveAmount = deltaPosition * moveSpeed;
            
            // 軸別移動制御を適用
            if (!enableXAxisMovement) moveAmount.x = 0f;
            if (!enableYAxisMovement) moveAmount.y = 0f;
            if (!enableZAxisMovement) moveAmount.z = 0f;
            
            Vector3 newPosition = transform.position + moveAmount;
            
            // 位置制限が有効な場合のみクランプ処理を適用
            if (usePositionConstraints)
            {
                newPosition.x = Mathf.Clamp(newPosition.x, minPosition.x, maxPosition.x);
                newPosition.y = Mathf.Clamp(newPosition.y, minPosition.y, maxPosition.y);
                newPosition.z = Mathf.Clamp(newPosition.z, minPosition.z, maxPosition.z);
            }
            
            // 位置を更新
            transform.position = newPosition;
            
            // デバッグログ（Hand Movementを常に出力）
            if (moveAmount.magnitude > 0.0001f)
            {
                Debug.Log($"CubeMovementController - Hand Movement Delta: X: {deltaPosition.x:F6}, Y: {deltaPosition.y:F6}, Z: {deltaPosition.z:F6}");
                
                if (showDebugLogs)
                {
                    Debug.Log($"Cube {cubeIndex}: Hand delta = {deltaPosition}, Move amount = {moveAmount}, New position = {newPosition}");
                    Debug.Log($"Cube {cubeIndex}: Axis control - X: {enableXAxisMovement}, Y: {enableYAxisMovement}, Z: {enableZAxisMovement}");
                    if (usePositionConstraints)
                    {
                        Debug.Log($"Cube {cubeIndex}: Position constraints applied. Min: {minPosition}, Max: {maxPosition}");
                    }
                }
            }
        }
        else
        {
            // 動作条件が満たされなくなった時の処理
            if (wasContactingLastFrame && enableGravityControl)
            {
                // 重力加速度を通常値に戻す
                Physics.gravity = new Vector3(0, normalGravity, 0);
                
                if (showDebugLogs)
                {
                    Debug.Log($"Cube {cubeIndex}: Movement condition not met - Gravity acceleration restored to {normalGravity} m/s²");
                }
            }
            
            wasContactingLastFrame = shouldMove;
            if (!shouldMove)
            {
                firstFrame = true;
            }
        }
    }

    void OnDestroy()
    {
        // オブジェクト破棄時に重力を通常値に戻す
        if (enableGravityControl && gravityControllerExists)
        {
            Physics.gravity = new Vector3(0, normalGravity, 0);
            gravityControllerExists = false;
            
            if (showDebugLogs)
            {
                Debug.Log($"CubeMovementController: Cube {cubeIndex} destroyed - Gravity restored to {normalGravity} m/s²");
            }
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
    /// 3D移動を有効/無効化
    /// </summary>
    public void SetMovementEnabled(bool enabled)
    {
        enableMovement = enabled;
        if (!enabled)
        {
            firstFrame = true;
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"Cube {cubeIndex}: 3D movement {(enabled ? "enabled" : "disabled")}");
        }
    }
    
    /// <summary>
    /// 軸別移動制御を設定
    /// </summary>
    public void SetAxisMovement(bool enableX, bool enableY, bool enableZ)
    {
        enableXAxisMovement = enableX;
        enableYAxisMovement = enableY;
        enableZAxisMovement = enableZ;
        
        if (showDebugLogs)
        {
            Debug.Log($"Cube {cubeIndex}: Axis movement set - X: {enableX}, Y: {enableY}, Z: {enableZ}");
        }
    }
    
    /// <summary>
    /// Y軸のみの移動に設定（便利メソッド）
    /// </summary>
    public void SetYAxisOnlyMovement()
    {
        SetAxisMovement(false, true, false);
        
        if (showDebugLogs)
        {
            Debug.Log($"Cube {cubeIndex}: Movement restricted to Y-axis only");
        }
    }
    
    /// <summary>
    /// 全軸移動を有効化
    /// </summary>
    public void SetAllAxisMovement()
    {
        SetAxisMovement(true, true, true);
        
        if (showDebugLogs)
        {
            Debug.Log($"Cube {cubeIndex}: All axis movement enabled");
        }
    }
    
    /// <summary>
    /// Z軸移動を有効/無効化（後方互換性のため保持）
    /// </summary>
    public void SetZMovementEnabled(bool enabled)
    {
        SetMovementEnabled(enabled);
    }
    
    /// <summary>
    /// 初期位置からの3D移動距離を取得
    /// </summary>
    public Vector3 GetMovementDistance()
    {
        return transform.position - initialPosition;
    }
    
    /// <summary>
    /// 初期位置からのZ軸移動距離を取得（後方互換性のため保持）
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
        if (bonejudgeNew == null || bonejudgeNew.isTouching == null)
            return false;
            
        if (cubeIndex < 0 || cubeIndex >= bonejudgeNew.isTouching.Length)
            return false;
            
        return bonejudgeNew.isTouching[cubeIndex];
    }

    /// <summary>
    /// 現在のハンドの動きをデバッグ出力する
    /// </summary>
    public void DebugHandMovement()
    {
        if (!IsCurrentlyTouching()) return;

        Vector3 currentHandPos = GetContactingBonePosition();
        Vector3 deltaFromPrevious = currentHandPos - previousHandPosition;
        
        Debug.Log($"=== Cube {cubeIndex} Hand Movement Debug ===");
        Debug.Log($"Current Hand Position: {currentHandPos}");
        Debug.Log($"Previous Hand Position: {previousHandPosition}");
        Debug.Log($"Delta Position: {deltaFromPrevious}");
        Debug.Log($"Delta X: {deltaFromPrevious.x:F6}, Y: {deltaFromPrevious.y:F6}, Z: {deltaFromPrevious.z:F6}");
        Debug.Log($"Move Speed: {moveSpeed}");
        Debug.Log($"Axis Movement Control - X: {enableXAxisMovement}, Y: {enableYAxisMovement}, Z: {enableZAxisMovement}");
        Debug.Log($"Use Position Constraints: {usePositionConstraints}");
        if (usePositionConstraints)
        {
            Debug.Log($"Min Position: {minPosition}, Max Position: {maxPosition}");
        }
        Debug.Log($"Cube Current Position: {transform.position}");
        
        // 重力加速度状態も表示
        if (enableGravityControl)
        {
            Debug.Log($"Current Gravity Acceleration: {Physics.gravity.y} m/s²");
            Debug.Log($"Normal Gravity: {normalGravity} m/s², Contact Gravity: {contactGravity} m/s²");
            if (cubeRigidbody != null)
            {
                Debug.Log($"Rigidbody useGravity: {cubeRigidbody.useGravity}");
                Debug.Log($"Velocity: {cubeRigidbody.velocity}");
            }
        }
    }

    /// <summary>
    /// 重力加速度を手動で設定する
    /// </summary>
    public void SetGravityAcceleration(float gravityY)
    {
        if (!enableGravityControl) return;
        
        Physics.gravity = new Vector3(0, gravityY, 0);
        
        if (showDebugLogs)
        {
            Debug.Log($"Cube {cubeIndex}: Gravity acceleration manually set to {gravityY} m/s²");
        }
    }

    /// <summary>
    /// 現在の重力加速度を取得
    /// </summary>
    public float GetCurrentGravityAcceleration()
    {
        return Physics.gravity.y;
    }

    /// <summary>
    /// 通常重力に戻す
    /// </summary>
    public void RestoreNormalGravity()
    {
        SetGravityAcceleration(normalGravity);
    }

    /// <summary>
    /// 接触時重力に設定
    /// </summary>
    public void SetContactGravity()
    {
        SetGravityAcceleration(contactGravity);
    }

    /// <summary>
    /// 重力制御機能を有効/無効化
    /// </summary>
    public void SetGravityControlEnabled(bool enabled)
    {
        enableGravityControl = enabled;
        
        if (!enabled)
        {
            // 重力制御を無効にする場合は、重力を通常状態に戻す
            Physics.gravity = new Vector3(0, normalGravity, 0);
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"Cube {cubeIndex}: Gravity control {(enabled ? "enabled" : "disabled")}");
        }
    }

    /// <summary>
    /// つかみ条件をチェック
    /// </summary>
    /// <returns>つかみ条件が満たされている場合true</returns>
    private bool CheckGrabCondition()
    {
        if (grabJudge == null)
            return false;

        // どちらかの手がつかんでいるかをチェック
        return grabJudge.IsAnyHandGrabbing();
    }

    /// <summary>
    /// 動作条件をチェック（接触+つかみの組み合わせ）
    /// </summary>
    /// <param name="isContacting">接触しているか</param>
    /// <param name="isGrabbing">つかんでいるか</param>
    /// <returns>動作させるべき場合true</returns>
    private bool CheckMovementCondition(bool isContacting, bool isGrabbing)
    {
        if (requireGrabAndContact)
        {
            // つかみ+接触の両方が必要
            return isContacting && isGrabbing;
        }
        else if (requireGrabOnly)
        {
            // つかみのみで動作
            return isGrabbing;
        }
        else if (requireContactOnly)
        {
            // 接触のみで動作
            return isContacting;
        }
        else
        {
            // デフォルト: つかみ+接触の両方が必要
            return isContacting && isGrabbing;
        }
    }

    /// <summary>
    /// 現在の動作状態を取得
    /// </summary>
    /// <returns>動作状態の情報</returns>
    public string GetMovementStatus()
    {
        if (bonejudgeNew == null || grabJudge == null)
            return "References not set";

        bool isContacting = bonejudgeNew.isTouching != null && 
                           cubeIndex >= 0 && cubeIndex < bonejudgeNew.isTouching.Length && 
                           bonejudgeNew.isTouching[cubeIndex];
        bool isGrabbing = CheckGrabCondition();
        bool shouldMove = CheckMovementCondition(isContacting, isGrabbing);

        return $"Contact: {isContacting}, Grab: {isGrabbing}, Moving: {shouldMove}";
    }

}