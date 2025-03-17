using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ChemicalCrux.PhysboneCalibrator.Runtime;
using com.vrcfury.api;
using com.vrcfury.api.Components;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.PhysBone.Components;
using VRC.SDKBase;
using VRC.SDKBase.Editor.BuildPipeline;

namespace ChemicalCrux.PhysboneCalibrator.Editor
{
    public class PhysboneCalibratorProcessor : MonoBehaviour, IVRCSDKPreprocessAvatarCallback
    {
        public int callbackOrder => -10001;

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            foreach (var declaration in avatarGameObject.GetComponentsInChildren<PhysboneCalibratorDeclaration>())
            {
                Process(declaration, avatarGameObject);
            }

            return true;
        }

        private static void Process(PhysboneCalibratorDeclaration declaration, GameObject avatarGameObject)
        {
            if (declaration.targets.Count == 0)
                return;

            FuryFullController fc = FuryComponents.CreateFullController(avatarGameObject);

            AnimatorController controller = new();
            VRCExpressionParameters parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();

            parameters.parameters = Array.Empty<VRCExpressionParameters.Parameter>();

            controller.AddParameter("Control/Adjusting", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Constant/One", AnimatorControllerParameterType.Float);

            var controllerParameters = controller.parameters;
            controllerParameters[^2].defaultBool = false;
            controllerParameters[^1].defaultFloat = 1f;
            controller.parameters = controllerParameters;

            var settingsMachine = new AnimatorStateMachine
            {
                name = "Settings"
            };

            var settingsLayer = new AnimatorControllerLayer
            {
                name = "Settings",
                defaultWeight = 1f,
                stateMachine = settingsMachine
            };

            controller.AddLayer(settingsLayer);

            var changeMachine = new AnimatorStateMachine
            {
                name = "Change Detector"
            };

            var changeLayer = new AnimatorControllerLayer
            {
                name = "Change Detector",
                defaultWeight = 1f,
                stateMachine = changeMachine
            };

            controller.AddLayer(changeLayer);

            var changeIdleState = changeMachine.AddState("Idle");
            var changeFireState = changeMachine.AddState("Fire");

            var changeIdleDriver = changeIdleState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            var changeFireDriver = changeFireState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();

            changeIdleDriver.localOnly = true;
            changeFireDriver.localOnly = true;

            changeIdleDriver.parameters = new List<VRC_AvatarParameterDriver.Parameter>
            {
                new()
                {
                    name = "Control/Adjusting",
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    value = 0f
                }
            };

            changeFireDriver.parameters = new List<VRC_AvatarParameterDriver.Parameter>
            {
                new()
                {
                    name = "Control/Adjusting",
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    value = 1f
                }
            };

            var changeReturnTransition = changeFireState.AddTransition(changeIdleState);
            changeReturnTransition.hasExitTime = true;
            changeReturnTransition.exitTime = 1f;
            changeReturnTransition.duration = 0f;

            var changeSensorMachine = new AnimatorStateMachine
            {
                name = "Change Sensor"
            };

            var changeSensorLayer = new AnimatorControllerLayer
            {
                name = "Change Sensor",
                defaultWeight = 1f,
                stateMachine = changeSensorMachine
            };

            controller.AddLayer(changeSensorLayer);

            var changeSensorState = changeSensorMachine.AddState("Change Sensor");

            var changeTree = new BlendTree
            {
                name = "Change Sensor Blend Tree",
                blendType = BlendTreeType.Direct
            };

            changeSensorState.motion = changeTree;

            GeneratorContext context = new()
            {
                declaration = declaration,
                avatarRoot = avatarGameObject.transform,
                generatedController = controller,
                generatedParameters = parameters,
                changeIdleState = changeIdleState,
                changeFireState = changeFireState,
                changeFireDriver = changeFireDriver,
                changeSensorTree = changeTree
            };

            changeIdleState.motion = context.oneSecondDelayClip;
            changeFireState.motion = context.oneSecondDelayClip;

            changeFireState.speed = 4f;

            AddPhysboneToggle(context);

            context.AddParameter(new VRCExpressionParameters.Parameter
            {
                name = "Control/Adjusting",
                defaultValue = 0f,
                networkSynced = !context.declaration.localOnly,
                valueType = VRCExpressionParameters.ValueType.Bool,
                saved = false
            });

            var blendState = settingsMachine.AddState("Settings");

            var root = new BlendTree
            {
                name = "Settings",
                blendType = BlendTreeType.Direct
            };

            blendState.motion = root;

            var mainBone = declaration.targets[0];

            root.AddChild(AnimateFloatProperty(context, "Forces/Pull", "pull", new Vector2(0, 1)));

            if (declaration.integrationTypeToggle)
            {
                root.AddChild(AnimateIntToggleProperty(context, "Forces/Integration Type", "integrationType",
                    new List<(string label, int value)>()
                    {
                        ("Simplified", 0),
                        ("Advanced", 1)
                    }));
            }
            else
            {
                var expected = mainBone.integrationType;

                foreach (var target in declaration.targets)
                {
                    if (target.integrationType != expected)
                    {
                        Debug.LogWarning("Target bones use a mixture of integration types!");
                    }
                }
            }

            root.AddChild(AnimateFloatProperty(context, "Forces/Spring or Momentum", "spring", new Vector2(0, 1)));
            
            if (declaration.integrationTypeToggle ||
                mainBone.integrationType == VRCPhysBoneBase.IntegrationType.Advanced)
            {
                root.AddChild(AnimateFloatProperty(context, "Forces/Stiffness", "stiffness", new Vector2(0, 1)));
            }

            root.AddChild(AnimateFloatProperty(context, "Forces/Gravity", "gravity", new Vector2(-1, 1)));
            root.AddChild(AnimateFloatProperty(context, "Forces/Gravity Falloff", "gravityFalloff", new Vector2(0, 1)));

            root.AddChild(AnimateIntToggleProperty(context, "Forces/Immobile Type", "immobileType",
                new List<(string label, int value)>()
                {
                    ("All Motion", 0),
                    ("World", 1)
                }));

            root.AddChild(AnimateFloatProperty(context, "Forces/Immobile", "immobile", new Vector2(0, 1)));

            root.AddChild(AnimateIntToggleProperty(context, "Limits/Limit Type", "limitType",
                new List<(string label, int value)>()
                {
                    ("None", 0),
                    ("Angle", 1),
                    ("Hinge", 2),
                    ("Polar", 3)
                }));

            string angleString = $"{declaration.angleRange.x:N0} .. {declaration.angleRange.y:N0}";
            
            root.AddChild(AnimateFloatProperty(context, "Limits/Max Angle (Pitch)\n" + angleString, "maxAngleX", declaration.angleRange));
            root.AddChild(AnimateFloatProperty(context, "Limits/Max Angle (Yaw)\n" + angleString, "maxAngleZ", declaration.angleRange));

            angleString = "-180 .. 180";
            root.AddChild(AnimateFloatProperty(context, "Limits/Rotation/X\n" + angleString, "limitRotation.x",
                new Vector2(-180, 180)));
            root.AddChild(AnimateFloatProperty(context, "Limits/Rotation/Y\n" + angleString, "limitRotation.y",
                new Vector2(-180, 180)));
            root.AddChild(AnimateFloatProperty(context, "Limits/Rotation/Z\n" + angleString, "limitRotation.z",
                new Vector2(-180, 180)));

            root.AddChild(AnimateFloatProperty(context, "Collision/Radius", "radius", new Vector2(0, 1)));

            root.AddChild(AnimateIntToggleProperty(context, "Collision/Mode", "allowCollision",
                new List<(string label, int value)>()
                {
                    ("False", 0),
                    ("True", 1),
                    ("Other", 2),
                }));

            root.AddChild(AnimateBoolProperty(context, "Collision/Allow Self", "collisionFilter.allowSelf"));
            root.AddChild(AnimateBoolProperty(context, "Collision/Allow Others", "collisionFilter.allowOthers"));

            root.AddChild(AnimateFloatProperty(context, "Stretch + Squish/Stretch Motion", "stretchMotion",
                new Vector2(0, 1)));
            root.AddChild(
                AnimateFloatProperty(context, "Stretch + Squish/Max Stretch", "maxStretch", new Vector2(0, 1)));
            root.AddChild(AnimateFloatProperty(context, "Stretch + Squish/Max Squish", "maxSquish", new Vector2(0, 1)));

            root.AddChild(AnimateFloatProperty(context, "Grab + Pose/Grab Movement", "grabMovement",
                new Vector2(0, 1)));
            root.AddChild(AnimateBoolProperty(context, "Grab + Pose/Snap to Hand", "snapToHand"));

            root.AddChild(AnimateBoolProperty(context, "Options/Is Animated", "isAnimated"));

            var rootChildren = root.children;

            for (int i = 0; i < rootChildren.Length; ++i)
            {
                rootChildren[i].directBlendParameter = "Constant/One";
            }

            root.children = rootChildren;

            var sensorChildren = context.changeSensorTree.children;

            for (int i = 0; i < sensorChildren.Length; ++i)
            {
                sensorChildren[i].directBlendParameter = "Constant/One";
            }

            context.changeSensorTree.children = sensorChildren;

            fc.AddController(controller);
            fc.AddParams(parameters);
            fc.AddMenu(context.ResolveHierarchy(8), declaration.menuPath);
        }

        private static Motion AnimateBoolProperty(GeneratorContext context, string menuPath, string propertyName)
        {
            string parameterName = $"Control/{propertyName}";
            VRCPhysBone mainBone = context.declaration.targets[0];

            SerializedObject obj = new(mainBone);
            SerializedProperty prop = obj.FindProperty(propertyName);
            bool currentValue = prop.boolValue;

            context.AddParameter(new VRCExpressionParameters.Parameter
            {
                name = parameterName,
                saved = true,
                defaultValue = currentValue ? 1f : 0f,
                networkSynced = !context.declaration.localOnly,
                valueType = VRCExpressionParameters.ValueType.Bool
            });

            context.generatedController.AddParameter(parameterName, AnimatorControllerParameterType.Float);

            AnimationClip minClip = new();
            AnimationClip maxClip = new();

            minClip.name = propertyName + " - Minimum";
            maxClip.name = propertyName + " - Maximum";

            foreach (var physbone in context.declaration.targets)
            {
                string path = GetPath(context.avatarRoot, physbone.gameObject.transform);
                Type type = typeof(VRCPhysBone);

                minClip.SetCurve(path, type, propertyName, AnimationCurve.Constant(0, 1, 0));
                maxClip.SetCurve(path, type, propertyName, AnimationCurve.Constant(0, 1, 1));
            }

            context.AddControl(menuPath, new VRCExpressionsMenu.Control
            {
                parameter = new VRCExpressionsMenu.Control.Parameter
                {
                    name = parameterName
                },
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
            });

            var root = new BlendTree
            {
                name = propertyName,
                blendType = BlendTreeType.Simple1D,
                blendParameter = parameterName,
                useAutomaticThresholds = false
            };

            root.AddChild(minClip);
            root.AddChild(maxClip);

            var children = root.children;

            children[0].threshold = 0f;
            children[1].threshold = 1f;

            root.children = children;

            context.AddChangeDetector(parameterName);

            return root;
        }

        // a convenient way to reinterpret an integer as a float!
        // https://stackoverflow.com/questions/173133/how-to-reinterpret-cast-a-float-to-an-int-is-there-a-non-static-conversion-oper
        [StructLayout(LayoutKind.Explicit)]
        private struct IntFloat
        {
            [FieldOffset(0)] public int IntValue;
            [FieldOffset(0)] public float FloatValue;
        }

        private static Motion AnimateIntToggleProperty(GeneratorContext context, string menuPath, string propertyName,
            List<(string label, int value)> choices)
        {
            string parameterName = $"Control/{propertyName}";
            VRCPhysBone mainBone = context.declaration.targets[0];

            SerializedObject obj = new(mainBone);
            SerializedProperty prop = obj.FindProperty(propertyName);
            int currentValue = prop.intValue;
            int defaultIndex = -1;

            int idx = 0;
            
            foreach (var choice in choices)
            {
                if (choice.value == currentValue)
                {
                    defaultIndex = idx;
                    break;
                }
                ++idx;
            }

            if (defaultIndex == -1)
            {
                Debug.LogError($"The current value for the {propertyName} enum isn't in the list of choices!");
                defaultIndex = 0;
            }

            context.AddParameter(new VRCExpressionParameters.Parameter
            {
                name = parameterName,
                saved = true,
                defaultValue = defaultIndex,
                networkSynced = !context.declaration.localOnly,
                valueType = VRCExpressionParameters.ValueType.Int
            });

            context.generatedController.AddParameter(parameterName, AnimatorControllerParameterType.Float);

            idx = 0;

            var root = new BlendTree
            {
                name = propertyName,
                blendType = BlendTreeType.Simple1D,
                blendParameter = parameterName,
                useAutomaticThresholds = false
            };

            foreach (var choice in choices)
            {
                // Integer curves are stored by reinterpreting an int as a float.
                IntFloat convert;
                convert.FloatValue = 0;
                convert.IntValue = choice.value;

                var clip = new AnimationClip
                {
                    name = propertyName + " - " + choice.value
                };

                foreach (var physbone in context.declaration.targets)
                {
                    string path = GetPath(context.avatarRoot, physbone.gameObject.transform);
                    Type type = typeof(VRCPhysBone);

                    clip.SetCurve(path, type, propertyName, AnimationCurve.Constant(0, 1, convert.FloatValue));
                }

                root.AddChild(clip);

                context.AddControl(menuPath + "/" + choice.label, new VRCExpressionsMenu.Control
                {
                    parameter = new VRCExpressionsMenu.Control.Parameter
                    {
                        name = parameterName
                    },
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                    value = idx
                });

                ++idx;
            }

            var children = root.children;

            for (int i = 0; i < children.Length; ++i)
            {
                children[i].threshold = i;
            }

            root.children = children;

            context.AddChangeDetector(parameterName);

            return root;
        }

        private static Motion AnimateFloatProperty(GeneratorContext context, string menuPath,
            string propertyName, Vector2 range)
        {
            string parameterName = $"Control/{propertyName}";
            VRCPhysBone mainBone = context.declaration.targets[0];

            SerializedObject obj = new(mainBone);
            SerializedProperty prop = obj.FindProperty(propertyName);
            float currentValue = prop.floatValue;

            float currentTime = Mathf.InverseLerp(range.x, range.y, currentValue);

            context.AddParameter(new VRCExpressionParameters.Parameter
            {
                name = parameterName,
                saved = true,
                defaultValue = currentTime,
                networkSynced = !context.declaration.localOnly,
                valueType = VRCExpressionParameters.ValueType.Float
            });

            context.generatedController.AddParameter(parameterName, AnimatorControllerParameterType.Float);

            AnimationClip minClip = new();
            AnimationClip maxClip = new();

            minClip.name = propertyName + " - Minimum";
            maxClip.name = propertyName + " - Maximum";

            foreach (var physbone in context.declaration.targets)
            {
                string path = GetPath(context.avatarRoot, physbone.gameObject.transform);
                Type type = typeof(VRCPhysBone);

                minClip.SetCurve(path, type, propertyName, AnimationCurve.Constant(0, 1, range.x));
                maxClip.SetCurve(path, type, propertyName, AnimationCurve.Constant(0, 1, range.y));
            }

            context.AddControl(menuPath, new VRCExpressionsMenu.Control
            {
                parameter = new VRCExpressionsMenu.Control.Parameter
                {
                    name = "Control/Adjusting"
                },
                type = VRCExpressionsMenu.Control.ControlType.RadialPuppet,
                subParameters = new[]
                {
                    new VRCExpressionsMenu.Control.Parameter
                    {
                        name = parameterName
                    }
                }
            });

            var root = new BlendTree
            {
                name = propertyName,
                blendType = BlendTreeType.Simple1D,
                blendParameter = parameterName,
                useAutomaticThresholds = false
            };

            root.AddChild(minClip);
            root.AddChild(maxClip);

            var children = root.children;

            children[0].threshold = 0f;
            children[1].threshold = 1f;

            root.children = children;

            return root;
        }

        private static void AddPhysboneToggle(GeneratorContext context)
        {
            var toggleMachine = new AnimatorStateMachine
            {
                name = "Toggle"
            };

            var toggleLayer = new AnimatorControllerLayer
            {
                name = "Toggle",
                defaultWeight = 1f,
                stateMachine = toggleMachine
            };

            context.generatedController.AddLayer(toggleLayer);

            var toggleOnState = toggleMachine.AddState("On");
            var toggleOffState = toggleMachine.AddState("Off");

            var toggleOnClip = new AnimationClip();
            var toggleOffClip = new AnimationClip();

            toggleOnClip.name = "Enable Physbones";
            toggleOffClip.name = "Disable Physbones";

            foreach (var physbone in context.declaration.targets)
            {
                var path = GetPath(context.avatarRoot, physbone.transform);
                Type type = typeof(VRCPhysBone);

                toggleOnClip.SetCurve(path, type, "m_Enabled", AnimationCurve.Constant(0, 1, 1));
                toggleOffClip.SetCurve(path, type, "m_Enabled", AnimationCurve.Constant(0, 1, 0));
            }

            toggleOnState.motion = toggleOnClip;
            toggleOffState.motion = toggleOffClip;

            var disableTransition = toggleOnState.AddTransition(toggleOffState);
            disableTransition.hasExitTime = false;
            disableTransition.AddCondition(AnimatorConditionMode.If, 0, "Control/Adjusting");
            disableTransition.duration = 0f;

            var enableTransition = toggleOffState.AddTransition(toggleOnState);
            enableTransition.hasExitTime = false;
            enableTransition.AddCondition(AnimatorConditionMode.IfNot, 0, "Control/Adjusting");
            enableTransition.duration = 0f;
        }

        private static string GetPath(Transform root, Transform target)
        {
            string path = target.name;
            target = target.parent;

            while (target != root)
            {
                path = target.name + "/" + path;
                target = target.parent;
            }

            return path;
        }
    }
}