using MotionMatching.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace MotionMatching.Editor
{
    [CustomEditor(typeof(MotionMatchingController))]
    public sealed class MotionMatchingControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var matching = (MotionMatchingController)target;
            if (!Application.isPlaying) { EditorGUILayout.HelpBox("Grounded motion matching outputs to Animator through one graph. The retained controller is an authored-action/fallback dependency. Previous Database preserves explicit rollback.", MessageType.Info); return; }
            EditorGUILayout.LabelField("Graph ready / matching", $"{matching.IsReady} / {matching.IsMatching}");
            if (!matching.IsReady) return;
            var result = matching.LastResult;
            EditorGUILayout.LabelField("Best candidate clip / row", $"{result.ClipIndex} / {result.Row}");
            EditorGUILayout.LabelField("Candidate sample / time", $"{result.LocalFrame} / {result.TimeSeconds:F3} s");
            EditorGUILayout.LabelField("Playing clip", matching.CurrentClipIndex.ToString());
            EditorGUILayout.LabelField("Playback clock", $"{matching.PlaybackTimeSeconds:F3} s");
            EditorGUILayout.LabelField("Cost", result.Cost.ToString("F5"));
            EditorGUILayout.LabelField("Feature / continuation cost", $"{result.FeatureCost:F3} / {matching.ContinuationCost:F3}");
            EditorGUILayout.LabelField("Actual switches / last second", $"{matching.SwitchCount} / {matching.SwitchesPerSecond:F0}");
            EditorGUILayout.LabelField("KD query / visited nodes", $"{matching.QueryTicks * 1000000.0 / System.Diagnostics.Stopwatch.Frequency:F1} us / {matching.NodesVisited}");
            EditorGUILayout.LabelField("Query allocation", matching.AllocationCounterSupported ? $"{matching.QueryAllocatedBytes} B" : "Counter unsupported (calibration failed)");
            EditorGUILayout.Vector3Field("Desired / raw velocity", matching.DesiredVelocity);
            EditorGUILayout.Vector3Field("Desired / spring velocity", matching.SmoothedVelocity);
            EditorGUILayout.Vector3Field("Actual capsule velocity", matching.GetComponent<PlayerController>().ActualVelocity);
            EditorGUILayout.Vector3Field("Submitted root delta (m)", matching.LastRootDelta);
            EditorGUILayout.LabelField("Submitted root yaw", $"{matching.LastRootYawDegrees:F3} deg");
            var stats=matching.Playback.InertialStatistics;
            EditorGUILayout.LabelField("Inertial transitions / evaluations", $"{stats.ReferenceEvaluations} / {stats.Evaluations}");
            EditorGUILayout.LabelField("Pose offset / angular offset", $"{stats.MaxPositionOffset:F5} m / {stats.MaxRotationOffsetDegrees:F2} deg");
            if (matching.Settings.CompareBruteForce) EditorGUILayout.LabelField("KD / brute-force agreement", matching.OracleAgrees.ToString());
            if (result.GroupCosts != null) EditorGUILayout.LabelField("Group costs", string.Join(", ", result.GroupCosts));
            EditorGUILayout.HelpBox("Weight edits rebuild the index once at the next query. The oracle shares the same weights, penalties and continuation window. Graph below is read-only; the action branch never feeds the locomotion Blend Tree from search.", MessageType.None);
            DrawNode(matching.Playback.Graph.GetOutput(0).GetSourcePlayable(), "AnimationPlayableOutput -> Animator", 0);
            Repaint();
        }
        private static void DrawNode(Playable node, string label, int depth)
        {
            if(!node.IsValid() || depth>8)return;
            EditorGUI.indentLevel=depth;
            EditorGUILayout.LabelField(label, $"{node.GetPlayableType().Name}  t={node.GetTime():F3}s");
            for(int input=0;input<node.GetInputCount();input++)
                if(node.GetInput(input).IsValid())DrawNode(node.GetInput(input),$"Input {input}, weight {node.GetInputWeight(input):F3}",depth+1);
            EditorGUI.indentLevel=0;
        }
    }
}
