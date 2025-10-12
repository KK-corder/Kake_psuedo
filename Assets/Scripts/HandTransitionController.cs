using UnityEngine;

//取得bone座標とシェーダーの始点・終点を一致させる
public class HandTransitionController : MonoBehaviour
{
    // BoneJudge.cs のコンポーネント。progressStart と progressEnd を保持している
    public BoneJudge boneJudge;
    
    // handtransition.shader を使用しているマテリアル
    public Material handTransitionMaterial;
    
    // シェーダー側のプロパティ名（handtransition.shader で設定されている名前に合わせる）
    private readonly string startPointProperty = "_StartPoint";
    private readonly string endPointProperty = "_EndPoint";
    
    void Update()
    {
        if (boneJudge == null || handTransitionMaterial == null)
        {
            return;
        }
        
        // BoneJudge.cs 内の progressStart と progressEnd の値を動的に取得し、シェーダーにセット
        handTransitionMaterial.SetVector(startPointProperty, boneJudge.progressStart);
        handTransitionMaterial.SetVector(endPointProperty, boneJudge.progressEnd);

    }
}
