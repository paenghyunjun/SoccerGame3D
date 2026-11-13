using Soccer3D.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

namespace Soccer3D.Gameplay
{
    public static class PitchBuilder
    {
        public static GameObject BuildStadium(out GoalTrigger leftGoalTrigger, out GoalTrigger rightGoalTrigger)
        {
            GameObject root = new GameObject("SoccerStadium_3D");

            ConfigureRealisticLighting(root.transform);

            float fieldLen = ShotCalculator.PitchHalfLength * 2f; // FIFA 정규 105m
            float fieldWid = ShotCalculator.PitchHalfWidth * 2f;  // FIFA 정규 68m
            float marginX = 8.5f;
            float marginZ = 7.5f;

            // 1. 천연 잔디 구장 바닥 (알베도 + 마이크로 잔디 노멀맵 PBR 적용)
            GameObject pitchFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pitchFloor.name = "GrassPitchFloor";
            pitchFloor.transform.SetParent(root.transform, false);
            pitchFloor.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            pitchFloor.transform.localScale = new Vector3(fieldLen + marginX * 2f, 0.5f, fieldWid + marginZ * 2f);

            MaterialHelper.CreateRealisticPitchTextures(fieldLen, fieldWid, marginX, marginZ, out Texture2D pitchAlbedo, out Texture2D pitchNormal);
            Material pitchMat = MaterialHelper.CreateLitMaterial(Color.white, smoothness: 0.20f, metallic: 0.0f);
            pitchMat.mainTexture = pitchAlbedo;
            if (pitchMat.HasProperty("_BaseMap")) pitchMat.SetTexture("_BaseMap", pitchAlbedo);
            if (pitchMat.HasProperty("_BumpMap"))
            {
                pitchMat.EnableKeyword("_NORMALMAP");
                pitchMat.SetTexture("_BumpMap", pitchNormal);
                if (pitchMat.HasProperty("_BumpScale"))
                    pitchMat.SetFloat("_BumpScale", 0.75f);
            }
            pitchFloor.GetComponent<Renderer>().sharedMaterial = pitchMat;

            PhysicsMaterial turfPhys = new PhysicsMaterial("TurfPhys")
            {
                bounciness = 0.55f,
                dynamicFriction = 0.42f,
                staticFriction = 0.45f
            };
            pitchFloor.GetComponent<Collider>().material = turfPhys;

            // 2. 양쪽 정식 3D 골대 & 직조 그물 & 골 트리거
            leftGoalTrigger = BuildGoal(root.transform, -1);
            rightGoalTrigger = BuildGoal(root.transform, +1);

            // 3. 코너 플랙 4개
            BuildCornerFlags(root.transform);

            // 4. 외곽 스폰서 LED 보드 & 볼 이탈 방지 물리 벽
            BuildPerimeterBoardsAndWalls(root.transform);

            // 5. 3면 대형 스타디움 관중석 · 지붕 캐노피 · 조명탑
            BuildStadiumAtmosphere(root.transform);

            return root;
        }

        private static void ConfigureRealisticLighting(Transform stadiumRoot)
        {
            // 야간 챔피언스리그 매치 분위기의 깊이감 있는 하늘 & 앰비언트 라이팅
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.28f, 0.36f, 0.52f);
            RenderSettings.ambientEquatorColor = new Color(0.22f, 0.25f, 0.30f);
            RenderSettings.ambientGroundColor = new Color(0.09f, 0.15f, 0.09f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.07f, 0.10f, 0.17f);
            RenderSettings.fogDensity = 0.0015f; // 105m 정규 구장 반대편 골문까지 선명하게 보이도록 안개 밀도 최적화

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.clearFlags = CameraClearFlags.SolidColor;
                mainCam.backgroundColor = new Color(0.05f, 0.08f, 0.14f);
            }

