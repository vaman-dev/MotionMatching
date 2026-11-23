using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MotionMatching.Tests.Runtime
{
    /// <summary>
    /// Exercises the existing gameplay controller without an Animator or root callback.
    /// Uses the consolidated Runtime types directly; reflection is limited to
    /// configuring private serialized fixture settings.
    /// </summary>
    public sealed class LegacyMotorRegressionTests
    {
        private GameObject _actor, _floor;
        private PlayerController _player;
        private float _originalTimeScale, _originalCaptureDeltaTime;

        [SetUp]
        public void SetUp()
        {
            _originalTimeScale = Time.timeScale;
            _originalCaptureDeltaTime = Time.captureDeltaTime;
            Time.timeScale = 1f;
            Time.captureDeltaTime = 1f / 60f;

            Assert.That(typeof(PlayerController).Assembly.GetName().Name, Is.EqualTo("MotionMatching.Runtime"));
            Assert.That(typeof(PlayerInputReader).Assembly.GetName().Name, Is.EqualTo("MotionMatching.Runtime"));
            _actor = new GameObject("Callback-independent gameplay motor regression");
            _actor.layer = 2; // Exclude the actor from the Default-layer ground query.
            _actor.transform.position = new Vector3(1000f, 10f, 1000f);
            CharacterController capsule = _actor.AddComponent<CharacterController>();
            capsule.height = 2f;
            capsule.radius = 0.3f;
            capsule.center = Vector3.up; // The actor pivot represents its feet.
            _actor.AddComponent<PlayerInputReader>();
            _player = _actor.AddComponent<PlayerController>();
            SetField("groundLayer", (LayerMask)0);
            Assert.That(_actor.GetComponentInChildren<Animator>(), Is.Null,
                "This fixture must never provide Animator.OnAnimatorMove callbacks.");
        }

        [TearDown]
        public void TearDown()
        {
            if (_actor != null) UnityEngine.Object.DestroyImmediate(_actor);
            if (_floor != null) UnityEngine.Object.DestroyImmediate(_floor);
            Time.captureDeltaTime = _originalCaptureDeltaTime;
            Time.timeScale = _originalTimeScale;
        }

        [UnityTest]
        public IEnumerator GravityMovesCapsuleWhenNoAnimatorCallbackExists()
        {
            Vector3 before = _actor.transform.position;
            for (int frame = 0; frame < 20; frame++) yield return null;

            Assert.That(_actor.transform.position.y, Is.LessThan(before.y - 0.1f),
                "Gravity must advance from the gameplay tick even when animation submits no root motion.");
            Assert.That(_actor.transform.position.x, Is.EqualTo(before.x).Within(0.001f));
            Assert.That(_actor.transform.position.z, Is.EqualTo(before.z).Within(0.001f));
            Assert.That(ReadGrounded(), Is.False, "The falling fixture has no eligible ground colliders.");
        }

        [UnityTest]
        public IEnumerator AuthoredJumpReachesApexAndLandsWithoutAnimatorCallbacks()
        {
            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.name = "Isolated jump regression floor";
            _floor.layer = 0;
            _floor.transform.position = new Vector3(1000f, 9.75f, 1000f);
            _floor.transform.localScale = new Vector3(10f, 0.5f, 10f);
            SetField("groundLayer", (LayerMask)1);
            Physics.SyncTransforms();
            for (int frame = 0; frame < 6; frame++) yield return null;
            Assert.That(ReadGrounded(), Is.True, "Fixture must be grounded before the authored jump.");

            float startingHeight = _actor.transform.position.y;
            float configuredHeight = (float)GetField("jumpHeight").GetValue(_player);
            _player.StartJump();
            float apex = startingHeight;
            bool airborne = !ReadGrounded();
            bool landedAfterRising = false;
            for (int frame = 0; frame < 90; frame++)
            {
                yield return null;
                apex = Mathf.Max(apex, _actor.transform.position.y);
                airborne |= !ReadGrounded();
                landedAfterRising |= apex > startingHeight + configuredHeight * 0.75f && ReadGrounded();
            }

            Assert.That(apex - startingHeight, Is.InRange(configuredHeight * 0.75f, configuredHeight * 1.05f),
                "The configured authored jump must produce physical ascent without an animation callback.");
            Assert.That(airborne, Is.True);
            Assert.That(landedAfterRising, Is.True, "Gravity and ground checks must bring the jump back to its floor.");
            Assert.That(ReadGrounded(), Is.True);
            Assert.That(_actor.transform.position.y, Is.EqualTo(startingHeight).Within(0.12f));
        }

        private bool ReadGrounded() => _player.IsGrounded;

        private FieldInfo GetField(string name)
        {
            FieldInfo field = typeof(PlayerController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Gameplay fixture field '{name}' changed; update the test deliberately.");
            return field;
        }

        private void SetField(string name, object value) => GetField(name).SetValue(_player, value);
    }
}
