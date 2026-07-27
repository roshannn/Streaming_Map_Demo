// Copyright 2021 saab AB

using GizmoSDK.Gizmo3D;
using GizmoSDK.GizmoBase;
using Saab.Unity.Extensions;
using UnityEngine;
using gzTransform = GizmoSDK.Gizmo3D.Transform;
using unTransform = UnityEngine.Transform;

namespace Saab.Foundation.Unity.MapStreamer.Traversal
{
    internal static class NodeTransformApplicator
    {
        public static void Apply(gzTransform node, unTransform transform)
        {
            if (!node.IsActive())
                return;

            node.GetTransform(out Matrix4 matrix);

            transform.localPosition = matrix.Translation().ToVector3();
            transform.localScale = matrix.Scale().ToVector3();
            transform.localRotation = matrix.Quaternion().ToQuaternion();
        }
    }
}
