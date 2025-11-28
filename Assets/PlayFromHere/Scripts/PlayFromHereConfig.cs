using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LightShaft.PFH {
    [CreateAssetMenu(fileName = "Data", menuName = "PlayFromHere/PlayFromHereConfig", order = 1)]
    public class PlayFromHereConfig : ScriptableObject
    {
        [HideInInspector] public Vector3 tempPlayerPosition;
        [HideInInspector] public Quaternion tempPlayerRotation;
        [HideInInspector] public bool playedFromPos;

        public Vector3 offset;
        public bool rotatePlayerWithView;
        public bool keepXPosition;
        public bool keepYPosition;
        public bool keepZPosition;
    }
}
