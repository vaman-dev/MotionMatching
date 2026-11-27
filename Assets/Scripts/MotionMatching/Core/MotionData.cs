using System;
using System.IO;
using System.Security.Cryptography;

namespace MotionMatching.Core
{
    [Serializable] public struct MotionVector
    {
        public float x, y, z;
        public MotionVector(float x, float y, float z) { this.x=x; this.y=y; this.z=z; }
        public static MotionVector operator +(MotionVector a, MotionVector b) => new MotionVector(a.x+b.x,a.y+b.y,a.z+b.z);
        public static MotionVector operator -(MotionVector a, MotionVector b) => new MotionVector(a.x-b.x,a.y-b.y,a.z-b.z);
        public static MotionVector operator *(MotionVector a, float b) => new MotionVector(a.x*b,a.y*b,a.z*b);
    }
    [Serializable] public struct MotionRotation
    {
        public float x,y,z,w;
        public MotionRotation(float x,float y,float z,float w) {this.x=x;this.y=y;this.z=z;this.w=w;}
        public static MotionRotation Identity => new MotionRotation(0,0,0,1);
    }
    [Serializable] public sealed class MotionSample
    {
        public MotionVector RootPosition, PelvisPosition, LeftFootPosition, RightFootPosition;
        public MotionRotation RootRotation = MotionRotation.Identity;
    }
    [Serializable] public sealed class MotionClipData
    {
        public string Name="", Guid="", SourceHash="", Tag="";
        public float Duration;
        public bool IsLoop;
        public MotionVector EndRootPosition;
        public MotionRotation EndRootRotation=MotionRotation.Identity;
        public MotionSample[] Samples=Array.Empty<MotionSample>();
        public string[] BoneNames=Array.Empty<string>();
        public float[] LocalPosePositions=Array.Empty<float>(), LocalPoseRotations=Array.Empty<float>();
        public int FrameOffset;
    }
    [Serializable] public sealed class MotionBakeSettings
    {
        public int SchemaVersion=1;
        public float SampleRate=60;
        public float[] FutureSeconds={20f/60,40f/60,1};
        public float NormalizationEpsilon=0.00001f;
        public float ContactHeight=0.05f, ContactSpeed=0.2f;
    }
    [Serializable] public sealed class MotionDatabaseData
    {
        public const int FeatureDimensions=27;
        public MotionBakeSettings Settings;
        public MotionClipData[] Clips;
        public float[] RawFeatures,NormalizedFeatures,Means,StandardDeviations,NormalizationScales;
        // Joint arrays are pelvis, left foot, right foot; root-local positions and velocities, xyz.
        public float[] RootLinearVelocities, RootAngularVelocities, JointPositions,JointVelocities;
        public bool[] ValidFrames,LeftFootContacts,RightFootContacts;
        public int FrameCount => ValidFrames.Length;
    }
    public static class MotionMath
    {
        public static bool Finite(float x) => !float.IsNaN(x)&&!float.IsInfinity(x);
        public static float Yaw(MotionRotation q)
        {
            double norm=(double)q.x*q.x+(double)q.y*q.y+(double)q.z*q.z+(double)q.w*q.w;
            if(!Finite(q.x)||!Finite(q.y)||!Finite(q.z)||!Finite(q.w)||norm<1e-12) throw new ArgumentException("Invalid root rotation");
            return (float)Math.Atan2(2*((double)q.w*q.y+(double)q.x*q.z)/norm,1-2*((double)q.y*q.y+(double)q.x*q.x)/norm);
        }
        public static float AngleDelta(float from,float to) => (float)Math.Atan2(Math.Sin(to-from),Math.Cos(to-from));
        public static MotionVector Rotate(MotionVector p,float yaw)
        {
            float c=(float)Math.Cos(yaw),s=(float)Math.Sin(yaw);
            return new MotionVector(c*p.x+s*p.z,p.y,-s*p.x+c*p.z);
        }
        public static MotionVector ToLocal(MotionVector world,MotionVector origin,float yaw) => Rotate(world-origin,-yaw);
        public static MotionVector Velocity(MotionVector before,MotionVector after,float elapsed)
        {
            if(!Finite(elapsed)||elapsed<=0)throw new ArgumentException("Velocity interval must be positive");
            return (after-before)*(1/elapsed);
        }
        /// <summary>Shortest quaternion difference as a world angular velocity in radians/second.</summary>
        public static MotionVector AngularVelocity(MotionRotation before,MotionRotation after,float elapsed)
        {
            if(!Finite(elapsed)||elapsed<=0)throw new ArgumentException("Angular velocity interval must be positive");
            Yaw(before);Yaw(after); // Validate finite, nonzero quaternions before normalization.
            double bn=Math.Sqrt((double)before.x*before.x+(double)before.y*before.y+(double)before.z*before.z+(double)before.w*before.w);
            double an=Math.Sqrt((double)after.x*after.x+(double)after.y*after.y+(double)after.z*after.z+(double)after.w*after.w);
            double bx=-before.x/bn,by=-before.y/bn,bz=-before.z/bn,bw=before.w/bn;
            double ax=after.x/an,ay=after.y/an,az=after.z/an,aw=after.w/an;
            double x=aw*bx+ax*bw+ay*bz-az*by;
            double y=aw*by-ax*bz+ay*bw+az*bx;
            double z=aw*bz+ax*by-ay*bx+az*bw;
            double w=aw*bw-ax*bx-ay*by-az*bz;
            if(w<0){x=-x;y=-y;z=-z;w=-w;}
            double magnitude=Math.Sqrt(x*x+y*y+z*z);
            double scale=magnitude<1e-12?2/elapsed:2*Math.Atan2(magnitude,w)/(magnitude*elapsed);
            return new MotionVector((float)(x*scale),(float)(y*scale),(float)(z*scale));
        }
        public static MotionRotation RotateByYaw(MotionRotation rotation,float yaw)
        {
            float s=(float)Math.Sin(yaw/2),c=(float)Math.Cos(yaw/2);
            return new MotionRotation(c*rotation.x+s*rotation.z,c*rotation.y+s*rotation.w,c*rotation.z-s*rotation.x,c*rotation.w-s*rotation.y);
        }
    }
    public static class MotionDatabaseBuilder
    {
        public static MotionDatabaseData Build(MotionClipData[] clips,MotionBakeSettings settings)
        {
            Validate(clips,settings);
            int count=0; foreach(var c in clips){c.FrameOffset=count;count=checked(count+c.Samples.Length);}
            var db=new MotionDatabaseData{Settings=settings,Clips=clips,RawFeatures=new float[count*27],NormalizedFeatures=new float[count*27],Means=new float[27],StandardDeviations=new float[27],NormalizationScales=new float[27],ValidFrames=new bool[count],LeftFootContacts=new bool[count],RightFootContacts=new bool[count],RootLinearVelocities=new float[count*3],RootAngularVelocities=new float[count*3],JointPositions=new float[count*9],JointVelocities=new float[count*9]};
            foreach(var c in clips)
            {
            float leftFloor=float.MaxValue,rightFloor=float.MaxValue;
            foreach(var sample in c.Samples)
            {
                leftFloor=Math.Min(leftFloor,sample.LeftFootPosition.y-sample.RootPosition.y);
                rightFloor=Math.Min(rightFloor,sample.RightFootPosition.y-sample.RootPosition.y);
            }
            for(int i=0;i<c.Samples.Length;i++)
            {
                int frame=c.FrameOffset+i, f=frame*27;
                float time=i/settings.SampleRate,yaw=MotionMath.Yaw(c.Samples[i].RootRotation);
                var sample=c.Samples[i];
                int before=Math.Max(0,i-1),after=Math.Min(c.Samples.Length-1,i+1);
                float elapsed=(after-before)/settings.SampleRate;
                MotionVector rootVelocity;
                MotionVector angular;
                if(c.IsLoop)
                {
                    RootAt(c,time-1/settings.SampleRate,settings.SampleRate,out var p0,out var y0);
                    RootAt(c,time+1/settings.SampleRate,settings.SampleRate,out var p1,out var y1);
                    rootVelocity=MotionMath.Velocity(p0,p1,2/settings.SampleRate);
                    angular=MotionMath.AngularVelocity(RootRotationAt(c,i-1),RootRotationAt(c,i+1),2/settings.SampleRate);
                }
                else
                {
                    rootVelocity=MotionMath.Velocity(c.Samples[before].RootPosition,c.Samples[after].RootPosition,elapsed);
                    angular=MotionMath.AngularVelocity(c.Samples[before].RootRotation,c.Samples[after].RootRotation,elapsed);
                }
                Put(db.RootLinearVelocities,frame*3,MotionMath.Rotate(rootVelocity,-yaw));
                Put(db.RootAngularVelocities,frame*3,MotionMath.Rotate(angular,-yaw));
                for(int joint=0;joint<3;joint++)
                {
                    var pos=MotionMath.ToLocal(Joint(sample,joint),sample.RootPosition,yaw);
                    MotionVector velocity;
                    if(c.IsLoop)
                    {
                        MotionVector a=JointAt(c,i-1,joint),b=JointAt(c,i+1,joint);
                        velocity=MotionMath.Velocity(a,b,2/settings.SampleRate);
                    }
                    else velocity=MotionMath.Velocity(Joint(c.Samples[before],joint),Joint(c.Samples[after],joint),elapsed);
                    velocity=MotionMath.Rotate(velocity,-yaw);
                    Put(db.JointPositions,frame*9+joint*3,pos);Put(db.JointVelocities,frame*9+joint*3,velocity);
                    if(joint==0)Put(db.RawFeatures,f+24,velocity);
                    else {Put(db.RawFeatures,f+12+(joint-1)*3,pos);Put(db.RawFeatures,f+18+(joint-1)*3,velocity);}
                    // Contact is only a heuristic against the clip's minimum sampled foot height.
                    if(joint>0)
                    {
                        float floor=joint==1?leftFloor:rightFloor;
                        bool contact=pos.y-floor<=settings.ContactHeight && velocity.x*velocity.x+velocity.y*velocity.y+velocity.z*velocity.z<=settings.ContactSpeed*settings.ContactSpeed;
                        if(joint==1)db.LeftFootContacts[frame]=contact;else db.RightFootContacts[frame]=contact;
                    }
                }
                bool valid=true;
                for(int h=0;h<3;h++)
                {
                    float future=time+settings.FutureSeconds[h];
                    if(!c.IsLoop&&future>(c.Samples.Length-1)/settings.SampleRate+0.000001f)valid=false;
                    RootAt(c,future,settings.SampleRate,out var p,out var r);
                    var local=MotionMath.ToLocal(p,sample.RootPosition,yaw);
                    db.RawFeatures[f+h*2]=local.x;db.RawFeatures[f+1+h*2]=local.z;
                    float delta=r-yaw;
                    db.RawFeatures[f+6+h*2]=(float)Math.Sin(delta);db.RawFeatures[f+7+h*2]=(float)Math.Cos(delta);
                }
                db.ValidFrames[frame]=valid;
            }
            }
            foreach(float value in db.RawFeatures)if(!MotionMath.Finite(value))throw new ArgumentException("Feature overflow: check source scale and sampling frequency");
            Normalize(db);return db;
        }
        public static void Normalize(MotionDatabaseData db)
        {
            int n=0;foreach(bool v in db.ValidFrames)if(v)n++;
            if(n==0)throw new ArgumentException("No searchable frames: add a longer clip or reduce trajectory horizons");
            for(int d=0;d<27;d++)
            {
                double sum=0;for(int i=0;i<db.FrameCount;i++)if(db.ValidFrames[i])sum+=db.RawFeatures[i*27+d];
                double mean=sum/n,variance=0;
                for(int i=0;i<db.FrameCount;i++)if(db.ValidFrames[i]){double delta=db.RawFeatures[i*27+d]-mean;variance+=delta*delta;}
                double std=Math.Sqrt(variance/n);db.Means[d]=(float)mean;db.StandardDeviations[d]=(float)std;db.NormalizationScales[d]=Math.Max((float)std,db.Settings.NormalizationEpsilon);
                for(int i=0;i<db.FrameCount;i++)db.NormalizedFeatures[i*27+d]=(db.RawFeatures[i*27+d]-db.Means[d])/db.NormalizationScales[d];
            }
        }
        static MotionVector Joint(MotionSample s,int j)=>j==0?s.PelvisPosition:j==1?s.LeftFootPosition:s.RightFootPosition;
        static MotionVector JointAt(MotionClipData c,int index,int joint)
        {
            int n=c.Samples.Length,cycle=(int)Math.Floor((double)index/n),wrapped=index-cycle*n;
            return TransformCycle(c,Joint(c.Samples[wrapped],joint),cycle);
        }
        static MotionRotation RootRotationAt(MotionClipData c,int index)
        {
            int n=c.Samples.Length,cycle=(int)Math.Floor((double)index/n),wrapped=index-cycle*n;
            float cycleYaw=MotionMath.AngleDelta(MotionMath.Yaw(c.Samples[0].RootRotation),MotionMath.Yaw(c.EndRootRotation));
            return MotionMath.RotateByYaw(c.Samples[wrapped].RootRotation,cycle*cycleYaw);
        }
        static MotionVector TransformCycle(MotionClipData c,MotionVector p,int cycles)
        {
            float yaw0=MotionMath.Yaw(c.Samples[0].RootRotation),yawEnd=MotionMath.Yaw(c.EndRootRotation),angle=MotionMath.AngleDelta(yaw0,yawEnd);
            MotionVector start=c.Samples[0].RootPosition,end=c.EndRootPosition;
            // Apply the cycle rigid transform (or inverse) repeatedly; no displacement reset at wrap.
            for(int k=0;k<Math.Abs(cycles);k++)p=cycles>0?end+MotionMath.Rotate(p-start,angle):start+MotionMath.Rotate(p-end,-angle);
            return p;
        }
        public static void RootAt(MotionClipData c,float time,float sampleRate,out MotionVector position,out float yaw)
        {
            int cycles=0;
            if(c.IsLoop){cycles=(int)Math.Floor(time/c.Duration);time-=cycles*c.Duration;}
            float index=Math.Max(0,time*sampleRate);int a=Math.Min((int)Math.Floor(index),c.Samples.Length-1),b=a+1;float t=Math.Min(1,index-a);
            var sa=c.Samples[a];MotionVector p1;float y1;
            if(b<c.Samples.Length){p1=c.Samples[b].RootPosition;y1=MotionMath.Yaw(c.Samples[b].RootRotation);}
            else if(c.IsLoop){p1=c.EndRootPosition;y1=MotionMath.Yaw(c.EndRootRotation);float interval=c.Duration-a/sampleRate;t=interval>0?(time-a/sampleRate)/interval:0;}
            else {p1=sa.RootPosition;y1=MotionMath.Yaw(sa.RootRotation);}
            float y0=MotionMath.Yaw(sa.RootRotation);position=TransformCycle(c,sa.RootPosition+(p1-sa.RootPosition)*t,cycles);
            yaw=y0+MotionMath.AngleDelta(y0,y1)*t+cycles*MotionMath.AngleDelta(MotionMath.Yaw(c.Samples[0].RootRotation),MotionMath.Yaw(c.EndRootRotation));
        }
        static void Put(float[] data,int offset,MotionVector v){data[offset]=v.x;data[offset+1]=v.y;data[offset+2]=v.z;}
        static void Check(MotionVector v){if(!MotionMath.Finite(v.x)||!MotionMath.Finite(v.y)||!MotionMath.Finite(v.z))throw new ArgumentException("Non-finite sample");}
        public static void Validate(MotionClipData[] clips,MotionBakeSettings s)
        {
            if(s==null||s.SchemaVersion!=1||!MotionMath.Finite(s.SampleRate)||s.SampleRate<=0||s.FutureSeconds==null||s.FutureSeconds.Length!=3||!MotionMath.Finite(s.NormalizationEpsilon)||s.NormalizationEpsilon<=0)throw new ArgumentException("Invalid schema/settings");
            if(!MotionMath.Finite(s.ContactHeight)||s.ContactHeight<0||!MotionMath.Finite(s.ContactSpeed)||s.ContactSpeed<0)throw new ArgumentException("Contact thresholds must be finite and nonnegative");
            float previous=0;foreach(float h in s.FutureSeconds){if(!MotionMath.Finite(h)||h<=previous)throw new ArgumentException("Horizons must be finite, positive and increasing");previous=h;}
            if(clips==null||clips.Length==0)throw new ArgumentException("No clips");
            foreach(var c in clips)
            {
                if(c==null||!MotionMath.Finite(c.Duration)||c.Duration<=0||c.Samples==null||c.Samples.Length<2)throw new ArgumentException("Clip needs at least two samples");
                float last=(c.Samples.Length-1)/s.SampleRate;
                if(last>c.Duration+0.00001f || (c.IsLoop && Math.Abs(c.Duration-c.Samples.Length/s.SampleRate)>0.0001f))throw new ArgumentException("Loop duration must contain an integer number of sample intervals; choose a compatible sample rate or use a nonloop clip");
                foreach(var frame in c.Samples){if(frame==null)throw new ArgumentException("Null sample");Check(frame.RootPosition);Check(frame.PelvisPosition);Check(frame.LeftFootPosition);Check(frame.RightFootPosition);MotionMath.Yaw(frame.RootRotation);}
                Check(c.EndRootPosition);MotionMath.Yaw(c.EndRootRotation);
                if(c.BoneNames==null||c.LocalPosePositions==null||c.LocalPoseRotations==null||c.LocalPosePositions.Length!=c.Samples.Length*c.BoneNames.Length*3||c.LocalPoseRotations.Length!=c.Samples.Length*c.BoneNames.Length*4)throw new ArgumentException("Pose array dimensions disagree");
                foreach(float v in c.LocalPosePositions)if(!MotionMath.Finite(v))throw new ArgumentException("Invalid pose position");
                foreach(float v in c.LocalPoseRotations)if(!MotionMath.Finite(v))throw new ArgumentException("Invalid pose rotation");
                for(int i=0;i<c.LocalPoseRotations.Length;i+=4)
                {
                    double norm=0;for(int j=0;j<4;j++)norm+=(double)c.LocalPoseRotations[i+j]*c.LocalPoseRotations[i+j];
                    if(norm<1e-12)throw new ArgumentException("Zero pose quaternion");
                }
            }
        }
    }
    public static class MotionDatabaseBinary
    {
        const int Magic=0x32444D4D;
        public static byte[] Serialize(MotionDatabaseData db)
        {
            ValidateData(db);
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream))
            {
                w.Write(Magic);w.Write(1);var s=db.Settings;w.Write(s.SchemaVersion);w.Write(s.SampleRate);Write(w,s.FutureSeconds);w.Write(s.NormalizationEpsilon);w.Write(s.ContactHeight);w.Write(s.ContactSpeed);
                w.Write(db.Clips.Length);foreach(var c in db.Clips)
                {
                    w.Write(c.Name??"");w.Write(c.Guid??"");w.Write(c.SourceHash??"");w.Write(c.Tag??"");w.Write(c.Duration);w.Write(c.IsLoop);Vector(w,c.EndRootPosition);Rotation(w,c.EndRootRotation);w.Write(c.FrameOffset);
                    w.Write(c.Samples.Length);foreach(var f in c.Samples){Vector(w,f.RootPosition);Rotation(w,f.RootRotation);Vector(w,f.PelvisPosition);Vector(w,f.LeftFootPosition);Vector(w,f.RightFootPosition);}
                    w.Write(c.BoneNames.Length);foreach(var name in c.BoneNames)w.Write(name);Write(w,c.LocalPosePositions);Write(w,c.LocalPoseRotations);
                }
                Write(w,db.RawFeatures);Write(w,db.NormalizedFeatures);Write(w,db.Means);Write(w,db.StandardDeviations);Write(w,db.NormalizationScales);Write(w,db.RootLinearVelocities);Write(w,db.RootAngularVelocities);Write(w,db.JointPositions);Write(w,db.JointVelocities);Write(w,db.ValidFrames);Write(w,db.LeftFootContacts);Write(w,db.RightFootContacts);return stream.ToArray();
            }
        }
        public static MotionDatabaseData Deserialize(byte[] bytes)
        {
            using(var stream=new MemoryStream(bytes,false))using(var r=new BinaryReader(stream))
            {
                if(r.ReadInt32()!=Magic||r.ReadInt32()!=1)throw new InvalidDataException("Unsupported motion database");
                var s=new MotionBakeSettings{SchemaVersion=r.ReadInt32(),SampleRate=r.ReadSingle(),FutureSeconds=Floats(r),NormalizationEpsilon=r.ReadSingle(),ContactHeight=r.ReadSingle(),ContactSpeed=r.ReadSingle()};
                var clips=new MotionClipData[Count(r)];for(int i=0;i<clips.Length;i++)
                {
                    var c=new MotionClipData{Name=r.ReadString(),Guid=r.ReadString(),SourceHash=r.ReadString(),Tag=r.ReadString(),Duration=r.ReadSingle(),IsLoop=r.ReadBoolean(),EndRootPosition=Vector(r),EndRootRotation=Rotation(r),FrameOffset=r.ReadInt32()};
                    c.Samples=new MotionSample[Count(r)];for(int j=0;j<c.Samples.Length;j++)c.Samples[j]=new MotionSample{RootPosition=Vector(r),RootRotation=Rotation(r),PelvisPosition=Vector(r),LeftFootPosition=Vector(r),RightFootPosition=Vector(r)};
                    c.BoneNames=new string[Count(r)];for(int j=0;j<c.BoneNames.Length;j++)c.BoneNames[j]=r.ReadString();c.LocalPosePositions=Floats(r);c.LocalPoseRotations=Floats(r);clips[i]=c;
                }
                var db=new MotionDatabaseData{Settings=s,Clips=clips,RawFeatures=Floats(r),NormalizedFeatures=Floats(r),Means=Floats(r),StandardDeviations=Floats(r),NormalizationScales=Floats(r),RootLinearVelocities=Floats(r),RootAngularVelocities=Floats(r),JointPositions=Floats(r),JointVelocities=Floats(r),ValidFrames=Bools(r),LeftFootContacts=Bools(r),RightFootContacts=Bools(r)};
                if(stream.Position!=stream.Length)throw new InvalidDataException("Trailing database bytes");
                ValidateData(db);return db;
            }
        }
        public static void ValidateData(MotionDatabaseData db)
        {
            if(db==null)throw new ArgumentNullException(nameof(db));
            MotionDatabaseBuilder.Validate(db.Clips,db.Settings);
            int n=0;foreach(var c in db.Clips){if(c.FrameOffset!=n)throw new InvalidDataException("Invalid frame offset");n=checked(n+c.Samples.Length);}
            var arrays=new[]{db.RawFeatures,db.NormalizedFeatures,db.Means,db.StandardDeviations,db.NormalizationScales,db.RootLinearVelocities,db.RootAngularVelocities,db.JointPositions,db.JointVelocities};
            foreach(var a in arrays)if(a==null)throw new InvalidDataException("Missing packed array");
            if(db.ValidFrames==null||db.LeftFootContacts==null||db.RightFootContacts==null||db.ValidFrames.Length!=n||db.RawFeatures.Length!=checked(n*27)||db.NormalizedFeatures.Length!=n*27||db.Means.Length!=27||db.StandardDeviations.Length!=27||db.NormalizationScales.Length!=27||db.RootLinearVelocities.Length!=n*3||db.RootAngularVelocities.Length!=n*3||db.JointPositions.Length!=n*9||db.JointVelocities.Length!=n*9||db.LeftFootContacts.Length!=n||db.RightFootContacts.Length!=n)throw new InvalidDataException("Invalid packed array dimensions");
            foreach(var a in arrays)foreach(float v in a)if(!MotionMath.Finite(v))throw new InvalidDataException("Non-finite database");
            for(int d=0;d<27;d++)
            {
                if(db.StandardDeviations[d]<0)throw new InvalidDataException("Invalid standard deviation");
                if(db.NormalizationScales[d]!=Math.Max(db.StandardDeviations[d],db.Settings.NormalizationEpsilon))throw new InvalidDataException("Invalid normalization scale");
            }
            bool any=false;foreach(bool valid in db.ValidFrames)any|=valid;
            if(!any)throw new InvalidDataException("Database has no searchable frames");
        }
        public static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        static void Write(BinaryWriter w,float[] a){w.Write(a.Length);foreach(float v in a)w.Write(v);}
        static void Write(BinaryWriter w,bool[] a){w.Write(a.Length);foreach(bool v in a)w.Write(v);}
        static int Count(BinaryReader r){int n=r.ReadInt32();if(n<0||n>100000000||n>r.BaseStream.Length-r.BaseStream.Position)throw new InvalidDataException("Invalid collection length");return n;}
        static float[] Floats(BinaryReader r){int n=Count(r);if(n>(r.BaseStream.Length-r.BaseStream.Position)/4)throw new InvalidDataException("Truncated float array");var a=new float[n];for(int i=0;i<n;i++)a[i]=r.ReadSingle();return a;}
        static bool[] Bools(BinaryReader r){var a=new bool[Count(r)];for(int i=0;i<a.Length;i++)a[i]=r.ReadBoolean();return a;}
        static void Vector(BinaryWriter w,MotionVector v){w.Write(v.x);w.Write(v.y);w.Write(v.z);}
        static MotionVector Vector(BinaryReader r)=>new MotionVector(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
        static void Rotation(BinaryWriter w,MotionRotation q){w.Write(q.x);w.Write(q.y);w.Write(q.z);w.Write(q.w);}
        static MotionRotation Rotation(BinaryReader r)=>new MotionRotation(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
    }
}
