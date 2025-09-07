using System;
using System.Collections.Generic;
using Crux.Core.Runtime.Upgrades;
using UnityEngine;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace Crux.PhysboneCalibrator.Runtime.Configuration
{
    [Serializable]
    [UpgradableVersion(1)]
    [UpgradablePropertyDrawer("4c0c7352ccd1e41bb91488f693dc7a32,9197481963319205126")]
    public class PhysboneCalibratorConfigurationV1 : PhysboneCalibratorConfiguration
    {
        public string menuPath;
        public List<VRCPhysBone> targets;

        public bool calibrateForces = true;
        public bool calibrateLimits = true;
        public bool calibrateCollision = true;
        public bool calibrateStretchAndSquish = true;
        public bool calibrateGrabAndPose = true;
        public bool calibrateOptions = true;

        [Tooltip("Add a submenu to switch between 'Simplified' and 'Advanced' modes")]
        public bool integrationTypeToggle = true;
        [Tooltip("The range of angles used for limits")]
        public Vector2 angleRange = new Vector2(0, 90);

        [Tooltip("Don't sync anything.")]
        public bool localOnly = true;
        public override PhysboneCalibratorConfiguration Upgrade()
        {
            return this;
        }
    }
}