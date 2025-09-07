using System;
using Crux.Core.Runtime.Upgrades;

namespace Crux.PhysboneCalibrator.Runtime.Configuration
{
    [Serializable]
    [UpgradableLatestVersion(1)]
    public abstract class PhysboneCalibratorConfiguration : Upgradable<PhysboneCalibratorConfiguration>
    {
        
    }
}