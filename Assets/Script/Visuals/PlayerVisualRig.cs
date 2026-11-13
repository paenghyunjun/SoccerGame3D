using System.Collections.Generic;
using Soccer3D.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace Soccer3D.Visuals
{
    public class PlayerVisualRig : MonoBehaviour
    {
        private enum AnimAction
        {
            None,
            NormalKick,
            OverheadKick,
            ScissorsVolley,
            Tackle,
            FoulFall,
            Stumble,
            WallJump,
            KeeperDive,
            Celebrate,
            TrapBall,
            Header,
            BlitzDash,
            ShoulderBarge
        }

        public enum CelebrationStyle
        {
            SonCamera,       // 손흥민 시그니처 찰칵 세레머니
            RonaldoSiu,      // 호날두 호우(Siuuu) 점프 반회전 착지
            KneeSlide,       // 무릎 슬라이딩 & 양팔 벌려 포효
            BellinghamStand  // 벨링엄 양팔 수평 포효
        }

        private Transform _bodyRoot;
        private Transform _pelvis;
        private Transform _spine;
        private Transform _head;
        private Transform _hairRoot;

        private Transform _leftHip;
        private Transform _leftKnee;
        private Transform _leftFootTip;

        private Transform _rightHip;
        private Transform _rightKnee;
        private Transform _rightFootTip;

        private Transform _leftShoulder;
        private Transform _leftElbow;
        private Transform _rightShoulder;
        private Transform _rightElbow;

        private GameObject _chargeFootAura;
        private GameObject _groundIndicatorObj;
        private GameObject _overheadCursorObj;
        private Renderer _groundIndicatorRenderer;
        private Color _teamJerseyColor = Color.white;
        private readonly List<Renderer> _firstPersonHiddenRenderers = new List<Renderer>();
        private float _walkCycle;
        private float _heightScale = 1f;
        private float _buildScale = 1f;
        private float _jumpHeightOffset;

        public bool IsFirstPersonMode { get; private set; }
        public float WalkCycle => _walkCycle;
        public bool IsPerformingAction => _currentAction != AnimAction.None;

        public void SetJumpHeightOffset(float offsetY)
        {
            _jumpHeightOffset = Mathf.Max(0f, offsetY);
        }

        public void SetBroadcastSoloVisible(bool visible)
        {
            if (_bodyRoot != null && _bodyRoot.gameObject.activeSelf != visible)
                _bodyRoot.gameObject.SetActive(visible);
        }

        private AnimAction _currentAction = AnimAction.None;
        private float _actionTimer;
        private float _actionDuration;
        private PreferredFoot _animKickFoot = PreferredFoot.Right;
        private bool _isFinessePose;
        private float _trapBallHeight = 0.25f;
        private float _diveDirSign;
        private CelebrationStyle _currentCelebrationStyle = CelebrationStyle.SonCamera;
        private GameObject _humanRingObj;
        private Transform _humanDirArrowPivot;
        private bool _isControlledPlayer;
        private Vector3 _lastControlDir = Vector3.right;
        private static Mesh _cachedFifaRingMesh;
        private static Mesh _cachedDirTriangleMesh;

        private static Mesh GetFifaRingMesh()
        {
            if (_cachedFifaRingMesh != null) return _cachedFifaRingMesh;
            const int segs = 64;
            const float innerR = 0.89f;
            const float outerR = 0.98f;
            Vector3[] verts = new Vector3[segs * 2];
            int[] tris = new int[segs * 6];
            for (int i = 0; i < segs; i++)
            {
                float ang = (i / (float)segs) * Mathf.PI * 2f;
                float c = Mathf.Cos(ang);
                float s = Mathf.Sin(ang);
                verts[i * 2] = new Vector3(c * innerR, 0f, s * innerR);
                verts[i * 2 + 1] = new Vector3(c * outerR, 0f, s * outerR);
                int next = (i + 1) % segs;
                int t = i * 6;
                tris[t] = i * 2;
                tris[t + 1] = i * 2 + 1;
                tris[t + 2] = next * 2 + 1;
                tris[t + 3] = i * 2;
                tris[t + 4] = next * 2 + 1;
                tris[t + 5] = next * 2;
            }
            Mesh m = new Mesh { name = "FifaControlRingMesh", vertices = verts, triangles = tris };
            m.RecalculateNormals();
            _cachedFifaRingMesh = m;
            return m;
        }

        private static Mesh GetDirectionTriangleMesh()
        {
            if (_cachedDirTriangleMesh != null) return _cachedDirTriangleMesh;
            Vector3[] verts =
            {
                new Vector3(0f, 0f, 0.28f),
                new Vector3(-0.18f, 0f, -0.06f),
                new Vector3(0.18f, 0f, -0.06f)
            };
            int[] tris = { 0, 2, 1 };
            Mesh m = new Mesh { name = "FifaDirTriangleMesh", vertices = verts, triangles = tris };
            m.RecalculateNormals();
            _cachedDirTriangleMesh = m;
            return m;
        }

        public void BuildFromProfile(PlayerProfile profile, bool isGoalkeeper = false)
        {
            ClearExistingChildren();

            _heightScale = profile != null ? profile.HeightScale : 1f;
            _buildScale = profile != null ? profile.BodyBuildScale : 1f;

            Color skin = profile != null ? profile.skinColor : new Color(0.90f, 0.72f, 0.58f);
            Color jersey = profile != null ? profile.jerseyPrimaryColor : new Color(0.15f, 0.45f, 0.95f);
            Color trim = profile != null ? profile.jerseySecondaryColor : Color.white;
            Color shorts = profile != null ? profile.shortsColor : new Color(0.12f, 0.14f, 0.20f);
            Color boot = profile != null ? profile.bootColor : new Color(1f, 0.35f, 0.1f);
            Color hair = profile != null ? profile.hairColor : new Color(0.12f, 0.10f, 0.09f);

            if (isGoalkeeper)
            {
                jersey = new Color(0.14f, 0.78f, 0.38f);
                shorts = new Color(0.09f, 0.15f, 0.12f);
            }

            // PBR 물성 차별화: 유니폼 매트 패브릭(0.18), 피부 자연스러운 윤기(0.36), 축구화 합성가죽 유광(0.75)
            Material matSkin = MaterialHelper.CreateLitMaterial(skin, smoothness: 0.36f, metallic: 0.0f);
            Material matJersey = MaterialHelper.CreateLitMaterial(jersey, smoothness: 0.18f, metallic: 0.0f);
            Material matTrim = MaterialHelper.CreateLitMaterial(trim, smoothness: 0.30f, metallic: 0.05f);
            Material matShorts = MaterialHelper.CreateLitMaterial(shorts, smoothness: 0.18f, metallic: 0.0f);
            Material matSocks = MaterialHelper.CreateLitMaterial(Color.Lerp(jersey, Color.white, 0.25f), smoothness: 0.20f);
            Material matBoot = MaterialHelper.CreateLitMaterial(boot, smoothness: 0.76f, metallic: 0.20f);
            Material matSole = MaterialHelper.CreateLitMaterial(new Color(0.92f, 0.94f, 0.96f), smoothness: 0.65f, metallic: 0.3f);
            Material matHair = MaterialHelper.CreateLitMaterial(hair, smoothness: 0.24f);
            Material matDark = MaterialHelper.CreateLitMaterial(new Color(0.07f, 0.07f, 0.09f), smoothness: 0.55f);
            Material matWhite = MaterialHelper.CreateLitMaterial(new Color(0.97f, 0.97f, 0.98f), smoothness: 0.45f);

            _bodyRoot = CreateNode("BodyRoot", transform, new Vector3(0f, 0.92f * _heightScale, 0f));
            _pelvis = CreateNode("Pelvis", _bodyRoot, Vector3.zero);

            // 반바지 골반 & 힙 라인
            CreatePart(PrimitiveType.Capsule, "ShortsPelvis", _pelvis,
                new Vector3(0f, 0.02f * _heightScale, 0f),
                new Vector3(0.35f * _buildScale, 0.155f * _heightScale, 0.245f * _buildScale), matShorts);

            // 상체 척추 & 운동선수 V-테이퍼 흉곽/허리 조형
            _spine = CreateNode("Spine", _pelvis, new Vector3(0f, 0.10f * _heightScale, 0f));

            // 허리 라인
            CreatePart(PrimitiveType.Capsule, "LowerTorsoWaist", _spine,
                new Vector3(0f, 0.16f * _heightScale, 0f),
                new Vector3(0.34f * _buildScale, 0.18f * _heightScale, 0.225f * _buildScale), matJersey);

            // 넓은 흉근/어깨 라인
            CreatePart(PrimitiveType.Capsule, "UpperChest", _spine,
                new Vector3(0f, 0.35f * _heightScale, 0.005f),
                new Vector3(0.42f * _buildScale, 0.21f * _heightScale, 0.265f * _buildScale), matJersey);

            // 유니폼 카라 & 가슴 라인 디테일
            CreatePart(PrimitiveType.Cylinder, "JerseyCollar", _spine,
                new Vector3(0f, 0.52f * _heightScale, 0.005f),
                new Vector3(0.155f * _buildScale, 0.025f * _heightScale, 0.155f * _buildScale), matTrim);
            CreatePart(PrimitiveType.Cube, "ChestAccent", _spine,
                new Vector3(0f, 0.38f * _heightScale, 0.125f * _buildScale),
                new Vector3(0.30f * _buildScale, 0.045f * _heightScale, 0.025f), matTrim);

            // 목(Neck) 실린더 — 머리가 몸통 위에 자연스럽게 연결되도록 개선!
            _head = CreateNode("HeadPivot", _spine, new Vector3(0f, 0.54f * _heightScale, 0f));
            float headScale = Mathf.Lerp(1f, _heightScale, 0.42f);

            CreatePart(PrimitiveType.Cylinder, "Neck", _head,
                new Vector3(0f, 0.035f * headScale, 0f),
                new Vector3(0.105f * headScale, 0.055f * headScale, 0.105f * headScale), matSkin);

            // 두상 & 턱선 & 코 & 귀 & 눈썹 입체 조형
            CreatePart(PrimitiveType.Sphere, "HeadSkull", _head,
                new Vector3(0f, 0.17f * headScale, 0f),
                new Vector3(0.245f * headScale, 0.275f * headScale, 0.26f * headScale), matSkin);
            CreatePart(PrimitiveType.Sphere, "JawChin", _head,
                new Vector3(0f, 0.105f * headScale, 0.025f * headScale),
                new Vector3(0.18f * headScale, 0.14f * headScale, 0.19f * headScale), matSkin);
            CreatePart(PrimitiveType.Sphere, "Nose", _head,
                new Vector3(0f, 0.16f * headScale, 0.132f * headScale),
                new Vector3(0.035f * headScale, 0.048f * headScale, 0.045f * headScale), matSkin);
            CreatePart(PrimitiveType.Sphere, "LeftEar", _head,
                new Vector3(-0.122f * headScale, 0.165f * headScale, -0.005f),
                new Vector3(0.032f * headScale, 0.058f * headScale, 0.042f * headScale), matSkin);
            CreatePart(PrimitiveType.Sphere, "RightEar", _head,
                new Vector3(0.122f * headScale, 0.165f * headScale, -0.005f),
                new Vector3(0.032f * headScale, 0.058f * headScale, 0.042f * headScale), matSkin);

            // 눈동자 & 흰자위 & 눈썹
            CreatePart(PrimitiveType.Sphere, "LeftSclera", _head,
                new Vector3(-0.048f * headScale, 0.182f * headScale, 0.116f * headScale),
                new Vector3(0.042f * headScale, 0.028f * headScale, 0.025f * headScale), matWhite);
            CreatePart(PrimitiveType.Sphere, "RightSclera", _head,
                new Vector3(0.048f * headScale, 0.182f * headScale, 0.116f * headScale),
                new Vector3(0.042f * headScale, 0.028f * headScale, 0.025f * headScale), matWhite);
            CreatePart(PrimitiveType.Sphere, "LeftPupil", _head,
                new Vector3(-0.048f * headScale, 0.182f * headScale, 0.125f * headScale),
                Vector3.one * (0.024f * headScale), matDark);
            CreatePart(PrimitiveType.Sphere, "RightPupil", _head,
                new Vector3(0.048f * headScale, 0.182f * headScale, 0.125f * headScale),
                Vector3.one * (0.024f * headScale), matDark);

            CreatePart(PrimitiveType.Cube, "LeftBrow", _head,
                new Vector3(-0.048f * headScale, 0.205f * headScale, 0.118f * headScale),
                new Vector3(0.048f * headScale, 0.012f * headScale, 0.018f * headScale), matHair);
            CreatePart(PrimitiveType.Cube, "RightBrow", _head,
                new Vector3(0.048f * headScale, 0.205f * headScale, 0.118f * headScale),
                new Vector3(0.048f * headScale, 0.012f * headScale, 0.018f * headScale), matHair);

            // 랜덤 헤어스타일 생성
            _hairRoot = CreateNode("HairRoot", _head, new Vector3(0f, 0.17f * headScale, 0f));
            _hairRoot.localScale = Vector3.one * headScale;
            HairStyleType hairType = profile != null ? profile.hairStyle : HairStyleType.ShortCrop;
            BuildHairStyle(hairType, matHair, matTrim);

            // 양팔 (삼각근 어깨 캡 -> 소매 -> 팔꿈치 관절 -> 전완근 -> 손목 테이핑 -> 손)
            Material handMat = isGoalkeeper
                ? MaterialHelper.CreateLitMaterial(new Color(0.98f, 0.90f, 0.16f), 0.55f)
                : matSkin;
            float handMult = isGoalkeeper ? 1.50f : 1.0f;

            _leftShoulder = BuildArm("LeftArm", -1f, matJersey, matTrim, matSkin, matWhite, handMat, handMult, out _leftElbow);
            _rightShoulder = BuildArm("RightArm", 1f, matJersey, matTrim, matSkin, matWhite, handMat, handMult, out _rightElbow);

            // 양다리 (대퇴사두근 -> 슬개골 무릎 -> 정강이 보호대 양말 -> 유선형 축구화 + 스터드)
            bool rightPreferred = profile == null || profile.preferredFoot == PreferredFoot.Right;
            _leftHip = BuildLeg("LeftLeg", -1f, matShorts, matSkin, matSocks, matTrim, matBoot, matSole, !rightPreferred && !isGoalkeeper, out _leftKnee, out _leftFootTip);
            _rightHip = BuildLeg("RightLeg", 1f, matShorts, matSkin, matSocks, matTrim, matBoot, matSole, rightPreferred && !isGoalkeeper, out _rightKnee, out _rightFootTip);

            // 아크로바틱 차징 시 발끝 에너지 링
            _chargeFootAura = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _chargeFootAura.name = "ChargeFootAura";
            RemoveCollider(_chargeFootAura);
            _chargeFootAura.transform.SetParent(_rightFootTip, false);
            _chargeFootAura.transform.localPosition = Vector3.zero;
            _chargeFootAura.transform.localScale = Vector3.one * 0.36f;
            _chargeFootAura.GetComponent<Renderer>().sharedMaterial =
                MaterialHelper.CreateHdrUnlitMaterial(new Color(1f, 0.65f, 0.12f, 1f), 4.0f);
            _chargeFootAura.SetActive(false);

            // 바닥 플레이어 링 마커 (더 얇고 세련된 링으로 업그레이드)
            _groundIndicatorObj = null;
            if (!isGoalkeeper)
            {
                GameObject baseRing = GameObject.CreatePrimitive(PrimitiveType.Quad);
                baseRing.name = "GroundIndicator";
                RemoveCollider(baseRing);
                baseRing.transform.SetParent(transform, false);
                baseRing.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                baseRing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                baseRing.transform.localScale = new Vector3(1.15f, 1.15f, 1f);
                Renderer gr = baseRing.GetComponent<Renderer>();
                gr.sharedMaterial = MaterialHelper.CreateAdditiveVfxMaterial(jersey, 2.0f, useRing: true);
                gr.shadowCastingMode = ShadowCastingMode.Off;
                _groundIndicatorRenderer = gr;
                _teamJerseyColor = jersey;
                _groundIndicatorObj = baseRing;

                // 피파 스타일 머리 위 조종 마커 (역삼각형/다이아몬드, 현재 조종 중인 선수만 활성화)
                GameObject cursorObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cursorObj.name = "HumanControlMarker";
                RemoveCollider(cursorObj);
                cursorObj.transform.SetParent(transform, false);
                cursorObj.transform.localPosition = new Vector3(0f, 2.35f * _heightScale, 0f);
                cursorObj.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
                cursorObj.transform.localScale = new Vector3(0.28f, 0.28f, 0.28f);
                Renderer cr = cursorObj.GetComponent<Renderer>();
                cr.sharedMaterial = MaterialHelper.CreateHdrUnlitMaterial(new Color(1.0f, 0.26f, 0.18f), 2.8f);
                cr.shadowCastingMode = ShadowCastingMode.Off;
                cursorObj.SetActive(false);
                _overheadCursorObj = cursorObj;

                Material fifaNeonMat = MaterialHelper.CreateHdrUnlitMaterial(new Color(0.45f, 1.0f, 0.08f, 0.98f), 2.3f);

                _humanRingObj = new GameObject("FifaControlRing");
                _humanRingObj.transform.SetParent(transform, false);
                _humanRingObj.transform.localPosition = new Vector3(0f, 0.025f, 0f);
                MeshFilter rf = _humanRingObj.AddComponent<MeshFilter>();
                rf.sharedMesh = GetFifaRingMesh();
                MeshRenderer rr = _humanRingObj.AddComponent<MeshRenderer>();
                rr.sharedMaterial = fifaNeonMat;
                rr.shadowCastingMode = ShadowCastingMode.Off;
                _humanRingObj.SetActive(false);

                GameObject arrowPivot = new GameObject("FifaDirArrowPivot");
                arrowPivot.transform.SetParent(transform, false);
                arrowPivot.transform.localPosition = new Vector3(0f, 0.028f, 0f);
                _humanDirArrowPivot = arrowPivot.transform;

                GameObject arrowTri = new GameObject("FifaDirTriangle");
                arrowTri.transform.SetParent(_humanDirArrowPivot, false);
                arrowTri.transform.localPosition = new Vector3(0f, 0f, 1.12f);
                MeshFilter tf = arrowTri.AddComponent<MeshFilter>();
                tf.sharedMesh = GetDirectionTriangleMesh();
                MeshRenderer tr = arrowTri.AddComponent<MeshRenderer>();
                tr.sharedMaterial = fifaNeonMat;
                tr.shadowCastingMode = ShadowCastingMode.Off;

                _humanDirArrowPivot.gameObject.SetActive(false);
            }

            CacheFirstPersonHiddenRenderers();
            SetFirstPersonViewMode(IsFirstPersonMode);
        }

        private void CacheFirstPersonHiddenRenderers()
        {
            _firstPersonHiddenRenderers.Clear();
            if (_spine == null) return;

            Renderer[] spineRenderers = _spine.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < spineRenderers.Length; i++)
            {
                Renderer r = spineRenderers[i];
                if (r == null) continue;

                // 전완근·손목 테이핑·손(_leftElbow / _rightElbow 하위)은 1인칭 달리기 시 시야 양옆에 자연스럽게 보이도록 유지!
                if ((_leftElbow != null && r.transform.IsChildOf(_leftElbow)) ||
                    (_rightElbow != null && r.transform.IsChildOf(_rightElbow)))
                {
                    continue;
                }

                // 머리·헤어·목·유니폼 칼라·상체 흉곽은 ShadowsOnly로 전환해 카메라 클리핑을 완벽히 차단하면서 잔디 그림자는 그대로 유지!
                _firstPersonHiddenRenderers.Add(r);
            }
        }

        public void SetFirstPersonViewMode(bool firstPerson)
        {
            IsFirstPersonMode = firstPerson;
            ShadowCastingMode mode = firstPerson ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            for (int i = 0; i < _firstPersonHiddenRenderers.Count; i++)
            {
                if (_firstPersonHiddenRenderers[i] != null)
                    _firstPersonHiddenRenderers[i].shadowCastingMode = mode;
            }

            if (_groundIndicatorObj != null)
                _groundIndicatorObj.SetActive(!firstPerson && !_isControlledPlayer);
            if (_humanRingObj != null)
                _humanRingObj.SetActive(!firstPerson && _isControlledPlayer);
            if (_humanDirArrowPivot != null)
                _humanDirArrowPivot.gameObject.SetActive(!firstPerson && _isControlledPlayer);
        }

        public void SetControlledPlayerHighlight(bool isControlled)
        {
            _isControlledPlayer = isControlled;

            if (_overheadCursorObj != null)
                _overheadCursorObj.SetActive(isControlled);

            if (_humanRingObj != null)
                _humanRingObj.SetActive(isControlled && !IsFirstPersonMode);

            if (_humanDirArrowPivot != null)
                _humanDirArrowPivot.gameObject.SetActive(isControlled && !IsFirstPersonMode);

            if (_groundIndicatorObj != null)
            {
                _groundIndicatorObj.SetActive(!isControlled && !IsFirstPersonMode);
                _groundIndicatorObj.transform.localScale = new Vector3(0.76f, 0.008f, 0.76f);
            }

            if (_groundIndicatorRenderer != null && _groundIndicatorRenderer.material != null)
                _groundIndicatorRenderer.material.color = new Color(_teamJerseyColor.r, _teamJerseyColor.g, _teamJerseyColor.b, 0.45f);
        }

        public void UpdateControlDirectionArrow(Vector2 moveInput, Vector3 fallbackForward)
        {
            if (_humanDirArrowPivot == null || !_isControlledPlayer) return;

            if (moveInput.sqrMagnitude > 0.04f)
            {
                _lastControlDir = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
            }
            else if (_lastControlDir.sqrMagnitude < 0.01f)
            {
                Vector3 f = new Vector3(fallbackForward.x, 0f, fallbackForward.z);
                _lastControlDir = f.sqrMagnitude > 0.01f ? f.normalized : Vector3.right;
            }

            Quaternion wantRot = Quaternion.LookRotation(_lastControlDir, Vector3.up);
            _humanDirArrowPivot.rotation = Quaternion.Slerp(_humanDirArrowPivot.rotation, wantRot, Mathf.Clamp01(Time.deltaTime * 24f));
        }

        public Vector3 GetFirstPersonEyeWorldPosition(Vector3 facingForward)
        {
            float rootY = _bodyRoot != null
                ? _bodyRoot.position.y
                : (transform.position.y + 0.92f * _heightScale);
            float eyeY = rootY + 0.62f * _heightScale;
            return new Vector3(transform.position.x, eyeY, transform.position.z) + facingForward * 0.16f;
        }

        private Transform BuildArm(
            string name,
            float sideSign,
            Material matSleeve,
            Material matTrim,
            Material matSkin,
            Material matTape,
            Material matHand,
            float handScale,
            out Transform elbowPivot)
        {
            Transform shoulder = CreateNode(name + "Shoulder", _spine,
                new Vector3(sideSign * 0.245f * _buildScale, 0.45f * _heightScale, 0f));

            // 삼각근 연결부 & 반팔 소매
            CreatePart(PrimitiveType.Sphere, "DeltoidCap", shoulder,
                Vector3.zero, new Vector3(0.135f * _buildScale, 0.135f * _heightScale, 0.135f * _buildScale), matSleeve);
            CreatePart(PrimitiveType.Capsule, "UpperSleeve", shoulder,
                new Vector3(0f, -0.095f * _heightScale, 0f),
                new Vector3(0.122f * _buildScale, 0.105f * _heightScale, 0.122f * _buildScale), matSleeve);
            CreatePart(PrimitiveType.Cylinder, "SleeveCuff", shoulder,
                new Vector3(0f, -0.17f * _heightScale, 0f),
                new Vector3(0.118f * _buildScale, 0.014f * _heightScale, 0.118f * _buildScale), matTrim);
            CreatePart(PrimitiveType.Capsule, "BicepSkin", shoulder,
                new Vector3(0f, -0.20f * _heightScale, 0f),
                new Vector3(0.102f * _buildScale, 0.06f * _heightScale, 0.102f * _buildScale), matSkin);

            elbowPivot = CreateNode(name + "Elbow", shoulder, new Vector3(0f, -0.24f * _heightScale, 0f));
            CreatePart(PrimitiveType.Capsule, "Forearm", elbowPivot,
                new Vector3(0f, -0.11f * _heightScale, 0.01f),
                new Vector3(0.088f * _buildScale, 0.125f * _heightScale, 0.088f * _buildScale), matSkin);
            CreatePart(PrimitiveType.Cylinder, "WristTape", elbowPivot,
                new Vector3(0f, -0.20f * _heightScale, 0.015f),
                new Vector3(0.082f * _buildScale, 0.018f * _heightScale, 0.082f * _buildScale), matTape);
            CreatePart(PrimitiveType.Sphere, "Hand", elbowPivot,
                new Vector3(0f, -0.245f * _heightScale, 0.02f),
                new Vector3(0.085f * handScale, 0.105f * handScale, 0.065f * handScale), matHand);

            return shoulder;
        }

        private Transform BuildLeg(
            string name,
            float sideSign,
            Material matShorts,
            Material matSkin,
            Material matSocks,
            Material matTrim,
            Material matBoot,
            Material matSole,
            bool isPreferredFoot,
            out Transform kneePivot,
            out Transform footTip)
        {
            Transform hip = CreateNode(name + "Hip", _pelvis,
                new Vector3(sideSign * 0.112f * _buildScale, -0.035f * _heightScale, 0f));

            // 반바지 통 & 탄탄한 허벅지 근육
            CreatePart(PrimitiveType.Capsule, "ShortsLeg", hip,
                new Vector3(0f, -0.125f * _heightScale, 0f),
                new Vector3(0.172f * _buildScale, 0.135f * _heightScale, 0.172f * _buildScale), matShorts);
            CreatePart(PrimitiveType.Capsule, "QuadThigh", hip,
                new Vector3(0f, -0.275f * _heightScale, 0.005f),
                new Vector3(0.145f * _buildScale, 0.125f * _heightScale, 0.150f * _buildScale), matSkin);

            kneePivot = CreateNode(name + "Knee", hip, new Vector3(0f, -0.40f * _heightScale, 0f));
            CreatePart(PrimitiveType.Sphere, "Kneecap", kneePivot,
                new Vector3(0f, 0f, 0.01f),
                new Vector3(0.125f * _buildScale, 0.115f * _heightScale, 0.125f * _buildScale), matSkin);

            // 축구 스타킹 & 밴드 커프
            CreatePart(PrimitiveType.Cylinder, "SockTopCuff", kneePivot,
                new Vector3(0f, -0.065f * _heightScale, 0f),
                new Vector3(0.128f * _buildScale, 0.022f * _heightScale, 0.128f * _buildScale), matTrim);
            CreatePart(PrimitiveType.Capsule, "CalfSock", kneePivot,
                new Vector3(0f, -0.22f * _heightScale, -0.005f),
                new Vector3(0.122f * _buildScale, 0.195f * _heightScale, 0.128f * _buildScale), matSocks);

            // 유선형 3D 축구화 (발등 캡슐 + 뒤꿈치 힐컵 + 바닥 스터드 솔플레이트)
            GameObject bootRoot = new GameObject("CleatRoot");
            bootRoot.transform.SetParent(kneePivot, false);
            bootRoot.transform.localPosition = new Vector3(0f, -0.43f * _heightScale, 0.045f);

            GameObject vamp = CreatePart(PrimitiveType.Capsule, "CleatUpper", bootRoot.transform,
                new Vector3(0f, 0.005f, 0.02f), new Vector3(0.115f, 0.135f, 0.088f), matBoot);
            vamp.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            CreatePart(PrimitiveType.Cube, "SolePlate", bootRoot.transform,
                new Vector3(0f, -0.036f, 0.02f), new Vector3(0.105f, 0.022f, 0.25f), matSole);

            // 주발 발목 골드 네온 앵클 밴드
            if (isPreferredFoot)
            {
                Material goldGlow = MaterialHelper.CreateHdrUnlitMaterial(new Color(1f, 0.85f, 0.18f), 2.5f);
                CreatePart(PrimitiveType.Cylinder, "PreferredFootBand", kneePivot,
                    new Vector3(0f, -0.365f * _heightScale, 0f),
                    new Vector3(0.138f * _buildScale, 0.018f, 0.138f * _buildScale), goldGlow);
            }

            footTip = CreateNode(name + "FootTip", bootRoot.transform, new Vector3(0f, 0f, 0.15f));
            return hip;
        }

        private void BuildHairStyle(HairStyleType style, Material matHair, Material matBand)
        {
            switch (style)
            {
                case HairStyleType.ShortCrop:
                    CreatePart(PrimitiveType.Sphere, "HairCap", _hairRoot,
                        new Vector3(0f, 0.042f, -0.012f), new Vector3(0.258f, 0.21f, 0.268f), matHair);
                    break;

                case HairStyleType.SpikyCrest:
                    CreatePart(PrimitiveType.Sphere, "HairBase", _hairRoot,
                        new Vector3(0f, 0.038f, -0.01f), new Vector3(0.255f, 0.19f, 0.26f), matHair);
                    for (int i = 0; i < 4; i++)
                    {
                        GameObject spike = CreatePart(PrimitiveType.Cube, "Spike" + i, _hairRoot,
                            new Vector3(0f, 0.155f - i * 0.012f, 0.055f - i * 0.042f),
                            new Vector3(0.082f, 0.10f, 0.065f), matHair);
                        spike.transform.localRotation = Quaternion.Euler(-22f + i * 8f, 0f, 15f);
                    }
                    break;

                case HairStyleType.BigAfro:
                    CreatePart(PrimitiveType.Sphere, "AfroMain", _hairRoot,
                        new Vector3(0f, 0.085f, -0.015f), new Vector3(0.41f, 0.39f, 0.41f), matHair);
                    break;

                case HairStyleType.TopKnotPonytail:
                    CreatePart(PrimitiveType.Sphere, "HairTight", _hairRoot,
                        new Vector3(0f, 0.038f, -0.01f), new Vector3(0.256f, 0.20f, 0.265f), matHair);
                    CreatePart(PrimitiveType.Sphere, "TopBun", _hairRoot,
                        new Vector3(0f, 0.16f, -0.085f), new Vector3(0.12f, 0.12f, 0.12f), matHair);
                    GameObject tail = CreatePart(PrimitiveType.Capsule, "Ponytail", _hairRoot,
                        new Vector3(0f, 0.075f, -0.16f), new Vector3(0.065f, 0.12f, 0.065f), matHair);
                    tail.transform.localRotation = Quaternion.Euler(42f, 0f, 0f);
                    break;

                case HairStyleType.LongDreads:
                    CreatePart(PrimitiveType.Sphere, "DreadTop", _hairRoot,
                        new Vector3(0f, 0.05f, -0.01f), new Vector3(0.28f, 0.22f, 0.285f), matHair);
                    for (int i = -2; i <= 2; i++)
                    {
                        GameObject lockObj = CreatePart(PrimitiveType.Capsule, "Dread" + i, _hairRoot,
                            new Vector3(i * 0.048f, -0.06f, -0.115f + Mathf.Abs(i) * 0.018f),
                            new Vector3(0.052f, 0.155f, 0.052f), matHair);
                        lockObj.transform.localRotation = Quaternion.Euler(18f, 0f, i * -6f);
                    }
                    break;

                case HairStyleType.Mohawk:
                    for (int i = 0; i < 5; i++)
                    {
                        float t = i / 4f;
                        float ang = Mathf.Lerp(-35f, 55f, t);
                        Quaternion rot = Quaternion.Euler(ang, 0f, 0f);
                        Vector3 pos = rot * new Vector3(0f, 0.162f, 0f);
                        GameObject blade = CreatePart(PrimitiveType.Cube, "Mohawk" + i, _hairRoot,
                            pos, new Vector3(0.050f, 0.12f, 0.07f), matHair);
                        blade.transform.localRotation = rot;
                    }
                    break;

                case HairStyleType.SidePart:
                    CreatePart(PrimitiveType.Sphere, "SideBase", _hairRoot,
                        new Vector3(0f, 0.045f, 0f), new Vector3(0.265f, 0.21f, 0.27f), matHair);
                    GameObject fringe = CreatePart(PrimitiveType.Cube, "SideFringe", _hairRoot,
                        new Vector3(0.042f, 0.105f, 0.088f), new Vector3(0.165f, 0.062f, 0.10f), matHair);
                    fringe.transform.localRotation = Quaternion.Euler(12f, -15f, -14f);
                    break;

                case HairStyleType.HeadbandLong:
                    CreatePart(PrimitiveType.Sphere, "LongTop", _hairRoot,
                        new Vector3(0f, 0.05f, -0.018f), new Vector3(0.285f, 0.235f, 0.295f), matHair);
                    CreatePart(PrimitiveType.Capsule, "BackFlow", _hairRoot,
                        new Vector3(0f, -0.045f, -0.115f), new Vector3(0.235f, 0.135f, 0.11f), matHair);
                    CreatePart(PrimitiveType.Cylinder, "Headband", _hairRoot,
                        new Vector3(0f, 0.046f, 0.004f), new Vector3(0.278f, 0.022f, 0.278f), matBand);
                    break;
            }
        }

        public Vector3 GetFootWorldPosition(PreferredFoot foot)
        {
            Transform tip = foot == PreferredFoot.Right ? _rightFootTip : _leftFootTip;
            return tip != null ? tip.position : transform.position + Vector3.up * 0.35f;
        }

        public void SetChargingFootAura(bool active, PreferredFoot foot)
        {
            if (_chargeFootAura == null) return;
            Transform tip = foot == PreferredFoot.Right ? _rightFootTip : _leftFootTip;
            if (tip != null && _chargeFootAura.transform.parent != tip)
            {
                _chargeFootAura.transform.SetParent(tip, false);
                _chargeFootAura.transform.localPosition = Vector3.zero;
            }
            if (_chargeFootAura.activeSelf != active)
                _chargeFootAura.SetActive(active);
        }

        public void TriggerNormalKick(PreferredFoot foot, bool finesse)
        {
            _currentAction = AnimAction.NormalKick;
            _animKickFoot = foot;
            _isFinessePose = finesse;
            _actionTimer = 0f;
            _actionDuration = 0.42f;
        }

        public void TriggerOverheadKick(PreferredFoot foot, float totalDuration = 0.78f)
        {
            _currentAction = AnimAction.OverheadKick;
            _animKickFoot = foot;
            _actionTimer = 0f;
            _actionDuration = totalDuration;
            SetChargingFootAura(true, foot);
        }

        public void TriggerScissorsVolley(PreferredFoot foot, float totalDuration = 0.68f)
        {
            _currentAction = AnimAction.ScissorsVolley;
            _animKickFoot = foot;
            _actionTimer = 0f;
            _actionDuration = totalDuration;
            SetChargingFootAura(true, foot);
        }

        public void TriggerTackle()
        {
            _currentAction = AnimAction.Tackle;
            _actionTimer = 0f;
            _actionDuration = 0.40f;
        }

        public void TriggerFoulFall(float duration = 1.25f)
        {
            _currentAction = AnimAction.FoulFall;
            _actionTimer = 0f;
            _actionDuration = duration;
        }

        public void TriggerWallJump(float duration = 0.60f)
        {
            _currentAction = AnimAction.WallJump;
            _actionTimer = 0f;
            _actionDuration = duration;
        }

        public void TriggerKeeperDive(float dirSign, float duration = 0.62f)
        {
            _currentAction = AnimAction.KeeperDive;
            _diveDirSign = Mathf.Sign(dirSign == 0f ? 1f : dirSign);
            _actionTimer = 0f;
            _actionDuration = duration;
        }

        public void TriggerCelebration(float duration = 3.0f, CelebrationStyle style = CelebrationStyle.SonCamera)
        {
            _currentAction = AnimAction.Celebrate;
            _currentCelebrationStyle = style;
            _actionTimer = 0f;
            _actionDuration = duration;
        }

        public void TriggerStumble(float duration = 0.42f)
        {
            if (_currentAction == AnimAction.FoulFall || _currentAction == AnimAction.Celebrate) return;
            _currentAction = AnimAction.Stumble;
            _actionTimer = 0f;
            _actionDuration = duration;
        }

        public void TriggerTrapping(PreferredFoot foot, float duration = 0.36f, float ballHeight = 0.25f)
        {
            _currentAction = AnimAction.TrapBall;
            _animKickFoot = foot;
            _trapBallHeight = ballHeight;
            _actionTimer = 0f;
            _actionDuration = duration;
        }

        public void TriggerHeader(float duration = 0.58f)
        {
            _currentAction = AnimAction.Header;
            _actionTimer = 0f;
            _actionDuration = duration;
        }

        public void TriggerBlitzDash(float duration = 0.28f)
        {
            _currentAction = AnimAction.BlitzDash;
            _actionTimer = 0f;
            _actionDuration = duration;
        }

        public void TriggerShoulderBarge(float sideSign = 1f, float duration = 0.28f)
        {
            _currentAction = AnimAction.ShoulderBarge;
            _diveDirSign = Mathf.Sign(sideSign == 0f ? 1f : sideSign);
            _actionTimer = 0f;
            _actionDuration = duration;
        }

        public void ResetToIdle()
        {
            _currentAction = AnimAction.None;
            _actionTimer = 0f;
            _jumpHeightOffset = 0f;
            SetChargingFootAura(false, _animKickFoot);
            if (_bodyRoot != null)
            {
                _bodyRoot.localPosition = new Vector3(0f, 0.92f * _heightScale, 0f);
                _bodyRoot.localRotation = Quaternion.identity;
            }
            if (_head != null)
                _head.localRotation = Quaternion.identity;
        }

        public void UpdateProceduralAnimation(Vector3 planarVelocity, bool isSprinting, float dt)
        {
            if (_bodyRoot == null) return;

            float baseRootY = 0.92f * _heightScale + _jumpHeightOffset;
            float speed = planarVelocity.magnitude;

            if (_currentAction != AnimAction.None)
            {
                _actionTimer += dt;
                float t = Mathf.Clamp01(_actionTimer / Mathf.Max(0.01f, _actionDuration));
                ApplyActionPose(_currentAction, t, baseRootY);

                if (_actionTimer >= _actionDuration)
                {
                    _currentAction = AnimAction.None;
                    SetChargingFootAura(false, _animKickFoot);
                }
                return;
            }

            if (_jumpHeightOffset > 0.05f)
            {
                float jumpBlend = Mathf.Clamp01(_jumpHeightOffset / 0.65f);
                _bodyRoot.localPosition = new Vector3(0f, baseRootY, 0f);
                _bodyRoot.localRotation = Quaternion.Lerp(_bodyRoot.localRotation, Quaternion.Euler(-6f * jumpBlend, 0f, 0f), dt * 14f);
                _spine.localRotation = Quaternion.Lerp(_spine.localRotation, Quaternion.Euler(-8f * jumpBlend, 0f, 0f), dt * 14f);
                _leftHip.localRotation = Quaternion.Lerp(_leftHip.localRotation, Quaternion.Euler(26f * jumpBlend, 0f, -5f), dt * 14f);
                _rightHip.localRotation = Quaternion.Lerp(_rightHip.localRotation, Quaternion.Euler(-16f * jumpBlend, 0f, 5f), dt * 14f);
                _leftKnee.localRotation = Quaternion.Lerp(_leftKnee.localRotation, Quaternion.Euler(52f * jumpBlend + 8f, 0f, 0f), dt * 14f);
                _rightKnee.localRotation = Quaternion.Lerp(_rightKnee.localRotation, Quaternion.Euler(36f * jumpBlend + 8f, 0f, 0f), dt * 14f);
                _leftShoulder.localRotation = Quaternion.Lerp(_leftShoulder.localRotation, Quaternion.Euler(-48f * jumpBlend, 0f, 30f * jumpBlend), dt * 14f);
                _rightShoulder.localRotation = Quaternion.Lerp(_rightShoulder.localRotation, Quaternion.Euler(-48f * jumpBlend, 0f, -30f * jumpBlend), dt * 14f);
                return;
            }

            _bodyRoot.localRotation = Quaternion.Lerp(_bodyRoot.localRotation, Quaternion.identity, dt * 14f);
            if (_head != null)
                _head.localRotation = Quaternion.Lerp(_head.localRotation, Quaternion.identity, dt * 14f);
            if (_overheadCursorObj != null && _overheadCursorObj.activeSelf)
                _overheadCursorObj.transform.Rotate(0f, 95f * dt, 0f, Space.Self);

            if (speed > 0.18f)
            {
                float strideFreq = Mathf.Lerp(7.5f, 15.5f, Mathf.InverseLerp(0f, 10f, speed)) / Mathf.Sqrt(_heightScale);
                _walkCycle += dt * strideFreq;

                float swing = Mathf.Sin(_walkCycle);
                float hipAngle = swing * Mathf.Lerp(22f, 52f, Mathf.InverseLerp(1f, 9.5f, speed));
                float bob = Mathf.Abs(Mathf.Cos(_walkCycle)) * 0.052f * _heightScale;

                _bodyRoot.localPosition = new Vector3(0f, baseRootY + bob, 0f);
                _spine.localRotation = Quaternion.Euler(isSprinting ? 17f : 9f, -swing * 9f, 0f);

                _leftHip.localRotation = Quaternion.Euler(hipAngle, 0f, 0f);
                _rightHip.localRotation = Quaternion.Euler(-hipAngle, 0f, 0f);

                _leftKnee.localRotation = Quaternion.Euler(Mathf.Max(0f, -swing) * 52f + 8f, 0f, 0f);
                _rightKnee.localRotation = Quaternion.Euler(Mathf.Max(0f, swing) * 52f + 8f, 0f, 0f);

                _leftShoulder.localRotation = Quaternion.Euler(-hipAngle * 0.85f, 0f, 9f);
                _rightShoulder.localRotation = Quaternion.Euler(hipAngle * 0.85f, 0f, -9f);
                if (_leftElbow != null) _leftElbow.localRotation = Quaternion.Euler(isSprinting ? -65f : -42f, 0f, 0f);
                if (_rightElbow != null) _rightElbow.localRotation = Quaternion.Euler(isSprinting ? -65f : -42f, 0f, 0f);
            }
            else
            {
                float breathe = Mathf.Sin(Time.time * 3.2f) * 0.016f * _heightScale;
                _bodyRoot.localPosition = Vector3.Lerp(_bodyRoot.localPosition, new Vector3(0f, baseRootY + breathe, 0f), dt * 12f);
                _spine.localRotation = Quaternion.Lerp(_spine.localRotation, Quaternion.Euler(3f, 0f, 0f), dt * 10f);
                _leftHip.localRotation = Quaternion.Lerp(_leftHip.localRotation, Quaternion.Euler(0f, 0f, -3f), dt * 12f);
                _rightHip.localRotation = Quaternion.Lerp(_rightHip.localRotation, Quaternion.Euler(0f, 0f, 3f), dt * 12f);
                _leftKnee.localRotation = Quaternion.Lerp(_leftKnee.localRotation, Quaternion.Euler(5f, 0f, 0f), dt * 12f);
                _rightKnee.localRotation = Quaternion.Lerp(_rightKnee.localRotation, Quaternion.Euler(5f, 0f, 0f), dt * 12f);
                _leftShoulder.localRotation = Quaternion.Lerp(_leftShoulder.localRotation, Quaternion.Euler(0f, 0f, 10f), dt * 10f);
                _rightShoulder.localRotation = Quaternion.Lerp(_rightShoulder.localRotation, Quaternion.Euler(0f, 0f, -10f), dt * 10f);
                if (_leftElbow != null) _leftElbow.localRotation = Quaternion.Lerp(_leftElbow.localRotation, Quaternion.Euler(-18f, 0f, 0f), dt * 10f);
                if (_rightElbow != null) _rightElbow.localRotation = Quaternion.Lerp(_rightElbow.localRotation, Quaternion.Euler(-18f, 0f, 0f), dt * 10f);
            }
        }

        private void ApplyActionPose(AnimAction action, float t, float baseRootY)
        {
            Transform kickHip = _animKickFoot == PreferredFoot.Right ? _rightHip : _leftHip;
            Transform kickKnee = _animKickFoot == PreferredFoot.Right ? _rightKnee : _leftKnee;
            Transform plantHip = _animKickFoot == PreferredFoot.Right ? _leftHip : _rightHip;
            Transform plantKnee = _animKickFoot == PreferredFoot.Right ? _leftKnee : _rightKnee;
            float sideSign = _animKickFoot == PreferredFoot.Right ? 1f : -1f;

            switch (action)
            {
                case AnimAction.NormalKick:
                {
                    float hipPitch;
                    float kneePitch;
                    if (t < 0.32f)
                    {
                        float p = t / 0.32f;
                        hipPitch = Mathf.Lerp(0f, 48f, p);
                        kneePitch = Mathf.Lerp(10f, 68f, p);
                    }
                    else if (t < 0.68f)
                    {
                        float p = (t - 0.32f) / 0.36f;
                        hipPitch = Mathf.Lerp(48f, -68f, p);
                        kneePitch = Mathf.Lerp(68f, 6f, p);
                    }
                    else
                    {
                        float p = (t - 0.68f) / 0.32f;
                        hipPitch = Mathf.Lerp(-68f, 0f, p);
                        kneePitch = Mathf.Lerp(6f, 8f, p);
                    }

                    float bodyLeanZ = _isFinessePose ? -sideSign * 18f * Mathf.Sin(t * Mathf.PI) : -sideSign * 8f * Mathf.Sin(t * Mathf.PI);
                    _bodyRoot.localPosition = new Vector3(0f, baseRootY, 0f);
                    _bodyRoot.localRotation = Quaternion.Euler(-12f * Mathf.Sin(t * Mathf.PI), -sideSign * 16f * Mathf.Sin(t * Mathf.PI), bodyLeanZ);
                    kickHip.localRotation = Quaternion.Euler(hipPitch, _isFinessePose ? -sideSign * 22f : 0f, 0f);
                    kickKnee.localRotation = Quaternion.Euler(kneePitch, 0f, 0f);
                    plantHip.localRotation = Quaternion.Euler(14f * Mathf.Sin(t * Mathf.PI), 0f, 0f);
                    plantKnee.localRotation = Quaternion.Euler(18f, 0f, 0f);
                    _leftShoulder.localRotation = Quaternion.Euler(-35f * Mathf.Sin(t * Mathf.PI), 0f, 28f);
                    _rightShoulder.localRotation = Quaternion.Euler(25f * Mathf.Sin(t * Mathf.PI), 0f, -28f);
                    break;
                }

                case AnimAction.OverheadKick:
                {
                    float jumpArc = Mathf.Sin(t * Mathf.PI);
                    float jumpHeight = jumpArc * (IsFirstPersonMode ? 1.25f : 1.48f) * _heightScale;

                    float bodyPitch;
                    if (IsFirstPersonMode)
                    {
                        // 1인칭 오버헤드킥: 어지러운 360도 카메라 뒤집힘 대신 상체를 역동적으로 뒤로 젖히며
                        // 빛나는 주발 축구화가 1인칭 시야 한가운데 하늘로 솟구쳐 공을 강타하도록 연출!
                        bodyPitch = -42f * jumpArc;
                    }
                    else if (t < 0.45f)
                        bodyPitch = Mathf.Lerp(0f, -152f, SmoothStep01(t / 0.45f));
                    else if (t < 0.80f)
                        bodyPitch = Mathf.Lerp(-152f, -315f, SmoothStep01((t - 0.45f) / 0.35f));
                    else
                        bodyPitch = Mathf.Lerp(-315f, -360f, SmoothStep01((t - 0.80f) / 0.20f));

                    _bodyRoot.localPosition = new Vector3(0f, baseRootY + jumpHeight, 0f);
                    _bodyRoot.localRotation = Quaternion.Euler(bodyPitch, 0f, sideSign * 10f * jumpArc);
                    _spine.localRotation = Quaternion.Euler(-14f * jumpArc, 0f, 0f);

                    float strikePeakHip = IsFirstPersonMode ? -124f : -108f;
                    if (t < 0.44f)
                    {
                        float p = t / 0.44f;
                        plantHip.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -78f, p), 0f, 0f);
                        plantKnee.localRotation = Quaternion.Euler(Mathf.Lerp(10f, 24f, p), 0f, 0f);
                        kickHip.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 52f, p), 0f, 0f);
                        kickKnee.localRotation = Quaternion.Euler(Mathf.Lerp(12f, 82f, p), 0f, 0f);
                    }
                    else if (t < 0.72f)
                    {
                        float p = (t - 0.44f) / 0.28f;
                        plantHip.localRotation = Quaternion.Euler(Mathf.Lerp(-78f, 35f, p), 0f, 0f);
                        plantKnee.localRotation = Quaternion.Euler(45f, 0f, 0f);
                        kickHip.localRotation = Quaternion.Euler(Mathf.Lerp(52f, strikePeakHip, SmoothStep01(Mathf.Min(1f, p * 1.6f))), 0f, 0f);
                        kickKnee.localRotation = Quaternion.Euler(Mathf.Lerp(82f, 4f, SmoothStep01(Mathf.Min(1f, p * 1.6f))), 0f, 0f);
                    }
                    else
                    {
                        float p = (t - 0.72f) / 0.28f;
                        plantHip.localRotation = Quaternion.Euler(Mathf.Lerp(35f, 0f, p), 0f, 0f);
                        kickHip.localRotation = Quaternion.Euler(Mathf.Lerp(strikePeakHip, 0f, p), 0f, 0f);
                        kickKnee.localRotation = Quaternion.Euler(18f, 0f, 0f);
                    }

                    _leftShoulder.localRotation = Quaternion.Euler(-65f * jumpArc, 0f, 48f * jumpArc);
                    _rightShoulder.localRotation = Quaternion.Euler(-65f * jumpArc, 0f, -48f * jumpArc);
                    break;
                }

                case AnimAction.ScissorsVolley:
                {
                    float arc = Mathf.Sin(t * Mathf.PI);
                    _bodyRoot.localPosition = new Vector3(0f, baseRootY + arc * 1.08f * _heightScale, 0f);
                    _bodyRoot.localRotation = Quaternion.Euler(-18f * arc, -sideSign * 35f * arc, -sideSign * 72f * arc);

                    float kickSweep = t < 0.45f
                        ? Mathf.Lerp(42f, -88f, SmoothStep01(t / 0.45f))
                        : Mathf.Lerp(-88f, 0f, SmoothStep01((t - 0.45f) / 0.55f));

                    kickHip.localRotation = Quaternion.Euler(kickSweep, 0f, sideSign * 18f * arc);
                    kickKnee.localRotation = Quaternion.Euler(t < 0.45f ? 30f : 6f, 0f, 0f);
                    plantHip.localRotation = Quaternion.Euler(35f * arc, 0f, -sideSign * 15f * arc);
                    plantKnee.localRotation = Quaternion.Euler(45f * arc, 0f, 0f);
                    _leftShoulder.localRotation = Quaternion.Euler(0f, 0f, 65f * arc);
                    _rightShoulder.localRotation = Quaternion.Euler(0f, 0f, -65f * arc);
                    break;
                }

                case AnimAction.Tackle:
                {
                    float lunge = Mathf.Sin(t * Mathf.PI);
                    _bodyRoot.localPosition = new Vector3(0f, baseRootY - 0.42f * lunge * _heightScale, 0.30f * lunge);
                    _bodyRoot.localRotation = Quaternion.Euler(-45f * lunge, 0f, 0f);
                    _rightHip.localRotation = Quaternion.Euler(-72f * lunge, 0f, 0f);
                    _rightKnee.localRotation = Quaternion.Euler(6f, 0f, 0f);
                    _leftHip.localRotation = Quaternion.Euler(48f * lunge, 0f, 0f);
                    _leftKnee.localRotation = Quaternion.Euler(78f * lunge, 0f, 0f);
                    break;
                }

                case AnimAction.FoulFall:
                {
                    if (t < 0.22f)
                    {
                        float p = SmoothStep01(t / 0.22f);
                        _bodyRoot.localPosition = new Vector3(0f, baseRootY + Mathf.Sin(p * Mathf.PI) * 0.08f * _heightScale, 0.12f * p);
                        _bodyRoot.localRotation = Quaternion.Euler(28f * p, 12f * p, 16f * p);
                        _spine.localRotation = Quaternion.Euler(18f * p, 8f * p, 12f * p);
                        _leftHip.localRotation = Quaternion.Euler(-22f * p, 0f, -8f);
                        _rightHip.localRotation = Quaternion.Euler(28f * p, 0f, 10f);
                        _leftKnee.localRotation = Quaternion.Euler(35f * p, 0f, 0f);
                        _rightKnee.localRotation = Quaternion.Euler(48f * p, 0f, 0f);
                        _leftShoulder.localRotation = Quaternion.Euler(-45f * p, 0f, 38f * p);
                        _rightShoulder.localRotation = Quaternion.Euler(-60f * p, 0f, -46f * p);
                    }
                    else if (t < 0.48f)
                    {
                        float p = SmoothStep01((t - 0.22f) / 0.26f);
                        _bodyRoot.localPosition = new Vector3(0f, Mathf.Lerp(baseRootY, 0.21f * _heightScale, p), 0.12f + 0.18f * p);
                        _bodyRoot.localRotation = Quaternion.Euler(Mathf.Lerp(28f, 76f, p), Mathf.Lerp(12f, 22f, p), Mathf.Lerp(16f, 46f, p));
                        _spine.localRotation = Quaternion.Euler(Mathf.Lerp(18f, 12f, p), 10f, 14f);
                        _leftHip.localRotation = Quaternion.Euler(Mathf.Lerp(-22f, -34f, p), 0f, -14f);
                        _rightHip.localRotation = Quaternion.Euler(Mathf.Lerp(28f, -14f, p), 0f, 16f);
                        _leftKnee.localRotation = Quaternion.Euler(Mathf.Lerp(35f, 72f, p), 0f, 0f);
                        _rightKnee.localRotation = Quaternion.Euler(Mathf.Lerp(48f, 44f, p), 0f, 0f);
                        _leftShoulder.localRotation = Quaternion.Euler(-65f, 0f, 52f);
                        _rightShoulder.localRotation = Quaternion.Euler(-82f, 0f, -26f);
                    }
                    else if (t < 0.72f)
                    {
                        float p = SmoothStep01((t - 0.48f) / 0.24f);
                        _bodyRoot.localPosition = new Vector3(0f, 0.21f * _heightScale, 0.30f - 0.06f * p);
                        _bodyRoot.localRotation = Quaternion.Euler(Mathf.Lerp(76f, 64f, p), Mathf.Lerp(22f, 14f, p), Mathf.Lerp(46f, 32f, p));
                        _spine.localRotation = Quaternion.Euler(14f, 6f, 8f);
                        _leftHip.localRotation = Quaternion.Euler(-42f, 0f, -12f);
                        _rightHip.localRotation = Quaternion.Euler(-18f, 0f, 14f);
                        _leftKnee.localRotation = Quaternion.Euler(78f, 0f, 0f);
                        _rightKnee.localRotation = Quaternion.Euler(52f, 0f, 0f);
                        _leftShoulder.localRotation = Quaternion.Euler(-78f, 0f, 34f);
                        _rightShoulder.localRotation = Quaternion.Euler(-62f, 0f, -20f);
                    }
                    else
                    {
                        float p = SmoothStep01((t - 0.72f) / 0.28f);
                        _bodyRoot.localPosition = new Vector3(0f, Mathf.Lerp(0.21f * _heightScale, baseRootY, p), Mathf.Lerp(0.24f, 0f, p));
                        _bodyRoot.localRotation = Quaternion.Euler(Mathf.Lerp(64f, 0f, p), Mathf.Lerp(14f, 0f, p), Mathf.Lerp(32f, 0f, p));
                        _spine.localRotation = Quaternion.Euler(Mathf.Lerp(24f, 0f, p), 0f, 0f);
                        _leftHip.localRotation = Quaternion.Euler(Mathf.Lerp(-42f, 0f, p), 0f, Mathf.Lerp(-10f, 0f, p));
                        _rightHip.localRotation = Quaternion.Euler(Mathf.Lerp(-18f, 0f, p), 0f, Mathf.Lerp(10f, 0f, p));
                        _leftKnee.localRotation = Quaternion.Euler(Mathf.Lerp(78f, 8f, p), 0f, 0f);
                        _rightKnee.localRotation = Quaternion.Euler(Mathf.Lerp(52f, 8f, p), 0f, 0f);
                        _leftShoulder.localRotation = Quaternion.Euler(Mathf.Lerp(-72f, 0f, p), 0f, Mathf.Lerp(28f, 4f, p));
                        _rightShoulder.localRotation = Quaternion.Euler(Mathf.Lerp(-52f, 0f, p), 0f, Mathf.Lerp(-20f, -4f, p));
                    }
                    break;
                }

                case AnimAction.Stumble:
                {
                    float w = Mathf.Sin(t * Mathf.PI);
                    float wobble = Mathf.Sin(t * Mathf.PI * 3f);
                    _bodyRoot.localPosition = new Vector3(0f, baseRootY - 0.10f * w * _heightScale, 0f);
                    _bodyRoot.localRotation = Quaternion.Euler(18f * w, 10f * wobble, 14f * wobble);
                    _spine.localRotation = Quaternion.Euler(14f * w, 0f, 0f);
                    _leftHip.localRotation = Quaternion.Euler(-22f * w, 0f, -8f);
                    _rightHip.localRotation = Quaternion.Euler(26f * w, 0f, 8f);
                    _leftKnee.localRotation = Quaternion.Euler(34f * w + 8f, 0f, 0f);
                    _rightKnee.localRotation = Quaternion.Euler(28f * w + 8f, 0f, 0f);
                    _leftShoulder.localRotation = Quaternion.Euler(-38f * w, 0f, 44f * w);
                    _rightShoulder.localRotation = Quaternion.Euler(-38f * w, 0f, -44f * w);
                    break;
                }

                case AnimAction.TrapBall:
                {
                    float trapArc = Mathf.Sin(t * Mathf.PI);
                    if (_trapBallHeight >= 0.92f)
                    {
                        // 공중볼 가슴(흉부) 쿠션 트래핑: 상체를 뒤로 젖히며 충격을 흡수하고 양팔로 밸런스 유지
                        _bodyRoot.localPosition = new Vector3(0f, baseRootY - 0.09f * trapArc * _heightScale, 0f);
                        _bodyRoot.localRotation = Quaternion.Euler(-8f * trapArc, 0f, 0f);
                        _spine.localRotation = Quaternion.Euler(-22f * trapArc, 0f, 0f);
                        _head.localRotation = Quaternion.Euler(-14f * trapArc, 0f, 0f);

                        kickHip.localRotation = Quaternion.Euler(-14f * trapArc, 0f, sideSign * 6f * trapArc);
                        kickKnee.localRotation = Quaternion.Euler(26f * trapArc + 10f, 0f, 0f);
                        plantHip.localRotation = Quaternion.Euler(12f * trapArc, 0f, 0f);
                        plantKnee.localRotation = Quaternion.Euler(24f * trapArc + 8f, 0f, 0f);

                        _leftShoulder.localRotation = Quaternion.Euler(12f * trapArc, 0f, 34f * trapArc);
                        _rightShoulder.localRotation = Quaternion.Euler(12f * trapArc, 0f, -34f * trapArc);
                    }
                    else if (_trapBallHeight >= 0.48f)
                    {
                        // 허벅지/골반 높이 공중볼 트래핑: 무릎과 허벅지를 들어 올려 공을 부드럽게 낙하 컨트롤
                        _bodyRoot.localPosition = new Vector3(0f, baseRootY - 0.05f * trapArc * _heightScale, 0f);
                        _bodyRoot.localRotation = Quaternion.Euler(-6f * trapArc, -sideSign * 8f * trapArc, 0f);
                        _spine.localRotation = Quaternion.Euler(-6f * trapArc, 0f, 0f);

                        kickHip.localRotation = Quaternion.Euler(-58f * trapArc, -sideSign * 10f * trapArc, 0f);
                        kickKnee.localRotation = Quaternion.Euler(82f * trapArc + 8f, 0f, 0f);
                        plantHip.localRotation = Quaternion.Euler(10f * trapArc, 0f, 0f);
                        plantKnee.localRotation = Quaternion.Euler(18f * trapArc + 8f, 0f, 0f);

                        _leftShoulder.localRotation = Quaternion.Euler(-14f * trapArc, 0f, 24f * trapArc);
                        _rightShoulder.localRotation = Quaternion.Euler(-14f * trapArc, 0f, -24f * trapArc);
                    }
                    else
                    {
                        // 낙하하거나 굴러오는 공을 발바닥/인사이드로 부드럽게 완충(Cushion) 트래핑하는 프로 선수 모션!
                        _bodyRoot.localPosition = new Vector3(0f, baseRootY - 0.08f * trapArc * _heightScale, 0f);
                        _bodyRoot.localRotation = Quaternion.Euler(6f * trapArc, -sideSign * 12f * trapArc, 0f);
                        _spine.localRotation = Quaternion.Euler(8f * trapArc, 0f, 0f);

                        kickHip.localRotation = Quaternion.Euler(-18f * trapArc, -sideSign * 24f * trapArc, 0f);
                        kickKnee.localRotation = Quaternion.Euler(32f * trapArc + 8f, 0f, 0f);
                        plantHip.localRotation = Quaternion.Euler(8f * trapArc, 0f, 0f);
                        plantKnee.localRotation = Quaternion.Euler(16f * trapArc + 6f, 0f, 0f);

                        _leftShoulder.localRotation = Quaternion.Euler(-12f * trapArc, 0f, 18f * trapArc);
                        _rightShoulder.localRotation = Quaternion.Euler(-12f * trapArc, 0f, -18f * trapArc);
                    }
                    break;
                }

                case AnimAction.Header:
                {
                    // 크로스 낙하 타이밍에 공중으로 솟구치며 이마로 강하게 내리꽂는 점프 파워 헤딩 모션!
                    float jumpArc = Mathf.Sin(t * Mathf.PI);
                    float jumpY = jumpArc * 0.85f * _heightScale;
                    _bodyRoot.localPosition = new Vector3(0f, baseRootY + jumpY, 0.18f * jumpArc);

                    float spineSnap;
                    if (t < 0.38f)
                    {
                        float p = t / 0.38f;
                        spineSnap = Mathf.Lerp(0f, -28f, SmoothStep01(p));
                    }
                    else if (t < 0.65f)
                    {
                        float p = (t - 0.38f) / 0.27f;
                        spineSnap = Mathf.Lerp(-28f, 32f, SmoothStep01(p));
                    }
                    else
                    {
                        float p = (t - 0.65f) / 0.35f;
                        spineSnap = Mathf.Lerp(32f, 0f, SmoothStep01(p));
                    }

                    _bodyRoot.localRotation = Quaternion.Euler(spineSnap * 0.75f, 0f, 0f);
                    _spine.localRotation = Quaternion.Euler(spineSnap, 0f, 0f);

                    _leftHip.localRotation = Quaternion.Euler(18f * jumpArc, 0f, -6f);
                    _rightHip.localRotation = Quaternion.Euler(18f * jumpArc, 0f, 6f);
                    _leftKnee.localRotation = Quaternion.Euler(42f * jumpArc, 0f, 0f);
                    _rightKnee.localRotation = Quaternion.Euler(42f * jumpArc, 0f, 0f);

                    _leftShoulder.localRotation = Quaternion.Euler(-45f * jumpArc, 0f, 35f * jumpArc);
                    _rightShoulder.localRotation = Quaternion.Euler(-45f * jumpArc, 0f, -35f * jumpArc);
                    break;
                }

                case AnimAction.WallJump:
                {
                    float jump = Mathf.Sin(t * Mathf.PI);
                    _bodyRoot.localPosition = new Vector3(0f, baseRootY + jump * 0.72f * _heightScale, 0f);
                    _bodyRoot.localRotation = Quaternion.identity;
                    _leftShoulder.localRotation = Quaternion.Euler(-15f * jump, 0f, -8f);
                    _rightShoulder.localRotation = Quaternion.Euler(-15f * jump, 0f, 8f);
                    _leftKnee.localRotation = Quaternion.Euler(22f * jump, 0f, 0f);
                    _rightKnee.localRotation = Quaternion.Euler(22f * jump, 0f, 0f);
                    break;
                }

                case AnimAction.KeeperDive:
                {
                    float arc = Mathf.Sin(t * Mathf.PI);
                    _bodyRoot.localPosition = new Vector3(_diveDirSign * 0.55f * arc, baseRootY + 0.32f * arc, 0f);
                    _bodyRoot.localRotation = Quaternion.Euler(0f, 0f, -_diveDirSign * 78f * arc);
                    _leftShoulder.localRotation = Quaternion.Euler(-145f * arc, 0f, 20f);
                    _rightShoulder.localRotation = Quaternion.Euler(-145f * arc, 0f, -20f);
                    break;
                }

                case AnimAction.Celebrate:
                {
                    switch (_currentCelebrationStyle)
                    {
                        case CelebrationStyle.SonCamera:
                        {
                            float poseIn = SmoothStep01(Mathf.Clamp01(t / 0.25f));
                            float sway = Mathf.Sin(t * Mathf.PI * 2f);
                            _bodyRoot.localPosition = new Vector3(0f, baseRootY - 0.04f * poseIn * _heightScale, 0f);
                            _bodyRoot.localRotation = Quaternion.Euler(4f * poseIn, 8f * sway, 0f);
                            _spine.localRotation = Quaternion.Euler(6f * poseIn, 0f, 0f);
                            if (_head != null) _head.localRotation = Quaternion.Euler(-4f * poseIn, 0f, 0f);
                            _leftShoulder.localRotation = Quaternion.Euler(-105f * poseIn, -32f * poseIn, 18f * poseIn);
                            _rightShoulder.localRotation = Quaternion.Euler(-105f * poseIn, 32f * poseIn, -18f * poseIn);
                            if (_leftElbow != null) _leftElbow.localRotation = Quaternion.Euler(-95f * poseIn, 0f, 0f);
                            if (_rightElbow != null) _rightElbow.localRotation = Quaternion.Euler(-95f * poseIn, 0f, 0f);
                            _leftHip.localRotation = Quaternion.Euler(-10f * poseIn, 0f, -6f);
                            _rightHip.localRotation = Quaternion.Euler(10f * poseIn, 0f, 6f);
                            _leftKnee.localRotation = Quaternion.Euler(14f * poseIn, 0f, 0f);
                            _rightKnee.localRotation = Quaternion.Euler(14f * poseIn, 0f, 0f);
                            break;
                        }
                        case CelebrationStyle.RonaldoSiu:
                        {
                            if (t < 0.45f)
                            {
                                float p = SmoothStep01(t / 0.45f);
                                float jump = Mathf.Sin(p * Mathf.PI);
                                _bodyRoot.localPosition = new Vector3(0f, baseRootY + jump * 0.62f * _heightScale, 0f);
                                _bodyRoot.localRotation = Quaternion.Euler(-8f * jump, Mathf.Lerp(0f, 180f, p), 0f);
                                _leftShoulder.localRotation = Quaternion.Euler(-85f * jump, 0f, 28f);
                                _rightShoulder.localRotation = Quaternion.Euler(-85f * jump, 0f, -28f);
                                _leftKnee.localRotation = Quaternion.Euler(35f * jump, 0f, 0f);
                                _rightKnee.localRotation = Quaternion.Euler(35f * jump, 0f, 0f);
                            }
                            else
                            {
                                float hold = SmoothStep01(Mathf.Clamp01((t - 0.45f) / 0.20f));
                                _bodyRoot.localPosition = new Vector3(0f, baseRootY - 0.08f * hold * _heightScale, 0f);
                                _bodyRoot.localRotation = Quaternion.Euler(-8f * hold, 180f, 0f);
                                _spine.localRotation = Quaternion.Euler(-12f * hold, 0f, 0f);
                                _leftShoulder.localRotation = Quaternion.Euler(34f * hold, 0f, 62f * hold);
                                _rightShoulder.localRotation = Quaternion.Euler(34f * hold, 0f, -62f * hold);
                                _leftHip.localRotation = Quaternion.Euler(-14f * hold, 0f, -18f * hold);
                                _rightHip.localRotation = Quaternion.Euler(-14f * hold, 0f, 18f * hold);
                                _leftKnee.localRotation = Quaternion.Euler(18f * hold, 0f, 0f);
                                _rightKnee.localRotation = Quaternion.Euler(18f * hold, 0f, 0f);
                            }
                            break;
                        }
                        case CelebrationStyle.KneeSlide:
                        {
                            float slide = t < 0.25f ? SmoothStep01(t / 0.25f) : (t > 0.80f ? 1f - SmoothStep01((t - 0.80f) / 0.20f) : 1f);
                            _bodyRoot.localPosition = new Vector3(0f, Mathf.Lerp(baseRootY, 0.50f * _heightScale, slide), 0.16f * slide);
                            _bodyRoot.localRotation = Quaternion.Euler(-16f * slide, 0f, 0f);
                            _spine.localRotation = Quaternion.Euler(-14f * slide, 0f, 0f);
                            _leftHip.localRotation = Quaternion.Euler(-22f * slide, 0f, -10f);
                            _rightHip.localRotation = Quaternion.Euler(-22f * slide, 0f, 10f);
                            _leftKnee.localRotation = Quaternion.Euler(96f * slide, 0f, 0f);
                            _rightKnee.localRotation = Quaternion.Euler(96f * slide, 0f, 0f);
                            _leftShoulder.localRotation = Quaternion.Euler(-24f * slide, 0f, 78f * slide);
                            _rightShoulder.localRotation = Quaternion.Euler(-24f * slide, 0f, -78f * slide);
                            break;
                        }
                        default:
                        {
                            float pose = SmoothStep01(Mathf.Clamp01(t / 0.28f));
                            _bodyRoot.localPosition = new Vector3(0f, baseRootY, 0f);
                            _bodyRoot.localRotation = Quaternion.Euler(-6f * pose, 0f, 0f);
                            _spine.localRotation = Quaternion.Euler(-10f * pose, 0f, 0f);
                            if (_head != null) _head.localRotation = Quaternion.Euler(-8f * pose, 0f, 0f);
                            _leftShoulder.localRotation = Quaternion.Euler(-12f * pose, 0f, 88f * pose);
                            _rightShoulder.localRotation = Quaternion.Euler(-12f * pose, 0f, -88f * pose);
                            _leftHip.localRotation = Quaternion.Euler(0f, 0f, -10f * pose);
                            _rightHip.localRotation = Quaternion.Euler(0f, 0f, 10f * pose);
                            _leftKnee.localRotation = Quaternion.Euler(6f, 0f, 0f);
                            _rightKnee.localRotation = Quaternion.Euler(6f, 0f, 0f);
                            break;
                        }
                    }
                    break;
                }

                case AnimAction.BlitzDash:
                {
                    float surge = Mathf.Sin(t * Mathf.PI);
                    float phase = t * Mathf.PI * 5.0f;
                    float stride = Mathf.Sin(phase);
                    float lift = Mathf.Abs(Mathf.Cos(phase));
                    _bodyRoot.localPosition = new Vector3(0f, baseRootY - 0.14f * surge * _heightScale + lift * 0.06f * _heightScale, 0.10f * surge);
                    _bodyRoot.localRotation = Quaternion.Euler(20f * surge, 4f * stride, 0f);
                    _spine.localRotation = Quaternion.Euler(14f * surge, -6f * stride, 0f);
                    _leftHip.localRotation = Quaternion.Euler(52f * stride, 0f, -3f);
                    _rightHip.localRotation = Quaternion.Euler(-52f * stride, 0f, 3f);
                    _leftKnee.localRotation = Quaternion.Euler(Mathf.Clamp(-38f * stride + 24f, 6f, 76f), 0f, 0f);
                    _rightKnee.localRotation = Quaternion.Euler(Mathf.Clamp(38f * stride + 24f, 6f, 76f), 0f, 0f);
                    _leftShoulder.localRotation = Quaternion.Euler(-46f * stride, 0f, 14f);
                    _rightShoulder.localRotation = Quaternion.Euler(46f * stride, 0f, -14f);
                    break;
                }

                case AnimAction.ShoulderBarge:
                {
                    // F키 어깨 밀치기 몸싸움 탈취 모션!
                    float bump = Mathf.Sin(t * Mathf.PI);
                    _bodyRoot.localPosition = new Vector3(_diveDirSign * 0.18f * bump, baseRootY - 0.08f * bump * _heightScale, 0.12f * bump);
                    _bodyRoot.localRotation = Quaternion.Euler(10f * bump, -_diveDirSign * 18f * bump, -_diveDirSign * 16f * bump);
                    _spine.localRotation = Quaternion.Euler(8f * bump, -_diveDirSign * 14f * bump, 0f);
                    _leftShoulder.localRotation = Quaternion.Euler(-20f * bump, 0f, 38f * bump);
                    _rightShoulder.localRotation = Quaternion.Euler(-20f * bump, 0f, -38f * bump);
                    break;
                }
            }
        }

        private static float SmoothStep01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private static Transform CreateNode(string name, Transform parent, Vector3 localPos)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        private static GameObject CreatePart(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            RemoveCollider(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = mat;
                // 미세 파츠(눈동자, 눈썹, 귀, 코, 머리카락 조각, 스터드 등)는 그림자 캐스터에서 제외하여 22명 회전 시 렌더링 드로우콜 부하 대폭 절감
                bool isTinyDetail = localScale.x < 0.12f && localScale.y < 0.12f;
                r.shadowCastingMode = isTinyDetail
                    ? UnityEngine.Rendering.ShadowCastingMode.Off
                    : UnityEngine.Rendering.ShadowCastingMode.On;
            }
            return go;
        }

        private static void RemoveCollider(GameObject go)
        {
            Collider col = go.GetComponent<Collider>();
            if (col == null) return;
            if (Application.isPlaying)
                Destroy(col);
            else
                DestroyImmediate(col);
        }

        private void ClearExistingChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
        }
    }
}
