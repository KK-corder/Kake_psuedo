using UnityEngine;

//取得bone座標とシェーダーの始点・終点を一致させる
public class HandTransitionController : MonoBehaviour
{
    // bonejudgeNew.cs のコンポーネント。progressStart と progressEnd を保持している
    public BoneJudgeNew bonejudgeNew;

    public ProgressSet progressSet;
    
    // handtransition.shader を使用しているマテリアル
    public Material handTransitionMaterial;
    
    [Header("Gradient Control")]
    // シェーダーのグラデーション方向をワールドY軸に固定するか（通常はfalse：手の方向を保持）
    public bool forceVerticalGradient = false;  // 手の方向ベクトルを保持するためfalse
    // 縦方向の長さ（メートル）。forceVerticalGradient 有効時のみ使用
    [Range(0.01f, 1.0f)] public float verticalLength = 0.2f;
    
    // シェーダー側のプロパティ名（handtransition.shader で設定されている名前に合わせる）
    private readonly string startPointProperty = "_StartPoint";
    private readonly string endPointProperty = "_EndPoint";
    
    void Update()
    {
        if (bonejudgeNew == null || handTransitionMaterial == null)
        {
            return;
        }
        
        // 手の実際の座標を取得（方向ベクトルを保持）
        Vector3 start = progressSet != null ? progressSet.progressStart : Vector3.zero;
        Vector3 end = progressSet != null ? progressSet.progressEnd : (start + Vector3.up * verticalLength);

        // forceVerticalGradient は使用しない（手の実際の方向を保持）
        // Y軸変化量のみでProgressを制御するが、シェーダの方向は手の向きを維持

        handTransitionMaterial.SetVector(startPointProperty, start);
        handTransitionMaterial.SetVector(endPointProperty, end);
        
        // デバッグ：シェーダの方向ベクトルを確認
        Vector3 direction = end - start;
        if (direction.magnitude > 0.001f)
        {
            Debug.Log($"HandTransition Direction Vector: X={direction.x:F6}, Y={direction.y:F6}, Z={direction.z:F6}");
            Debug.Log($"Start: {start}, End: {end}, ForceVertical: {forceVerticalGradient}");
        }

    }
}