            // 메인 디렉셔널 스타디움 라이트 튜닝 (부드럽고 선명한 그림자)
            Light[] lights = Object.FindObjectsByType<Light>();
            Light dirLight = null;
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional)
                {
                    dirLight = lights[i];
                    break;
                }
            }

            if (dirLight != null)
            {
                dirLight.color = new Color(1.0f, 0.96f, 0.90f);
                dirLight.intensity = 1.75f;
                dirLight.shadows = LightShadows.Hard;
                dirLight.shadowStrength = 0.80f;
                dirLight.transform.rotation = Quaternion.Euler(44f, -28f, 0f);
            }

            // 마우스 회전 시 그림자 캐스케이드 재계산 부하를 줄여 프레임 드랍 방지
            QualitySettings.shadowCascades = 2;
            QualitySettings.shadowDistance = 65f;

            // 반대편 보조 림 라이트 (선수 입체감 극대화)
            GameObject rimLightObj = new GameObject("StadiumRimFillLight");
            rimLightObj.transform.SetParent(stadiumRoot, false);
            rimLightObj.transform.rotation = Quaternion.Euler(35f, 152f, 0f);
            Light rimLight = rimLightObj.AddComponent<Light>();
            rimLight.type = LightType.Directional;
            rimLight.color = new Color(0.55f, 0.72f, 1.0f);
            rimLight.intensity = 0.48f;
            rimLight.shadows = LightShadows.None;
        }

        private static void BuildCornerFlags(Transform parent)
        {
            float hx = ShotCalculator.PitchHalfLength;
            float hz = ShotCalculator.PitchHalfWidth;
            Material poleMat = MaterialHelper.CreateLitMaterial(new Color(0.96f, 0.96f, 0.96f), 0.6f);
            Material flagMat = MaterialHelper.CreateLitMaterial(new Color(1.0f, 0.38f, 0.08f), 0.3f, 0f, new Color(0.25f, 0.08f, 0.01f));

            Vector3[] pts =
            {
                new Vector3(-hx, 0f, -hz),
                new Vector3(-hx, 0f, hz),
                new Vector3(hx, 0f, -hz),
                new Vector3(hx, 0f, hz)
            };

            for (int i = 0; i < pts.Length; i++)
            {
                GameObject cf = new GameObject("CornerFlag_" + i);
                cf.transform.SetParent(parent, false);
                cf.transform.localPosition = pts[i];

                CreateVisualCylinder("Pole", cf.transform, new Vector3(0f, 0.75f, 0f), new Vector3(0.04f, 0.75f, 0.04f), Quaternion.identity, poleMat);
                CreateVisualBar("Cloth", cf.transform, new Vector3(0.18f, 1.32f, 0f), new Vector3(0.36f, 0.24f, 0.02f), flagMat);
            }
        }

        private static GoalTrigger BuildGoal(Transform parent, int sideSign)
        {
            float goalX = sideSign * ShotCalculator.PitchHalfLength;
            float halfW = ShotCalculator.GoalHalfWidth; // 3.80m
            float height = ShotCalculator.GoalHeight;   // 2.55m
            float depth = 2.45f;

            GameObject goalRoot = new GameObject(sideSign > 0 ? "Goal_Right_AI" : "Goal_Left_Player");
            goalRoot.transform.SetParent(parent, false);
            goalRoot.transform.localPosition = new Vector3(goalX, 0f, 0f);

            Material postMat = MaterialHelper.CreateLitMaterial(new Color(0.97f, 0.98f, 1.0f), 0.85f, 0.30f);
            PhysicsMaterial postPhys = new PhysicsMaterial("GoalPostPhys")
            {
                bounciness = 0.85f,
                dynamicFriction = 0.1f,
                staticFriction = 0.1f,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };

            // 좌·우 골포스트 & 크로스바
            CreatePostCylinder("LeftPost", goalRoot.transform,
                new Vector3(0f, height * 0.5f, -halfW - 0.08f),
                new Vector3(0.16f, height * 0.5f, 0.16f), Quaternion.identity, postMat, postPhys);

            CreatePostCylinder("RightPost", goalRoot.transform,
                new Vector3(0f, height * 0.5f, halfW + 0.08f),
                new Vector3(0.16f, height * 0.5f, 0.16f), Quaternion.identity, postMat, postPhys);

            CreatePostCylinder("Crossbar", goalRoot.transform,
                new Vector3(0f, height + 0.05f, 0f),
                new Vector3(0.16f, halfW + 0.16f, 0.16f), Quaternion.Euler(90f, 0f, 0f), postMat, postPhys);

            // 유럽 구장 스타일 후면 스테인리스 텐션 폴 & 하단 베이스 프레임
            Material stanchionMat = MaterialHelper.CreateLitMaterial(new Color(0.22f, 0.26f, 0.32f), 0.7f, 0.6f);
            CreateVisualBar("BackBottomBar", goalRoot.transform,
                new Vector3(sideSign * depth, 0.05f, 0f), new Vector3(0.08f, 0.08f, halfW * 2f + 0.16f), stanchionMat);
            CreateVisualBar("SideBottomL", goalRoot.transform,
                new Vector3(sideSign * depth * 0.5f, 0.05f, -halfW - 0.06f), new Vector3(depth, 0.07f, 0.07f), stanchionMat);
            CreateVisualBar("SideBottomR", goalRoot.transform,
                new Vector3(sideSign * depth * 0.5f, 0.05f, halfW + 0.06f), new Vector3(depth, 0.07f, 0.07f), stanchionMat);
            CreateVisualCylinder("TensionPoleL", goalRoot.transform,
                new Vector3(sideSign * (depth + 0.25f), height * 0.55f, -halfW - 0.15f),
                new Vector3(0.07f, height * 0.55f, 0.07f), Quaternion.identity, stanchionMat);
            CreateVisualCylinder("TensionPoleR", goalRoot.transform,
                new Vector3(sideSign * (depth + 0.25f), height * 0.55f, halfW + 0.15f),
                new Vector3(0.07f, height * 0.55f, 0.07f), Quaternion.identity, stanchionMat);

            // 직조 그물 텍스처 기반 리얼 골망 시각 오브젝트
            GameObject netVisual = new GameObject("NetVisualRoot");
            netVisual.transform.SetParent(goalRoot.transform, false);

            Material backNetMat = CreateWovenNetMaterial(new Vector2(22f, 8f));
            Material sideNetMat = CreateWovenNetMaterial(new Vector2(8f, 8f));
            Material topNetMat = CreateWovenNetMaterial(new Vector2(8f, 22f));

            CreateVisualBar("BackNetMesh", netVisual.transform,
                new Vector3(sideSign * depth, height * 0.5f, 0f),
                new Vector3(0.02f, height, halfW * 2f), backNetMat);
            CreateVisualBar("TopNetMesh", netVisual.transform,
                new Vector3(sideSign * depth * 0.5f, height, 0f),
                new Vector3(depth, 0.02f, halfW * 2f), topNetMat);
            CreateVisualBar("SideNetA", netVisual.transform,
                new Vector3(sideSign * depth * 0.5f, height * 0.5f, -halfW),
                new Vector3(depth, height, 0.02f), sideNetMat);
            CreateVisualBar("SideNetB", netVisual.transform,
                new Vector3(sideSign * depth * 0.5f, height * 0.5f, halfW),
                new Vector3(depth, height, 0.02f), sideNetMat);

            // 그물 물리 콜라이더
            PhysicsMaterial netPhys = new PhysicsMaterial("GoalNetPhys")
            {
                bounciness = 0.12f,
                dynamicFriction = 0.85f,
                staticFriction = 0.85f,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };

            AddInvisibleCollider("BackNetCol", goalRoot.transform,
                new Vector3(sideSign * (depth + 0.12f), height * 0.5f, 0f),
                new Vector3(0.25f, height + 0.4f, halfW * 2f + 0.5f), netPhys);
            AddInvisibleCollider("TopNetCol", goalRoot.transform,
                new Vector3(sideSign * depth * 0.5f, height + 0.18f, 0f),
                new Vector3(depth + 0.3f, 0.22f, halfW * 2f + 0.4f), netPhys);
            AddInvisibleCollider("SideNetColL", goalRoot.transform,
                new Vector3(sideSign * depth * 0.5f, height * 0.5f, -halfW - 0.18f),
                new Vector3(depth + 0.3f, height + 0.3f, 0.25f), netPhys);
            AddInvisibleCollider("SideNetColR", goalRoot.transform,
                new Vector3(sideSign * depth * 0.5f, height * 0.5f, halfW + 0.18f),
                new Vector3(depth + 0.3f, height + 0.3f, 0.25f), netPhys);

            // 골라인 통과 감지 트리거
            GameObject trigObj = new GameObject("GoalScoreTrigger");
            trigObj.transform.SetParent(goalRoot.transform, false);
            trigObj.transform.localPosition = new Vector3(sideSign * (depth * 0.55f), height * 0.48f, 0f);
            BoxCollider trigBox = trigObj.AddComponent<BoxCollider>();
            trigBox.isTrigger = true;
            trigBox.size = new Vector3(depth * 0.78f, height * 0.94f, halfW * 1.94f);

            GoalTrigger gt = trigObj.AddComponent<GoalTrigger>();
            gt.goalSideSign = sideSign;
            gt.netVisualRoot = netVisual.transform;

            return gt;
        }

        private static void BuildPerimeterBoardsAndWalls(Transform root)
        {
            float halfL = ShotCalculator.PitchHalfLength;
            float halfW = ShotCalculator.PitchHalfWidth;
            float goalHalfW = ShotCalculator.GoalHalfWidth + 0.22f;

            Material boardMatDark = MaterialHelper.CreateLitMaterial(new Color(0.07f, 0.10f, 0.17f), 0.65f, 0.2f);
            Material ledAccentBlue = MaterialHelper.CreateHdrUnlitMaterial(new Color(0.10f, 0.58f, 1.0f), 1.8f);
            Material ledAccentGold = MaterialHelper.CreateHdrUnlitMaterial(new Color(1.0f, 0.72f, 0.15f), 1.8f);

            PhysicsMaterial wallBounce = new PhysicsMaterial("PerimeterBounce")
            {
                bounciness = 0.58f,
                dynamicFriction = 0.25f,
                staticFriction = 0.25f
            };

            float sideZOffset = halfW + 2.35f;
            CreateVisualBar("AdBoard_North", root, new Vector3(0f, 0.52f, sideZOffset), new Vector3(halfL * 2f + 6f, 1.04f, 0.22f), boardMatDark);
            CreateVisualBar("AdStrip_North", root, new Vector3(0f, 0.98f, sideZOffset - 0.12f), new Vector3(halfL * 2f + 5.8f, 0.10f, 0.04f), ledAccentBlue);
            CreateVisualBar("AdBoard_South", root, new Vector3(0f, 0.52f, -sideZOffset), new Vector3(halfL * 2f + 6f, 1.04f, 0.22f), boardMatDark);
            CreateVisualBar("AdStrip_South", root, new Vector3(0f, 0.98f, -sideZOffset + 0.12f), new Vector3(halfL * 2f + 5.8f, 0.10f, 0.04f), ledAccentBlue);

            float sideWallZ = halfW + 0.15f;
            AddInvisibleCollider("Wall_North", root, new Vector3(0f, 18f, sideWallZ), new Vector3(halfL * 2f + 16f, 36f, 0.8f), wallBounce);
            AddInvisibleCollider("Wall_South", root, new Vector3(0f, 18f, -sideWallZ), new Vector3(halfL * 2f + 16f, 36f, 0.8f), wallBounce);
            AddInvisibleCollider("SkyCeiling", root, new Vector3(0f, 36.0f, 0f), new Vector3(halfL * 2f + 18f, 1.0f, halfW * 2f + 16f), wallBounce);

            float wingWidth = (halfW + 2.4f) - goalHalfW;
            float wingCenterZ = goalHalfW + wingWidth * 0.5f;

            int[] signs = { -1, 1 };
            foreach (int sx in signs)
            {
                // 골라인(X축): 선수들이 골라인을 넘어 그물 옆이나 골대 뒤로 자유롭게 이동할 수 있도록 광고판 뒤(halfL + 3.75f)로 배치
                float xWall = sx * (halfL + 3.75f);
                AddInvisibleCollider($"EndWall_{sx}_TopZ", root, new Vector3(xWall, 18f, wingCenterZ), new Vector3(0.6f, 36f, wingWidth), wallBounce);
                AddInvisibleCollider($"EndWall_{sx}_BotZ", root, new Vector3(xWall, 18f, -wingCenterZ), new Vector3(0.6f, 36f, wingWidth), wallBounce);
                AddInvisibleCollider($"EndWall_{sx}_AboveGoal", root, new Vector3(xWall, 19.66f, 0f), new Vector3(0.6f, 34f, goalHalfW * 2f), wallBounce);

                float xBoard = sx * (halfL + 3.6f);
                CreateVisualBar($"EndBoard_{sx}", root, new Vector3(xBoard, 0.52f, 0f), new Vector3(0.22f, 1.04f, halfW * 2f + 4.6f), boardMatDark);
                CreateVisualBar($"EndStrip_{sx}", root, new Vector3(xBoard - sx * 0.12f, 0.98f, 0f), new Vector3(0.04f, 0.10f, halfW * 2f + 4.4f), ledAccentGold);
            }
        }

        private static void BuildStadiumAtmosphere(Transform root)
        {
            float halfL = ShotCalculator.PitchHalfLength;
            float halfW = ShotCalculator.PitchHalfWidth;

            Material concreteDark = MaterialHelper.CreateLitMaterial(new Color(0.12f, 0.15f, 0.21f), 0.25f);
            Material seatBlue = MaterialHelper.CreateLitMaterial(new Color(0.13f, 0.26f, 0.52f), 0.35f);
            Material seatRed = MaterialHelper.CreateLitMaterial(new Color(0.45f, 0.15f, 0.20f), 0.35f);
            Material roofSteel = MaterialHelper.CreateLitMaterial(new Color(0.18f, 0.21f, 0.27f), 0.65f, 0.55f);
            Material fasciaGlow = MaterialHelper.CreateHdrUnlitMaterial(new Color(0.22f, 0.65f, 1.0f), 2.1f);
            Material floodGlow = MaterialHelper.CreateHdrUnlitMaterial(new Color(0.98f, 0.97f, 0.92f), 5.5f);

            float sideStandBaseZ = halfW + 6.4f;
            float sideStandLenX = halfL * 2f + 22f;

            // 북측 & 남측 대형 스탠드 (1인칭 360도 시점 어디를 둘러봐도 웅장한 4면 정규 구장 완성!)
            int[] zSigns = { 1, -1 };
            foreach (int sz in zSigns)
            {
                string label = sz > 0 ? "North" : "South";
                for (int tier = 0; tier < 6; tier++)
                {
                    Material m = (tier % 2 == 0) ? seatBlue : concreteDark;
                    CreateVisualBar($"{label}StandTier_{tier}", root,
                        new Vector3(0f, 1.3f + tier * 1.85f, sz * (sideStandBaseZ + tier * 2.55f)),
                        new Vector3(sideStandLenX, 2.0f, 2.7f), m);
                }
                CreateVisualBar($"{label}FasciaRibbon", root,
                    new Vector3(0f, 5.4f, sz * (sideStandBaseZ + 4.2f)),
                    new Vector3(sideStandLenX - 2f, 0.28f, 0.18f), fasciaGlow);

                GameObject sideRoof = CreateVisualBar($"{label}RoofCanopy", root,
                    new Vector3(0f, 14.8f, sz * (sideStandBaseZ + 8.5f)),
                    new Vector3(sideStandLenX + 4f, 0.55f, 14.5f), roofSteel);
                sideRoof.transform.localRotation = Quaternion.Euler(sz * 8f, 0f, 0f);
            }

            // 서측(좌) & 동측(우) 골대 뒤 스탠드
            float endStandBaseX = halfL + 7.4f;
            float endStandLenZ = halfW * 2f + 18f;
            int[] sideSigns = { -1, 1 };
            foreach (int sx in sideSigns)
            {
                Material seatMat = sx < 0 ? seatBlue : seatRed;
                for (int tier = 0; tier < 5; tier++)
                {
                    CreateVisualBar($"EndStand_{sx}_{tier}", root,
                        new Vector3(sx * (endStandBaseX + tier * 2.5f), 1.3f + tier * 1.85f, 0f),
                        new Vector3(2.7f, 2.0f, endStandLenZ), (tier % 2 == 0) ? seatMat : concreteDark);
                }
                CreateVisualBar($"EndFascia_{sx}", root,
                    new Vector3(sx * (endStandBaseX + 3.8f), 5.2f, 0f),
                    new Vector3(0.18f, 0.28f, endStandLenZ - 2f), fasciaGlow);

                GameObject endRoof = CreateVisualBar($"EndRoof_{sx}", root,
                    new Vector3(sx * (endStandBaseX + 7.2f), 13.0f, 0f),
                    new Vector3(13.0f, 0.50f, endStandLenZ + 2f), roofSteel);
                endRoof.transform.localRotation = Quaternion.Euler(0f, 0f, sx * 8f);
            }

            // 4각 초대형 스타디움 조명탑 + 장거리 스포트라이트 (105m x 68m 전 구역 균일 조명)
            float cornerX = halfL + 9.2f;
            float cornerZ = halfW + 7.8f;
            Vector3[] corners =
            {
                new Vector3(-cornerX, 0f, cornerZ),
                new Vector3(cornerX, 0f, cornerZ),
                new Vector3(-cornerX, 0f, -cornerZ),
                new Vector3(cornerX, 0f, -cornerZ)
            };

            for (int i = 0; i < corners.Length; i++)
            {
                GameObject tower = new GameObject("FloodlightTower_" + i);
                tower.transform.SetParent(root, false);
                tower.transform.localPosition = corners[i];

                CreateVisualCylinder("Pylon", tower.transform, new Vector3(0f, 14.5f, 0f), new Vector3(0.62f, 14.5f, 0.62f), Quaternion.identity, roofSteel);

                GameObject headPanel = CreateVisualBar("LampBank", tower.transform, new Vector3(0f, 29.2f, 0f), new Vector3(5.2f, 2.6f, 0.55f), floodGlow);
                Vector3 aimSpot = new Vector3(corners[i].x * 0.28f, 0f, 0f);
                Vector3 toField = (aimSpot - (corners[i] + Vector3.up * 29f)).normalized;
                headPanel.transform.rotation = Quaternion.LookRotation(toField);

                GameObject spotObj = new GameObject("TowerLight_" + i);
                spotObj.transform.SetParent(tower.transform, false);
                spotObj.transform.localPosition = new Vector3(0f, 28.2f, 0f);
                spotObj.transform.rotation = Quaternion.LookRotation(toField);
                Light spot = spotObj.AddComponent<Light>();
                spot.type = LightType.Spot;
                spot.spotAngle = 82f;
                spot.range = 138f;
                spot.intensity = 4.6f;
                spot.color = new Color(0.95f, 0.97f, 1.0f);
                spot.shadows = LightShadows.None;
            }
        }

        private static Material CreateWovenNetMaterial(Vector2 tiling)
        {
            Shader s = Shader.Find("Sprites/Default");
            Material m = new Material(s != null ? s : MaterialHelper.GetUnlitShader());
            Texture2D netTex = MaterialHelper.GetGoalNetTexture();
            m.mainTexture = netTex;
            m.mainTextureScale = tiling;
            if (m.HasProperty("_BaseMap"))
            {
                m.SetTexture("_BaseMap", netTex);
                m.SetTextureScale("_BaseMap", tiling);
            }
            if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(0.96f, 0.98f, 1f, 0.88f));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.96f, 0.98f, 1f, 0.88f));
            return m;
        }

        private static void CreatePostCylinder(string name, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, Material mat, PhysicsMaterial phys)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.GetComponent<Collider>().material = phys;
        }

        private static void CreateVisualCylinder(string name, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Collider c = go.GetComponent<Collider>();
            if (Application.isPlaying) Object.Destroy(c); else Object.DestroyImmediate(c);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static GameObject CreateVisualBar(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Collider c = go.GetComponent<Collider>();
            if (Application.isPlaying) Object.Destroy(c); else Object.DestroyImmediate(c);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        private static void AddInvisibleCollider(string name, Transform parent, Vector3 pos, Vector3 size, PhysicsMaterial phys)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.size = size;
            bc.material = phys;
        }
    }
}
