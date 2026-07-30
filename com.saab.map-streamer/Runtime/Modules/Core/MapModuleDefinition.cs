using System;
using System.Collections.Generic;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Modules
{
    public interface IMapModuleServices
    {
        T Get<T>();
    }

    public interface IMapModuleRegistrar
    {
        void Register(MapModuleDefinition definition);
    }

    public abstract class MapModuleDefinition : ScriptableObject
    {
        [SerializeField]
        private bool enabled = true;

        [SerializeField]
        private int executionOrder;

        [SerializeField]
        private string[] dependencies = Array.Empty<string>();

        public abstract string ModuleId { get; }
        public bool Enabled => enabled;
        public int ExecutionOrder => executionOrder;
        public IReadOnlyList<string> Dependencies => dependencies;

        public virtual bool TryValidate(out string failure)
        {
            if (string.IsNullOrWhiteSpace(ModuleId))
            {
                failure = $"{name} has no module ID.";
                return false;
            }

            failure = null;
            return true;
        }

        public abstract IMapModule CreateRuntime(IMapModuleServices services);
    }
}
