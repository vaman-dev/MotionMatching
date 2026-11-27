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

            const string sourceRoot = "Assets/Scripts/MotionMatching/";
            // Verified imported vendor scripts. Never capture them in project assemblies.
            var vendorSources = new HashSet<string> {
                "Assets/Kevin Iglesias/Human Animations/Scripts/SpineProxy.cs",
                "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Basic Motions/Script/HumanBasicMotionsDemo.cs"
            };
            foreach (var assembly in assemblies)
            {
                foreach (string file in assembly.sourceFiles)
                {
                    string path = file.Replace('\\', '/');
                    if (!path.StartsWith("Assets/")) continue;
                    if (vendorSources.Contains(path))
                    {
                        if (assembly.name.StartsWith("MotionMatching.")) errors.Add("Vendor source captured by project assembly: " + path);
                        continue;
                    }
                    if (!path.StartsWith(sourceRoot))
                        errors.Add("Project source outside authoritative root: " + path);
                    if (assembly.name.StartsWith("Assembly-CSharp"))
                        errors.Add("Project source in predefined assembly: " + path);
                }
            }
            return errors.ToArray();
        }
    }
}
