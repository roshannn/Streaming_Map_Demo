using System;
using System.Reflection;

using NUnit.Framework;

using Saab.Foundation.Unity.MapStreamer.Modules;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Tests
{
    public sealed class MapModuleCatalogTests
    {
        private sealed class TestDefinition : MapModuleDefinition
        {
            private string _id;

            public override string ModuleId => _id;

            public static TestDefinition Create(
                string id,
                int order = 0,
                params string[] dependencies)
            {
                var definition =
                    CreateInstance<TestDefinition>();
                definition._id = id;
                SetBaseField(
                    definition,
                    "executionOrder",
                    order);
                SetBaseField(
                    definition,
                    "dependencies",
                    dependencies);
                return definition;
            }

            public override IMapModule CreateRuntime(
                IMapModuleServices services) =>
                new TestModule();

            private static void SetBaseField(
                TestDefinition definition,
                string name,
                object value)
            {
                typeof(MapModuleDefinition)
                    .GetField(
                        name,
                        BindingFlags.Instance |
                        BindingFlags.NonPublic)
                    .SetValue(definition, value);
            }
        }

        private sealed class TestModule : IMapModule
        {
            public void Initialize() { }
            public void Shutdown() { }
        }

        [Test]
        public void OrdersDependenciesBeforeConsumers()
        {
            var catalog = new MapModuleCatalog();
            catalog.Register(
                TestDefinition.Create("consumer", -10, "provider"));
            catalog.Register(TestDefinition.Create("provider", 100));

            var ordered = catalog.GetOrderedDefinitions();

            Assert.That(ordered[0].ModuleId, Is.EqualTo("provider"));
            Assert.That(ordered[1].ModuleId, Is.EqualTo("consumer"));
        }

        [Test]
        public void RejectsDuplicateIds()
        {
            var catalog = new MapModuleCatalog();
            catalog.Register(TestDefinition.Create("duplicate"));
            catalog.Register(TestDefinition.Create("duplicate"));

            Assert.Throws<InvalidOperationException>(
                () => catalog.GetOrderedDefinitions());
        }

        [Test]
        public void RejectsMissingDependencies()
        {
            var catalog = new MapModuleCatalog();
            catalog.Register(
                TestDefinition.Create("consumer", 0, "missing"));

            Assert.Throws<InvalidOperationException>(
                () => catalog.GetOrderedDefinitions());
        }

        [Test]
        public void RejectsCircularDependencies()
        {
            var catalog = new MapModuleCatalog();
            catalog.Register(TestDefinition.Create("a", 0, "b"));
            catalog.Register(TestDefinition.Create("b", 0, "a"));

            Assert.Throws<InvalidOperationException>(
                () => catalog.GetOrderedDefinitions());
        }
    }
}
