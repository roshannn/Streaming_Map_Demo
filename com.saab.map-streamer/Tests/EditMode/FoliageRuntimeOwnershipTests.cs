using System;
using System.Collections.Generic;

using NUnit.Framework;

using Saab.Foundation.Map;
using Saab.Foundation.Unity.MapStreamer.Modules;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Configuration;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Resources;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Runtime;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Terrain;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Tests
{
    public sealed class FoliageRuntimeOwnershipTests
    {
        [Test]
        public void SerializedFeatureCreatesIndependentConfigurationSnapshot()
        {
            var serialized = new FeatureSet
            {
                Enabled = true,
                BufferSize = 42,
                Density = 0.25f,
                ScreenCoverage = 0.01f,
                NodeMaxWidth = 512,
                Shadows = true,
                Crossboard = true
            };

            var snapshot = serialized.Snapshot();
            serialized.BufferSize = 99;
            serialized.Density = 1f;

            Assert.That(snapshot.BufferSize, Is.EqualTo(42));
            Assert.That(snapshot.Density, Is.EqualTo(0.25f));
            Assert.That(snapshot.ScreenCoverage, Is.EqualTo(0.01f));
            Assert.That(snapshot.NodeMaxWidth, Is.EqualTo(512));
            Assert.That(snapshot.Shadows, Is.True);
            Assert.That(snapshot.Crossboard, Is.True);
        }

        [Test]
        public void BuildScopeDisposesUncommittedState()
        {
            var identity = new TerrainModuleIdentity(7, 2);
            var state = new FoliageTerrainState(
                identity,
                new List<FoliageFeatureRuntime>());

            using (new FoliageTerrainBuildScope(state))
            {
            }

            Assert.DoesNotThrow(state.Dispose);
        }

        [Test]
        public void RegistryUsesCompleteTerrainIdentity()
        {
            var registry = new FoliageTerrainRegistry();
            var first = new TerrainModuleIdentity(7, 1);
            var second = new TerrainModuleIdentity(7, 2);
            registry.Add(CreateState(first));
            registry.Add(CreateState(second));

            registry.Remove(first);

            Assert.That(registry.Contains(first), Is.False);
            Assert.That(registry.Contains(second), Is.True);
            Assert.That(registry.Count, Is.EqualTo(1));
            registry.Dispose();
        }

        [Test]
        public void RegistryRejectsDuplicateIdentity()
        {
            var registry = new FoliageTerrainRegistry();
            var identity = new TerrainModuleIdentity(11, 3);
            registry.Add(CreateState(identity));
            var duplicate = CreateState(identity);

            Assert.Throws<ArgumentException>(() => registry.Add(duplicate));

            duplicate.Dispose();
            registry.Dispose();
        }

        [Test]
        public void ResourcePoolReusesOnlyCompatibleBuffers()
        {
            var pool = new FoliageResourcePool(1024);
            var original = new ComputeBuffer(4, sizeof(float));
            try
            {
                pool.Return(original);
                Assert.That(pool.PooledBytes, Is.EqualTo(16));

                var rented = pool.Rent(4, sizeof(float));

                Assert.That(rented, Is.SameAs(original));
                Assert.That(pool.PooledBytes, Is.Zero);
                pool.Return(rented);
                original = null;
            }
            finally
            {
                original?.Release();
                pool.Dispose();
            }
        }

        [Test]
        public void ResourcePoolReleasesBuffersBeyondCeiling()
        {
            var pool = new FoliageResourcePool(0);
            var buffer = new ComputeBuffer(4, sizeof(float));

            pool.Return(buffer);

            Assert.That(pool.PooledBytes, Is.Zero);
            pool.Dispose();
        }

        [Test]
        public void ProfileRuntimeUsesConfigurationSnapshot()
        {
            var constructors = typeof(FoliageModuleRuntime)
                .GetConstructors();
            Assert.That(constructors, Has.Length.EqualTo(1));
            var parameters = constructors[0].GetParameters();

            Assert.That(parameters, Has.Length.EqualTo(3));
            Assert.That(
                parameters[0].ParameterType,
                Is.EqualTo(typeof(FoliageModuleConfiguration)));
            Assert.That(
                parameters[1].ParameterType,
                Is.EqualTo(typeof(CameraControl)));
            Assert.That(
                parameters[2].ParameterType,
                Is.EqualTo(typeof(IMapCoordinates)));
        }

        private static FoliageTerrainState CreateState(
            TerrainModuleIdentity identity) =>
            new FoliageTerrainState(
                identity,
                new List<FoliageFeatureRuntime>());
    }
}
