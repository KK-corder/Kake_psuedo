using UnityEngine;

public class ContactProgressController : MonoBehaviour
{
    // BoneJudge.cs の参照（接触判定用）
    public BoneJudge boneJudge;

    //public TaskManage taskmanage;


    // handtransition.shader を使用しているマテリアルの参照
    public Material handTransitionMaterial;

    // progress の基礎増加率（Inspectorから調整可能）
    public float[] progressIncreaseRate;



    // 内部で接触時間および現在の progress を保持
    private float[] contactTime;
    private float[] currentProgress;

    // 前フレームの手モデルの z 座標
    private float previousHandZ;

    public OVRSkeleton handModel;




    void Start()
    {
        // Basic validation and debug
        if (boneJudge == null)
        {
            Debug.LogError("ContactProgressController: boneJudge is not assigned in the Inspector.");
            return;
        }

 
            previousHandZ = GetHandZ();
        

        int count = Mathf.Max(1, boneJudge.cubes != null ? boneJudge.cubes.Length : 1);
        contactTime = new float[count];
        currentProgress = new float[count];

        for (int i = 0; i < count; i++)
        {
            // 接触がなくなった場合はリセット
            contactTime[i] = 0f;
            currentProgress[i] = 1f;
        }



    }

    void Update()
    {
        bool contactActive = false;

        // BoneJudge の isTouching 配列のうち、いずれかが true なら接触中とする
        if (boneJudge != null && boneJudge.isTouching != null)
        {
            foreach (bool touching in boneJudge.isTouching)
            {
                if (touching)
                {
                    contactActive = true;
                    break;
                }
            }
        }

        if (contactActive)
        {
            // 現在の手モデルの z 座標を取得
            float currentHandZ = GetHandZ();
            // 符号付きの変化量を算出（正なら進行方向、負なら逆方向）
            float delta = currentHandZ - previousHandZ;
            previousHandZ = currentHandZ;

            if (logShouldShow())
            {
                Debug.Log($"ContactProgressController.Update: contactActive={contactActive}, currentHandZ={currentHandZ:F4}, delta={delta:F4}");
            }

            // 変化量の絶対値と期待される変化量の比率を求める
            float handAbsDelta = Mathf.Abs(delta);
  

            // 接触しているCubeに基づいてcurrentProgressを更新
            for (int i = 0; i < boneJudge.cubes.Length; i++)
            {
                if (boneJudge.isTouching[i])
                {
                    contactTime[i] += Time.deltaTime;

                    // protect against progressIncreaseRate missing or too short
                    float pRate = 0.5f;
                    if (progressIncreaseRate != null && i < progressIncreaseRate.Length)
                        pRate = progressIncreaseRate[i];

                    //progressIncreaseRate[i]を塁上にしてもっと差分を大きく
                    float oldProgress = currentProgress[i];
                    if (delta > 0)
                    {
                        // 手モデルが正方向に移動：currentProgress を減少
                        currentProgress[i] -= pRate * handAbsDelta * Time.deltaTime * 10f;
                    }
                    else if (delta < 0)
                    {
                        // 手モデルが負方向に移動：currentProgress を増加
                        currentProgress[i] += pRate * handAbsDelta * Time.deltaTime * 10f;
                    }
                    
                    // デバッグ：変化量を確認
                    if (Mathf.Abs(oldProgress - currentProgress[i]) > 0.0001f)
                    {
                        Debug.Log($"Progress changed for cube[{i}]: {oldProgress:F6} -> {currentProgress[i]:F6}, delta={delta:F6}");
                    }
                    else if (delta != 0)
                    {
                        Debug.Log($"Progress NOT changed for cube[{i}]: delta={delta:F6}, pRate={pRate:F6}, changeAmount={pRate * handAbsDelta * Time.deltaTime * 10f:F6}");
                    }
                    else
                    {
                        Debug.Log($"No hand movement detected for cube[{i}]: delta=0, currentHandZ={currentHandZ:F6}, previousHandZ={previousHandZ:F6}");
                    }

                    if (logShouldShow())
                        Debug.Log($"ContactProgressController: cube[{i}] touching, pRate={pRate:F3}, currentProgress={currentProgress[i]:F4}");
                }
            }
        }
        else
        {
            for (int i = 0; i < boneJudge.cubes.Length; i++)
            {
                // 接触がなくなった場合はリセット
                contactTime[i] = 0f;
                currentProgress[i] = 1f;

                // デバッグログでリセットを確認
                //Debug.Log($"Cube {i}: Reset currentProgress to {currentProgress[i]}");
            }

            // シェーダーの _Progress プロパティを初期値に戻す
            if (handTransitionMaterial != null)
            {
                handTransitionMaterial.SetFloat("_Progress", 1.0f);
                //Debug.Log("Reset _Progress to 1.0f");
            }


                previousHandZ = GetHandZ();
            
        }

        for (int i = 0; i < boneJudge.cubes.Length; i++)
        {
            // currentProgress を 0～1 の範囲にクランプ
            currentProgress[i] = Mathf.Clamp01(currentProgress[i]);

            if (boneJudge.isTouching[i])
            {
                // シェーダーの _Progress プロパティを更新
                if (handTransitionMaterial != null)
                {
                    handTransitionMaterial.SetFloat("_Progress", currentProgress[i]);

                    // デバッグログで _Progress の値を確認
                    float progress = handTransitionMaterial.GetFloat("_Progress");
                    if (progressIncreaseRate != null && i < progressIncreaseRate.Length)
                    {
                        Debug.Log($"Cube {i}: Shader _Progress = {progress:F6}, Current Progress = {currentProgress[i]:F6}, pRate={progressIncreaseRate[i]:F6}");
                    }
                    else
                    {
                        Debug.Log($"Cube {i}: Shader _Progress = {progress:F6}, Current Progress = {currentProgress[i]:F6}, pRate=default(0.5)");
                    }
                }
            }
        }
    }


    // BoneJudge の progressStart の z 座標を取得する関数
    private float GetHandZ()
    {
        if (boneJudge != null)
        {
            // BoneJudgeのprogressStartを使用（より一貫性がある）
            return boneJudge.progressStart.z;
        }
        
        // フォールバック: 従来のOVRSkeletonからの取得
        Vector3 handPos = Vector3.zero;
        if (handModel == null || handModel.Bones == null)
            return 0f;

        foreach (var bone in handModel.Bones)
        {
            if (bone.Transform != null && bone.Transform.name == "Hand_ForearmStub")
            {
                handPos = bone.Transform.position;
                break;
            }
        }
        return handPos.z;
    }

    private bool logShouldShow()
    {
        // enable some logs only in development/editor to avoid spam
        return true;
    }
}
