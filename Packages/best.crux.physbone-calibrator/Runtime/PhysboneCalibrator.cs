using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Dynamics.PhysBone.Components;
using VRC.SDKBase;

namespace ChemicalCrux.PhysboneCalibrator.Runtime
{
    public class PhysboneCalibrator : MonoBehaviour, IEditorOnly
    {
        public string menuPath;
        public List<VRCPhysBone> targets;

        [Header("Categories")] public bool calibrateForces = true;
        public bool calibrateLimits = true;
        public bool calibrateCollision = true;
        public bool calibrateStretchAndSquish = true;
        public bool calibrateGrabAndPose = true;
        public bool calibrateOptions = true;

        [Header("Features")] 
        
        [Tooltip("Add a submenu to switch between 'Simplified' and 'Advanced' modes")]
        public bool integrationTypeToggle;
        [Tooltip("The range of angles used for limits")]
        public Vector2 angleRange = new Vector2(0, 90);

        [Tooltip("Don't sync anything.")]
        public bool localOnly = true;
    }
}
