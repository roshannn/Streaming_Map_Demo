using System.Collections.Generic;
using StreamingMapDemo.Drones;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

namespace StreamingMapDemo.Drones.Editor
{
    public static class DroneViewPrefabBuilder
    {
        private const string RootFolder = "Assets/DroneView";
        private const string MaterialsFolder = RootFolder + "/Materials";
        private const string PrefabFolder = RootFolder + "/Prefabs";
        private const string PrefabPath = PrefabFolder + "/DroneView.prefab";
        private const string BulletPrefabPath = PrefabFolder + "/DroneBullet.prefab";
        private const string CrosshairPrefabPath = PrefabFolder + "/Crosshair.prefab";
        private const string EnemyPrefabPath = PrefabFolder + "/EnemyDroneView.prefab";
        private const string ProjectileViewPrefabPath = PrefabFolder + "/ProjectileView.prefab";
        private const string HudPrefabPath = PrefabFolder + "/CombatHud.prefab";
        private const string ImpactVfxPrefabPath = PrefabFolder + "/ProjectileImpactVfx.prefab";
        private const string DestructionVfxPrefabPath = PrefabFolder + "/DroneDestructionVfx.prefab";

        [InitializeOnLoadMethod]
        private static void BuildNewCombatAssetsOnce()
        {
            GameObject crosshair = AssetDatabase.LoadAssetAtPath<GameObject>(CrosshairPrefabPath);
            GameObject drone = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            DroneView droneView = drone != null ? drone.GetComponent<DroneView>() : null;
            bool hasHealthBillboard = false;
            if (droneView != null)
            {
                SerializedProperty healthCanvas = new SerializedObject(droneView).FindProperty("healthCanvas");
                hasHealthBillboard = healthCanvas != null && healthCanvas.objectReferenceValue != null;
            }
            if (crosshair != null && crosshair.GetComponent<DroneTargetingView>() != null &&
                crosshair.transform.Find("TargetBrackets/AcquisitionLockBar") != null &&
                hasHealthBillboard &&
                AssetDatabase.LoadAssetAtPath<CombatHudPresenter>(HudPrefabPath) != null &&
                AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath).transform.Find("LockStatus") != null &&
                AssetDatabase.LoadAssetAtPath<ParticleSystem>(ImpactVfxPrefabPath) != null &&
                AssetDatabase.LoadAssetAtPath<ParticleSystem>(DestructionVfxPrefabPath) != null) return;
            EditorApplication.delayCall += Build;
        }

