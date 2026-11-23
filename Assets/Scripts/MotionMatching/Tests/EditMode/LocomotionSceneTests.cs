using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MotionMatching.Tests.Editor
{
    public sealed class LocomotionSceneTests
    {
        private const string ScenePath = "Assets/Scenes/MotionMatchingTest.unity";
        private const string ControllerPath = "Assets/Animations/MotionMatching/UnarmedLocomotion.controller";
        private Scene _scene;
        private Component[] _components;

        [SetUp]
        public void OpenIsolatedPreview()
        {
            _scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            _components = _scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToArray();
        }

        [TearDown]
        public void ClosePreview()
        {
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
        }

        [Test]
        public void LocomotionRetainsCapsuleInputRootRelayAndFreeCamera()
        {
            Assert.That(_components.Count(component => component is CharacterController), Is.EqualTo(1));
            Component player = RequireType("PlayerController");
            Component input = RequireType("PlayerInputReader");
            Component relay = RequireType("RootMotionRelay");
            Component facade = RequireType("PlayerAnimator");
            foreach (string field in new[] { "inputReader", "playerAnimator", "cameraTransform", "groundCheckPoint" })
                RequireReference(player, field);
            foreach (string field in new[] { "moveAction", "sprintAction", "jumpAction", "diveRollAction" })
                RequireReference(input, field);
            RequireReference(relay, "animator");
            RequireReference(relay, "playerController");
            RequireReference(facade, "animator");

            Animator animator = _components.OfType<Animator>().Single();
            Assert.That(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController), Is.EqualTo(ControllerPath));
            Assert.That(animator.avatar, Is.Not.Null, "The preserved character needs its imported avatar.");
            Component freeCamera = RequireType("CinemachineCamera");
            RequireReference(freeCamera, "Target.TrackingTarget");
            RequireType("CinemachineBrain");
            RequireType("CinemachineOrbitalFollow");
            RequireType("CinemachineInputAxisController");
            Component cameraInput = RequireType("FreeCameraLookInput");
            RequireReference(cameraInput, "axisController");
            RequireReference(cameraInput, "inputActions");
            Assert.That(_components.OfType<Camera>().Count(camera => camera.CompareTag("MainCamera")), Is.EqualTo(1));
        }

        [Test]
        public void SceneHasNoShooterComponentsOrMissingSerializedReferences()
        {
            var forbidden = new HashSet<string> {
                "WeaponPickupItem", "WeaponAimAlignment", "RifleAimAlignmentDebug",
                "PlayerInteractionSystem", "PlayerAimTargetProvider", "PlayerAimRigController",
                "PlayerLeftHandIK", "TPSCameraController", "MouseWorldSphereTest"
            };
            foreach (Component component in _components)
            {
                Assert.That(component, Is.Not.Null, "The preview scene contains a missing script component.");
                Type type = component.GetType();
                Assert.That(forbidden.Contains(type.Name), Is.False, $"Shooter component remains: {type.FullName}");
                if (type.Name != "BoneRenderer")
                    Assert.That(type.Namespace ?? "", Does.Not.StartWith("UnityEngine.Animations.Rigging"),
                        $"Weapon rig or constraint remains: {type.FullName}");
                AuditReferences(component);
            }
        }

        [Test]
        public void DedicatedAnimatorRetainsUnarmedLocomotionJumpAndDiveOnly()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.layers.Length, Is.EqualTo(1), "Rifle overlay layers must be detached.");
            var states = new HashSet<string>();
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                AssertUnarmedName(layer.name);
                AuditStateMachine(layer.stateMachine, states);
            }
            Assert.That(states, Does.Contain("Locomotion"));
            Assert.That(states, Does.Contain("Jump"));
            Assert.That(states.Any(name => name.Replace(" ", "").Equals("DiveRoll", StringComparison.OrdinalIgnoreCase)), Is.True);
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath)) AuditReferences(asset);
        }

        private Component RequireType(string name)
        {
            Component[] matches = _components.Where(component => component != null && component.GetType().Name == name).ToArray();
            Assert.That(matches.Length, Is.EqualTo(1), $"Expected one preserved {name}.");
            return matches[0];
        }

        private static void RequireReference(Component owner, string field)
        {
            using (var serialized = new SerializedObject(owner))
            {
                SerializedProperty property = serialized.FindProperty(field);
                Assert.That(property, Is.Not.Null, $"Missing field {owner.GetType().Name}.{field}.");
                Assert.That(property.objectReferenceValue, Is.Not.Null, $"Unwired {owner.GetType().Name}.{field}.");
            }
        }

        private static void AuditReferences(UnityEngine.Object owner)
        {
            using (var serialized = new SerializedObject(owner))
            {
                SerializedProperty property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference &&
                        property.objectReferenceValue == null && !property.objectReferenceEntityIdValue.Equals(default(EntityId)))
                        Assert.Fail($"Missing object reference: {owner.name}.{property.propertyPath}");
            }
        }

        private static void AuditStateMachine(AnimatorStateMachine machine, HashSet<string> states)
        {
            AssertUnarmedName(machine.name);
            foreach (ChildAnimatorState child in machine.states)
            {
                states.Add(child.state.name);
                AssertUnarmedName(child.state.name);
                if (child.state.name.Replace(" ", "").Equals("DiveRoll", StringComparison.OrdinalIgnoreCase))
                    Assert.That(child.state.tag, Is.EqualTo("RootMotionAction"),
                        "Authored dive root motion must survive a zero locomotion Speed parameter.");
                AuditMotion(child.state.motion);
            }
            foreach (ChildAnimatorStateMachine child in machine.stateMachines) AuditStateMachine(child.stateMachine, states);
        }

        private static void AuditMotion(Motion motion)
        {
            if (motion == null) return;
            AssertUnarmedName(motion.name);
            if (motion is BlendTree tree)
                foreach (ChildMotion child in tree.children) AuditMotion(child.motion);
        }

        private static void AssertUnarmedName(string name)
        {
            string lower = name.ToLowerInvariant();
            Assert.That(lower.Contains("rifle") || lower.Contains("aim") || lower.Contains("ads"), Is.False,
                $"Weapon-specific graph node remains: {name}");
        }
    }
}
