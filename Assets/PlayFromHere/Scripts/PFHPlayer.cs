using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LightShaft.PFH
{
    public class PFHPlayer : MonoBehaviour
    {
        public static PFHPlayer instance;
        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(this);
            }
        }
    }
}

