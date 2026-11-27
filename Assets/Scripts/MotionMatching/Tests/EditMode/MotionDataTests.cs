using System;
using System.IO;
using NUnit.Framework;
using MotionMatching.Core;

namespace MotionMatching.Tests.Editor
{
    public class MotionDataTests
    {
        static MotionClipData Linear(bool loop=false,int count=121)
        {
            var c=new MotionClipData{Name="linear",Guid="test",SourceHash="hash",Duration=loop?count/60f:(count-1)/60f,IsLoop=loop,EndRootPosition=new MotionVector(0,0,loop?count/60f:(count-1)/60f),Samples=new MotionSample[count]};
            for(int i=0;i<count;i++)
            {
                float z=i/60f;c.Samples[i]=new MotionSample{RootPosition=new MotionVector(0,0,z),PelvisPosition=new MotionVector(0,1,z),LeftFootPosition=new MotionVector(-.1f,0,z),RightFootPosition=new MotionVector(.1f,0,z)};
            }
            return c;
        }
        [Test] public void RootLocalYawUsesUnityAxes()
        {
            var p=MotionMath.ToLocal(new MotionVector(1,2,0),new MotionVector(0,1,0),(float)Math.PI/2);
            Assert.That(p.x,Is.EqualTo(0).Within(1e-6));Assert.That(p.y,Is.EqualTo(1));Assert.That(p.z,Is.EqualTo(1).Within(1e-6));
        }
        [Test] public void WorldDerivativeRetainsRootTranslationInJointVelocities()
        {
            var d=MotionDatabaseBuilder.Build(new[]{Linear()},new MotionBakeSettings());
            Assert.That(d.JointVelocities[60*9+2],Is.EqualTo(1).Within(1e-5));
            Assert.That(d.RootLinearVelocities[60*3+2],Is.EqualTo(1).Within(1e-5));
            Assert.That(d.JointPositions[60*9+2],Is.EqualTo(0).Within(1e-6));
        }
        [Test] public void FeatureOrderAndFutureSecondsAreStable()
        {
            var d=MotionDatabaseBuilder.Build(new[]{Linear()},new MotionBakeSettings());
            Assert.That(d.RawFeatures[0],Is.EqualTo(0));Assert.That(d.RawFeatures[1],Is.EqualTo(1f/3).Within(1e-6));
            Assert.That(d.RawFeatures[3],Is.EqualTo(2f/3).Within(1e-6));Assert.That(d.RawFeatures[5],Is.EqualTo(1).Within(1e-6));
            Assert.That(d.RawFeatures[6],Is.EqualTo(0));Assert.That(d.RawFeatures[7],Is.EqualTo(1));
            Assert.That(d.RawFeatures[12],Is.EqualTo(-.1f));Assert.That(d.RawFeatures[15],Is.EqualTo(.1f));
            Assert.That(d.RawFeatures[20],Is.EqualTo(1).Within(1e-5));Assert.That(d.RawFeatures[26],Is.EqualTo(1).Within(1e-5));
        }
        [Test] public void NonloopFutureFramesAreExcludedFromStatistics()
        {
            var d=MotionDatabaseBuilder.Build(new[]{Linear()},new MotionBakeSettings());
            Assert.That(d.ValidFrames[60],Is.True);Assert.That(d.ValidFrames[61],Is.False);
            Assert.That(d.Means[5],Is.EqualTo(1).Within(1e-5));
        }
        [Test] public void LoopContinuesRootDisplacementAndJointVelocity()
        {
            var d=MotionDatabaseBuilder.Build(new[]{Linear(true,60)},new MotionBakeSettings());
            Assert.That(d.ValidFrames,Is.All.True);
            Assert.That(d.RawFeatures[59*27+5],Is.EqualTo(1).Within(1e-5));
            Assert.That(d.JointVelocities[2],Is.EqualTo(1).Within(1e-5));
        }
        [Test] public void TurningLoopAccumulatesRigidTransform()
        {
            var c=Linear(true,60);c.EndRootRotation=new MotionRotation(0,(float)Math.Sin(Math.PI/4),0,(float)Math.Cos(Math.PI/4));
            MotionDatabaseBuilder.RootAt(c,2,60,out var position,out float yaw);
            Assert.That(position.x,Is.EqualTo(1).Within(1e-5));Assert.That(position.z,Is.EqualTo(1).Within(1e-5));Assert.That(yaw,Is.EqualTo(Math.PI).Within(1e-5));
            MotionDatabaseBuilder.RootAt(c,-1,60,out position,out yaw);
            Assert.That(position.x,Is.EqualTo(1).Within(1e-5));Assert.That(position.z,Is.EqualTo(0).Within(1e-5));
        }
        [Test] public void ConstantDimensionHasSafeNormalizationScale()
        {
            var d=MotionDatabaseBuilder.Build(new[]{Linear()},new MotionBakeSettings());
            Assert.That(d.StandardDeviations[0],Is.EqualTo(0));Assert.That(d.NormalizationScales[0],Is.EqualTo(d.Settings.NormalizationEpsilon));Assert.That(d.NormalizedFeatures[0],Is.EqualTo(0));
        }
        [Test] public void CentralDifferenceComputesQuadraticVelocity()
        {
            var c=Linear();
            for(int i=0;i<c.Samples.Length;i++)
            {
                float t=i/60f;
                c.Samples[i].PelvisPosition=new MotionVector(t*t,1,t);
            }
            var d=MotionDatabaseBuilder.Build(new[]{c},new MotionBakeSettings());
            Assert.That(d.JointVelocities[60*9],Is.EqualTo(2).Within(1e-5));
            Assert.That(d.JointVelocities[0],Is.EqualTo(1f/60).Within(1e-6));
        }
        [Test] public void QuaternionAngularVelocityRetainsPitchAndHandlesSignEquivalence()
        {
            float angle=.2f;
            var q=new MotionRotation((float)Math.Sin(angle/2),0,0,(float)Math.Cos(angle/2));
            var v=MotionMath.AngularVelocity(MotionRotation.Identity,q,.1f);
            Assert.That(v.x,Is.EqualTo(2).Within(1e-5));Assert.That(v.y,Is.EqualTo(0).Within(1e-6));Assert.That(v.z,Is.EqualTo(0).Within(1e-6));
            var negative=new MotionRotation(-q.x,-q.y,-q.z,-q.w);
            v=MotionMath.AngularVelocity(q,negative,.1f);
            Assert.That(v.x,Is.EqualTo(0).Within(1e-6));
        }
        [Test] public void QuaternionAngularVelocityUsesShortestPathAcrossYawWrap()
        {
            float a=179*(float)Math.PI/180,b=-179*(float)Math.PI/180;
            var before=new MotionRotation(0,(float)Math.Sin(a/2),0,(float)Math.Cos(a/2));
            var after=new MotionRotation(0,(float)Math.Sin(b/2),0,(float)Math.Cos(b/2));
            var v=MotionMath.AngularVelocity(before,after,1);
            Assert.That(v.y,Is.EqualTo(2*Math.PI/180).Within(1e-6));
        }
        [Test] public void BakedRootAngularVelocityIncludesAllAxes()
        {
            var c=Linear();
            for(int i=0;i<c.Samples.Length;i++)
            {
                float angle=i/60f*.2f;
                c.Samples[i].RootRotation=new MotionRotation((float)Math.Sin(angle/2),0,0,(float)Math.Cos(angle/2));
            }
            var d=MotionDatabaseBuilder.Build(new[]{c},new MotionBakeSettings());
            Assert.That(d.RootAngularVelocities[60*3],Is.EqualTo(.2f).Within(1e-5));
            Assert.That(d.RootAngularVelocities[60*3+1],Is.EqualTo(0).Within(1e-6));
        }
        [Test] public void HorizonUsesSecondsAtDifferentSampleRate()
        {
            var c=Linear(false,121);c.Duration=4;
            for(int i=0;i<c.Samples.Length;i++)
            {
                float t=i/30f;
                c.Samples[i].RootPosition.z=t;
                c.Samples[i].PelvisPosition.z=t;
                c.Samples[i].LeftFootPosition.z=t;
                c.Samples[i].RightFootPosition.z=t;
            }
            var d=MotionDatabaseBuilder.Build(new[]{c},new MotionBakeSettings{SampleRate=30});
            Assert.That(d.RawFeatures[1],Is.EqualTo(1f/3).Within(1e-5));
            Assert.That(d.RawFeatures[5],Is.EqualTo(1).Within(1e-5));
        }
        [Test] public void NonAlignedLoopIsRejectedWithoutInventingSeamSamples()
        {
            var c=Linear(true,60);c.Duration=1.001f;
            Assert.Throws<ArgumentException>(()=>MotionDatabaseBuilder.Build(new[]{c},new MotionBakeSettings()));
        }
        [Test] public void PartialNonloopEndpointDoesNotCreateSearchableFuture()
        {
            var c=Linear(false,121);c.Duration=2.009f;c.EndRootPosition.z=c.Duration;
            var d=MotionDatabaseBuilder.Build(new[]{c},new MotionBakeSettings());
            Assert.That(d.ValidFrames[60],Is.True);Assert.That(d.ValidFrames[61],Is.False);
            Assert.That(d.RawFeatures[120*27+5],Is.EqualTo(0).Within(1e-6));
        }
        [Test] public void NormalizedValidFramesHaveUnitPopulationVariance()
        {
            var c=Linear();for(int i=0;i<c.Samples.Length;i++)c.Samples[i].LeftFootPosition.x=i*.01f;
            var d=MotionDatabaseBuilder.Build(new[]{c},new MotionBakeSettings());double sum=0,square=0;int n=0;
            for(int i=0;i<d.FrameCount;i++)if(d.ValidFrames[i]){double x=d.NormalizedFeatures[i*27+12];sum+=x;square+=x*x;n++;}
            Assert.That(sum/n,Is.EqualTo(0).Within(1e-6));Assert.That(square/n,Is.EqualTo(1).Within(1e-5));
        }
        [Test] public void BinaryRoundTripRetainsFeaturesPoseAndDeterministicBytes()
        {
            var c=Linear();c.BoneNames=new[]{"Hips"};c.LocalPosePositions=new float[c.Samples.Length*3];c.LocalPoseRotations=new float[c.Samples.Length*4];for(int i=0;i<c.Samples.Length;i++)c.LocalPoseRotations[i*4+3]=1;
            var a=MotionDatabaseBinary.Serialize(MotionDatabaseBuilder.Build(new[]{c},new MotionBakeSettings()));
            var loaded=MotionDatabaseBinary.Deserialize(a);var b=MotionDatabaseBinary.Serialize(loaded);
            CollectionAssert.AreEqual(a,b);Assert.That(loaded.Clips[0].BoneNames[0],Is.EqualTo("Hips"));Assert.That(MotionDatabaseBinary.Hash(a),Is.EqualTo(MotionDatabaseBinary.Hash(b)));
        }
        [Test] public void RepeatedBakeHasIdenticalHash()
        {
            var a=MotionDatabaseBinary.Serialize(MotionDatabaseBuilder.Build(new[]{Linear()},new MotionBakeSettings()));
            var b=MotionDatabaseBinary.Serialize(MotionDatabaseBuilder.Build(new[]{Linear()},new MotionBakeSettings()));CollectionAssert.AreEqual(a,b);
        }
        [Test] public void InvalidInputsAreRejected()
        {
            Assert.Throws<ArgumentException>(()=>MotionDatabaseBuilder.Build(Array.Empty<MotionClipData>(),new MotionBakeSettings()));
            var c=Linear();c.Samples[4].RootPosition.x=float.NaN;Assert.Throws<ArgumentException>(()=>MotionDatabaseBuilder.Build(new[]{c},new MotionBakeSettings()));
            Assert.Throws<ArgumentException>(()=>MotionDatabaseBuilder.Build(new[]{Linear()},new MotionBakeSettings{FutureSeconds=new[]{1f,1f,2f}}));
            Assert.Throws<ArgumentException>(()=>MotionDatabaseBuilder.Build(new[]{Linear(false,4)},new MotionBakeSettings()));
        }
        [Test] public void CorruptBinaryIsRejected()
        {
            var a=MotionDatabaseBinary.Serialize(MotionDatabaseBuilder.Build(new[]{Linear()},new MotionBakeSettings()));a[0]=0;
            Assert.Throws<InvalidDataException>(()=>MotionDatabaseBinary.Deserialize(a));
        }
    }
}
