using System.Collections.Generic;
using System.Linq;
using MotionMatching.Runtime;
using UnityEditor.Compilation;

namespace MotionMatching.Editor
{
    /// <summary>Inspects Unity's compiled graph rather than assuming asmdef JSON was imported.</summary>
    public static class AssemblyBoundaryAudit
    {
        public static string[] ValidateCompiledAssemblies()
        {
            var errors = new List<string>();
            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
            foreach (string name in new[] { "MotionMatching.Core", "MotionMatching.Runtime", "MotionMatching.Editor",
                         "MotionMatching.Tests.Editor", "MotionMatching.Tests.Runtime" })
                if (!assemblies.Any(a => a.name == name)) errors.Add("Missing compiled assembly: " + name);

            if (typeof(CharacterMotor).Assembly.GetReferencedAssemblies().Any(r => r.Name.StartsWith("UnityEditor")))
                errors.Add("Runtime references UnityEditor.");
            return errors.ToArray();
        }
    }
}
