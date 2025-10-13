using UnityEngine;

public class ContactProgressController : MonoBehaviour
{
    // BoneJudgeNew.cs の参照（接触判定用）
    public BoneJudgeNew bonejudgeNew;

    public ProgressSet progressSet;

    //public TaskManage taskmanage;


    // handtransition.shader を使用しているマテリアルの参照
    public Material handTransitionMaterial;

    // progress の基礎増加率（Inspectorから調整可能）
    public float[] progressIncreaseRate;



    // 内部で接触時間および現在の progress を保持
    private float[] contactTime;
    private float[] currentProgress;

    // 接触開始時の手モデルの座標（各キューブごと）
    private Vector3[] contactStartPosition;
    // 接触状態の追跡（各キューブごと）
    private bool[] wasContactingLastFrame;

    public OVRSkeleton handModel;




    void Start()
    {
        // Basic validation and debug
        if (bonejudgeNew == null)
        {
            Debug.LogError("ContactProgressController: bonejudgeNew is not assigned in the Inspector.");
            return;
        }

        int count = Mathf.Max(1, bonejudgeNew.cubes != null ? bonejudgeNew.cubes.Length : 1);
        contactTime = new float[count];
        currentProgress = new float[count];
        contactStartPosition = new Vector3[count];
        wasContactingLastFrame = new bool[count];

        for (int i = 0; i < count; i++)
        {
            // 接触がなくなった場合はリセット
            contactTime[i] = 0f;
            currentProgress[i] = 1f;
            contactStartPosition[i] = Vector3.zero;
            wasContactingLastFrame[i] = false;
        }
    }

    void Update()
    {
        bool contactActive = false;

        // bonejudgeNew の isTouching 配列のうち、いずれかが true なら接触中とする
        if (bonejudgeNew != null && bonejudgeNew.isTouching != null)
        {
            foreach (bool touching in bonejudgeNew.isTouching)
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
            // 接触しているCubeに基づいてcurrentProgressを更新
            for (int i = 0; i < bonejudgeNew.cubes.Length; i++)
            {
                if (bonejudgeNew.isTouching[i])
                {
                    // 接触開始時の座標を記録
                    if (!wasContactingLastFrame[i])
                    {
                        contactStartPosition[i] = GetHandPosition();
                        wasContactingLastFrame[i] = true;
                        Debug.Log($"Contact started for cube[{i}] at position: {contactStartPosition[i]}");
                    }

                    contactTime[i] += Time.deltaTime;

                    // 現在の手の座標を取得
                    Vector3 currentHandPosition = GetHandPosition();
                    
                    // 接触開始時からのY軸方向の変位のみを計算
                    float yDisplacement = currentHandPosition.y - contactStartPosition[i].y;

                    // protect against progressIncreaseRate missing or too short
                    float pRate = 0.5f;
                    if (progressIncreaseRate != null && i < progressIncreaseRate.Length)
                        pRate = progressIncreaseRate[i];

                    float oldProgress = currentProgress[i];
                    
                    // Y軸方向の変化に基づいてprogressを更新
                    if (yDisplacement > 0)
                    {
                        // Y軸正方向への移動：currentProgress を減少
                        currentProgress[i] -= pRate * Mathf.Abs(yDisplacement) * Time.deltaTime;
                    }
                    else if (yDisplacement < 0)
                    {
                        // Y軸負方向への移動：currentProgress を増加
                        currentProgress[i] += pRate * Mathf.Abs(yDisplacement) * Time.deltaTime;
                    }
                    
                    // デバッグ：変化量を確認
                    if (Mathf.Abs(oldProgress - currentProgress[i]) > 0.0001f)
                    {
                        Debug.Log($"Progress changed for cube[{i}]: {oldProgress:F6} -> {currentProgress[i]:F6}, Y displacement: {yDisplacement:F6}");
                    }

                    if (logShouldShow())
                        Debug.Log($"ContactProgressController: cube[{i}] touching, pRate={pRate:F3}, currentProgress={currentProgress[i]:F4}, Y displacement={yDisplacement:F4}");
                }
                else
                {
                    // 接触がなくなった場合
                    if (wasContactingLastFrame[i])
                    {
                        wasContactingLastFrame[i] = false;
                        //Debug.Log($"Contact ended for cube[{i}]");
                    }
                }
            }
        }
        else
        {
            for (int i = 0; i < bonejudgeNew.cubes.Length; i++)
            {
                // 接触がなくなった場合はリセット
                contactTime[i] = 0f;
                currentProgress[i] = 1f;
                wasContactingLastFrame[i] = false;

                // デバッグログでリセットを確認
                //Debug.Log($"Cube {i}: Reset currentProgress to {currentProgress[i]}");
            }

            // シェーダーの _Progress プロパティを初期値に戻す
            if (handTransitionMaterial != null)
            {
                handTransitionMaterial.SetFloat("_Progress", 1.0f);
                //Debug.Log("Reset _Progress to 1.0f");
            }
        }

        for (int i = 0; i < bonejudgeNew.cubes.Length; i++)
        {
            // currentProgress を 0～1 の範囲にクランプ
            currentProgress[i] = Mathf.Clamp01(currentProgress[i]);

            if (bonejudgeNew.isTouching[i])
            {
                // シェーダーの _Progress プロパティを更新
                if (handTransitionMaterial != null)
                {
                    handTransitionMaterial.SetFloat("_Progress", currentProgress[i]);

                    // デバッグログで _Progress の値を確認
                    float progress = handTransitionMaterial.GetFloat("_Progress");
                    if (progressIncreaseRate != null && i < progressIncreaseRate.Length)
                    {
                        //Debug.Log($"Cube {i}: Shader _Progress = {progress:F6}, Current Progress = {currentProgress[i]:F6}, pRate={progressIncreaseRate[i]:F6}");
                    }
                    else
                    {
                        //Debug.Log($"Cube {i}: Shader _Progress = {progress:F6}, Current Progress = {currentProgress[i]:F6}, pRate=default(0.5)");
                    }
                }
            }
        }
    }


    // bonejudgeNew の progressStart の z 座標を取得する関数
    private float GetHandZ()
    {
        if (bonejudgeNew != null)
        {
            // bonejudgeNewのprogressStartを使用（より一貫性がある）
            return progressSet.progressStart.z;
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

    // 手の3D座標を取得する関数（X、Y、Z軸すべて）
    private Vector3 GetHandPosition()
    {
        if (progressSet != null)
        {
            // progressSetのprogressStartを使用（より一貫性がある）
            return progressSet.progressStart;
        }
        
        // フォールバック: 従来のOVRSkeletonからの取得
        Vector3 handPos = Vector3.zero;
        if (handModel == null || handModel.Bones == null)
            return Vector3.zero;

        foreach (var bone in handModel.Bones)
        {
            if (bone.Transform != null && bone.Transform.name == "Hand_ForearmStub")
            {
                handPos = bone.Transform.position;
                break;
            }
        }
        return handPos;
    }

    private bool logShouldShow()
    {
        // enable some logs only in development/editor to avoid spam
        return true;
    }
}