        [MenuItem("Tools/Drone View/Rebuild Prefab")]
        public static void Build()
        {
            EnsureFolder(MaterialsFolder);
            EnsureFolder(PrefabFolder);
            Material airframe = GetOrCreateMaterial("DroneAirframe", Color.white);
            Material neutral = GetOrCreateMaterial("DroneNeutral", new Color(0.08f, 0.09f, 0.11f, 1f));
            Material marker = GetOrCreateMaterial("DroneFrontMarker", new Color(1f, 0.22f, 0.08f, 1f));
            Material bulletMaterial = GetOrCreateMaterial("DroneBullet", new Color(1f, 0.65f, 0.08f, 1f));
            Material tracerMaterial = GetOrCreateTracerMaterial();
            DroneBullet bulletPrefab = BuildBullet(bulletMaterial, tracerMaterial);
            ProjectileView projectileViewPrefab = BuildProjectileView(bulletMaterial, tracerMaterial);
            CombatHudPresenter hudPrefab = BuildHud();
            ParticleSystem impactVfxPrefab = BuildParticleEffect("ProjectileImpactVfx", ImpactVfxPrefabPath,
                new Color(1f, .65f, .1f, 1f), 18, .28f, .09f, 3.5f, tracerMaterial);
            ParticleSystem destructionVfxPrefab = BuildParticleEffect("DroneDestructionVfx", DestructionVfxPrefabPath,
                new Color(1f, .18f, .03f, 1f), 55, .7f, .18f, 7f, tracerMaterial);
            BuildCrosshair();

            GameObject root = new GameObject("DroneView");
            root.layer = LayerMask.NameToLayer("Ignore Raycast");
            try
            {
                Transform innerBody = NewChild(root.transform, "InnerBody");
                var colored = new List<Renderer>();
                var propellers = new List<Transform>();
                colored.Add(CreatePart(innerBody, "Body", PrimitiveType.Sphere, Vector3.zero,
                    new Vector3(1.3f, 0.35f, 0.9f), airframe));

                CreateArm(innerBody, "FrontLeft", new Vector3(-0.68f, 0f, 0.68f), -45f, airframe, neutral, colored, propellers);
                CreateArm(innerBody, "FrontRight", new Vector3(0.68f, 0f, 0.68f), 45f, airframe, neutral, colored, propellers);
                CreateArm(innerBody, "RearLeft", new Vector3(-0.68f, 0f, -0.68f), 45f, airframe, neutral, colored, propellers);
                CreateArm(innerBody, "RearRight", new Vector3(0.68f, 0f, -0.68f), -45f, airframe, neutral, colored, propellers);
                CreatePart(innerBody, "FrontMarker", PrimitiveType.Cube, new Vector3(0f, 0f, 0.48f),
                    new Vector3(0.3f, 0.12f, 0.08f), marker);

                Transform turret = NewChild(innerBody, "Turret");
                turret.localPosition = new Vector3(0f, -0.22f, 0.08f);
                CreatePart(turret, "Base", PrimitiveType.Cylinder, Vector3.zero,
                    new Vector3(0.34f, 0.12f, 0.34f), neutral);
                Transform turretPivot = NewChild(turret, "AimPivot");
                turretPivot.localPosition = new Vector3(0f, -0.03f, 0.12f);
                CreatePart(turretPivot, "Barrel", PrimitiveType.Cube, new Vector3(0f, 0f, 0.3f),
                    new Vector3(0.14f, 0.12f, 0.6f), neutral);
                Transform turretPoint = NewChild(turretPivot, "TurretPoint");
                turretPoint.localPosition = new Vector3(0f, 0f, 0.62f);
                Image healthFill = CreateHealthBar(root.transform);

                DroneView view = root.AddComponent<DroneView>();
                BoxCollider droneCollider = root.AddComponent<BoxCollider>();
                droneCollider.center = new Vector3(0f, -0.02f, 0f);
                droneCollider.size = new Vector3(1.9f, 0.5f, 1.9f);
                Rigidbody droneBody = root.AddComponent<Rigidbody>();
                droneBody.useGravity = false;
                droneBody.mass = 2f;
                droneBody.drag = 1f;
                droneBody.angularDrag = 4f;
                droneBody.interpolation = RigidbodyInterpolation.Interpolate;
                droneBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                SerializedObject serialized = new SerializedObject(view);
                SetReference(serialized, "innerBody", innerBody);
                SetReference(serialized, "turretPivot", turretPivot);
                SetReference(serialized, "turretPoint", turretPoint);
                SetReference(serialized, "bulletPrefab", bulletPrefab);
                SetReference(serialized, "healthFill", healthFill);
                SetReference(serialized, "healthCanvas", healthFill.GetComponentInParent<Canvas>().transform);
                SetReference(serialized, "healthCanvasComponent", healthFill.GetComponentInParent<Canvas>());
                SetReference(serialized, "droneBody", droneBody);
                SetArray(serialized, "airframeRenderers", colored.ToArray());
                SetArray(serialized, "propellers", propellers.ToArray());
                serialized.ApplyModifiedPropertiesWithoutUndo();

                GameObject enemyAsset = PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
                DroneView enemyPrefab = enemyAsset.GetComponent<DroneView>();
                root.AddComponent<DronePlayerInput>();
                root.AddComponent<DroneCameraFollow>();
                Transform vfxRoot = NewChild(root.transform, "CombatVfx");
                CombatVfxPresenter vfx = vfxRoot.gameObject.AddComponent<CombatVfxPresenter>();
                SerializedObject vfxSerialized = new SerializedObject(vfx);
                SetReference(vfxSerialized, "impactPrefab", impactVfxPrefab);
                SetReference(vfxSerialized, "destructionPrefab", destructionVfxPrefab);
                vfxSerialized.ApplyModifiedPropertiesWithoutUndo();
                DroneCombatController combat = root.AddComponent<DroneCombatController>();
                SerializedObject combatSerialized = new SerializedObject(combat);
                SetReference(combatSerialized, "playerView", view);
                SetReference(combatSerialized, "enemyViewPrefab", enemyPrefab);
                SetReference(combatSerialized, "projectileViewPrefab", projectileViewPrefab);
                SetReference(combatSerialized, "hudPrefab", hudPrefab);
                SetReference(combatSerialized, "vfxPresenter", vfx);
                combatSerialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"Drone View prefab saved to {PrefabPath}");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static ParticleSystem BuildParticleEffect(string name, string path, Color color,
            int burstCount, float lifetime, float size, float speed, Material material)
        {
            GameObject root = new GameObject(name);
            try
            {
                ParticleSystem particles = root.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule main = particles.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = .15f;
                main.startLifetime = lifetime;
                main.startSpeed = speed;
                main.startSize = size;
                main.startColor = color;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.stopAction = ParticleSystemStopAction.None;

                ParticleSystem.EmissionModule emission = particles.emission;
                emission.enabled = true;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });

                ParticleSystem.ShapeModule shape = particles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Hemisphere;
                shape.radius = .12f;

                ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
                colorOverLifetime.enabled = true;
                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(color, 0f), new GradientColorKey(new Color(.15f,.02f,0f), 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
                colorOverLifetime.color = gradient;

                ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<ParticleSystem>();
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static ProjectileView BuildProjectileView(Material material, Material tracerMaterial)
        {
            GameObject root = new GameObject("ProjectileView");
            try
            {
                CreatePart(root.transform, "Visual", PrimitiveType.Sphere, Vector3.zero, new Vector3(.12f,.12f,.32f), material);
                TrailRenderer tracer = root.AddComponent<TrailRenderer>();
                tracer.sharedMaterial = tracerMaterial; tracer.time = .35f; tracer.startWidth = .08f; tracer.endWidth = 0f;
                tracer.shadowCastingMode = ShadowCastingMode.Off;
                ProjectileView view = root.AddComponent<ProjectileView>();
                SerializedObject serialized = new SerializedObject(view); SetReference(serialized, "trail", tracer); serialized.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, ProjectileViewPrefabPath).GetComponent<ProjectileView>();
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static CombatHudPresenter BuildHud()
        {
            GameObject root = new GameObject("CombatHud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            try
            {
                Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 90;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920,1080);
                Text health = CreateText(root.transform, "PlayerHealth", "Health: 100/100", new Vector2(24,-24), TextAnchor.UpperLeft);
                Text kills = CreateText(root.transform, "KillCounter", "Enemies destroyed: 0/10", new Vector2(-24,-24), TextAnchor.UpperRight);
                kills.rectTransform.anchorMin = kills.rectTransform.anchorMax = new Vector2(1,1); kills.rectTransform.pivot = new Vector2(1,1);
                Text lockStatus = CreateText(root.transform, "LockStatus", "NO TARGET LOCK", new Vector2(0f,-72f), TextAnchor.UpperCenter);
                lockStatus.rectTransform.anchorMin = lockStatus.rectTransform.anchorMax = new Vector2(.5f,1f);
                lockStatus.rectTransform.pivot = new Vector2(.5f,1f);
                lockStatus.rectTransform.sizeDelta = new Vector2(360f,50f);
                lockStatus.fontSize = 22;
                lockStatus.color = Color.white;
                Outline lockOutline = lockStatus.gameObject.AddComponent<Outline>();
                lockOutline.effectColor = new Color(0f,0f,0f,.9f);
                lockOutline.effectDistance = new Vector2(2f,-2f);
                GameObject victory = CreatePanel(root.transform, "VictoryPanel", "VICTORY\nPress R to restart", new Color(.08f,.45f,.14f,.88f));
                GameObject defeat = CreatePanel(root.transform, "DefeatPanel", "DRONE DESTROYED\nPress R to restart", new Color(.5f,.08f,.08f,.88f));
                victory.SetActive(false); defeat.SetActive(false);
                CombatHudPresenter presenter = root.AddComponent<CombatHudPresenter>();
                SerializedObject serialized = new SerializedObject(presenter); SetReference(serialized,"playerHealth",health); SetReference(serialized,"killCounter",kills); SetReference(serialized,"lockStatus",lockStatus); SetReference(serialized,"victoryPanel",victory); SetReference(serialized,"defeatPanel",defeat); serialized.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath).GetComponent<CombatHudPresenter>();
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Text CreateText(Transform parent, string name, string value, Vector2 position, TextAnchor alignment)
        {
            GameObject item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)); item.transform.SetParent(parent,false);
            Text text = item.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); text.text = value; text.fontSize = 26; text.color = Color.white; text.alignment = alignment;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0,1); text.rectTransform.pivot = new Vector2(0,1); text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = new Vector2(420,70); return text;
        }

