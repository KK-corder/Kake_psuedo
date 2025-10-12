using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VisualHand : MonoBehaviour
{
    [SerializeField] Transform sourceOculusHand;
    [SerializeField] Transform OculusHand;
    [SerializeField] private Transform rootSourceBone;
    [SerializeField] private Transform rootBone;
    [SerializeField] private Vector3 adjustRotate;

    [SerializeField] private Rigidbody OculusHandRB;

    private List<Transform> _sourceBones;
    private List<Transform> _bones;

    public bool isSetBoans = true;

    void Start()
    {
        _sourceBones = new List<Transform>();
        _bones = new List<Transform>();
        GetBones(rootSourceBone, _sourceBones);
        GetBones(rootBone, _bones);
    }

    void Update()
    {
        OculusHand.localScale = sourceOculusHand.localScale;
        if (isSetBoans)
        {
            SetBones();
        }
    }

    private void FixedUpdate()
    {
        SetHand();
    }

    private void SetHand()
    {
        OculusHandRB.MoveRotation(sourceOculusHand.rotation * Quaternion.Euler(adjustRotate));
    }

    private void SetBones()
    {
        if (_sourceBones.Count == _bones.Count)
        {
            for (int i = 0; i < _sourceBones.Count; i++)
            {
                _bones[i].localRotation = _sourceBones[i].localRotation;
            }
        }
    }

    private void GetBones(Transform rootBone, List<Transform> bone)
    {
        int length = rootBone.childCount;

        for (int i = 0; i < length; i++)
        {
            Transform b = rootBone.GetChild(i);
            if(b.name.IndexOf("b_") == 0)
            {
                bone.Add(b);

                if (b.childCount > 0)
                {
                    GetBones(b, bone);
                }
            }
        }
    }
}
