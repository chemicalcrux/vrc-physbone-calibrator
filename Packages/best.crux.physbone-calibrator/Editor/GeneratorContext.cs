using System;
using System.Collections.Generic;
using Crux.PhysboneCalibrator.Runtime;
using Crux.PhysboneCalibrator.Runtime.Configuration;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

namespace Crux.PhysboneCalibrator.Editor
{
    public class GeneratorContext
    {
        public PhysboneCalibratorConfigurationV1 configuration;
        public Transform avatarRoot;
        
        public AnimatorController generatedController;
        public VRCExpressionParameters generatedParameters;

        public AnimatorState changeIdleState;
        public AnimatorState changeFireState;

        public VRCAvatarParameterDriver changeFireDriver;

        public BlendTree changeSensorTree;

        private int column;
        private int row;

        public AnimationClip oneSecondDelayClip;
        
        public GeneratorContext()
        {
            oneSecondDelayClip = new AnimationClip();
            oneSecondDelayClip.SetCurve("_Dummy", typeof(GameObject), "m_Enabled", AnimationCurve.Constant(0, 1, 0));
        }
        
        public void NextColumn()
        {
            ++column;
            row = 0;
        }
        
        public Vector2 GetNextPosition()
        {
            Vector2 result = new(column * 250 + 200, row * 50 + 200);
            ++row;
            return result;
        }

        public void AddParameter(VRCExpressionParameters.Parameter parameter)
        {
            Array.Resize(ref generatedParameters.parameters, generatedParameters.parameters.Length + 1);
            generatedParameters.parameters[^1] = parameter;
        }

        private readonly Dictionary<string, Dictionary<float, AnimationClip>> caches = new();

        private readonly MenuHierarchy hierarchy = new()
        {
            label = "Root"
        };
        
        public void AddControl(string path, VRCExpressionsMenu.Control control)
        {
            var parts = path.Split("/");

            MenuHierarchy target = hierarchy;

            for (int i = 0; i < parts.Length - 1; ++i)
            {
                target = target.GetOrCreateBranch(parts[i]);
            }

            control.name = parts[^1];

            target.leaves.Add(control);
        }

        public VRCExpressionsMenu ResolveHierarchy(int slotLimit)
        {
            hierarchy.TrySplit(slotLimit);
            
            return hierarchy.Resolve(this, "Menu");
        }

        public AnimationClip GetClip(string param, float value)
        {
            if (!caches.TryGetValue(param, out var cache))
            {
                cache = new();
                caches[param] = cache;
            }
    
            if (cache.TryGetValue(value, out var clip))
            {
                return clip;
            }

            clip = new AnimationClip
            {
                name = $"{param}: {value:N3}",
                hideFlags = HideFlags.HideInHierarchy
            };

            clip.SetCurve("", typeof(Animator), param, AnimationCurve.Constant(0f, 1f, value));
    
            cache[value] = clip;

            return clip;
        }

        public void AddChangeDetector(string parameterName)
        {
            string parameterNamePrevious = parameterName + "_Previous";
            string parameterNameDelta = parameterName + "_Delta";

            generatedController.AddParameter(parameterNamePrevious, AnimatorControllerParameterType.Float);
            generatedController.AddParameter(parameterNameDelta, AnimatorControllerParameterType.Float);
            
            var enableTransitionLess = changeIdleState.AddTransition(changeFireState);
            enableTransitionLess.hasExitTime = false;
            enableTransitionLess.duration = 0f;
            enableTransitionLess.AddCondition(AnimatorConditionMode.Less, 0, parameterNameDelta);
            
            var enableTransitionGreater = changeIdleState.AddTransition(changeFireState);
            enableTransitionGreater.hasExitTime = false;
            enableTransitionGreater.duration = 0f;
            enableTransitionGreater.AddCondition(AnimatorConditionMode.Greater, 0, parameterNameDelta);
            
            changeFireDriver.parameters.Add(new VRC_AvatarParameterDriver.Parameter
            {
                name = parameterNamePrevious,
                type = VRC_AvatarParameterDriver.ChangeType.Copy,
                source = parameterName,
            });

            var subtract = Subtract(parameterName, parameterNamePrevious, parameterNameDelta);

            changeSensorTree.AddChild(subtract);
        }
    
        protected BlendTree Subtract(string lhsParam, string rhsParam, string outParam)
        {
            var subtractTree = new BlendTree
            {
                name = "Subtraction",
                blendType = BlendTreeType.Direct,
                hideFlags = HideFlags.HideInHierarchy
            };

            var subtractLeft = new BlendTree
            {
                name = "LHS",
                blendType = BlendTreeType.Simple1D,
                blendParameter = lhsParam,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy
            };

            var subtractRight = new BlendTree
            {
                name = "RHS",
                blendType = BlendTreeType.Simple1D,
                blendParameter = rhsParam,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy
            };

            subtractLeft.AddChild(GetClip(outParam, -100));
            subtractLeft.AddChild(GetClip(outParam, 100));

            var children = subtractLeft.children;

            children[0].threshold = -100;
            children[1].threshold = 100;

            subtractLeft.children = children;

            subtractRight.AddChild(GetClip(outParam, 100));
            subtractRight.AddChild(GetClip(outParam, -100));

            children = subtractRight.children;

            children[0].threshold = -100;
            children[1].threshold = 100;

            subtractRight.children = children;

            subtractTree.AddChild(subtractLeft);
            subtractTree.AddChild(subtractRight);

            children = subtractTree.children;
            children[0].directBlendParameter = "Constant/One";
            children[1].directBlendParameter = "Constant/One";
            subtractTree.children = children;

            return subtractTree;
        }
    }
}