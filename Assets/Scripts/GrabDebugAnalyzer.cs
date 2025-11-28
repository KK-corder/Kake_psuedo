using UnityEngine;

/// <summary>
/// Grab動作デバッグ用のスクリプト
/// ContactProgressController、GrabJudge、BoneJudgeNewの連携状態を詳しく調査
/// </summary>
public class GrabDebugAnalyzer : MonoBehaviour
{
    [Header("Debug Targets")]
    public ContactProgressController contactProgressController;
    public GrabJudge grabJudge;
    public BoneJudgeNew boneJudgeNew;
    
    [Header("Debug Settings")]
    public bool enableDetailedLogging = true;
    public bool logFrameByFrame = false;
    public int logInterval = 30; // フレーム間隔
    
    [Header("Analysis Results")]
    [SerializeField] private bool currentGrabState = false;
    [SerializeField] private bool currentContactState = false;
    [SerializeField] private bool shouldProgress = false;
    [SerializeField] private string activationMode = "";
    
    private int frameCount = 0;
    
    void Update()
    {
        frameCount++;
        
        if (enableDetailedLogging && (logFrameByFrame || frameCount % logInterval == 0))
        {
            AnalyzeGrabState();
        }
    }
    
    private void AnalyzeGrabState()
    {
        if (contactProgressController == null || grabJudge == null || boneJudgeNew == null)
        {
            Debug.LogWarning("GrabDebugAnalyzer: Some references are missing!");
            return;
        }
        
        // 現在の状態を取得
        currentGrabState = grabJudge.IsAnyHandGrabbing();
        currentContactState = IsAnyContactActive();
        
        // 発動条件をチェック
        shouldProgress = CheckProgressConditions();
        activationMode = GetActivationModeString();
        
        // 詳細ログ出力
        Debug.Log($"=== Grab Debug Analysis (Frame {frameCount}) ===");
        Debug.Log($"Grab State: {currentGrabState} (R:{grabJudge.isRightHandGrabbing}, L:{grabJudge.isLeftHandGrabbing})");
        Debug.Log($"Contact State: {currentContactState} - Details: {GetContactDetails()}");
        Debug.Log($"Activation Mode: {activationMode}");
        Debug.Log($"Should Progress: {shouldProgress}");
        Debug.Log($"Progress Settings - RequireGrabAndContact: {contactProgressController.requireGrabAndContact}, RequireContactOnly: {contactProgressController.requireContactOnly}");
        
        // 円柱設定情報
        LogCylinderSettings();
        
        // 異常状態の検出
        DetectAnomalies();
    }
    
    private bool IsAnyContactActive()
    {
        if (boneJudgeNew.isTouching == null) return false;
        
        for (int i = 0; i < boneJudgeNew.isTouching.Length; i++)
        {
            if (boneJudgeNew.isTouching[i]) return true;
        }
        return false;
    }
    
    private bool CheckProgressConditions()
    {
        if (contactProgressController.requireGrabAndContact)
        {
            return currentContactState && currentGrabState;
        }
        else if (contactProgressController.requireContactOnly)
        {
            return currentContactState;
        }
        else
        {
            // デフォルト: 握り+接触
            return currentContactState && currentGrabState;
        }
    }
    
    private string GetActivationModeString()
    {
        if (contactProgressController.requireGrabAndContact)
            return "GrabAndContact";
        else if (contactProgressController.requireContactOnly)
            return "ContactOnly";
        else
            return "Default(GrabAndContact)";
    }
    
    private string GetContactDetails()
    {
        if (boneJudgeNew.isTouching == null) return "No contact data";
        
        string details = "";
        for (int i = 0; i < boneJudgeNew.isTouching.Length; i++)
        {
            details += $"Cube{i}:{boneJudgeNew.isTouching[i]} ";
        }
        return details.TrimEnd();
    }
    
    private void LogCylinderSettings()
    {
        Debug.Log($"Cylinder Settings - VolumeDetection: {boneJudgeNew.useVolumeBasedDetection}, " +
                 $"RadiusMultiplier: {boneJudgeNew.radiusMultiplier:F2}, " +
                 $"HeightMultiplier: {boneJudgeNew.heightMultiplier:F2}, " +
                 $"ContactMargin: {boneJudgeNew.contactMargin:F3}");
    }
    
    private void DetectAnomalies()
    {
        // 握っていないのに動いている異常を検出
        if (!currentGrabState && shouldProgress && activationMode.Contains("Grab"))
        {
            Debug.LogError($"ANOMALY DETECTED: Progress is active without grabbing! " +
                          $"Grab: {currentGrabState}, Contact: {currentContactState}, " +
                          $"ShouldProgress: {shouldProgress}, Mode: {activationMode}");
        }
        
        // 接触していないのに動いている異常を検出
        if (!currentContactState && shouldProgress)
        {
            Debug.LogError($"ANOMALY DETECTED: Progress is active without contact! " +
                          $"Contact: {currentContactState}, Grab: {currentGrabState}, " +
                          $"ShouldProgress: {shouldProgress}");
        }
        
        // 円柱設定が過度に敏感な場合の警告
        if (boneJudgeNew.radiusMultiplier > 1.5f || boneJudgeNew.contactMargin > 0.03f)
        {
            Debug.LogWarning($"Cylinder settings may be too sensitive! " +
                           $"RadiusMultiplier: {boneJudgeNew.radiusMultiplier:F2}, " +
                           $"ContactMargin: {boneJudgeNew.contactMargin:F3}");
        }
    }
    
    [ContextMenu("Analyze Current State")]
    public void AnalyzeCurrentState()
    {
        AnalyzeGrabState();
    }
    
    [ContextMenu("Reset Cylinder Settings to Conservative")]
    public void ResetCylinderSettings()
    {
        boneJudgeNew.radiusMultiplier = 1.05f; // 5%拡大に縮小
        boneJudgeNew.heightMultiplier = 1.02f; // 2%拡大に縮小
        boneJudgeNew.contactMargin = 0.005f; // 0.5cmに縮小
        
        Debug.Log("Cylinder settings reset to conservative values");
    }
    
    [ContextMenu("Test Grab Detection Only")]
    public void TestGrabDetectionOnly()
    {
        Debug.Log($"=== Grab Detection Test ===");
        Debug.Log($"Right Hand Grabbing: {grabJudge.isRightHandGrabbing}");
        Debug.Log($"Left Hand Grabbing: {grabJudge.isLeftHandGrabbing}");
        Debug.Log($"Any Hand Grabbing: {grabJudge.IsAnyHandGrabbing()}");
        Debug.Log($"Grab Threshold: {grabJudge.grabThreshold}");
        Debug.Log($"Release Threshold: {grabJudge.releaseThreshold}");
    }
}