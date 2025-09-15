using Crux.Core.Runtime.Attributes;
using Crux.PhysboneCalibrator.Runtime.Configuration;
using UnityEngine;
using VRC.SDKBase;

namespace Crux.PhysboneCalibrator.Runtime
{
    [HideIcon]
    public class PhysboneCalibrator : MonoBehaviour, IEditorOnly
    {
        [SerializeField, SerializeReference] internal PhysboneCalibratorConfiguration configuration = new PhysboneCalibratorConfigurationV1();

        void Reset()
        {
            configuration = new PhysboneCalibratorConfigurationV1();
        }
    }
}
