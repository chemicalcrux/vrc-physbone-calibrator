using System.Collections;
using System.Collections.Generic;
using Crux.PhysboneCalibrator.Runtime.Configuration;
using UnityEngine;
using VRC.SDK3.Dynamics.PhysBone.Components;
using VRC.SDKBase;

namespace Crux.PhysboneCalibrator.Runtime
{
    public class PhysboneCalibrator : MonoBehaviour, IEditorOnly
    {
        [SerializeField, SerializeReference] internal PhysboneCalibratorConfiguration configuration = new PhysboneCalibratorConfigurationV1();

        void Reset()
        {
            configuration = new PhysboneCalibratorConfigurationV1();
        }
    }
}
