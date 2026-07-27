// Copyright 2021 saab AB

using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;
using Saab.Utility.Unity.NodeUtils;

namespace Saab.Foundation.Unity.MapStreamer.Traversal
{
    internal sealed class HierarchyTraversalHelper
    {
        private readonly SceneTraverser _sceneTraverser;

        public HierarchyTraversalHelper(SceneTraverser sceneTraverser)
        {
            _sceneTraverser = sceneTraverser;
        }

        public void TraverseChildren(
            Group group,
            in TraversalContext context,
            bool addActionInterfaces,
            NodeAction actionReceiver)
        {
            var parent = context.Node.Transform;

            foreach (var child in group)
            {
                var childContext = context;
                var result = _sceneTraverser.Traverse(child, ref childContext);

                if (!result.HasGameObject)
                    continue;

                var gameObject = result.GameObject;

                if (addActionInterfaces)
                    RegisterActionInterfaces(
                        child,
                        result.Node,
                        actionReceiver);

                gameObject.transform.SetParent(parent, false);
            }
        }

        private static void RegisterActionInterfaces(
            Node child,
            TraversalNode node,
            NodeAction actionReceiver)
        {
            var childPtr = child.GetNativeReference();
            if (NodeUtils.HasGameObjectsUnsafe(childPtr))
                return;

            NodeUtils.AddGameObjectReferenceUnsafe(
                childPtr,
                node.GameObject);

            node.MarkRegisteredInNodeUtils();

            child.AddActionInterface(actionReceiver, NodeActionEvent.IS_TRAVERSABLE);
            child.AddActionInterface(actionReceiver, NodeActionEvent.IS_NOT_TRAVERSABLE);
        }
    }
}
