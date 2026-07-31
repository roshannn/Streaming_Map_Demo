using System;

using NUnit.Framework;

using Saab.Foundation.Unity.MapStreamer.Composition.Modules;

namespace Saab.Foundation.Unity.MapStreamer.Tests
{
    public sealed class ProfileOnlyModuleTests
    {
        [Test]
        public void CompositionRejectsMissingModuleProfile()
        {
            var installer = new ModulesInstaller(null);

            var exception = Assert.Throws<InvalidOperationException>(
                () => installer.Install(null));

            Assert.That(
                exception.Message,
                Does.Contain("MapModuleProfile"));
        }
    }
}
