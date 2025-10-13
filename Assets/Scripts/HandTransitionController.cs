using UnityEngine;

//取得bone座標とシェーダーの始点・終点を一致させる
public class HandTransitionController : MonoBehaviour
{
    // bonejudgeNew.cs のコンポーネント。progressStart と progressEnd を保持している
    public BoneJudgeNew bonejudgeNew;

    public ProgressSet progressSet;
    
    // handtransition.shader を使用しているマテリアル
    public Material handTransitionMaterial;
    
    // シェーダー側のプロパティ名（handtransition.shader で設定されている名前に合わせる）
    private readonly string startPointProperty = "_StartPoint";
    private readonly string endPointProperty = "_EndPoint";
    
    void Update()
    {
        if (bonejudgeNew == null || handTransitionMaterial == null)
        {
            return;
        }
        
        // bonejudgeNew.cs 内の progressStart と progressEnd の値を動的に取得し、シェーダーにセット
        handTransitionMaterial.SetVector(startPointProperty, progressSet.progressStart);
        handTransitionMaterial.SetVector(endPointProperty, progressSet.progressEnd);

    }
}
