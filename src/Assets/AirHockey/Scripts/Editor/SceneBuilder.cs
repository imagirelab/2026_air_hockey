using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AirHockey.EditorTools
{
    /// <summary>エアホッケーのシーンを組み立てて保存する。</summary>
    public static class SceneBuilder
    {
        const string ScenePath = "Assets/Scenes/AirHockey.unity";
        const string MatDir = "Assets/AirHockey/Materials";

        [MenuItem("AirHockey/シーンを構築")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/AirHockey", "Materials");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            float Y = HockeyConfig.TableY;
            float W = HockeyConfig.HalfWidth, L = HockeyConfig.HalfLength, G = HockeyConfig.GoalHalfWidth;

            // 木目テクスチャ：メープル（盤面）、ウォルナット（枠）、オーク（床）、杉（壁）
            WoodTextures.Generate("Maple", new WoodTextures.WoodStyle { light = new Color(0.93f, 0.8f, 0.6f), dark = new Color(0.78f, 0.58f, 0.36f), rings = 7, grain = 30, planks = 4, warp = 0.12f, seed = 1 }, 1024, out var tMaple, out var nMaple);
            WoodTextures.Generate("Walnut", new WoodTextures.WoodStyle { light = new Color(0.45f, 0.27f, 0.15f), dark = new Color(0.2f, 0.1f, 0.05f), rings = 9, grain = 40, planks = 0, warp = 0.2f, seed = 2 }, 512, out var tWalnut, out var nWalnut);
            WoodTextures.Generate("Oak", new WoodTextures.WoodStyle { light = new Color(0.62f, 0.44f, 0.27f), dark = new Color(0.38f, 0.24f, 0.13f), rings = 5, grain = 25, planks = 6, warp = 0.15f, seed = 3 }, 1024, out var tOak, out var nOak);
            WoodTextures.Generate("Cedar", new WoodTextures.WoodStyle { light = new Color(0.7f, 0.5f, 0.34f), dark = new Color(0.48f, 0.3f, 0.18f), rings = 4, grain = 18, planks = 8, warp = 0.1f, seed = 4 }, 512, out var tCedar, out var nCedar);

            var mSurface = Mat("Surface", Color.white, 0.82f, tMaple, nMaple);
            mSurface.mainTextureScale = new Vector2(1f, 1.6f);
            var mRail = Mat("Rail", Color.white, 0.7f, tWalnut, nWalnut);
            var mFrame = Mat("Frame", new Color(0.9f, 0.85f, 0.8f), 0.55f, tWalnut, nWalnut);
            var mPuck = Mat("Puck", new Color(0.72f, 0.1f, 0.06f), 0.85f);
            var mPlayer = Mat("MalletPlayer", new Color(1f, 0.85f, 0.7f), 0.75f, tMaple, nMaple);
            var mAI = Mat("MalletAI", new Color(0.75f, 0.65f, 0.6f), 0.75f, tWalnut, nWalnut);
            var mFloor = Mat("Floor", Color.white, 0.55f, tOak, nOak);
            mFloor.mainTextureScale = new Vector2(4f, 4f);
            var mWall = Mat("Wall", new Color(0.85f, 0.8f, 0.75f), 0.25f, tCedar, nCedar);
            mWall.mainTextureScale = new Vector2(3f, 1f);
            var mDark = Mat("Dark", new Color(0.05f, 0.03f, 0.02f), 0.2f);
            var mLine = Mat("Line", new Color(0.6f, 0.15f, 0.08f), 0.6f);

            // ---- テーブル ----
            var table = new GameObject("Table");
            Box("Surface", table, new Vector3(0, Y - 0.006f - 0.02f, 0), new Vector3(W * 2, 0.04f, L * 2), mSurface);
            Box("Apron", table, new Vector3(0, Y - 0.1f, 0), new Vector3(W * 2 + 0.16f, 0.12f, L * 2 + 0.16f), mFrame);
            float rt = 0.07f, rh = 0.05f, ry = Y - 0.01f + rh * 0.5f;
            Box("RailL", table, new Vector3(-W - rt * 0.5f, ry, 0), new Vector3(rt, rh, L * 2 + rt * 2), mRail);
            Box("RailR", table, new Vector3(W + rt * 0.5f, ry, 0), new Vector3(rt, rh, L * 2 + rt * 2), mRail);
            float segW = W - G;
            foreach (int s in new[] { -1, 1 })
            {
                foreach (int side in new[] { -1, 1 })
                    Box("RailEnd", table, new Vector3(side * (G + segW * 0.5f), ry, s * (L + rt * 0.5f)), new Vector3(segW, rh, rt), mRail);
                Box("GoalSlot", table, new Vector3(0, Y - 0.04f, s * (L + rt * 0.5f)), new Vector3(G * 2, 0.06f, rt), mDark);
            }
            // センターライン・サークル・ゴールエリア
            Box("CenterLine", table, new Vector3(0, Y - 0.0055f, 0), new Vector3(W * 2, 0.001f, 0.012f), mLine);
            Ring("CenterCircle", table, Vector3.up * (Y - 0.0055f), 0.2f, 0.008f, mLine);
            Ring("GoalArcP", table, new Vector3(0, Y - 0.0055f, -L), 0.28f, 0.008f, mLine);
            Ring("GoalArcA", table, new Vector3(0, Y - 0.0055f, L), 0.28f, 0.008f, mLine);
            foreach (var p in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
                Box("Leg", table, new Vector3(p.x * (W - 0.02f), (Y - 0.16f) * 0.5f, p.y * (L - 0.05f)), new Vector3(0.1f, Y - 0.16f, 0.1f), mFrame);

            // ---- パックとマレット ----
            var puck = Cyl("Puck", null, Vector3.up * Y, HockeyConfig.PuckRadius, 0.012f, mPuck);
            var puckComp = puck.AddComponent<Puck>();
            var player = MakeMallet("PlayerMallet", mPlayer, true, new Vector3(0, Y, -L + 0.25f));
            var ai = MakeMallet("NPCMallet", mAI, false, new Vector3(0, Y, L - 0.2f));

            // ---- 部屋 ----
            var room = new GameObject("Room");
            Box("Floor", room, new Vector3(0, -0.05f, 0), new Vector3(12, 0.1f, 12), mFloor);
            Box("WallN", room, new Vector3(0, 2, 5), new Vector3(12, 4, 0.2f), mWall);
            Box("WallS", room, new Vector3(0, 2, -5), new Vector3(12, 4, 0.2f), mWall);
            Box("WallE", room, new Vector3(5, 2, 0), new Vector3(0.2f, 4, 12), mWall);
            Box("WallW", room, new Vector3(-5, 2, 0), new Vector3(0.2f, 4, 12), mWall);
            Box("Ceiling", room, new Vector3(0, 3.6f, 0), new Vector3(12, 0.1f, 12), mWall);

            // ---- ライト ----
            var sun = new GameObject("KeyLight").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.86f, 0.68f);
            sun.intensity = 0.6f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55, -30, 0);
            var lamp = new GameObject("PendantLamp").AddComponent<Light>();
            lamp.type = LightType.Spot;
            lamp.color = new Color(1f, 0.8f, 0.55f);
            lamp.intensity = 12f;
            lamp.range = 6f;
            lamp.spotAngle = 95f;
            lamp.innerSpotAngle = 50f;
            lamp.shadows = LightShadows.Soft;
            lamp.transform.position = new Vector3(0, 2.6f, 0);
            lamp.transform.rotation = Quaternion.Euler(90, 0, 0);

            // ペンダントランプの傘と電球
            var mShade = Mat("LampShade", new Color(0.25f, 0.14f, 0.07f), 0.5f, tWalnut, nWalnut);
            var mBulb = Mat("Bulb", new Color(1f, 0.85f, 0.6f), 0.9f);
            mBulb.EnableKeyword("_EMISSION");
            mBulb.SetColor("_EmissionColor", new Color(1f, 0.7f, 0.4f) * 6f);
            var shade = Cyl("LampShade", null, new Vector3(0, 2.72f, 0), 0.32f, 0.16f, mShade);
            Cyl("Bulb", shade, new Vector3(0, -0.5f, 0), 0.25f, 0.3f, mBulb).transform.localScale = new Vector3(0.35f, 0.6f, 0.35f);
            Box("Cord", null, new Vector3(0, 3.2f, 0), new Vector3(0.01f, 0.8f, 0.01f), mDark);
            // 壁際の暖かいアクセントライト
            foreach (var p in new[] { new Vector3(-3.5f, 1.8f, 3.5f), new Vector3(3.5f, 1.8f, 3.5f), new Vector3(-3.5f, 1.8f, -3.5f), new Vector3(3.5f, 1.8f, -3.5f) })
            {
                var pl = new GameObject("WarmLight").AddComponent<Light>();
                pl.type = LightType.Point;
                pl.color = new Color(1f, 0.62f, 0.32f);
                pl.intensity = 2.5f;
                pl.range = 4.5f;
                pl.transform.position = p;
                Cyl("Sconce", pl.gameObject, Vector3.zero, 0.08f, 0.2f, mBulb);
            }
            // 壁の腰板と棚
            foreach (int s in new[] { -1, 1 })
            {
                Box("Wainscot", room, new Vector3(0, 0.5f, s * 4.88f), new Vector3(12, 1f, 0.06f), mRail);
                Box("Shelf", room, new Vector3(s * 1.5f, 1.6f, 4.8f), new Vector3(1.6f, 0.05f, 0.3f), mRail);
            }

            // ---- ポストプロセス ----
            var profilePath = "Assets/AirHockey/Materials/HockeyVolume.asset";
            AssetDatabase.DeleteAsset(profilePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.6f); bloom.threshold.Override(0.95f); bloom.scatter.Override(0.7f);
            bloom.tint.Override(new Color(1f, 0.8f, 0.6f));
            var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            var vig = profile.Add<Vignette>(true); vig.intensity.Override(0.32f); vig.smoothness.Override(0.5f);
            var ca = profile.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.35f); ca.contrast.Override(12f); ca.colorFilter.Override(new Color(1f, 0.95f, 0.88f)); ca.saturation.Override(5f);
            var wb = profile.Add<WhiteBalance>(true); wb.temperature.Override(14f);
            var chroma = profile.Add<ChromaticAberration>(true); chroma.intensity.Override(0f);
            var dof = profile.Add<DepthOfField>(true); dof.mode.Override(DepthOfFieldMode.Gaussian); dof.gaussianStart.Override(3.2f); dof.gaussianEnd.Override(7f);
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            var volGo = new GameObject("PostVolume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.32f, 0.24f, 0.18f);
            RenderSettings.ambientEquatorColor = new Color(0.22f, 0.15f, 0.1f);
            RenderSettings.ambientGroundColor = new Color(0.1f, 0.06f, 0.04f);

            // ---- カメラ ----
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(UniversalAdditionalCameraData));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.03f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.05f, 0.03f);
            camGo.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            var rig = camGo.AddComponent<CameraRig>();
            rig.follow = player.GetComponent<Mallet>();
            camGo.transform.position = rig.basePos;
            camGo.transform.LookAt(rig.lookAt);

            // ---- ゲーム管理 ----
            var gmGo = new GameObject("GameManager");
            var gm = gmGo.AddComponent<GameManager>();
            gm.player = player.GetComponent<Mallet>();
            gm.ai = ai.GetComponent<Mallet>();
            gm.puck = puckComp;
            gm.cam = cam;
            gmGo.AddComponent<HockeyUI>();
            gmGo.AddComponent<HockeyAudio>();

            // テーブル縁のLEDストリップ（演出で光る）
            var mLed = Mat("LED", new Color(0.2f, 0.12f, 0.08f), 0.9f);
            mLed.EnableKeyword("_EMISSION");
            mLed.SetColor("_EmissionColor", Color.black);
            var leds = new System.Collections.Generic.List<Renderer>();
            foreach (int s in new[] { -1, 1 })
                leds.Add(Box("LEDStrip", table, new Vector3(s * (W + 0.081f), Y - 0.06f, 0), new Vector3(0.006f, 0.012f, L * 2 + 0.1f), mLed).GetComponent<Renderer>());
            foreach (int s in new[] { -1, 1 })
                leds.Add(Box("LEDStrip", table, new Vector3(0, Y - 0.06f, s * (L + 0.081f)), new Vector3(W * 2 + 0.1f, 0.012f, 0.006f), mLed).GetComponent<Renderer>());
            var fx = gmGo.AddComponent<HockeyFX>();
            fx.lamp = lamp;
            fx.volume = vol;
            fx.ledStrips = leds.ToArray();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[AirHockey] シーンを構築しました: " + ScenePath);
        }

        static GameObject MakeMallet(string name, Material m, bool isPlayer, Vector3 pos)
        {
            var root = new GameObject(name);
            root.transform.position = pos;
            float r = HockeyConfig.MalletRadius;
            Cyl("Base", root, new Vector3(0, 0.006f, 0), r, 0.024f, m);
            Cyl("Neck", root, new Vector3(0, 0.04f, 0), r * 0.38f, 0.05f, m);
            var knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            knob.name = "Knob";
            Object.DestroyImmediate(knob.GetComponent<Collider>());
            knob.transform.SetParent(root.transform, false);
            knob.transform.localPosition = new Vector3(0, 0.075f, 0);
            knob.transform.localScale = Vector3.one * r * 0.95f;
            knob.GetComponent<Renderer>().sharedMaterial = m;
            var mallet = root.AddComponent<Mallet>();
            mallet.isPlayer = isPlayer;
            mallet.maxSpeed = isPlayer ? 12f : 4f;
            return root;
        }

        public static GameObject Box(string name, GameObject parent, Vector3 pos, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            if (parent) go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        public static GameObject Cyl(string name, GameObject parent, Vector3 pos, float radius, float height, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            if (parent) go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(radius * 2, height * 0.5f, radius * 2);
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        static void Ring(string name, GameObject parent, Vector3 center, float radius, float width, Material m)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = center;
            const int seg = 64;
            var verts = new Vector3[(seg + 1) * 2];
            var tris = new int[seg * 6];
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                verts[i * 2] = d * (radius - width * 0.5f);
                verts[i * 2 + 1] = d * (radius + width * 0.5f);
                if (i < seg)
                {
                    int k = i * 6, v = i * 2;
                    tris[k] = v; tris[k + 1] = v + 2; tris[k + 2] = v + 1;
                    tris[k + 3] = v + 1; tris[k + 4] = v + 2; tris[k + 5] = v + 3;
                }
            }
            var mesh = new Mesh { name = name, vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var path = $"{MatDir}/{name}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
        }

        public static Material Mat(string name, Color color, float smoothness, Texture2D tex = null, Texture2D normal = null, float metallic = 0f)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            m.SetTexture("_BaseMap", tex);
            m.SetTexture("_BumpMap", normal);
            if (normal) { m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 0.6f); }
            else m.DisableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
