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
            WallJump,
            KeeperDive,
            Celebrate,
            TrapBall,
            Header,
            BlitzDash,
            ShoulderBarge
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
        private readonly List<Renderer> _firstPersonHiddenRenderers = new List<Renderer>();
        private float _walkCycle;
        private float _heightScale = 1f;
        private float _buildScale = 1f;
        private float _jumpHeightOffset;

        public bool IsFirstPersonMode { get; private set; }
        public float WalkCycle => _walkCycle;

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
        private float _diveDirSign;

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
                _groundIndicatorObj = baseRing;
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
                _groundIndicatorObj.SetActive(!firstPerson);
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

        public void TriggerCelebration(float duration = 2.8f)
        {
            _currentAction = AnimAction.Celebrate;
            _actionTimer = 0f;
            _actionDuration = duration;
        }

        public void TriggerTrapping(PreferredFoot foot, float duration = 0.36f)
        {
            _currentAction = AnimAction.TrapBall;
            _animKickFoot = foot;
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
                    // 위험한 태클에 걸려 공중으로 걸려 넘어졌다가 잔디에 구르는 연출!
                    float down = t < 0.35f ? SmoothStep01(t / 0.35f) : (t > 0.82f ? 1f - SmoothStep01((t - 0.82f) / 0.18f) : 1f);
                    float hop = t < 0.35f ? Mathf.Sin((t / 0.35f) * Mathf.PI) * 0.45f : 0f;
                    _bodyRoot.localPosition = new Vector3(0f, Mathf.Lerp(baseRootY, 0.25f * _heightScale, down) + hop, 0f);
                    _bodyRoot.localRotation = Quaternion.Euler(82f * down, 25f * down, 35f * down);
                    _leftKnee.localRotation = Quaternion.Euler(65f * down, 0f, 0f);
                    _rightKnee.localRotation = Quaternion.Euler(55f * down, 0f, 0f);
                    _leftShoulder.localRotation = Quaternion.Euler(-55f * down, 0f, 40f * down);
                    _rightShoulder.localRotation = Quaternion.Euler(-55f * down, 0f, -40f * down);
                    break;
                }

                case AnimAction.TrapBall:
                {
                    // 낙하하거나 굴러오는 공을 발바닥/인사이드로 부드럽게 완충(Cushion) 트래핑하는 프로 선수 모션!
                    float trapArc = Mathf.Sin(t * Mathf.PI);
                    _bodyRoot.localPosition = new Vector3(0f, baseRootY - 0.08f * trapArc * _heightScale, 0f);
                    _bodyRoot.localRotation = Quaternion.Euler(6f * trapArc, -sideSign * 12f * trapArc, 0f);
                    _spine.localRotation = Quaternion.Euler(8f * trapArc, 0f, 0f);

                    kickHip.localRotation = Quaternion.Euler(-18f * trapArc, -sideSign * 24f * trapArc, 0f);
                    kickKnee.localRotation = Quaternion.Euler(32f * trapArc + 8f, 0f, 0f);
                    plantHip.localRotation = Quaternion.Euler(8f * trapArc, 0f, 0f);
                    plantKnee.localRotation = Quaternion.Euler(16f * trapArc + 6f, 0f, 0f);

                    _leftShoulder.localRotation = Quaternion.Euler(-12f * trapArc, 0f, 18f * trapArc);
                    _rightShoulder.localRotation = Quaternion.Euler(-12f * trapArc, 0f, -18f * trapArc);
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
                    float bounce = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 5f));
                    _bodyRoot.localPosition = new Vector3(0f, baseRootY + bounce * 0.55f * _heightScale, 0f);
                    _bodyRoot.localRotation = Quaternion.Euler(-10f * bounce, t * 720f, 0f);
                    _leftShoulder.localRotation = Quaternion.Euler(-155f, 0f, 25f);
                    _rightShoulder.localRotation = Quaternion.Euler(-155f, 0f, -25f);
                    _leftHip.localRotation = Quaternion.Euler(-25f * bounce, 0f, -12f);
                    _rightHip.localRotation = Quaternion.Euler(25f * bounce, 0f, 12f);
                    break;
                }

                case AnimAction.BlitzDash:
                {
                    // 스페이스 2연타 순간 돌진 탈취: 낮은 중심의 폭발적인 전광석화 돌진 자세!
                    float surge = Mathf.Sin(t * Mathf.PI);
                    _bodyRoot.localPosition = new Vector3(0f, baseRootY - 0.22f * surge * _heightScale, 0.22f * surge);
                    _bodyRoot.localRotation = Quaternion.Euler(24f * surge, 0f, 0f);
                    _spine.localRotation = Quaternion.Euler(18f * surge, 0f, 0f);
                    _rightHip.localRotation = Quaternion.Euler(-58f * surge, 0f, 0f);
                    _rightKnee.localRotation = Quaternion.Euler(22f * surge + 8f, 0f, 0f);
                    _leftHip.localRotation = Quaternion.Euler(52f * surge, 0f, 0f);
                    _leftKnee.localRotation = Quaternion.Euler(64f * surge + 8f, 0f, 0f);
                    _leftShoulder.localRotation = Quaternion.Euler(42f * surge, 0f, 22f);
                    _rightShoulder.localRotation = Quaternion.Euler(-48f * surge, 0f, -22f);
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
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
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
