using System;
using MotionMatching.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MotionMatching.Tests.Editor
{
    public sealed class MotionTransitionMathTests
    {
        [TestCase(0f)] [TestCase(.00001f)] [TestCase(45f)] [TestCase(179.999f)]
        [TestCase(180f)] [TestCase(181f)] [TestCase(359.999f)]
        public void LogExpPreservesRotationAtSmallAnglesAndHalfTurn(float degrees)
        {
            Quaternion rotation = Quaternion.AngleAxis(degrees, new Vector3(.3f, -.7f, .5f).normalized);
            Vector3 logarithm = MotionTransitionMath.Log(rotation);
            Assert.That(float.IsFinite(logarithm.x) && float.IsFinite(logarithm.y) && float.IsFinite(logarithm.z), Is.True);
            Assert.That(logarithm.magnitude, Is.LessThanOrEqualTo(Mathf.PI + 1e-5f));
            Assert.That(Quaternion.Angle(MotionTransitionMath.Exp(logarithm), rotation), Is.LessThan(.05f));
            if (degrees < .001f)
                Assert.That(Vector3.Distance(logarithm, new Vector3(.3f, -.7f, .5f).normalized * degrees * Mathf.Deg2Rad), Is.LessThan(1e-7f));
        }

        [Test] public void QuaternionSignDoesNotChangeShortestLogAwayFromPiBranch()
        {
            foreach (float angle in new[] { .001f, 90f, 179.99f, 180.01f, 270f })
            {
                var q = Quaternion.AngleAxis(angle, new Vector3(1, 2, -3).normalized);
                var negative = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                Assert.That(Vector3.Distance(MotionTransitionMath.Log(q), MotionTransitionMath.Log(negative)), Is.LessThan(1e-6f));
            }
            // At exactly pi the two axis signs are equivalent; require rotation equivalence, not a unique log axis.
            foreach (var q in new[] { new Quaternion(1, 0, 0, 0), new Quaternion(-1, 0, 0, 0) })
                Assert.That(Quaternion.Angle(MotionTransitionMath.Exp(MotionTransitionMath.Log(q)), q), Is.LessThan(.05f));
        }

        [TestCase(0f, 0f, 0f)] [TestCase(.4f, -.8f, .3f)] [TestCase(.3f, .4f, 3f)]
        public void SpatialLogDerivativeMatchesIndependentCentralRotationDifference(float x, float y, float z)
        {
            Vector3 log = new Vector3(x, y, z), omega = new Vector3(.2f, .6f, -.5f);
            Quaternion q = Quaternion.AngleAxis(log.magnitude * Mathf.Rad2Deg, log.normalized);
            const float dt = .001f;
            Quaternion advance = Quaternion.AngleAxis(omega.magnitude * dt * Mathf.Rad2Deg, omega.normalized);
            Vector3 finiteDifference = (IndependentRotationVector(advance * q) - IndependentRotationVector(Quaternion.Inverse(advance) * q)) / (2 * dt);
            Assert.That(Vector3.Distance(MotionTransitionMath.LogDerivative(log, omega), finiteDifference), Is.LessThan(.003f),
                "Spatial angular velocity left-multiplies the rotation; using the body Jacobian changes the cross-product sign.");
        }

        [Test] public void CriticalDecayHasSpecifiedPositionalHalfLifeAndAnalyticVelocity()
        {
            const float halfLife = .08f;
            Vector3 offset = new Vector3(1, -2, .5f), velocity = Vector3.zero;
            MotionTransitionMath.Decay(ref offset, ref velocity, halfLife, halfLife);
            Assert.That(Vector3.Distance(offset, new Vector3(.5f, -1, .25f)), Is.LessThan(1e-6f));
            double k = 1.67834699001666 / halfLife;
            float expectedVelocity = (float)(-k * k * halfLife * Math.Exp(-k * halfLife));
            Assert.That(Vector3.Distance(velocity, new Vector3(1, -2, .5f) * expectedVelocity), Is.LessThan(1e-5f));
        }

        [Test] public void CriticalDecayIsInvariantToStepPartitionWithNonzeroVelocity()
        {
            Vector3 once = new Vector3(2, -3, .5f), onceVelocity = new Vector3(.6f, -.4f, 1.2f);
            Vector3 partitioned = once, partitionedVelocity = onceVelocity;
            MotionTransitionMath.Decay(ref once, ref onceVelocity, .11f, .37f);
            for (int i = 0; i < 37; i++) MotionTransitionMath.Decay(ref partitioned, ref partitionedVelocity, .11f, .01f);
            Assert.That(Vector3.Distance(once, partitioned), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance(onceVelocity, partitionedVelocity), Is.LessThan(1e-5f));
        }

        [Test] public void ZeroDurationPreservesStateAndZeroHalfLifeClearsOffsets()
        {
            Vector3 offset = new Vector3(1, 2, 3), velocity = new Vector3(-4, 5, 6);
            MotionTransitionMath.Decay(ref offset, ref velocity, .08f, 0);
            Assert.That(offset, Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(velocity, Is.EqualTo(new Vector3(-4, 5, 6)));
            MotionTransitionMath.Decay(ref offset, ref velocity, 0, .016f);
            Assert.That(offset, Is.EqualTo(Vector3.zero)); Assert.That(velocity, Is.EqualTo(Vector3.zero));
        }

        static Vector3 IndependentRotationVector(Quaternion value)
        {
            value = value.normalized;
            if (value.w < 0) value = new Quaternion(-value.x, -value.y, -value.z, -value.w);
            // Avoid ToAngleAxis precision loss for a nearly identity quaternion in the finite-difference oracle.
            if (value.w > .9999f) return new Vector3(value.x, value.y, value.z) * 2;
            value.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180) angle -= 360;
            return axis * (angle * Mathf.Deg2Rad);
        }
    }
}