        private static GameObject CreatePanel(Transform parent, string name, string label, Color color)
        {
            Image image = CreateImage(parent,name,color); RectTransform rect = image.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f); rect.sizeDelta = new Vector2(520,180); rect.anchoredPosition = Vector2.zero;
            Text text = CreateText(image.transform,"Label",label,Vector2.zero,TextAnchor.MiddleCenter); Stretch(text.rectTransform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero); return image.gameObject;
        }

        private static void BuildCrosshair()
        {
            GameObject root = new GameObject("Crosshair", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            try
            {
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                Transform reticle = NewUiChild(root.transform, "Reticle");
                RectTransform reticleRect = (RectTransform)reticle;
                reticleRect.anchorMin = reticleRect.anchorMax = new Vector2(0.5f, 0.5f);
                reticleRect.pivot = new Vector2(0.5f, 0.5f);
                reticleRect.anchoredPosition = Vector2.zero;
                reticleRect.sizeDelta = new Vector2(48f, 48f);

                var graphics = new List<Graphic>
                {
                    CreateCrosshairBar(reticle, "Top", new Vector2(0f, 14f), new Vector2(3f, 12f)),
                    CreateCrosshairBar(reticle, "Bottom", new Vector2(0f, -14f), new Vector2(3f, 12f)),
                    CreateCrosshairBar(reticle, "Left", new Vector2(-14f, 0f), new Vector2(12f, 3f)),
                    CreateCrosshairBar(reticle, "Right", new Vector2(14f, 0f), new Vector2(12f, 3f)),
                    CreateCrosshairBar(reticle, "Center", Vector2.zero, new Vector2(3f, 3f))
                };

                CrosshairView view = root.AddComponent<CrosshairView>();
                DroneFireInput fireInput = root.AddComponent<DroneFireInput>();
                RectTransform targetBrackets = CreateTargetMarker(root.transform, "TargetBrackets", new Color(.2f, .9f, 1f, 1f), 58f);
                RectTransform leadMarker = CreateTargetMarker(root.transform, "LeadMarker", new Color(1f, .72f, .12f, 1f), 26f);
                Image acquisitionRing = CreateImage(targetBrackets, "AcquisitionLockBar", Color.white);
                acquisitionRing.type = Image.Type.Simple;
                acquisitionRing.rectTransform.anchorMin = new Vector2(0f, 0f);
                acquisitionRing.rectTransform.anchorMax = new Vector2(1f, 1f);
                acquisitionRing.rectTransform.pivot = new Vector2(0f, .5f);
                acquisitionRing.rectTransform.anchoredPosition = new Vector2(0f, -42f);
                acquisitionRing.rectTransform.sizeDelta = new Vector2(0f, -50f);
                acquisitionRing.raycastTarget = false;
                DroneTargetingView targeting = root.AddComponent<DroneTargetingView>();
                SerializedObject serialized = new SerializedObject(view);
                SetArray(serialized, "graphics", graphics.ToArray());
                serialized.ApplyModifiedPropertiesWithoutUndo();
                SerializedObject targetingSerialized = new SerializedObject(targeting);
                SetReference(targetingSerialized, "acquisitionRing", acquisitionRing);
                SetReference(targetingSerialized, "targetBrackets", targetBrackets);
                SetReference(targetingSerialized, "leadMarker", leadMarker);
                SetReference(targetingSerialized, "canvas", canvas);
                targetingSerialized.ApplyModifiedPropertiesWithoutUndo();
                SerializedObject fireSerialized = new SerializedObject(fireInput);
                SetReference(fireSerialized, "targetingView", targeting);
                fireSerialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, CrosshairPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static RectTransform CreateTargetMarker(Transform parent, string name, Color color, float size)
        {
            RectTransform marker = (RectTransform)NewUiChild(parent, name);
            marker.anchorMin = marker.anchorMax = new Vector2(.5f, .5f);
            marker.pivot = new Vector2(.5f, .5f);
            marker.sizeDelta = new Vector2(size, size);
            const float thickness = 3f;
            const float length = 14f;
            CreateMarkerBar(marker, "Top", color, new Vector2(0, size * .5f), new Vector2(length, thickness));
            CreateMarkerBar(marker, "Bottom", color, new Vector2(0, -size * .5f), new Vector2(length, thickness));
            CreateMarkerBar(marker, "Left", color, new Vector2(-size * .5f, 0), new Vector2(thickness, length));
            CreateMarkerBar(marker, "Right", color, new Vector2(size * .5f, 0), new Vector2(thickness, length));
            marker.gameObject.SetActive(false);
            return marker;
        }

        private static void CreateMarkerBar(Transform parent, string name, Color color, Vector2 position, Vector2 size)
        {
            Image bar = CreateImage(parent, name, color);
            bar.raycastTarget = false;
            bar.rectTransform.anchorMin = bar.rectTransform.anchorMax = new Vector2(.5f, .5f);
            bar.rectTransform.pivot = new Vector2(.5f, .5f);
            bar.rectTransform.anchoredPosition = position;
            bar.rectTransform.sizeDelta = size;
        }

        private static Image CreateCrosshairBar(Transform parent, string name, Vector2 position, Vector2 size)
        {
            Image image = CreateImage(parent, name, Color.white);
            image.raycastTarget = false;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size;
            return image;
        }

        private static Transform NewUiChild(Transform parent, string name)
        {
            Transform child = new GameObject(name, typeof(RectTransform)).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static DroneBullet BuildBullet(Material material, Material tracerMaterial)
        {
            GameObject bullet = new GameObject("DroneBullet");
            try
            {
                CreatePart(bullet.transform, "Visual", PrimitiveType.Sphere, Vector3.zero,
                    new Vector3(0.12f, 0.12f, 0.32f), material);
                SphereCollider collider = bullet.AddComponent<SphereCollider>();
                collider.radius = 0.12f;
                collider.isTrigger = false;
                Rigidbody body = bullet.AddComponent<Rigidbody>();
                body.useGravity = false;
                body.isKinematic = false;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                TrailRenderer tracer = bullet.AddComponent<TrailRenderer>();
                tracer.sharedMaterial = tracerMaterial;
                tracer.time = 0.35f;
                tracer.minVertexDistance = 0.04f;
                tracer.widthCurve = new AnimationCurve(
                    new Keyframe(0f, 0.07f),
                    new Keyframe(1f, 0f));
                tracer.colorGradient = new Gradient
                {
                    colorKeys = new[]
                    {
                        new GradientColorKey(new Color(1f, 0.9f, 0.45f), 0f),
                        new GradientColorKey(new Color(1f, 0.22f, 0.02f), 1f)
                    },
                    alphaKeys = new[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(0f, 1f)
                    }
                };
                tracer.alignment = LineAlignment.View;
                tracer.textureMode = LineTextureMode.Stretch;
                tracer.shadowCastingMode = ShadowCastingMode.Off;
                tracer.receiveShadows = false;
                DroneBullet projectile = bullet.AddComponent<DroneBullet>();
                SerializedObject serialized = new SerializedObject(projectile);
                SetReference(serialized, "tracer", tracer);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                GameObject asset = PrefabUtility.SaveAsPrefabAsset(bullet, BulletPrefabPath);
                return asset.GetComponent<DroneBullet>();
            }
            finally
            {
                Object.DestroyImmediate(bullet);
            }
        }

        private static void CreateArm(Transform body, string name, Vector3 motorPosition, float yaw,
            Material airframe, Material neutral, ICollection<Renderer> colored, ICollection<Transform> propellers)
        {
            colored.Add(CreatePart(body, name + "Arm", PrimitiveType.Cube, motorPosition * 0.5f,
                new Vector3(0.16f, 0.12f, motorPosition.magnitude), airframe, new Vector3(0f, yaw, 0f)));
            Transform motor = NewChild(body, name + "Motor");
            motor.localPosition = motorPosition;
            CreatePart(motor, "Housing", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.28f, 0.16f, 0.28f), neutral);
            Renderer propeller = CreatePart(motor, "Propeller", PrimitiveType.Cube, new Vector3(0f, 0.15f, 0f),
                new Vector3(0.95f, 0.035f, 0.1f), neutral);
            propellers.Add(propeller.transform);
        }

        private static Image CreateHealthBar(Transform root)
        {
            GameObject canvasObject = new GameObject("HealthCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(root, false);
            canvasObject.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            canvasObject.transform.localScale = Vector3.one * 0.01f;
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(120f, 16f);

            Image background = CreateImage(canvasObject.transform, "Background", new Color(0.08f, 0.08f, 0.08f, 0.9f));
            Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image fill = CreateImage(background.transform, "Fill", new Color(0.2f, 0.85f, 0.25f, 1f));
            Stretch(fill.rectTransform, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            fill.type = Image.Type.Simple;
            fill.fillAmount = 1f;
            return fill;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            GameObject item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            item.transform.SetParent(parent, false);
            Image image = item.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
        }

        private static Transform NewChild(Transform parent, string name)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static Renderer CreatePart(Transform parent, string name, PrimitiveType type, Vector3 position,
            Vector3 scale, Material material, Vector3 rotation = default)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name; part.transform.SetParent(parent, false); part.transform.localPosition = position;
            part.transform.localEulerAngles = rotation; part.transform.localScale = scale;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            Renderer renderer = part.GetComponent<Renderer>(); renderer.sharedMaterial = material; return renderer;
        }

        private static void SetReference(SerializedObject target, string name, Object value) =>
            target.FindProperty(name).objectReferenceValue = value;

        private static void SetArray<T>(SerializedObject target, string name, T[] values) where T : Object
        {
            SerializedProperty property = target.FindProperty(name); property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Standard")) { name = name }; AssetDatabase.CreateAsset(material, path); }
            material.color = color; EditorUtility.SetDirty(material); return material;
        }

        private static Material GetOrCreateTracerMaterial()
        {
            const string name = "DroneTracer";
            string path = $"{MaterialsFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Particles/Standard Unlit");
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (shader != null)
            {
                material.shader = shader;
            }
            material.SetColor("_Color", new Color(1f, 0.55f, 0.08f, 1f));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            string current = "Assets";
            foreach (string part in path.Substring(7).Split('/'))
            {
                string next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }
    }
}
