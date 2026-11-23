using MotionMatching.Core;
using MotionMatching.Editor;
using NUnit.Framework;

namespace MotionMatching.Tests.Editor
{
    public sealed class RootMotionAccumulatorTests
    {
        [Test]
        public void SameFrameSamplesAreSummedAndConsumedOnce()
        {
            var buffer = new RootMotionAccumulator();
            buffer.Submit(10, 1f, 2f);
            buffer.Submit(10, -0.5f, 3f);
            buffer.Consume(10, out float x, out float z);
            Assert.That(x, Is.EqualTo(0.5f));
            Assert.That(z, Is.EqualTo(5f));
            buffer.Consume(10, out x, out z);
            Assert.That(x, Is.Zero);
            Assert.That(z, Is.Zero);
        }

        [Test]
        public void MissingCallbacksDiscardPriorFrameTravel()
        {
            var buffer = new RootMotionAccumulator();
            buffer.Submit(10, 5f, 6f);
            buffer.Consume(11, out float x, out float z);
            Assert.That(x, Is.Zero);
            Assert.That(z, Is.Zero);
        }

        [Test]
        public void ClearAndNewFrameCannotReplayOldSamples()
        {
            var buffer = new RootMotionAccumulator();
            buffer.Submit(10, 5f, 6f);
            buffer.Clear();
            buffer.Submit(11, 1f, 2f);
            buffer.Consume(11, out float x, out float z);
            Assert.That(x, Is.EqualTo(1f));
            Assert.That(z, Is.EqualTo(2f));
        }

        [Test]
        public void UnityImportedAllFiveBoundaries()
        {
            Assert.That(AssemblyBoundaryAudit.ValidateCompiledAssemblies(), Is.Empty);
            Assert.That(typeof(RootMotionAccumulator).Assembly.GetReferencedAssemblies(),
                Has.None.Property("Name").StartsWith("UnityEngine"));
        }
    }
}
