using NUnit.Framework;
using StreamingMapDemo.Drones;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using StreamingMapDemo.Pooling;
using Saab.Foundation.Unity.MapStreamer;

namespace StreamingMapDemo.Drones.Tests
{
    public sealed class DroneViewTests
    {
        private const string PrefabPath = "Assets/DroneView/Prefabs/DroneView.prefab";
        private const string BulletPrefabPath = "Assets/DroneView/Prefabs/DroneBullet.prefab";
        private const string CrosshairPrefabPath = "Assets/DroneView/Prefabs/Crosshair.prefab";

        [Test]
        public void PrefabHasExpectedVisualPartsAndNoPhysics()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.name, Is.EqualTo("DroneView"));
            Assert.That(prefab.GetComponent<DroneView>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<DronePlayerInput>(), Is.Not.Null);
            var playerInput = new SerializedObject(prefab.GetComponent<DronePlayerInput>());
            Assert.That(playerInput.FindProperty("mouseDamping").floatValue, Is.GreaterThan(0f));
            DroneMapBridge mapBridge = prefab.GetComponent<DroneMapBridge>();
            DroneCombatPresenter presenter =
                prefab.GetComponent<DroneCombatPresenter>();
            DroneCombatController combat =
                prefab.GetComponent<DroneCombatController>();
            Assert.That(mapBridge, Is.Not.Null);
            Assert.That(presenter, Is.Not.Null);
            Assert.That(combat, Is.Not.Null);
            var combatObject = new SerializedObject(combat);
            Assert.That(
                combatObject.FindProperty("mapBridge").objectReferenceValue,
                Is.SameAs(mapBridge));
            Assert.That(
                combatObject.FindProperty("presenter").objectReferenceValue,
                Is.SameAs(presenter));
            Assert.That(prefab.GetComponent<DroneCameraFollow>(), Is.Not.Null);
            var cameraFollow = new SerializedObject(prefab.GetComponent<DroneCameraFollow>());
            Assert.That(cameraFollow.FindProperty("lookHeight").floatValue, Is.GreaterThan(0.5f));
            Assert.That(prefab.transform.Find("InnerBody"), Is.Not.Null);
            Assert.That(prefab.transform.Find("InnerBody/Turret/Base"), Is.Not.Null);
            Assert.That(prefab.transform.Find("InnerBody/Turret/AimPivot/Barrel"), Is.Not.Null);
            Assert.That(prefab.transform.Find("InnerBody/Turret/AimPivot/TurretPoint"), Is.Not.Null);
            Assert.That(prefab.transform.Find("HealthCanvas/Background/Fill").GetComponent<Image>(), Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(16));
            Assert.That(prefab.GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<Rigidbody>().useGravity, Is.False);
            Assert.That(prefab.GetComponent<Rigidbody>().collisionDetectionMode,
                Is.EqualTo(CollisionDetectionMode.ContinuousDynamic));
        }

        [Test]
        public void BulletPrefabIsPhysicsReady()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BulletPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<DroneBullet>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<SphereCollider>().isTrigger, Is.False);
            Assert.That(prefab.GetComponent<Rigidbody>().useGravity, Is.False);
            Assert.That(prefab.GetComponent<Rigidbody>().isKinematic, Is.False);
            Assert.That(prefab.GetComponent<Rigidbody>().collisionDetectionMode,
                Is.EqualTo(CollisionDetectionMode.ContinuousDynamic));
            TrailRenderer tracer = prefab.GetComponent<TrailRenderer>();
            Assert.That(tracer, Is.Not.Null);
            Assert.That(tracer.time, Is.GreaterThan(0f));
            Assert.That(tracer.sharedMaterial, Is.Not.Null);
        }

        [Test]
        public void CrosshairIsCenteredScreenSpaceOverlay()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrosshairPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(prefab.GetComponent<CrosshairView>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<DroneFireInput>(), Is.Not.Null);
            RectTransform reticle = prefab.transform.Find("Reticle").GetComponent<RectTransform>();
            Assert.That(reticle.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(reticle.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(reticle.GetComponentsInChildren<Image>(true).Length, Is.EqualTo(5));
        }

        [Test]
        public void GlobalMovementUsesUnityToGizmoAxisConversion()
        {
            GameObject gameObject = new GameObject("MapCamera");
            try
            {
                CameraControl cameraControl = gameObject.AddComponent<CameraControl>();
                cameraControl.ApplyGlobalMovement(new Vector3(2f, 3f, 4f));

                Assert.That(cameraControl.X, Is.EqualTo(2d));
                Assert.That(cameraControl.Y, Is.EqualTo(3d));
                Assert.That(cameraControl.Z, Is.EqualTo(-4d));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void GenericPoolPrewarmsExpandsAndReusesComponents()
        {
            DroneBullet prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BulletPrefabPath).GetComponent<DroneBullet>();
            var pool = new ComponentPool<DroneBullet>(prefab, 1);
            try
            {
                Assert.That(pool.Count, Is.EqualTo(1));
                DroneBullet first = pool.Get(Vector3.zero, Quaternion.identity);
                DroneBullet second = pool.Get(Vector3.one, Quaternion.identity);
                Assert.That(pool.Count, Is.EqualTo(2));
                Assert.That(pool.LeasedCount, Is.EqualTo(2));

                Assert.That(pool.Release(first), Is.True);
                Assert.That(pool.Release(first), Is.False);
                DroneBullet reused = pool.Get(Vector3.right, Quaternion.identity);
                Assert.That(reused, Is.SameAs(first));

                pool.Release(reused);
                pool.Release(second);
            }
            finally
            {
                pool.Dispose();
            }
        }

        [Test]
        public void SetColorChangesEachInstanceWithoutChangingSharedMaterial()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject first = Object.Instantiate(prefab);
            GameObject second = Object.Instantiate(prefab);
            try
            {
                DroneView firstView = first.GetComponent<DroneView>();
                DroneView secondView = second.GetComponent<DroneView>();
                Renderer firstBody = first.transform.Find("InnerBody/Body").GetComponent<Renderer>();
                Renderer secondBody = second.transform.Find("InnerBody/Body").GetComponent<Renderer>();
                Material sharedMaterial = firstBody.sharedMaterial;
                Color originalSharedColor = sharedMaterial.color;

                firstView.SetColor(Color.red);
                secondView.SetColor(Color.green);

                var block = new MaterialPropertyBlock();
                firstBody.GetPropertyBlock(block);
                Assert.That(block.GetColor("_Color"), Is.EqualTo(Color.red));
                secondBody.GetPropertyBlock(block);
                Assert.That(block.GetColor("_Color"), Is.EqualTo(Color.green));
                Assert.That(sharedMaterial, Is.SameAs(secondBody.sharedMaterial));
                Assert.That(sharedMaterial.color, Is.EqualTo(originalSharedColor));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void MovementAndHealthUseAbstractCommands()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                DroneView view = instance.GetComponent<DroneView>();
                view.SetMovementCommand(new Vector3(2f, 1f, -3f));
                view.SetFaceMovement(false);
                view.SetOrientation(Quaternion.Euler(15f, 40f, 0f));
                view.AimTurret(Vector3.right);
                view.SetHealth(25f, 100f);

                Assert.That(view.CommandedVelocity, Is.EqualTo(new Vector3(2f, 1f, -3f)));
                Assert.That(view.transform.eulerAngles.y, Is.EqualTo(40f).Within(0.01f));
                Assert.That(Vector3.Dot(view.TurretPoint.forward, Vector3.right), Is.GreaterThan(0.999f));
                Assert.That(view.TurretPivot, Is.SameAs(instance.transform.Find("InnerBody/Turret/AimPivot")));
                Assert.That(view.TurretPoint,
                    Is.SameAs(instance.transform.Find("InnerBody/Turret/AimPivot/TurretPoint")));
                Assert.That(instance.transform.Find("HealthCanvas/Background/Fill").GetComponent<Image>().fillAmount,
                    Is.EqualTo(0.25f));

                view.StopMovement();
                Assert.That(view.CommandedVelocity, Is.EqualTo(Vector3.zero));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void ShootCreatesBulletAtTurretPoint()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject instance = Object.Instantiate(prefab);
            DroneBullet bullet = null;
            try
            {
                DroneView view = instance.GetComponent<DroneView>();
                bullet = view.Shoot(Vector3.right);

                Assert.That(bullet, Is.Not.Null);
                Assert.That(bullet.transform.position, Is.EqualTo(view.TurretPoint.position));
                Assert.That(bullet.Direction, Is.EqualTo(Vector3.right));
            }
            finally
            {
                if (bullet != null) bullet.ReturnToPool();
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void SetColorToleratesMissingRendererAssignments()
        {
            GameObject gameObject = new GameObject("DroneView");
            try
            {
                DroneView view = gameObject.AddComponent<DroneView>();
                Assert.DoesNotThrow(() => view.SetColor(Color.magenta));
                Assert.That(view.DroneColor, Is.EqualTo(Color.magenta));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
