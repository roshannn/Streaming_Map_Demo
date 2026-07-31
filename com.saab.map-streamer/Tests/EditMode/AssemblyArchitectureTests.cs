using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using NUnit.Framework;

using UnityEditor.PackageManager;
using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Tests
{
    public sealed class AssemblyArchitectureTests
    {
        [Serializable]
        private sealed class AssemblyDefinitionData
        {
            public string name;
            public string rootNamespace;
            public string[] references;
            public string[] includePlatforms;
        }

        private static readonly string[] RuntimeAssemblies =
        {
            "MAPSTREAMER.Streaming.Core",
            "Saab.Foundation.Map",
            "MAPSTREAMER.GizmoAdapter",
            "MAPSTREAMER.Unity.Utilities",
            "Saab.Utility.GfxCaps",
            "MAPSTREAMER",
            "MAPSTREAMER.Modules",
            "MAPSTREAMER.Modules.Foliage",
            "MAPSTREAMER.Modules.TerrainShading",
            "MAPSTREAMER.Composition"
        };

        [Test]
        public void RuntimeAssemblyGraphIsAcyclic()
        {
            var definitions = LoadDefinitions();
            var visiting = new HashSet<string>();
            var visited = new HashSet<string>();

            foreach (var assembly in RuntimeAssemblies)
                Visit(assembly, definitions, visiting, visited);
        }

        [Test]
        public void RuntimeAndFeaturesDoNotReferenceCompositionOrEditor()
        {
            var definitions = LoadDefinitions();
            var forbiddenConsumers = RuntimeAssemblies
                .Where(name => name != "MAPSTREAMER.Composition");

            foreach (var assembly in forbiddenConsumers)
            {
                var references = definitions[assembly].references ??
                    Array.Empty<string>();
                Assert.That(
                    references,
                    Does.Not.Contain("MAPSTREAMER.Composition"),
                    assembly);
                Assert.That(
                    references.Any(value =>
                        value.EndsWith(
                            ".Editor",
                            StringComparison.Ordinal)),
                    Is.False,
                    assembly);
            }
        }

        [Test]
        public void FeatureAssembliesDoNotReferenceEachOther()
        {
            var definitions = LoadDefinitions();

            Assert.That(
                definitions["MAPSTREAMER.Modules.Foliage"].references,
                Does.Not.Contain(
                    "MAPSTREAMER.Modules.TerrainShading"));
            Assert.That(
                definitions["MAPSTREAMER.Modules.TerrainShading"].references,
                Does.Not.Contain("MAPSTREAMER.Modules.Foliage"));
        }

        [Test]
        public void RuntimeTreeContainsNoUnityEditorImports()
        {
            var runtime = Path.Combine(PackageRoot, "Runtime");
            var offenders = Directory
                .GetFiles(runtime, "*.cs", SearchOption.AllDirectories)
                .Where(path =>
                    File.ReadAllText(path).Contains("using UnityEditor;"))
                .ToArray();

            Assert.That(offenders, Is.Empty);
        }

        [Test]
        public void LegacyDuplicateDirectoryRootsAreGone()
        {
            var runtime = Path.Combine(PackageRoot, "Runtime");

            Assert.That(
                Directory.Exists(
                    Path.Combine(runtime, "Saab.Foundation.Unity")),
                Is.False);
            Assert.That(
                Directory.Exists(Path.Combine(runtime, "Saab.Unity")),
                Is.False);
        }

        [Test]
        public void LayeredModuleInternalsMatchDirectoryNamespaces()
        {
            AssertLayerNamespaces(
                Path.Combine(PackageRoot, "Runtime", "Modules", "Foliage"),
                "Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime");
            AssertLayerNamespaces(
                Path.Combine(
                    PackageRoot,
                    "Runtime",
                    "Modules",
                    "TerrainShading"),
                "Saab.Foundation.Unity.MapStreamer.Modules.TerrainShading");
        }

        [Test]
        public void InternalRuntimeTypesMatchOwnedDirectoryNamespaces()
        {
            var runtime = Path.Combine(PackageRoot, "Runtime");
            var mapStreamer = Path.Combine(runtime, "MapStreamer");

            AssertDirectoryNamespace(
                Path.Combine(mapStreamer, "Building", "Builders"),
                "Saab.Foundation.Unity.MapStreamer.Building.Builders");
            AssertDirectoryNamespace(
                Path.Combine(mapStreamer, "Building", "Coordination"),
                "Saab.Foundation.Unity.MapStreamer.Building.Coordination");
            AssertDirectoryNamespace(
                Path.Combine(mapStreamer, "Nodes", "Pooling"),
                "Saab.Foundation.Unity.MapStreamer.Nodes.Pooling");
            AssertDirectoryNamespace(
                Path.Combine(mapStreamer, "Nodes", "Processing"),
                "Saab.Foundation.Unity.MapStreamer.Nodes.Processing");
            AssertDirectoryNamespace(
                Path.Combine(mapStreamer, "Traversal", "Contracts"),
                "Saab.Foundation.Unity.MapStreamer.Traversal.Contracts");
            AssertDirectoryNamespace(
                Path.Combine(mapStreamer, "Traversal", "Operations"),
                "Saab.Foundation.Unity.MapStreamer.Traversal.Operations");
            AssertDirectoryNamespace(
                Path.Combine(mapStreamer, "Traversal", "Processing"),
                "Saab.Foundation.Unity.MapStreamer.Traversal.Processing");
            AssertDirectoryNamespace(
                Path.Combine(runtime, "Modules", "Core", "Runtime"),
                "Saab.Foundation.Unity.MapStreamer.Modules.Runtime");
        }

        [Test]
        public void SourcesDoNotImportTheirOwnNamespace()
        {
            var offenders = Directory
                .GetFiles(
                    Path.Combine(PackageRoot, "Runtime"),
                    "*.cs",
                    SearchOption.AllDirectories)
                .Where(path =>
                {
                    var source = File.ReadAllText(path);
                    var declaration = source
                        .Split('\n')
                        .Select(line => line.Trim())
                        .FirstOrDefault(line =>
                            line.StartsWith(
                                "namespace ",
                                StringComparison.Ordinal));
                    if (declaration == null)
                        return false;
                    var currentNamespace =
                        declaration.Substring("namespace ".Length).Trim();
                    return source.Contains(
                        $"using {currentNamespace};");
                })
                .ToArray();

            Assert.That(offenders, Is.Empty);
        }

        private static string PackageRoot =>
            PackageInfo.FindForAssembly(
                    typeof(AssemblyArchitectureTests).Assembly)
                .resolvedPath;

        private static void AssertLayerNamespaces(
            string featureRoot,
            string namespaceRoot)
        {
            foreach (var layer in new[]
                     {
                         "Configuration",
                         "Rendering",
                         "Resources",
                         "Runtime",
                         "Terrain"
                     })
            {
                var directory = Path.Combine(featureRoot, layer);
                if (!Directory.Exists(directory))
                    continue;

                AssertDirectoryNamespace(
                    directory,
                    $"{namespaceRoot}.{layer}");
            }
        }

        private static void AssertDirectoryNamespace(
            string directory,
            string expectedNamespace)
        {
            var offenders = Directory
                .GetFiles(directory, "*.cs", SearchOption.TopDirectoryOnly)
                .Where(path =>
                {
                    var source = File.ReadAllText(path);
                    if (!Regex.IsMatch(
                            source,
                            @"(?m)^\s*internal\s+(?:(?:sealed|static|abstract|partial|readonly)\s+)*(?:class|struct|interface|enum)\s+"))
                        return false;
                    return !source.Contains(
                        $"namespace {expectedNamespace}");
                })
                .ToArray();

            Assert.That(
                offenders,
                Is.Empty,
                $"Internal sources must use {expectedNamespace}.");
        }

        private static Dictionary<string, AssemblyDefinitionData>
            LoadDefinitions() =>
            Directory
                .GetFiles(
                    PackageRoot,
                    "*.asmdef",
                    SearchOption.AllDirectories)
                .Select(path =>
                    JsonUtility.FromJson<AssemblyDefinitionData>(
                        File.ReadAllText(path)))
                .ToDictionary(definition => definition.name);

        private static void Visit(
            string assembly,
            IReadOnlyDictionary<string, AssemblyDefinitionData> definitions,
            ISet<string> visiting,
            ISet<string> visited)
        {
            Assert.That(
                definitions.ContainsKey(assembly),
                Is.True,
                $"Missing assembly definition {assembly}.");
            if (visited.Contains(assembly))
                return;
            Assert.That(
                visiting.Add(assembly),
                Is.True,
                $"Assembly dependency cycle at {assembly}.");

            foreach (var dependency in
                     definitions[assembly].references ??
                     Array.Empty<string>())
            {
                if (dependency.StartsWith(
                        "GUID:",
                        StringComparison.Ordinal) ||
                    !definitions.ContainsKey(dependency))
                    continue;
                Visit(dependency, definitions, visiting, visited);
            }

            visiting.Remove(assembly);
            visited.Add(assembly);
        }
    }
}
