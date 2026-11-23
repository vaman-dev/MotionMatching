using System.Collections;
using MotionMatching.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MotionMatching.Tests.Runtime
{
    public sealed class CharacterMotorTests
    {
        private GameObject _actor, _wall;
        private CharacterController _capsule;
        private CharacterMotor _motor;

        [SetUp]
        public void SetUp()
        {
            // An isolated location avoids the loaded scene's geometry.
            _actor = new GameObject("Motor test capsule");
            _actor.transform.position = new Vector3(1000f, 10f, 1000f);
            _capsule = _actor.AddComponent<CharacterController>();
            _motor = new CharacterMotor(_capsule);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_actor);
            if (_wall != null) Object.DestroyImmediate(_wall);
        }

        [Test]
        public void RootMotionAndGameplayAreAppliedOnceIgnoringAnimationY()
        {
            Vector3 before = _actor.transform.position;
            _motor.SubmitRootMotion(new Vector3(1f, 50f, 2f));
            _motor.Step(new Vector3(0f, -0.25f, 0f), 0.1f);
            _motor.Step(Vector3.one * 10f, 0.1f);
            Assert.That(Vector3.Distance(_actor.transform.position - before, new Vector3(1f, -0.25f, 2f)), Is.LessThan(0.001f));
            _motor.ClearPendingRootMotion();
            Assert.That(_motor.ActualVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(_motor.CollisionFlags, Is.EqualTo(CollisionFlags.None));
        }

        [UnityTest]
        public IEnumerator MissingAndLateCallbacksNeverReplayRootTravel()
        {
            Vector3 before = _actor.transform.position;
            _motor.SubmitRootMotion(Vector3.right);
            _motor.Step(Vector3.zero, 0.1f);
            _motor.SubmitRootMotion(Vector3.right * 20f);
            yield return null;
            _motor.Step(Vector3.down * 0.25f, 0.1f);
            Assert.That(Vector3.Distance(_actor.transform.position - before, new Vector3(1f, -0.25f, 0f)), Is.LessThan(0.001f));
        }

        [UnityTest]
        public IEnumerator DisabledCapsuleDropsPendingTravel()
        {
            Vector3 before = _actor.transform.position;
            _capsule.enabled = false;
            _motor.SubmitRootMotion(Vector3.right * 20f);
            _motor.Step(Vector3.zero, 0.1f);
            yield return null;
            _capsule.enabled = true;
            _motor.Step(Vector3.down * 0.25f, 0.1f);
            Assert.That(Vector3.Distance(_actor.transform.position - before, Vector3.down * 0.25f), Is.LessThan(0.001f));
        }

        [Test]
        public void CapsuleCollisionResolvesActualDisplacement()
        {
            Vector3 before = _actor.transform.position;
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = before + Vector3.right * 2f;
            _wall.transform.localScale = new Vector3(0.5f, 10f, 10f);
            Physics.SyncTransforms();
            _motor.SubmitRootMotion(Vector3.right * 5f);
            _motor.Step(Vector3.zero, 0.1f);
            float travel = _actor.transform.position.x - before.x;
            Assert.That(travel, Is.InRange(0.5f, 1.5f));
            Assert.That(_motor.ActualVelocity.x, Is.EqualTo(travel / 0.1f).Within(0.001f));
            Assert.That(_motor.CollisionFlags & CollisionFlags.Sides, Is.EqualTo(CollisionFlags.Sides));
        }
    }
}
