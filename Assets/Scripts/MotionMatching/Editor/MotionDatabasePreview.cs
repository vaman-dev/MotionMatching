using System;
using MotionMatching.Core;
using UnityEditor;
using UnityEngine;

namespace MotionMatching.Editor
{
    public enum MotionPreviewDisplay { MeshOnly, SkeletonOnly, MeshAndSkeleton }

    /// <summary>Elapsed-time preview clock independent of GUI repaint frequency.</summary>
    public sealed class MotionPreviewClock
    {
        private double lastTime;
        public double Time { get; private set; }
        public bool IsPlaying { get; private set; }

        public void Reset(double time, double now, bool playing = false)
        {
            if (double.IsNaN(time) || double.IsInfinity(time) || time < 0) throw new ArgumentOutOfRangeException(nameof(time));
            Time = time; lastTime = now; IsPlaying = playing;
        }

        public void SetPlaying(bool playing, double now)
        {
            IsPlaying = playing;
            lastTime = now;
        }

        public double Advance(double now, double speed, double maxStep = double.PositiveInfinity)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || double.IsNaN(speed) || double.IsInfinity(speed) || speed < 0 ||
                double.IsNaN(maxStep) || maxStep <= 0)
                throw new ArgumentOutOfRangeException(nameof(now));
            if (IsPlaying) Time += Math.Min(maxStep, Math.Max(0, now - lastTime)) * speed;
            lastTime = now;
            return Time;
        }
    }

    /// <summary>Owns the render scene, camera and isolated clip session for one selected source.</summary>
    public sealed class MotionDatabasePreview : IDisposable
    {
        private PreviewRenderUtility utility;
        public MotionPreviewSession Session { get; private set; }
        private readonly MotionDatabaseData data;
        private readonly MotionClipData clip;
        private readonly Renderer[] renderers;
        private readonly Vector3[] path;
        private Vector2 orbit = new Vector2(155, 10);
        private Vector3 pan;
        private float distance = 3.2f;
        private int comparedFrame = -1;
        public MotionPoseComparison Comparison { get; private set; }
        public bool IsDisposed => utility == null;

        public MotionDatabasePreview(GameObject character, Avatar avatar, AnimationClip source, MotionDatabaseData data, int clipIndex)
        {
            this.data = data; clip = data.Clips[clipIndex];
            try
            {
                Session = new MotionPreviewSession(character, avatar, source, clip, data.Settings);
                utility = new PreviewRenderUtility();
                utility.AddSingleGO(Session.Instance);
                utility.camera.fieldOfView = 35;
                utility.camera.nearClipPlane = 0.02f; utility.camera.farClipPlane = 200;
                utility.camera.clearFlags = CameraClearFlags.SolidColor;
                utility.camera.backgroundColor = new Color(0.10f, 0.12f, 0.15f, 1);
                utility.lights[0].intensity = 1.3f; utility.lights[0].transform.rotation = Quaternion.Euler(40, 40, 0);
                utility.lights[1].intensity = 1; utility.lights[1].transform.rotation = Quaternion.Euler(20, 220, 0);
                utility.ambientColor = new Color(0.45f, 0.45f, 0.45f);
                renderers = Session.Instance.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in renderers) if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
                path = new Vector3[clip.Samples.Length];
                for (int i = 0; i < path.Length; i++) path[i] = MotionPreviewSession.V(clip.Samples[i].RootPosition);
            }
            catch { Dispose(); throw; }
        }

        public void ResetCamera() { orbit = new Vector2(155, 10); pan = Vector3.zero; distance = 3.2f; }

        public void Draw(Rect rect, double seconds, bool stored, MotionPreviewDisplay mode, bool diagnostics, float velocitySeconds, bool compare)
        {
            if (IsDisposed) return;
            Event e = Event.current;
            if (rect.Contains(e.mousePosition))
            {
                if (e.type == EventType.ScrollWheel) { distance = Mathf.Clamp(distance * Mathf.Exp(e.delta.y * 0.07f), 0.3f, 50); e.Use(); }
                else if (e.type == EventType.MouseDrag)
                {
                    if (e.button == 2 || (e.button == 0 && e.shift))
                        pan += utility.camera.transform.right * (-e.delta.x * distance / rect.height) + utility.camera.transform.up * (e.delta.y * distance / rect.height);
                    else { orbit.x += e.delta.x * 0.5f; orbit.y = Mathf.Clamp(orbit.y + e.delta.y * 0.5f, -80, 80); }
                    e.Use();
                }
            }
            if (e.type != EventType.Repaint) return;
            Session.SetTime(seconds, stored);
            int frame = Session.CurrentSample;
            if (compare && comparedFrame != frame) { Comparison = Session.CompareSample(frame); comparedFrame = frame; }
            Session.SetTime(seconds, stored);
            foreach (var renderer in renderers) renderer.enabled = mode != MotionPreviewDisplay.SkeletonOnly;
            Camera camera = utility.camera;
            Vector3 focus = Session.Root.position + Vector3.up * 0.9f + pan;
            camera.transform.rotation = Quaternion.Euler(orbit.y, orbit.x, 0);
            camera.transform.position = focus - camera.transform.forward * distance;
            camera.aspect = rect.width / Mathf.Max(1, rect.height);
            utility.BeginPreview(rect, GUIStyle.none);
            Texture image;
            try { utility.Render(true, false); }
            finally { image = utility.EndPreview(); }
            GUI.DrawTexture(rect, image, ScaleMode.StretchToFill, false);
            GUI.BeginClip(rect);
            Handles.BeginGUI();
            Color old = Handles.color;
            try
            {
                if (mode != MotionPreviewDisplay.MeshOnly)
                {
                    Handles.color = new Color(1, 0.8f, 0.35f);
                    for (int i = 0; i < Session.BoneCount; i++)
                    {
                        int parent = Session.BoneParents[i];
                        if (parent >= 0) Line(Session.Bones[parent].position, Session.Bones[i].position, rect);
                        Dot(Session.Bones[i].position, rect, 2);
                    }
                }
                if (diagnostics) DrawDiagnostics(rect, frame, velocitySeconds, seconds);
            }
            finally { Handles.color = old; Handles.EndGUI(); GUI.EndClip(); }
            double shownTime = clip.IsLoop ? seconds % clip.Duration : Math.Min(seconds, clip.Duration);
            GUI.Label(new Rect(rect.x + 7, rect.y + 5, rect.width - 14, 20), $"{(stored ? "Stored pose" : "Evaluated clip")} · sample {frame} · {shownTime:F3} s", EditorStyles.whiteMiniLabel);
        }

        private void DrawDiagnostics(Rect rect, int frame, float velocitySeconds, double previewTime)
        {
            Handles.color = Color.green;
            for (int i = 1; i < path.Length; i++)
                Line(Session.TransformCyclePoint(path[i - 1], previewTime), Session.TransformCyclePoint(path[i], previewTime), rect);
            Vector3 root = Session.Root.position;
            Dot(root, rect, 5); Line(root, root + Session.Root.forward * 0.4f, rect);
            int global = clip.FrameOffset + frame;
            Quaternion yaw = Quaternion.Euler(0, MotionMath.Yaw(clip.Samples[frame].RootRotation) * Mathf.Rad2Deg, 0);
            for (int j = 0; j < 3; j++)
            {
                int index = global * 9 + j * 3;
                Vector3 p = root + yaw * Packed(data.JointPositions, index);
                Vector3 v = yaw * Packed(data.JointVelocities, index);
                Handles.color = j == 0 ? Color.cyan : j == 1 ? Color.yellow : Color.magenta;
                Line(p, p + v * velocitySeconds, rect); Dot(p, rect, 4);
                bool contact = j == 1 ? data.LeftFootContacts[global] : j == 2 && data.RightFootContacts[global];
                if (contact)
                {
                    // Small ground-plane cross distinguishes contact from the ordinary joint dot.
                    Line(p - Vector3.right * 0.07f, p + Vector3.right * 0.07f, rect);
                    Line(p - Vector3.forward * 0.07f, p + Vector3.forward * 0.07f, rect);
                }
            }
        }

        private bool Project(Vector3 point, Rect rect, out Vector3 pixel)
        {
            Vector3 v = utility.camera.WorldToViewportPoint(point);
            pixel = new Vector3(v.x * rect.width, (1 - v.y) * rect.height, 0);
            return v.z > 0;
        }
        private void Line(Vector3 a, Vector3 b, Rect rect)
        { if (Project(a, rect, out var x) && Project(b, rect, out var y)) Handles.DrawLine(x, y); }
        private void Dot(Vector3 p, Rect rect, float radius)
        { if (Project(p, rect, out var pixel)) Handles.DrawSolidDisc(pixel, Vector3.forward, radius); }
        private static Vector3 Packed(float[] values, int i) => new Vector3(values[i], values[i + 1], values[i + 2]);
        public void Dispose()
        {
            Session?.Dispose(); Session = null;
            utility?.Cleanup(); utility = null;
        }
    }
}
