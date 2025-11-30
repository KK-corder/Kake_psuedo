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
    
    [Header("Grab & Contact Control")]
    public bool requireGrabAndContact = true; // 握り+接触の両方を必要とするか
    public bool requireGrabOnly = false; // 握りのみで動作させるか
    public bool requireContactOnly = false; // 接触のみで動作させるか
    
    [Header("Hand Matching Control")]
    public bool showHandMatchingDebug = false; // 手の一致判定のデバッグ情報を表示
    
    [Header("Axis Movement Control")]
    public bool enableXAxisMovement = false; // X軸移動を無効
    public bool enableYAxisMovement = true;  // Y軸移動のみ有効
    public bool enableZAxisMovement = false; // Z軸移動を無効
    
    [Header("Rotation Control")]
    public bool enableRotation = false; // 回転制御を有効にするか
    public float rotationSpeed = 1.0f; // 回転の倍率
    public bool enableXAxisRotation = true; // X軸回転を有効
    public bool enableYAxisRotation = true; // Y軸回転を有効
    public bool enableZAxisRotation = true; // Z軸回転を有効
    
    [Header("Physics Settings")]
    public bool enableGravityControl = true; // 重力制御を有効にするかどうか
    public float normalGravity = -9.8f; // 通常時の重力加速度
    public float contactGravity = 0f; // 接触時の重力加速度
    
    [Header("Debug")]
    public bool showDebugLogs = false;

    private Vector3 previousHandPosition = Vector3.zero;
    private Quaternion previousHandRotation = Quaternion.identity;
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
    
    // 接触しているボーンの回転を取得する関数
    private Quaternion GetContactingBoneRotation()
    {
        if (bonejudgeNew != null)
        {
            return bonejudgeNew.GetContactingBoneRotation(cubeIndex);
        }
        return Quaternion.identity;
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
        
        // 初期状態では重力を有効にしておく（接触していない状態）
        if (cubeRigidbody != null)
        {
            cubeRigidbody.useGravity = true; // 初期状態は重力有効
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"CubeMovementController: Cube {cubeIndex} initialized at position {initialPosition}");
        }
    }

    void Update()
    {
        if (bonejudgeNew == null || bonejudgeNew.isTouching == null)
            return;
            
        if (cubeIndex < 0 || cubeIndex >= bonejudgeNew.isTouching.Length)
            return;

        // 接触状態とつかみ状態をチェック
        bool isContacting = bonejudgeNew.isTouching[cubeIndex];
        bool isGrabbing = CheckGrabCondition();
        
        // 手の一致判定をチェック
        bool handsMatch = CheckHandMatching(cubeIndex);
        
        // 動作条件をチェック
        bool shouldMove = CheckMovementCondition(isContacting, isGrabbing, handsMatch);
        
        if (showDebugLogs)
        {
            Debug.Log($"Cube {cubeIndex}: Contact={isContacting}, Grab={isGrabbing}, HandsMatch={handsMatch}, ShouldMove={shouldMove}");
        }

        if (shouldMove)
        {
            // 接触開始時の処理
            if (!wasContactingLastFrame && enableGravityControl)
            {
                // このオブジェクトの重力を無効化（個別制御）
                if (cubeRigidbody != null)
                {
                    cubeRigidbody.useGravity = false; // 重力を無効化
                    cubeRigidbody.velocity = Vector3.zero; // 速度リセット
                    cubeRigidbody.angularVelocity = Vector3.zero; // 角速度リセット
                }
                
                if (showDebugLogs)
                {
                    Debug.Log($"Cube {cubeIndex}: Contact+Grab started - Gravity disabled, velocity reset");
                }
            }
            
            wasContactingLastFrame = true;
            
            Vector3 currentHandPosition = GetContactingBonePosition();
            Quaternion currentHandRotation = GetContactingBoneRotation();
            
            if (firstFrame)
            {
                previousHandPosition = currentHandPosition;
                previousHandRotation = currentHandRotation;
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
            
            // 位置を更新
            transform.position = newPosition;
            
            // 回転処理
            if (enableRotation)
            {
                // 手の回転変化を計算
                Quaternion deltaRotation = currentHandRotation * Quaternion.Inverse(previousHandRotation);
                
                // 回転変化をオイラー角で取得
                Vector3 deltaEuler = deltaRotation.eulerAngles;
                
                // 180度を超える回転を-180~180の範囲に正規化
                if (deltaEuler.x > 180f) deltaEuler.x -= 360f;
                if (deltaEuler.y > 180f) deltaEuler.y -= 360f;
                if (deltaEuler.z > 180f) deltaEuler.z -= 360f;
                
                // 回転速度を適用
                deltaEuler *= rotationSpeed;
                
                // 軸別回転制御を適用
                if (!enableXAxisRotation) deltaEuler.x = 0f;
                if (!enableYAxisRotation) deltaEuler.y = 0f;
                if (!enableZAxisRotation) deltaEuler.z = 0f;
                
                // 現在の回転に追加
                if (deltaEuler.magnitude > 0.01f) // 小さな回転変化は無視
                {
                    transform.rotation = transform.rotation * Quaternion.Euler(deltaEuler);
                    
                    if (showDebugLogs)
                    {
                        Debug.Log($"Cube {cubeIndex}: Rotation delta = {deltaEuler}, New rotation = {transform.rotation.eulerAngles}");
                    }
                }
            }
            
            // 前フレームの値を更新
            previousHandRotation = currentHandRotation;
            
            // デバッグログ（Hand Movementを常に出力）
            if (moveAmount.magnitude > 0.0001f)
            {
                Debug.Log($"CubeMovementController - Hand Movement Delta: X: {deltaPosition.x:F6}, Y: {deltaPosition.y:F6}, Z: {deltaPosition.z:F6}");
                
                if (showDebugLogs)
                {
                    Debug.Log($"Cube {cubeIndex}: Hand delta = {deltaPosition}, Move amount = {moveAmount}, New position = {newPosition}");
                    Debug.Log($"Cube {cubeIndex}: Axis control - X: {enableXAxisMovement}, Y: {enableYAxisMovement}, Z: {enableZAxisMovement}");
                }
            }
        }
        else
        {
            // 動作条件が満たされなくなった時の処理
            if (wasContactingLastFrame && enableGravityControl)
            {
                // このオブジェクトの重力を有効化（個別制御）
                if (cubeRigidbody != null)
                {
                    cubeRigidbody.useGravity = true; // 重力を有効化
                }
                
                if (showDebugLogs)
                {
                    Debug.Log($"Cube {cubeIndex}: Movement condition not met - Gravity enabled");
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
        // オブジェクト破棄時に重力を有効化しておく
        if (enableGravityControl && cubeRigidbody != null)
        {
            cubeRigidbody.useGravity = true; // 重力を有効化
            
            if (showDebugLogs)
            {
                Debug.Log($"CubeMovementController: Cube {cubeIndex} destroyed - Gravity enabled");
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
    /// 接触している手と握っている手が一致しているかをチェック
    /// </summary>
    /// <param name="cubeIndex">キューブのインデックス</param>
    /// <returns>接触している手が握っている場合true（もう片方の手の握り状態は不問）</returns>
    private bool CheckHandMatching(int cubeIndex)
    {
        if (bonejudgeNew == null || grabJudge == null)
        {
            if (showHandMatchingDebug)
            {
                Debug.Log($"Cube {cubeIndex}: Hand matching check failed - missing references");
            }
            return false;
        }

        // 接触していない場合は手の一致判定も不要
        if (!bonejudgeNew.isTouching[cubeIndex])
        {
            return false;
        }

        // 右手で接触している場合
        bool isRightHandTouching = bonejudgeNew.IsRightHandTouching(cubeIndex);
        // 左手で接触している場合  
        bool isLeftHandTouching = bonejudgeNew.IsLeftHandTouching(cubeIndex);
        
        // 右手が握っているかどうか（左手の握り状態は不問）
        bool isRightHandGrabbing = grabJudge.IsRightHandGrabbing();
        // 左手が握っているかどうか（右手の握り状態は不問）
        bool isLeftHandGrabbing = grabJudge.IsLeftHandGrabbing();
        
        // 手の一致判定：接触している手が握っていればOK（もう片方の手の状態は関係なし）
        bool handsMatch = (isRightHandTouching && isRightHandGrabbing) || 
                         (isLeftHandTouching && isLeftHandGrabbing);
        
        if (showHandMatchingDebug)
        {
            Debug.Log($"Cube {cubeIndex}: RightTouch={isRightHandTouching}, LeftTouch={isLeftHandTouching}, " +
                     $"RightGrab={isRightHandGrabbing}, LeftGrab={isLeftHandGrabbing}, Match={handsMatch}");
        }
        
        return handsMatch;
    }

    /// <summary>
    /// 動作条件をチェック（接触+つかみ+手の一致の組み合わせ）
    /// </summary>
    /// <param name="isContacting">接触しているか</param>
    /// <param name="isGrabbing">つかんでいるか</param>
    /// <param name="handsMatch">接触している手と握っている手が一致しているか</param>
    /// <returns>動作させるべき場合true</returns>
    private bool CheckMovementCondition(bool isContacting, bool isGrabbing, bool handsMatch)
    {
        // 基本的な動作条件をチェック
        bool basicCondition = false;
        
        if (requireGrabAndContact)
        {
            // つかみ+接触の両方が必要
            basicCondition = isContacting && isGrabbing;
        }
        else if (requireGrabOnly)
        {
            // つかみのみで動作
            basicCondition = isGrabbing;
        }
        else if (requireContactOnly)
        {
            // 接触のみで動作
            basicCondition = isContacting;
        }
        else
        {
            // デフォルト: つかみ+接触の両方が必要
            basicCondition = isContacting && isGrabbing;
        }
        
        // 基本条件に加えて手の一致も必要
        return basicCondition && handsMatch;
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
        bool handsMatch = CheckHandMatching(cubeIndex);
        bool shouldMove = CheckMovementCondition(isContacting, isGrabbing, handsMatch);

        return $"Contact: {isContacting}, Grab: {isGrabbing}, HandsMatch: {handsMatch}, Moving: {shouldMove}";
    }

}