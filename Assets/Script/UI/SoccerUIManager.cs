using System;
using System.Collections.Generic;
using Soccer3D.CameraSystem;
using Soccer3D.Data;
using Soccer3D.Gameplay;
using Soccer3D.Visuals;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Soccer3D.UI
{
    public class SoccerUIManager : MonoBehaviour
    {
        private class StatCardRefs
        {
            public UnityEngine.UI.Text nameText;
            public UnityEngine.UI.Text archetypeText;
            public UnityEngine.UI.Text ovrText;
            public UnityEngine.UI.Image ovrBadgeBg;
            public UnityEngine.UI.Text physicalInfoText;
            public UnityEngine.UI.Text feelSummaryText;
            public UnityEngine.UI.Image[] statBarFills = new UnityEngine.UI.Image[8];
            public UnityEngine.UI.Text[] statValueTexts = new UnityEngine.UI.Text[8];
        }

        private struct LineupEntry
        {
            public PlayerProfile profile;
            public Transform actorTransform;
            public Vector2 pitchNormPos; // 2D 전술판 좌표 (x: -0.78~+0.78 좌우, y: -0.82 GK ~ +0.56 ST)
            public bool isGoalkeeper;
            public string controlKeyLabel;
        }

        private Font _koreanFont;
        private Canvas _mainCanvas;
        private Camera _mainCam;

        // 프리매치 11 vs 11 SBS Sports 중계풍 선발 라인업 1명씩 소개 & 스탯 확인창
        private GameObject _preMatchPanel;
        private UnityEngine.UI.Text _preMatchHeaderTitle;
        private UnityEngine.UI.Text _closeBtnLabel;
        private StatCardRefs _playerCard;
        private StatCardRefs _aiCard;

        // 좌측 방송 라인업 보드 + 중앙 2D 미니 전술판 + 우측 3D 캐릭터 하단 자막(Lower-Third) 레퍼런스
        private UnityEngine.UI.Image _broadcastHeaderBg;
        private UnityEngine.UI.Image _broadcastFlagAccent;
        private UnityEngine.UI.Text _broadcastFlagCodeText;
        private UnityEngine.UI.Text _broadcastTeamTitleText;
        private UnityEngine.UI.Text _switchTeamBtnLabel;
        private UnityEngine.UI.Text _managerNameText;
        private UnityEngine.UI.Text _formationTacticSubText;

        private readonly UnityEngine.UI.Image[] _rosterRowBgs = new UnityEngine.UI.Image[11];
        private readonly UnityEngine.UI.Text[] _rosterRowNumTexts = new UnityEngine.UI.Text[11];
        private readonly UnityEngine.UI.Text[] _rosterRowNameTexts = new UnityEngine.UI.Text[11];
        private readonly UnityEngine.UI.Text[] _rosterRowPosTexts = new UnityEngine.UI.Text[11];

        private readonly RectTransform[] _pitchBadgeRects = new RectTransform[11];
        private readonly UnityEngine.UI.Image[] _pitchBadgeWingBgs = new UnityEngine.UI.Image[11];
        private readonly UnityEngine.UI.Image[] _pitchBadgeInnerBgs = new UnityEngine.UI.Image[11];
        private readonly UnityEngine.UI.Text[] _pitchBadgeNumTexts = new UnityEngine.UI.Text[11];
        private readonly UnityEngine.UI.Text[] _pitchBadgeNameTexts = new UnityEngine.UI.Text[11];

        private UnityEngine.UI.Image _spotlightPosBadgeBg;
        private UnityEngine.UI.Text _spotlightPosNumText;
        private UnityEngine.UI.Text _spotlightStepLabel;
        private UnityEngine.UI.Image _spotlightTimerFill;
        private UnityEngine.UI.Text _autoPlayBtnLabel;

        private readonly List<LineupEntry> _blueLineupEntries = new List<LineupEntry>(11);
        private readonly List<LineupEntry> _redLineupEntries = new List<LineupEntry>(11);
        private FormationPreset _cachedBlueFormation;
        private FormationPreset _cachedRedFormation;
        private string _blueManagerName = "거스 히딩크 (Guus Hiddink)";
        private string _redManagerName = "펩 과르디올라 (Pep Guardiola)";
        private bool _isInspectingBlueTeam = true;
        private int _inspectedLineupIndex;
        private bool _autoAdvanceLineup = true;
        private float _lineupAutoTimer;
        private const float LineupAutoStepDuration = 2.4f;

        // 인게임 HUD
        private GameObject _hudRoot;
        private UnityEngine.UI.Text _scoreLeftName;
        private UnityEngine.UI.Text _scoreRightName;
        private UnityEngine.UI.Text _scoreNumbersText;
        private UnityEngine.UI.Text _timerText;

        // 1인칭 중앙 조준점, 볼 소유(Possession) 이펙트 링/배너, 포켓몬고 스핀 위젯, 슛 게이지, 오버헤드킥 알림 배지 & 회전형 공 방향 화살표 HUD
        private GameObject _fpCrosshairRoot;
        private UnityEngine.UI.Image[] _fpCrosshairParts;
        private UnityEngine.UI.Text _fpAimActionLabel;
        private RectTransform _possessionCrosshairRingRect;
        private UnityEngine.UI.Image _possessionCrosshairRingImg;
        private GameObject _possessionHudBadge;
        private UnityEngine.UI.Image _possessionHudBadgeBg;
        private UnityEngine.UI.Text _possessionHudBadgeText;
        private GameObject _pokeSpinWidgetRoot;
        private RectTransform _pokeSpinRingRect;
        private UnityEngine.UI.Image _pokeSpinRingImg;
        private RectTransform _pokeDragOrbRect;
        private UnityEngine.UI.Image _pokeDragOrbImg;
        private RectTransform _ballRadarRect;
        private RectTransform _ballRadarArrowRect;
        private UnityEngine.UI.Image _ballRadarArrowImg;
        private UnityEngine.UI.Text _ballRadarText;
        private RectTransform _playerTrackerRect;
        private GameObject _shotGaugeContainer;
        private UnityEngine.UI.Image _shotGaugeFill;
        private UnityEngine.UI.Text _shotGaugeLabel;
        private GameObject _acrobaticPromptBadge;
        private UnityEngine.UI.Text _acrobaticPromptText;

        // 좌측 상단 5대5 실시간 전술 미니맵
        private RectTransform _minimapPitchRect;
        private RectTransform _minimapHumanDot;
        private RectTransform _minimapHumanViewArrow;
        private readonly List<RectTransform> _minimapBlueMateDots = new List<RectTransform>();
        private readonly List<RectTransform> _minimapRedDots = new List<RectTransform>();
        private RectTransform _minimapBlueGkDot;
        private RectTransform _minimapRedGkDot;
        private RectTransform _minimapBallHalo;
        private RectTransform _minimapBallDot;
        private const float MinimapPitchWidthPx = 340f;
        private const float MinimapPitchHeightPx = 220f;

        // 직접 프리킥 전용 조준 에임 & 안내 HUD
        private GameObject _freeKickHudRoot;
        private UnityEngine.UI.Text _freeKickBannerText;
        private RectTransform _freeKickReticleRect;
        private UnityEngine.UI.Image[] _freeKickCrosshairParts;
        private GameObject _freeKickGaugeObj;
        private UnityEngine.UI.Image _freeKickGaugeFill;
        private UnityEngine.UI.Text _freeKickModeLabel;

        // 하단 슈팅 임팩트 피드백 토스트
        private CanvasGroup _toastGroup;
        private UnityEngine.UI.Image _toastBg;
        private UnityEngine.UI.Text _toastText;
        private float _toastTimer;

        // 골 세리머니 대형 배너
        private GameObject _goalBannerRoot;
        private RectTransform _goalBannerCard;
        private UnityEngine.UI.Text _goalMainTitle;
        private UnityEngine.UI.Text _goalDetailSubtitle;
        private float _goalBannerAnimTime;

        // 경기 종료 결과 패널
        private GameObject _matchEndPanel;
        private UnityEngine.UI.Text _matchEndTitle;
        private UnityEngine.UI.Text _matchEndScore;

        private Action _onCloseStatScreenCallback;
        private Action _onRerollPlayersCallback;
        private Action _onNextMatchCallback;

        private static readonly string[] StatLabels =
        {
            "속력 (PACE)",
            "민첩성 (AGILITY)",
            "골 결정력·공격 (FINISH)",
            "슛 파워 (POWER)",
            "패스·시야 (PASS)",
            "커브·감아차기 (CURVE)",
            "아크로바틱 (ACRO)",
            "수비·태클 (DEFENSE)"
        };

        public bool IsStatWindowOpen => _preMatchPanel != null && _preMatchPanel.activeSelf;

        public void Initialize(Action onCloseStatsAndStart, Action onRerollPlayers, Action onNextMatch)
        {
            _onCloseStatScreenCallback = onCloseStatsAndStart;
            _onRerollPlayersCallback = onRerollPlayers;
            _onNextMatchCallback = onNextMatch;
            _mainCam = Camera.main;

            EnsureEventSystemWithNewInput();
            LoadKoreanDynamicFont();
            BuildCanvasHierarchy();
        }

        private void EnsureEventSystemWithNewInput()
        {
            EventSystem es = FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                es = esObj.AddComponent<EventSystem>();
                esObj.AddComponent<InputSystemUIInputModule>();
            }
            else if (es.GetComponent<InputSystemUIInputModule>() == null)
            {
                StandaloneInputModule legacy = es.GetComponent<StandaloneInputModule>();
                if (legacy != null) Destroy(legacy);
                es.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        private void LoadKoreanDynamicFont()
        {
            string[] candidates =
            {
                "Malgun Gothic Bold", "Malgun Gothic", "맑은 고딕",
                "Apple SD Gothic Neo", "NanumGothic", "Noto Sans CJK KR", "Arial"
            };
            _koreanFont = Font.CreateDynamicFontFromOSFont(candidates, 28);
            if (_koreanFont == null)
                _koreanFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void BuildCanvasHierarchy()
        {
            GameObject canvasObj = new GameObject("SoccerUI_Canvas");
            canvasObj.transform.SetParent(transform, false);

            _mainCanvas = canvasObj.AddComponent<Canvas>();
            _mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _mainCanvas.sortingOrder = 20;

            UnityEngine.UI.CanvasScaler scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            BuildInGameHud(canvasObj.transform);
            BuildGoalCelebrationBanner(canvasObj.transform);
            BuildPreMatchStatsOverlay(canvasObj.transform);
            BuildMatchEndOverlay(canvasObj.transform);
        }

        private void BuildInGameHud(Transform canvasRoot)
        {
            _hudRoot = CreateUiElement("InGameHUD", canvasRoot);
            StretchFull(_hudRoot.GetComponent<RectTransform>());

            // 1. 상단 중계 스코어보드 (차콜-슬레이트 무채색 둥근 플레이트)
            GameObject topBar = CreatePanel("TopScoreboard", _hudRoot.transform,
                new Color(0.09f, 0.10f, 0.12f, 0.90f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -18f), new Vector2(880f, 74f), PanelShape.Rounded);

            // 왼쪽 블루 팀 캡슐 액센트
            CreatePanel("BlueAccent", topBar.transform, new Color(0.42f, 0.52f, 0.66f, 0.85f),
                new Vector2(0f, 0.16f), new Vector2(0f, 0.84f), new Vector2(0f, 0.5f),
                new Vector2(8f, 0f), new Vector2(6f, 0f), PanelShape.Pill);

            // 오른쪽 레드 팀 캡슐 액센트
            CreatePanel("RedAccent", topBar.transform, new Color(0.66f, 0.42f, 0.45f, 0.85f),
                new Vector2(1f, 0.16f), new Vector2(1f, 0.84f), new Vector2(1f, 0.5f),
                new Vector2(-8f, 0f), new Vector2(6f, 0f), PanelShape.Pill);

            _scoreLeftName = CreateText("PlayerNameScore", topBar.transform, "손흥민 (ME)", 23, FontStyle.Bold,
                new Color(0.88f, 0.91f, 0.95f), TextAnchor.MiddleRight,
                new Vector2(0f, 0f), new Vector2(0.36f, 1f), new Vector2(18f, 0f), new Vector2(-12f, 0f));

            _scoreNumbersText = CreateText("ScoreDigits", topBar.transform, "0  :  0", 36, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.36f, 0.22f), new Vector2(0.64f, 1f), Vector2.zero, Vector2.zero);

            _timerText = CreateText("MatchTimer", topBar.transform, "02:00", 18, FontStyle.Bold,
                new Color(0.82f, 0.85f, 0.90f), TextAnchor.LowerCenter,
                new Vector2(0.36f, 0.04f), new Vector2(0.64f, 0.36f), Vector2.zero, Vector2.zero);

            _scoreRightName = CreateText("AiNameScore", topBar.transform, "호날두 (AI)", 23, FontStyle.Bold,
                new Color(0.85f, 0.88f, 0.92f), TextAnchor.MiddleLeft,
                new Vector2(0.64f, 0f), new Vector2(1f, 1f), new Vector2(12f, 0f), new Vector2(-18f, 0f));

            // 우측 상단 둥근 미니 버튼들 (무채색 다크 티타늄 톤)
            CreateButton("BtnPeekStats", _hudRoot.transform, "📊 선수 스탯 보기 [Tab]", 18,
                new Color(0.16f, 0.18f, 0.22f, 0.92f), new Color(0.90f, 0.92f, 0.96f),
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -20f), new Vector2(230f, 44f),
                () => ShowPreMatchStats(isMidGamePeek: true), PanelShape.Rounded);

            CreateButton("BtnNewRandomMatch", _hudRoot.transform, "🎲 새 판 (랜덤 스탯) [T]", 18,
                new Color(0.20f, 0.22f, 0.26f, 0.92f), new Color(0.92f, 0.94f, 0.98f),
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -72f), new Vector2(230f, 44f),
                () => _onNextMatchCallback?.Invoke(), PanelShape.Rounded);

            // 1-B. 좌측 상단 11대11 실시간 전술 미니맵
            BuildTacticalMinimap(_hudRoot.transform);

            // 2. 좌측 하단 1인칭 조작키 미니 가이드 (무채색 다크 글래스 둥근 패널)
            GameObject keyGuideBox = CreatePanel("ControlsMiniBox", _hudRoot.transform,
                new Color(0.08f, 0.09f, 0.11f, 0.84f),
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(22f, 20f), new Vector2(630f, 305f), PanelShape.Rounded);

            string guideStr =
                "<color=#E2E6ED><b>[FIFA 표준 조작 가이드 (3인칭 중계)]</b></color>\n" +
                "• <b>방향키 (↑↓←→)</b> : 이동·방향  |  <b>E</b> : <color=#C8D0DC><b>달리기</b></color>  |  <b>Shift (홀드)</b> : <color=#C8D0DC><b>치고 달리기(치달)</b></color>\n" +
                "• <b>S</b> : 짧은 패스 (수비시 <b>S/Q</b> 선수 전환)  |  <b>W</b> : 스루패스 (<b>Q+W</b> 로빙스루)\n" +
                "• <b>Space</b> : 태클 (<b>E+Space</b> 슬라이딩) | <b>F</b> : 어깨몸싸움 | <b>V</b> : 시점전환 | <b>1~0</b> : 선수선택\n" +
                "• <b>A</b> : 크로스·로빙패스 (수비시 <b>A</b> 슬라이딩 태클)\n" +
                "• <b>D</b> : <color=#E2E6ED><b>슛 · 헤딩 · 발리</b></color> (수비시 <b>D</b> 스탠딩 태클 | <b>Z+D</b> : 감아차기!)\n" +
                "• <b>Q (단타)</b> : 동료 침투  |  <b>마우스</b> : 좌클릭 슛(드래그 감아차기) · 우클릭 패스\n" +
                "• <b>C / 1~0</b> : 선수 전환  |  <b>Z</b> : 정밀 드리블/감아차기 보조";

            CreateText("ControlsGuideText", keyGuideBox.transform, guideStr, 14, FontStyle.Normal,
                new Color(0.88f, 0.90f, 0.94f), TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.one, new Vector2(16f, 10f), new Vector2(-14f, -10f));

            // 2-B. 1인칭 화면 정중앙 에임 크로스헤어 & 360도 회전형 공 위치 추적 화살표 HUD
            _fpCrosshairRoot = CreateUiElement("FirstPersonCrosshair", _hudRoot.transform);
            RectTransform fpRt = _fpCrosshairRoot.GetComponent<RectTransform>();
            fpRt.anchorMin = new Vector2(0.5f, 0.5f);
            fpRt.anchorMax = new Vector2(0.5f, 0.5f);
            fpRt.sizeDelta = new Vector2(44f, 44f);
            fpRt.anchoredPosition = Vector2.zero;

            Color chCol = new Color(0.92f, 0.94f, 0.98f, 0.85f);
            _fpCrosshairParts = new UnityEngine.UI.Image[5];
            _fpCrosshairParts[0] = CreatePanel("FpDot", _fpCrosshairRoot.transform, chCol,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(5f, 5f)).GetComponent<UnityEngine.UI.Image>();
            _fpCrosshairParts[1] = CreatePanel("FpTop", _fpCrosshairRoot.transform, chCol,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 12f), new Vector2(3f, 9f)).GetComponent<UnityEngine.UI.Image>();
            _fpCrosshairParts[2] = CreatePanel("FpBottom", _fpCrosshairRoot.transform, chCol,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -12f), new Vector2(3f, 9f)).GetComponent<UnityEngine.UI.Image>();
            _fpCrosshairParts[3] = CreatePanel("FpLeft", _fpCrosshairRoot.transform, chCol,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-12f, 0f), new Vector2(9f, 3f)).GetComponent<UnityEngine.UI.Image>();
            _fpCrosshairParts[4] = CreatePanel("FpRight", _fpCrosshairRoot.transform, chCol,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(12f, 0f), new Vector2(9f, 3f)).GetComponent<UnityEngine.UI.Image>();

            // 볼 소유 중일 때 크로스헤어 둘레에 은은하게 점등되는 무채색 실버 링
            GameObject possRingObj = CreatePanel("PossessionCrosshairRing", _fpCrosshairRoot.transform,
                new Color(0.85f, 0.88f, 0.94f, 0.70f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(58f, 58f));
            _possessionCrosshairRingRect = possRingObj.GetComponent<RectTransform>();
            _possessionCrosshairRingImg = possRingObj.GetComponent<UnityEngine.UI.Image>();
            _possessionCrosshairRingImg.sprite = MaterialHelper.GetCircleRingSprite();
            possRingObj.SetActive(false);

            _fpAimActionLabel = CreateText("FpAimActionBadge", _fpCrosshairRoot.transform, "", 16, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-240f, -44f), new Vector2(240f, -14f));

            // 볼 소유 중(DRIBBLING)일 때 화면 하단 중앙에 점등되는 캡슐형 둥근 배너
            _possessionHudBadge = CreatePanel("BallPossessionHudBadge", _hudRoot.transform,
                new Color(0.12f, 0.13f, 0.16f, 0.92f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 96f), new Vector2(610f, 44f), PanelShape.Pill);
            _possessionHudBadgeBg = _possessionHudBadge.GetComponent<UnityEngine.UI.Image>();
            CreatePanel("PossessionTopGlowBar", _possessionHudBadge.transform, new Color(0.82f, 0.85f, 0.90f, 0.80f),
                new Vector2(0.15f, 0.95f), new Vector2(0.85f, 0.95f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 2f), PanelShape.Pill);
            _possessionHudBadgeText = CreateText("PossessionHudLabel", _possessionHudBadge.transform,
                "⚡ [볼 소유 중 — DRIBBLING]  S: 패스  |  W: 스루  |  A: 크로스  |  D: 슛 (Z+D 감아차기)  |  E: 달리기  |  Shift: 치달", 17, FontStyle.Bold,
                new Color(0.92f, 0.94f, 0.97f), TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, new Vector2(10f, 2f), new Vector2(-10f, -2f));
            _possessionHudBadge.SetActive(false);

            // 포켓몬고 스타일 드래그 & 스핀 감아차기 비주얼 가이드 링 + 드래그 볼 오브!
            _pokeSpinWidgetRoot = CreateUiElement("PokeCurveSpinWidget", _hudRoot.transform);
            RectTransform pokeRt = _pokeSpinWidgetRoot.GetComponent<RectTransform>();
            pokeRt.anchorMin = new Vector2(0.5f, 0.5f);
            pokeRt.anchorMax = new Vector2(0.5f, 0.5f);
            pokeRt.sizeDelta = new Vector2(220f, 220f);
            pokeRt.anchoredPosition = Vector2.zero;

            GameObject spinRingObj = CreatePanel("PokeSpinRing", _pokeSpinWidgetRoot.transform,
                new Color(0.85f, 0.88f, 0.94f, 0.40f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(150f, 150f));
            _pokeSpinRingRect = spinRingObj.GetComponent<RectTransform>();
            _pokeSpinRingImg = spinRingObj.GetComponent<UnityEngine.UI.Image>();
            _pokeSpinRingImg.sprite = MaterialHelper.GetCircleRingSprite();

            GameObject dragOrbObj = CreatePanel("PokeDragBallOrb", _pokeSpinWidgetRoot.transform,
                new Color(0.95f, 0.96f, 0.98f, 0.95f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(30f, 30f));
            _pokeDragOrbRect = dragOrbObj.GetComponent<RectTransform>();
            _pokeDragOrbImg = dragOrbObj.GetComponent<UnityEngine.UI.Image>();
            _pokeDragOrbImg.sprite = MaterialHelper.GetCircleSprite();

            _pokeSpinWidgetRoot.SetActive(false);

            // 공 위치 방향 화살표 + 캡슐형 거리 표시기
            GameObject radarRoot = CreateUiElement("BallDirectionPointerHUD", _hudRoot.transform);
            _ballRadarRect = radarRoot.GetComponent<RectTransform>();
            _ballRadarRect.anchorMin = new Vector2(0.5f, 0.5f);
            _ballRadarRect.anchorMax = new Vector2(0.5f, 0.5f);
            _ballRadarRect.sizeDelta = new Vector2(140f, 64f);
            _ballRadarRect.anchoredPosition = Vector2.zero;

            GameObject arrowIconObj = CreatePanel("BallArrowIcon", radarRoot.transform,
                new Color(0.88f, 0.91f, 0.95f, 0.95f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 12f), new Vector2(36f, 36f));
            _ballRadarArrowRect = arrowIconObj.GetComponent<RectTransform>();
            _ballRadarArrowImg = arrowIconObj.GetComponent<UnityEngine.UI.Image>();
            _ballRadarArrowImg.sprite = MaterialHelper.GetArrowSprite();

            GameObject distPill = CreatePanel("BallDistPill", radarRoot.transform,
                new Color(0.10f, 0.11f, 0.13f, 0.90f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -18f), new Vector2(132f, 26f), PanelShape.Pill);
            _ballRadarText = CreateText("BallRadarLabel", distPill.transform, "⚽ 18m [R주시]", 15, FontStyle.Bold,
                new Color(0.88f, 0.91f, 0.95f), TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, new Vector2(4f, 1f), new Vector2(-4f, -1f));
            radarRoot.SetActive(false);

            // 3. 1인칭 중앙 하단 슛 게이지 & 캡슐형 오버헤드킥 찬스 배지
            GameObject trackerObj = CreateUiElement("PlayerWorldTracker", _hudRoot.transform);
            _playerTrackerRect = trackerObj.GetComponent<RectTransform>();
            _playerTrackerRect.anchorMin = new Vector2(0.5f, 0.5f);
            _playerTrackerRect.anchorMax = new Vector2(0.5f, 0.5f);
            _playerTrackerRect.sizeDelta = new Vector2(280f, 210f);
            _playerTrackerRect.anchoredPosition = Vector2.zero;

            _shotGaugeContainer = CreatePanel("ShotGaugeBg", trackerObj.transform,
                new Color(0.08f, 0.09f, 0.11f, 0.92f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 8f), new Vector2(210f, 26f), PanelShape.Pill);

            GameObject fillObj = CreatePanel("ShotGaugeFill", _shotGaugeContainer.transform,
                new Color(0.82f, 0.86f, 0.92f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f),
                new Vector2(3f, 3f), new Vector2(-6f, -6f), PanelShape.Pill);
            _shotGaugeFill = fillObj.GetComponent<UnityEngine.UI.Image>();
            _shotGaugeFill.type = UnityEngine.UI.Image.Type.Filled;
            _shotGaugeFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            _shotGaugeFill.fillOrigin = 0;

            _shotGaugeLabel = CreateText("GaugeTypeText", _shotGaugeContainer.transform, "POWER SHOT", 14, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _shotGaugeContainer.SetActive(false);

            _acrobaticPromptBadge = CreatePanel("AcrobaticChanceBadge", trackerObj.transform,
                new Color(0.18f, 0.19f, 0.22f, 0.95f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -6f), new Vector2(276f, 44f), PanelShape.Pill);

            _acrobaticPromptText = CreateText("AcrobaticPromptLabel", _acrobaticPromptBadge.transform,
                "⚡ 오버헤드킥 찬스! [D]", 20, FontStyle.Bold,
                new Color(0.92f, 0.94f, 0.97f), TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _acrobaticPromptBadge.SetActive(false);

            // 4. 직접 프리킥 전용 조준선 & 상단 가이드 바
            _freeKickHudRoot = CreateUiElement("FreeKickHUD", _hudRoot.transform);
            StretchFull(_freeKickHudRoot.GetComponent<RectTransform>());

            GameObject fkTopInfo = CreatePanel("FreeKickInstructionBar", _freeKickHudRoot.transform,
                new Color(0.10f, 0.11f, 0.13f, 0.94f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -104f), new Vector2(980f, 50f), PanelShape.Rounded);
            _freeKickBannerText = CreateText("FreeKickInstructionText", fkTopInfo.transform, "", 20, FontStyle.Bold,
                new Color(0.92f, 0.94f, 0.98f), TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, new Vector2(14f, 2f), new Vector2(-14f, -2f));

            GameObject reticleRoot = CreateUiElement("FreeKickAimReticle", _freeKickHudRoot.transform);
            _freeKickReticleRect = reticleRoot.GetComponent<RectTransform>();
            _freeKickReticleRect.sizeDelta = new Vector2(90f, 90f);

            Color fkCrosshairCol = new Color(0.88f, 0.91f, 0.95f, 0.95f);
            _freeKickCrosshairParts = new UnityEngine.UI.Image[5];
            _freeKickCrosshairParts[0] = CreatePanel("CenterDot", reticleRoot.transform, fkCrosshairCol,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(10f, 10f)).GetComponent<UnityEngine.UI.Image>();
            _freeKickCrosshairParts[1] = CreatePanel("TickTop", reticleRoot.transform, fkCrosshairCol,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 24f), new Vector2(5f, 20f)).GetComponent<UnityEngine.UI.Image>();
            _freeKickCrosshairParts[2] = CreatePanel("TickBottom", reticleRoot.transform, fkCrosshairCol,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -24f), new Vector2(5f, 20f)).GetComponent<UnityEngine.UI.Image>();
            _freeKickCrosshairParts[3] = CreatePanel("TickLeft", reticleRoot.transform, fkCrosshairCol,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-24f, 0f), new Vector2(20f, 5f)).GetComponent<UnityEngine.UI.Image>();
            _freeKickCrosshairParts[4] = CreatePanel("TickRight", reticleRoot.transform, fkCrosshairCol,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(24f, 0f), new Vector2(20f, 5f)).GetComponent<UnityEngine.UI.Image>();

            _freeKickModeLabel = CreateText("FkModeBadge", reticleRoot.transform, "🎯 파워 프리킥 [D]", 16, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-115f, 38f), new Vector2(115f, 66f));

            _freeKickGaugeObj = CreatePanel("FkChargeBarBg", reticleRoot.transform,
                new Color(0.08f, 0.09f, 0.11f, 0.94f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -52f), new Vector2(176f, 22f), PanelShape.Pill);
            GameObject fkFillObj = CreatePanel("FkChargeFill", _freeKickGaugeObj.transform,
                new Color(0.80f, 0.84f, 0.90f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f),
                new Vector2(3f, 3f), new Vector2(-6f, -6f), PanelShape.Pill);
            _freeKickGaugeFill = fkFillObj.GetComponent<UnityEngine.UI.Image>();
            _freeKickGaugeFill.type = UnityEngine.UI.Image.Type.Filled;
            _freeKickGaugeFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            _freeKickGaugeFill.fillOrigin = 0;

            _freeKickHudRoot.SetActive(false);

            // 5. 하단 중앙 슈팅 임팩트 & 약발 판정 실시간 피드백 토스트 (둥근 슬레이트 패널)
            GameObject toastObj = CreatePanel("ShotFeedbackToast", _hudRoot.transform,
                new Color(0.12f, 0.13f, 0.16f, 0.95f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 34f), new Vector2(820f, 54f), PanelShape.Rounded);
            _toastBg = toastObj.GetComponent<UnityEngine.UI.Image>();
            _toastGroup = toastObj.AddComponent<CanvasGroup>();
            _toastGroup.alpha = 0f;
            _toastGroup.blocksRaycasts = false;

            _toastText = CreateText("ToastMessage", toastObj.transform, "", 21, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(16f, 4f), new Vector2(-16f, -4f));
        }

        private void BuildTacticalMinimap(Transform hudParent)
        {
            GameObject mapFrame = CreatePanel("TacticalMinimapFrame", hudParent,
                new Color(0.09f, 0.10f, 0.12f, 0.92f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(22f, -18f), new Vector2(364f, 264f), PanelShape.Rounded);

            CreateText("MinimapHeader", mapFrame.transform,
                "🗺️ <b>11 vs 11 미니맵</b>  <size=13><color=#C8D4E4>●나</color> <color=#7088A8>●우리팀</color> <color=#A87078>●상대</color> <color=#EAECEE>●공</color></size>",
                15, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -30f), new Vector2(-10f, -4f));

            // 105m : 68m 실제 경기장 비율(340 x 220) 다크 택티컬 잔디 구장 캔버스
            GameObject pitchBg = CreatePanel("MinimapPitch", mapFrame.transform,
                new Color(0.12f, 0.14f, 0.13f, 0.95f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 10f), new Vector2(MinimapPitchWidthPx, MinimapPitchHeightPx), PanelShape.Rounded);
            _minimapPitchRect = pitchBg.GetComponent<RectTransform>();

            Color lineCol = new Color(0.68f, 0.72f, 0.70f, 0.35f);

            // 외곽 터치라인 & 골라인 테두리
            CreatePanel("LineTop", pitchBg.transform, lineCol, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 2f));
            CreatePanel("LineBottom", pitchBg.transform, lineCol, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 2f));
            CreatePanel("LineLeft", pitchBg.transform, lineCol, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(2f, 0f));
            CreatePanel("LineRight", pitchBg.transform, lineCol, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(2f, 0f));

            // 하프라인 & 센터서클 (9.15m 반경 -> 직경 약 59px)
            CreatePanel("HalfwayLine", pitchBg.transform, lineCol,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 0f));
            GameObject centerRing = CreatePanel("CenterCircle", pitchBg.transform, lineCol,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f));
            centerRing.GetComponent<UnityEngine.UI.Image>().sprite = MaterialHelper.GetCircleRingSprite();

            // 좌·우 페널티박스 (16.5m x 40.32m -> 53px x 130px)
            float penW = (16.5f / 105f) * MinimapPitchWidthPx;
            float penH = (40.32f / 68f) * MinimapPitchHeightPx;
            CreateMinimapBoxOutline("LeftPenaltyBox", pitchBg.transform, new Vector2(-MinimapPitchWidthPx * 0.5f + penW * 0.5f, 0f), new Vector2(penW, penH), lineCol);
            CreateMinimapBoxOutline("RightPenaltyBox", pitchBg.transform, new Vector2(MinimapPitchWidthPx * 0.5f - penW * 0.5f, 0f), new Vector2(penW, penH), lineCol);

            // 좌측(블루 수비 골문) & 우측(레드 수비 골문) 표시 바
            CreatePanel("BlueGoalBar", pitchBg.transform, new Color(0.40f, 0.52f, 0.66f, 0.95f),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(5f, 28f), PanelShape.Pill);
            CreatePanel("RedGoalBar", pitchBg.transform, new Color(0.66f, 0.40f, 0.44f, 0.95f),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(5f, 28f), PanelShape.Pill);

            // 골키퍼 마커 2개
            _minimapBlueGkDot = CreateMinimapDot("BlueGKDot", pitchBg.transform, new Color(0.48f, 0.62f, 0.56f, 1f), 11f);
            _minimapRedGkDot = CreateMinimapDot("RedGKDot", pitchBg.transform, new Color(0.66f, 0.52f, 0.45f, 1f), 11f);

            // 블루팀 동료 필드 선수 9명 마커 (+ 내 캐릭터 1명 + 골키퍼 1명 = 11명)
            _minimapBlueMateDots.Clear();
            for (int i = 0; i < 9; i++)
            {
                _minimapBlueMateDots.Add(CreateMinimapDot("BlueMateDot_" + i, pitchBg.transform, new Color(0.42f, 0.54f, 0.68f, 0.95f), 11f));
            }

            // 레드팀 필드 선수 10명 마커 (+ 골키퍼 1명 = 11명)
            _minimapRedDots.Clear();
            for (int i = 0; i < 10; i++)
            {
                float sz = i == 0 ? 12.5f : 11f;
                _minimapRedDots.Add(CreateMinimapDot("RedPlayerDot_" + i, pitchBg.transform, new Color(0.68f, 0.42f, 0.45f, 0.95f), sz));
            }

            // 내 캐릭터(1P) 마커 + 1인칭 바라보는 방향 화살표!
            _minimapHumanDot = CreateMinimapDot("HumanPlayerDot", pitchBg.transform, new Color(0.85f, 0.92f, 0.98f, 1f), 15f);
            GameObject viewArrowObj = CreatePanel("HumanViewArrow", _minimapHumanDot,
                new Color(0.85f, 0.92f, 0.98f, 0.95f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.15f),
                Vector2.zero, new Vector2(15f, 19f));
            _minimapHumanViewArrow = viewArrowObj.GetComponent<RectTransform>();
            viewArrowObj.GetComponent<UnityEngine.UI.Image>().sprite = MaterialHelper.GetArrowSprite();

            // 최상단 레이어: 공 위치 하이라이트 후광 + 클린 실버-화이트 코어 마커
            _minimapBallHalo = CreateMinimapDot("BallHaloDot", pitchBg.transform, new Color(0.88f, 0.90f, 0.95f, 0.40f), 24f);
            _minimapBallDot = CreateMinimapDot("BallCoreDot", pitchBg.transform, Color.white, 13f);
        }

        private static void CreateMinimapBoxOutline(string name, Transform parent, Vector2 centerPos, Vector2 size, Color lineCol)
        {
            GameObject box = CreateUiElement(name, parent);
            RectTransform rt = box.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = centerPos;
            rt.sizeDelta = size;

            CreatePanel("Top", box.transform, lineCol, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 1.8f));
            CreatePanel("Bot", box.transform, lineCol, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 1.8f));
            CreatePanel("Left", box.transform, lineCol, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(1.8f, 0f));
            CreatePanel("Right", box.transform, lineCol, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(1.8f, 0f));
        }

        private static RectTransform CreateMinimapDot(string name, Transform parent, Color color, float diameter)
        {
            GameObject dot = CreatePanel(name, parent, color,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(diameter, diameter));
            UnityEngine.UI.Image img = dot.GetComponent<UnityEngine.UI.Image>();
            img.sprite = MaterialHelper.GetCircleSprite();
            return dot.GetComponent<RectTransform>();
        }

        private static Vector2 WorldToMinimapPos(Vector3 worldPos)
        {
            float nx = Mathf.Clamp(worldPos.x / ShotCalculator.PitchHalfLength, -1.03f, 1.03f);
            float nz = Mathf.Clamp(worldPos.z / ShotCalculator.PitchHalfWidth, -1.03f, 1.03f);
            return new Vector2(
                nx * (MinimapPitchWidthPx * 0.5f),
                nz * (MinimapPitchHeightPx * 0.5f));
        }

        public void UpdateTacticalMinimap(
            SoccerPlayerController humanPlayer,
            IReadOnlyList<SoccerPlayerController> blueOutfield,
            SoccerPlayerController redStriker,
            IReadOnlyList<SoccerPlayerController> redDefenders,
            GoalkeeperAI blueGk,
            GoalkeeperAI redGk,
            SoccerBall ball)
        {
            if (_minimapPitchRect == null || !_hudRoot.activeSelf)
                return;

            if (humanPlayer != null && _minimapHumanDot != null)
            {
                _minimapHumanDot.anchoredPosition = WorldToMinimapPos(humanPlayer.transform.position);
                if (_minimapHumanViewArrow != null)
                    _minimapHumanViewArrow.localRotation = Quaternion.Euler(0f, 0f, -humanPlayer.FirstPersonYaw);
            }

            if (blueOutfield != null)
            {
                int mateDotIdx = 0;
                for (int i = 0; i < blueOutfield.Count && mateDotIdx < _minimapBlueMateDots.Count; i++)
                {
                    SoccerPlayerController bp = blueOutfield[i];
                    if (bp == null || bp == humanPlayer)
                        continue;

                    RectTransform dot = _minimapBlueMateDots[mateDotIdx];
                    if (!dot.gameObject.activeSelf)
                        dot.gameObject.SetActive(true);
                    dot.anchoredPosition = WorldToMinimapPos(bp.transform.position);
                    mateDotIdx++;
                }

                for (int j = mateDotIdx; j < _minimapBlueMateDots.Count; j++)
                {
                    if (_minimapBlueMateDots[j].gameObject.activeSelf)
                        _minimapBlueMateDots[j].gameObject.SetActive(false);
                }
            }

            if (_minimapRedDots.Count >= 1)
            {
                if (redStriker != null)
                {
                    _minimapRedDots[0].anchoredPosition = WorldToMinimapPos(redStriker.transform.position);
                }
                if (redDefenders != null)
                {
                    int maxRedMates = _minimapRedDots.Count - 1;
                    for (int i = 0; i < maxRedMates; i++)
                    {
                        bool active = i < redDefenders.Count && redDefenders[i] != null;
                        if (_minimapRedDots[i + 1].gameObject.activeSelf != active)
                            _minimapRedDots[i + 1].gameObject.SetActive(active);
                        if (active)
                            _minimapRedDots[i + 1].anchoredPosition = WorldToMinimapPos(redDefenders[i].transform.position);
                    }
                }
            }

            if (blueGk != null && _minimapBlueGkDot != null)
                _minimapBlueGkDot.anchoredPosition = WorldToMinimapPos(blueGk.transform.position);
            if (redGk != null && _minimapRedGkDot != null)
                _minimapRedGkDot.anchoredPosition = WorldToMinimapPos(redGk.transform.position);

            if (ball != null && _minimapBallDot != null)
            {
                Vector2 bp = WorldToMinimapPos(ball.transform.position);
                _minimapBallDot.anchoredPosition = bp;
                if (_minimapBallHalo != null)
                {
                    _minimapBallHalo.anchoredPosition = bp;
                    float pulse = 1.0f + Mathf.Sin(Time.unscaledTime * 9.0f) * 0.22f;
                    _minimapBallHalo.localScale = new Vector3(pulse, pulse, 1f);
                }
            }
        }

        private void BuildGoalCelebrationBanner(Transform canvasRoot)
        {
            _goalBannerRoot = CreateUiElement("GoalCelebrationBanner", canvasRoot);
            StretchFull(_goalBannerRoot.GetComponent<RectTransform>());

            GameObject card = CreatePanel("BannerCard", _goalBannerRoot.transform,
                new Color(0.08f, 0.09f, 0.11f, 0.96f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 110f), new Vector2(1060f, 195f), PanelShape.Rounded);
            _goalBannerCard = card.GetComponent<RectTransform>();

            // 은은한 플래티넘-실버 상하 캡슐 라인 장식
            CreatePanel("TopTrim", card.transform, new Color(0.75f, 0.78f, 0.84f, 0.75f),
                new Vector2(0.05f, 1f), new Vector2(0.95f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 4f), PanelShape.Pill);
            CreatePanel("BottomTrim", card.transform, new Color(0.75f, 0.78f, 0.84f, 0.75f),
                new Vector2(0.05f, 0f), new Vector2(0.95f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 4f), PanelShape.Pill);

            _goalMainTitle = CreateText("GoalMainTitle", card.transform, "⚽ GOOOAAAL!!", 68, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.38f), new Vector2(1f, 0.96f), Vector2.zero, Vector2.zero);

            _goalDetailSubtitle = CreateText("GoalDetailSub", card.transform, "", 26, FontStyle.Bold,
                new Color(0.85f, 0.88f, 0.92f), TextAnchor.MiddleCenter,
                new Vector2(0f, 0.06f), new Vector2(1f, 0.42f), new Vector2(20f, 0f), new Vector2(-20f, 0f));

            _goalBannerRoot.SetActive(false);
        }

        private void BuildPreMatchStatsOverlay(Transform canvasRoot)
        {
            // 배경을 투명하게 열어 우측에 서 있는 실제 3D 선수 캐릭터와 야간 경기장 조명이 그대로 보이도록 구성!
            _preMatchPanel = CreatePanel("PreMatchStatsOverlay", canvasRoot,
                new Color(0.03f, 0.03f, 0.04f, 0.25f),
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _preMatchPanel.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;

            // 상단 얇은 브로드캐스트 안내 바 (둥근 다크 차콜 플레이트)
            GameObject topBar = CreatePanel("BroadcastTopStrip", _preMatchPanel.transform,
                new Color(0.08f, 0.09f, 0.11f, 0.90f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 54f), PanelShape.Rounded);

            _preMatchHeaderTitle = CreateText("HeaderTitle", topBar.transform,
                "📺 STARTING XI BROADCAST — 선발 11인(GK → DF → MF → FW) 1명씩 차례대로 소개 중", 21, FontStyle.Bold,
                new Color(0.92f, 0.94f, 0.98f), TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(0.75f, 1f), new Vector2(28f, 0f), Vector2.zero);

            // 우측 상단 방송 워터마크 로고
            GameObject liveLogoRoot = CreateUiElement("BroadcastLiveWatermark", _preMatchPanel.transform);
            RectTransform liveLogoRt = liveLogoRoot.GetComponent<RectTransform>();
            liveLogoRt.anchorMin = new Vector2(1f, 1f);
            liveLogoRt.anchorMax = new Vector2(1f, 1f);
            liveLogoRt.pivot = new Vector2(1f, 1f);
            liveLogoRt.anchoredPosition = new Vector2(-34f, -66f);
            liveLogoRt.sizeDelta = new Vector2(260f, 64f);

            CreateText("BroadcastChannelTitle", liveLogoRoot.transform, "<i>SOCCER 3D</i> <b>Sports</b>", 26, FontStyle.Bold,
                Color.white, TextAnchor.UpperRight,
                new Vector2(0f, 0.35f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            GameObject livePill = CreatePanel("LivePill", liveLogoRoot.transform,
                new Color(0.22f, 0.24f, 0.28f, 0.96f),
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 2f), new Vector2(76f, 24f), PanelShape.Pill);
            CreateText("LivePillText", livePill.transform, "LIVE", 15, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // =========================================================================
            // 1) 좌측~중앙: 선발 라인업 프레임 (11인 명단 + 2D 미니 전술판, 둥근 모서리 & 무채색)
            // =========================================================================
            GameObject broadcastFrame = CreatePanel("BroadcastLineupFrame", _preMatchPanel.transform,
                new Color(0.09f, 0.10f, 0.12f, 0.88f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-385f, 22f), new Vector2(1010f, 770f), PanelShape.Rounded);

            Color slateBorderCol = new Color(0.28f, 0.31f, 0.36f, 0.50f);

            // 상단 국가/팀 헤더 바
            GameObject teamHeader = CreatePanel("BroadcastTeamHeader", broadcastFrame.transform,
                new Color(0.13f, 0.14f, 0.17f, 0.98f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 68f), PanelShape.Rounded);
            _broadcastHeaderBg = teamHeader.GetComponent<UnityEngine.UI.Image>();

            GameObject flagBox = CreatePanel("TeamFlagBox", teamHeader.transform,
                new Color(0.20f, 0.22f, 0.26f, 1f),
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(114f, 0f), PanelShape.Pill);

            GameObject flagCircle = CreatePanel("TaegeukCircle", flagBox.transform,
                new Color(0.38f, 0.42f, 0.48f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 8f), new Vector2(30f, 30f), PanelShape.Pill);
            _broadcastFlagAccent = flagCircle.GetComponent<UnityEngine.UI.Image>();

            _broadcastFlagCodeText = CreateText("FlagCodeText", flagBox.transform, "KOR · 1P", 14, FontStyle.Bold,
                new Color(0.88f, 0.91f, 0.95f), TextAnchor.LowerCenter,
                Vector2.zero, Vector2.one, new Vector2(0f, 4f), Vector2.zero);

            _broadcastTeamTitleText = CreateText("TeamTitleText", teamHeader.transform,
                "대한민국 (BLUE FC)   |   4-3-3 어택", 28, FontStyle.Bold,
                Color.white, TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(0.74f, 1f), new Vector2(132f, 0f), Vector2.zero);

            GameObject switchTeamBtn = CreateButton("BtnSwitchInspectTeam", teamHeader.transform,
                "🔄 상대팀 라인업 보기 [Tab]", 16,
                new Color(0.20f, 0.22f, 0.26f, 0.96f), new Color(0.90f, 0.92f, 0.96f),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-14f, 0f), new Vector2(236f, 46f),
                ToggleInspectedTeam, PanelShape.Rounded);
            _switchTeamBtnLabel = switchTeamBtn.GetComponentInChildren<UnityEngine.UI.Text>();

            // 좌측 컬럼: 선발 11인 명단(GK 1번 ~ 공격수 11번) + 하단 감독 박스
            GameObject leftRosterCol = CreatePanel("LeftRosterColumn", broadcastFrame.transform,
                new Color(0.10f, 0.11f, 0.13f, 0.94f),
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(396f, -68f), PanelShape.Rounded);

            // 11명 선수 행 (위에서부터 GK -> DF -> MF -> FW 순서로 11줄, 개별 둥근 모서리 행)
            for (int i = 0; i < 11; i++)
            {
                int capturedIdx = i;
                float rowY = -10f - i * 49f;
                Color baseRowCol = (i % 2 == 0)
                    ? new Color(0.13f, 0.14f, 0.16f, 0.92f)
                    : new Color(0.16f, 0.17f, 0.19f, 0.92f);

                GameObject rowObj = CreatePanel("RosterRow_" + i, leftRosterCol.transform, baseRowCol,
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, rowY), new Vector2(-8f, 45f), PanelShape.Rounded);

                UnityEngine.UI.Image rowImg = rowObj.GetComponent<UnityEngine.UI.Image>();
                rowImg.raycastTarget = true;
                _rosterRowBgs[i] = rowImg;

                UnityEngine.UI.Button rowBtn = rowObj.AddComponent<UnityEngine.UI.Button>();
                rowBtn.targetGraphic = rowImg;
                rowBtn.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
                rowBtn.onClick.AddListener(() => SelectLineupPlayerIndex(capturedIdx, manualClick: true));

                // 행 하단 얇은 구분선
                CreatePanel("RowDivider", rowObj.transform, new Color(0.28f, 0.31f, 0.36f, 0.25f),
                    new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                    Vector2.zero, new Vector2(0f, 1.5f));

                _rosterRowNumTexts[i] = CreateText("RowJerseyNum", rowObj.transform,
                    i == 0 ? "🧤 1" : (i + 1).ToString(), 19, FontStyle.Bold,
                    new Color(0.85f, 0.88f, 0.92f), TextAnchor.MiddleCenter,
                    new Vector2(0f, 0f), new Vector2(0.20f, 1f), new Vector2(4f, 0f), Vector2.zero);

                _rosterRowNameTexts[i] = CreateText("RowPlayerName", rowObj.transform,
                    "손흥민 (Son)", 19, FontStyle.Bold,
                    Color.white, TextAnchor.MiddleLeft,
                    new Vector2(0.20f, 0f), new Vector2(0.76f, 1f), new Vector2(6f, 0f), Vector2.zero);

                _rosterRowPosTexts[i] = CreateText("RowPosCode", rowObj.transform,
                    i == 0 ? "GK" : "ST", 16, FontStyle.Bold,
                    new Color(0.70f, 0.74f, 0.80f), TextAnchor.MiddleRight,
                    new Vector2(0.76f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-12f, 0f));
            }

            // 좌측 하단 감독(MANAGER) & 포메이션 전술 박스
            GameObject managerBox = CreatePanel("ManagerFooterBox", leftRosterCol.transform,
                new Color(0.12f, 0.13f, 0.15f, 0.96f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, 146f), PanelShape.Rounded);

            CreateText("ManagerTitleLabel", managerBox.transform, "감독 (MANAGER)", 17, FontStyle.Bold,
                new Color(0.70f, 0.74f, 0.80f), TextAnchor.MiddleCenter,
                new Vector2(0f, 0.68f), new Vector2(1f, 0.95f), Vector2.zero, Vector2.zero);

            CreatePanel("ManagerDividerLeft", managerBox.transform, new Color(0.35f, 0.38f, 0.44f, 0.50f),
                new Vector2(0.18f, 0.65f), new Vector2(0.46f, 0.65f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(0f, 2f));
            CreatePanel("ManagerDividerDot", managerBox.transform, new Color(0.68f, 0.72f, 0.78f, 0.90f),
                new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(7f, 7f), PanelShape.Pill);
            CreatePanel("ManagerDividerRight", managerBox.transform, new Color(0.35f, 0.38f, 0.44f, 0.50f),
                new Vector2(0.54f, 0.65f), new Vector2(0.82f, 0.65f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(0f, 2f));

            _managerNameText = CreateText("ManagerNameValue", managerBox.transform,
                "거스 히딩크 (Guus Hiddink)", 21, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.34f), new Vector2(1f, 0.64f), Vector2.zero, Vector2.zero);

            _formationTacticSubText = CreateText("FormationTacticSub", managerBox.transform,
                "🎲 랜덤 포메이션: 4-3-3 어택", 15, FontStyle.Bold,
                new Color(0.82f, 0.85f, 0.90f), TextAnchor.MiddleCenter,
                new Vector2(0f, 0.05f), new Vector2(1f, 0.34f), new Vector2(8f, 0f), new Vector2(-8f, 0f));

            // 중앙 2D 미니 축구장 전술판 영역
            GameObject pitchCol = CreatePanel("CenterPitchColumn", broadcastFrame.transform,
                new Color(0.01f, 0.02f, 0.03f, 0.28f),
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(198f, -34f), new Vector2(-396f, -68f), PanelShape.Rounded);

            BuildBroadcast2DPitchGraphic(pitchCol.transform);

            // 차분한 슬레이트-메탈릭 프레임 테두리 라인
            CreatePanel("SlateBorderLeft", broadcastFrame.transform, slateBorderCol,
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(2f, 0f));
            CreatePanel("SlateBorderMid", broadcastFrame.transform, slateBorderCol,
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0.5f, 0f),
                new Vector2(396f, 0f), new Vector2(2f, -68f));
            CreatePanel("SlateBorderRight", broadcastFrame.transform, slateBorderCol,
                new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                Vector2.zero, new Vector2(2f, 0f));
            CreatePanel("SlateBorderBottom", broadcastFrame.transform, slateBorderCol,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                Vector2.zero, new Vector2(0f, 2f));

            // =========================================================================
            // 2) 우측 하단: 3D 선수 캐릭터 바로 밑에 뜨는 방송 자막(Lower-Third) 이름표 & 포지션 특화 스탯 카드!
            // =========================================================================
            _playerCard = BuildSpotlightLowerThirdCard(_preMatchPanel.transform, new Vector2(455f, -210f));
            _aiCard = _playerCard;

            // =========================================================================
            // 3) 최하단 컨트롤 바 (이전/다음 선수 수동 넘김, 일시정지, 다시 뽑기, 경기 시작)
            // =========================================================================
            GameObject bottomBar = CreatePanel("BroadcastBottomControlBar", _preMatchPanel.transform,
                new Color(0.08f, 0.09f, 0.11f, 0.94f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                Vector2.zero, new Vector2(0f, 72f), PanelShape.Rounded);

            CreateButton("BtnPrevLineupPlayer", bottomBar.transform, "◀ 이전 선수 (A / ←)", 18,
                new Color(0.18f, 0.20f, 0.24f, 0.95f), new Color(0.90f, 0.92f, 0.96f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-710f, 0f), new Vector2(220f, 48f),
                () => StepLineupPlayer(-1, manualClick: true), PanelShape.Rounded);

            GameObject autoBtn = CreateButton("BtnToggleAutoIntro", bottomBar.transform, "⏸ 1명씩 자동소개 일시정지", 17,
                new Color(0.20f, 0.22f, 0.26f, 0.95f), new Color(0.90f, 0.92f, 0.96f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-465f, 0f), new Vector2(240f, 48f),
                ToggleAutoAdvanceLineup, PanelShape.Rounded);
            _autoPlayBtnLabel = autoBtn.GetComponentInChildren<UnityEngine.UI.Text>();

            CreateButton("BtnNextLineupPlayer", bottomBar.transform, "다음 선수 (D / →) ▶", 18,
                new Color(0.18f, 0.20f, 0.24f, 0.95f), new Color(0.90f, 0.92f, 0.96f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-220f, 0f), new Vector2(220f, 48f),
                () => StepLineupPlayer(1, manualClick: true), PanelShape.Rounded);

            CreateButton("BtnRerollRandom", bottomBar.transform, "🎲 포메이션 & 22인 다시 뽑기", 19,
                new Color(0.22f, 0.24f, 0.28f, 0.95f), new Color(0.92f, 0.94f, 0.98f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(115f, 0f), new Vector2(310f, 50f),
                () => _onRerollPlayersCallback?.Invoke(), PanelShape.Rounded);

            GameObject mainStartBtn = CreateButton("BtnExitAndStartMatch", bottomBar.transform,
                "⚽ 라인업 확인 완료 — 경기 시작! (Space / Enter)", 21,
                new Color(0.25f, 0.28f, 0.33f, 1f), Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(560f, 0f), new Vector2(520f, 52f),
                HandleCloseStatWindowClicked, PanelShape.Rounded);
            _closeBtnLabel = mainStartBtn.GetComponentInChildren<UnityEngine.UI.Text>();
        }

        private void BuildBroadcast2DPitchGraphic(Transform parent)
        {
            // 중앙 2D 미니 축구장 그래픽 (다크 택티컬 슬레이트 톤 + 둥근 프레임)
            GameObject pitchRoot = CreatePanel("TacticalPitch2D", parent,
                new Color(0.12f, 0.14f, 0.13f, 0.95f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 12f), new Vector2(390f, 530f), PanelShape.Rounded);

            // 잔디 줄무늬 디테일 (은은한 저채도 톤)
            for (int s = 0; s < 5; s++)
            {
                float stripeY = -210f + s * 105f;
                CreatePanel("TurfStripe_" + s, pitchRoot.transform,
                    new Color(0.14f, 0.16f, 0.15f, 0.45f),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, stripeY), new Vector2(384f, 52f));
            }

            Color lineCol = new Color(0.68f, 0.72f, 0.70f, 0.35f);

            // 외곽 터치라인 & 골라인
            CreatePanel("PitchBorderTop", pitchRoot.transform, lineCol,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 2.5f));
            CreatePanel("PitchBorderBottom", pitchRoot.transform, lineCol,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 2.5f));
            CreatePanel("PitchBorderLeft", pitchRoot.transform, lineCol,
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(2.5f, 0f));
            CreatePanel("PitchBorderRight", pitchRoot.transform, lineCol,
                new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(2.5f, 0f));

            // 하프라인 & 센터서클
            CreatePanel("HalfwayLine", pitchRoot.transform, lineCol,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 2f));

            GameObject centerCircleOuter = CreatePanel("CenterCircleOuter", pitchRoot.transform, lineCol,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96f, 96f));
            CreatePanel("CenterCircleInner", centerCircleOuter.transform, new Color(0.12f, 0.14f, 0.13f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92f, 92f));
            CreatePanel("HalfwayLineThroughCircle", centerCircleOuter.transform, lineCol,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 2f));

            // 상단(공격 방향) 페널티 박스 & 골 에어리어
            CreateHollowBox("TopPenaltyBox", pitchRoot.transform, lineCol, new Vector2(0f, 217f), new Vector2(210f, 92f), 2f);
            CreateHollowBox("TopGoalBox", pitchRoot.transform, lineCol, new Vector2(0f, 244f), new Vector2(106f, 38f), 2f);

            // 하단(GK 진영) 페널티 박스 & 골 에어리어
            CreateHollowBox("BottomPenaltyBox", pitchRoot.transform, lineCol, new Vector2(0f, -217f), new Vector2(210f, 92f), 2f);
            CreateHollowBox("BottomGoalBox", pitchRoot.transform, lineCol, new Vector2(0f, -244f), new Vector2(106f, 38f), 2f);

            // 11명 포메이션 등번호 배지 (모두 둥근 캡슐형 뱃지 디자인)
            for (int i = 0; i < 11; i++)
            {
                int capturedIdx = i;
                GameObject badgeRoot = CreateUiElement("PitchBadge_" + i, pitchRoot.transform);
                RectTransform rt = badgeRoot.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, -210f + i * 38f);
                rt.sizeDelta = new Vector2(64f, 36f);
                _pitchBadgeRects[i] = rt;

                // 둥근 캡슐 외곽 프레임
                GameObject wingBg = CreatePanel("WingBg", badgeRoot.transform,
                    new Color(0.20f, 0.22f, 0.26f, 0.95f),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(62f, 32f), PanelShape.Pill);
                _pitchBadgeWingBgs[i] = wingBg.GetComponent<UnityEngine.UI.Image>();

                // 둥근 캡슐 내부 등번호 박스
                GameObject innerBg = CreatePanel("InnerNumBox", badgeRoot.transform,
                    new Color(0.14f, 0.15f, 0.18f, 0.98f),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(44f, 32f), PanelShape.Pill);
                UnityEngine.UI.Image innerImg = innerBg.GetComponent<UnityEngine.UI.Image>();
                innerImg.raycastTarget = true;
                _pitchBadgeInnerBgs[i] = innerImg;

                UnityEngine.UI.Button bBtn = innerBg.AddComponent<UnityEngine.UI.Button>();
                bBtn.targetGraphic = innerImg;
                bBtn.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
                bBtn.onClick.AddListener(() => SelectLineupPlayerIndex(capturedIdx, manualClick: true));

                _pitchBadgeNumTexts[i] = CreateText("BadgeNumText", innerBg.transform,
                    (i + 1).ToString(), 18, FontStyle.Bold,
                    Color.white, TextAnchor.MiddleCenter,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                _pitchBadgeNameTexts[i] = CreateText("BadgeSubName", badgeRoot.transform,
                    "", 13, FontStyle.Bold,
                    new Color(0.85f, 0.88f, 0.92f), TextAnchor.UpperCenter,
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-55f, -18f), new Vector2(55f, -1f));
            }
        }

        private static void CreateHollowBox(string name, Transform parent, Color lineCol, Vector2 centerPos, Vector2 size, float thickness)
        {
            GameObject box = CreateUiElement(name, parent);
            RectTransform rt = box.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = centerPos;
            rt.sizeDelta = size;

            CreatePanel("Top", box.transform, lineCol, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, thickness));
            CreatePanel("Bottom", box.transform, lineCol, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, thickness));
            CreatePanel("Left", box.transform, lineCol, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(thickness, 0f));
            CreatePanel("Right", box.transform, lineCol, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(thickness, 0f));
        }

        /// <summary>
        /// 화면 우측에 서 있는 3D 선수 캐릭터 바로 발밑/허리 아래에 뜨는 TV 중계 자막(Lower-Third) 이름표 & 포지션 특화 스탯 카드!
        /// (무채색 다크 흑요석 슬레이트 & 둥근 모서리)
        /// </summary>
        private StatCardRefs BuildSpotlightLowerThirdCard(Transform parent, Vector2 centerOffset)
        {
            StatCardRefs refs = new StatCardRefs();

            GameObject card = CreatePanel("SpotlightLowerThirdCard", parent,
                new Color(0.09f, 0.10f, 0.12f, 0.94f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                centerOffset, new Vector2(780f, 345f), PanelShape.Rounded);

            // 상단 1명씩 자동 소개 진행 바 & 순서 표시 배너
            GameObject stepHeader = CreatePanel("StepProgressHeader", card.transform,
                new Color(0.13f, 0.14f, 0.17f, 0.98f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 34f), PanelShape.Rounded);

            _spotlightStepLabel = CreateText("SpotlightStepLabel", stepHeader.transform,
                "🎥 STARTING XI 선수 소개  [ 1 / 11 — 골키퍼 (GK) ]", 16, FontStyle.Bold,
                new Color(0.88f, 0.91f, 0.95f), TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(14f, 0f), new Vector2(-14f, 0f));

            GameObject timerBarBg = CreatePanel("StepTimerBarBg", stepHeader.transform,
                new Color(0.06f, 0.07f, 0.08f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
                Vector2.zero, new Vector2(0f, 5f), PanelShape.Pill);

            GameObject timerBarFill = CreatePanel("StepTimerBarFill", timerBarBg.transform,
                new Color(0.75f, 0.80f, 0.88f, 1f),
                Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero, PanelShape.Pill);
            _spotlightTimerFill = timerBarFill.GetComponent<UnityEngine.UI.Image>();
            _spotlightTimerFill.type = UnityEngine.UI.Image.Type.Filled;
            _spotlightTimerFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            _spotlightTimerFill.fillOrigin = 0;

            // 메인 방송 이름표 배너 (둥근 캡슐 포지션 박스 + 이름 + OVR)
            GameObject posBox = CreatePanel("SpotlightPosBox", card.transform,
                new Color(0.18f, 0.20f, 0.24f, 0.98f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -44f), new Vector2(118f, 76f), PanelShape.Rounded);
            _spotlightPosBadgeBg = posBox.GetComponent<UnityEngine.UI.Image>();

            _spotlightPosNumText = CreateText("SpotlightPosNum", posBox.transform,
                "GK\n#1", 25, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            refs.nameText = CreateText("PlayerFamousName", card.transform,
                "조현우 (Jo Hyeon-woo)", 32, FontStyle.Bold,
                Color.white, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0.82f, 1f), new Vector2(148f, -86f), new Vector2(0f, -42f));

            refs.archetypeText = CreateText("ArchetypeBadge", card.transform,
                "🧤 월드클래스 수문장 (GK)", 15, FontStyle.Bold,
                new Color(0.78f, 0.82f, 0.88f), TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0.84f, 1f), new Vector2(148f, -118f), new Vector2(0f, -86f));
            refs.archetypeText.horizontalOverflow = HorizontalWrapMode.Overflow;

            // 우측 OVR 둥근 배지
            GameObject ovrBox = CreatePanel("OvrBox", card.transform,
                new Color(0.18f, 0.20f, 0.24f, 0.98f),
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-16f, -44f), new Vector2(96f, 76f), PanelShape.Rounded);
            refs.ovrBadgeBg = ovrBox.GetComponent<UnityEngine.UI.Image>();

            CreateText("OvrSmall", ovrBox.transform, "OVR", 14, FontStyle.Bold,
                new Color(0.85f, 0.88f, 0.92f), TextAnchor.UpperCenter,
                Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -4f));

            refs.ovrText = CreateText("OvrNum", ovrBox.transform, "90", 34, FontStyle.Bold,
                Color.white, TextAnchor.LowerCenter,
                Vector2.zero, Vector2.one, new Vector2(0f, 4f), Vector2.zero);

            // 포지션 특화 스탯 요약 줄 (상/하단 2줄 분리로 텍스트 겹침 방지 + 둥근 모서리)
            GameObject bioStrip = CreatePanel("BioSpecialtyStrip", card.transform,
                new Color(0.12f, 0.13f, 0.15f, 0.92f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -126f), new Vector2(-32f, 58f), PanelShape.Rounded);

            refs.physicalInfoText = CreateText("PhysicalInfo", bioStrip.transform, "", 15, FontStyle.Bold,
                new Color(0.88f, 0.91f, 0.95f), TextAnchor.MiddleLeft,
                new Vector2(0f, 0.50f), new Vector2(1f, 1f), new Vector2(14f, 0f), new Vector2(-14f, -2f));
            refs.physicalInfoText.horizontalOverflow = HorizontalWrapMode.Overflow;

            refs.feelSummaryText = CreateText("FeelSummary", bioStrip.transform, "", 15, FontStyle.Bold,
                new Color(0.82f, 0.85f, 0.90f), TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(1f, 0.50f), new Vector2(14f, 2f), new Vector2(-14f, 0f));
            refs.feelSummaryText.horizontalOverflow = HorizontalWrapMode.Overflow;

            // 하단 2열 x 4행 컴팩트 8대 스탯 바 (캡슐형 바 + 무채색 톤)
            for (int i = 0; i < 8; i++)
            {
                int col = i / 4;
                int row = i % 4;
                float colX = col == 0 ? 18f : 398f;
                float rowY = -194f - row * 35f;

                GameObject statRow = CreateUiElement("StatRow_" + i, card.transform);
                RectTransform rt = statRow.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(colX, rowY);
                rt.sizeDelta = new Vector2(362f, 30f);

                CreateText("StatName", statRow.transform, StatLabels[i], 14, FontStyle.Bold,
                    new Color(0.82f, 0.85f, 0.90f), TextAnchor.MiddleLeft,
                    new Vector2(0f, 0f), new Vector2(0.46f, 1f), Vector2.zero, Vector2.zero);

                GameObject barBg = CreatePanel("BarBg", statRow.transform,
                    new Color(0.07f, 0.08f, 0.09f, 1f),
                    new Vector2(0.47f, 0.24f), new Vector2(0.86f, 0.76f), new Vector2(0f, 0.5f),
                    Vector2.zero, Vector2.zero, PanelShape.Pill);

                GameObject barFill = CreatePanel("BarFill", barBg.transform,
                    new Color(0.80f, 0.84f, 0.90f, 1f),
                    Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero, PanelShape.Pill);
                UnityEngine.UI.Image fillImg = barFill.GetComponent<UnityEngine.UI.Image>();
                fillImg.type = UnityEngine.UI.Image.Type.Filled;
                fillImg.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
                fillImg.fillOrigin = 0;
                refs.statBarFills[i] = fillImg;

                refs.statValueTexts[i] = CreateText("StatVal", statRow.transform, "85", 17, FontStyle.Bold,
                    Color.white, TextAnchor.MiddleRight,
                    new Vector2(0.87f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            }

            return refs;
        }

        private void BuildMatchEndOverlay(Transform canvasRoot)
        {
            _matchEndPanel = CreatePanel("MatchEndOverlay", canvasRoot,
                new Color(0.04f, 0.04f, 0.05f, 0.88f),
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _matchEndPanel.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;

            GameObject box = CreatePanel("ResultCard", _matchEndPanel.transform,
                new Color(0.09f, 0.10f, 0.12f, 0.96f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(740f, 430f), PanelShape.Rounded);

            _matchEndTitle = CreateText("ResultTitle", box.transform, "🏆 경기 종료 — 승리!", 44, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.65f), new Vector2(1f, 0.92f), Vector2.zero, Vector2.zero);

            _matchEndScore = CreateText("ResultScore", box.transform, "3  :  1", 62, FontStyle.Bold,
                new Color(0.92f, 0.94f, 0.98f), TextAnchor.MiddleCenter,
                new Vector2(0f, 0.36f), new Vector2(1f, 0.66f), Vector2.zero, Vector2.zero);

            CreateButton("BtnPlayNextRandomMatch", box.transform,
                "🎲 다음 판 하기 (새 랜덤 포메이션 & 22인 배정!)", 24,
                new Color(0.24f, 0.27f, 0.32f, 1f), Color.white,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 44f), new Vector2(540f, 68f),
                () =>
                {
                    _matchEndPanel.SetActive(false);
                    _onNextMatchCallback?.Invoke();
                }, PanelShape.Rounded);

            _matchEndPanel.SetActive(false);
        }

        public void PopulateMatchLineupsAndProfiles(
            FormationPreset blueFormation,
            FormationPreset redFormation,
            GoalkeeperAI blueGk,
            IReadOnlyList<SoccerPlayerController> blueOutfield,
            GoalkeeperAI redGk = null,
            IReadOnlyList<SoccerPlayerController> redOutfield = null)
        {
            SetLineupSoloSpotlightVisibility(null);

            _cachedBlueFormation = blueFormation;
            _cachedRedFormation = redFormation;
            _blueManagerName = PlayerProfileGenerator.PickRandomManagerName();
            _redManagerName = PlayerProfileGenerator.PickRandomManagerName(_blueManagerName);

            BuildOrderedLineupEntries(_blueLineupEntries, blueGk, blueOutfield);
            BuildOrderedLineupEntries(_redLineupEntries, redGk, redOutfield);

            _isInspectingBlueTeam = true;
            _autoAdvanceLineup = true;
            _lineupAutoTimer = 0f;
            if (_autoPlayBtnLabel != null)
                _autoPlayBtnLabel.text = "⏸ 1명씩 자동소개 일시정지";

            // 항상 1번 골키퍼(GK)부터 차례대로 1명씩 소개 시작!
            SelectLineupPlayerIndex(0, manualClick: false);
        }

        private static void BuildOrderedLineupEntries(
            List<LineupEntry> targetList,
            GoalkeeperAI gk,
            IReadOnlyList<SoccerPlayerController> outfield)
        {
            targetList.Clear();

            // 0번: 골키퍼 (GK #1) — TV 중계 라인업 소개의 첫 번째 주인공!
            if (gk != null && gk.Profile != null)
            {
                targetList.Add(new LineupEntry
                {
                    profile = gk.Profile,
                    actorTransform = gk.transform,
                    pitchNormPos = new Vector2(0f, -0.82f),
                    isGoalkeeper = true,
                    controlKeyLabel = "GK"
                });
            }

            if (outfield == null) return;

            // 필드플레이어 10명을 수비수(DF) -> 미드필더(MF) -> 공격수(FW) 순서(뒤에서 앞으로, 왼쪽에서 오른쪽으로)로 정렬!
            List<SoccerPlayerController> sortedOutfield = new List<SoccerPlayerController>(outfield.Count);
            for (int i = 0; i < outfield.Count; i++)
            {
                if (outfield[i] != null && outfield[i].Profile != null)
                    sortedOutfield.Add(outfield[i]);
            }

            sortedOutfield.Sort((a, b) =>
            {
                int lineCompare = b.AssignedSlot.line.CompareTo(a.AssignedSlot.line); // Defender(2) -> Midfielder(1) -> Forward(0)
                if (lineCompare != 0) return lineCompare;
                int depthCompare = a.AssignedSlot.normDepthX.CompareTo(b.AssignedSlot.normDepthX);
                if (depthCompare != 0) return depthCompare;
                return a.AssignedSlot.normLaneZ.CompareTo(b.AssignedSlot.normLaneZ);
            });

            for (int i = 0; i < sortedOutfield.Count; i++)
            {
                SoccerPlayerController p = sortedOutfield[i];
                FormationSlot slot = p.AssignedSlot;

                // 2D 미니 전술판 좌표 매핑 (가로 x: -0.76~+0.76, 세로 y: 수비 -0.45 ~ 공격 +0.58)
                float normX = Mathf.Clamp(slot.normLaneZ, -0.78f, 0.78f);
                float normY = Mathf.Lerp(-0.46f, 0.56f, Mathf.InverseLerp(-0.70f, 0.40f, slot.normDepthX));

                int keyInt = (p.SquadIndex + 1) % 10;
                targetList.Add(new LineupEntry
                {
                    profile = p.Profile,
                    actorTransform = p.transform,
                    pitchNormPos = new Vector2(normX, normY),
                    isGoalkeeper = false,
                    controlKeyLabel = $"{keyInt}번키"
                });
            }
        }

        private void ToggleInspectedTeam()
        {
            _isInspectingBlueTeam = !_isInspectingBlueTeam;
            _lineupAutoTimer = 0f;
            SelectLineupPlayerIndex(0, manualClick: false);
        }

        private void ToggleAutoAdvanceLineup()
        {
            _autoAdvanceLineup = !_autoAdvanceLineup;
            _lineupAutoTimer = 0f;
            if (_autoPlayBtnLabel != null)
            {
                _autoPlayBtnLabel.text = _autoAdvanceLineup
                    ? "⏸ 1명씩 자동소개 일시정지"
                    : "▶ 1명씩 자동소개 재생";
            }
        }

        private void StepLineupPlayer(int delta, bool manualClick)
        {
            List<LineupEntry> activeList = _isInspectingBlueTeam ? _blueLineupEntries : _redLineupEntries;
            if (activeList == null || activeList.Count == 0) return;

            int nextIdx = (_inspectedLineupIndex + delta + activeList.Count) % activeList.Count;
            SelectLineupPlayerIndex(nextIdx, manualClick);
        }

        private void SelectLineupPlayerIndex(int index, bool manualClick)
        {
            List<LineupEntry> activeList = _isInspectingBlueTeam ? _blueLineupEntries : _redLineupEntries;
            if (activeList == null || activeList.Count == 0) return;

            _inspectedLineupIndex = Mathf.Clamp(index, 0, activeList.Count - 1);
            _lineupAutoTimer = 0f;

            LineupEntry current = activeList[_inspectedLineupIndex];

            // 프리매치 라인업 소개 창이 열려 있는 동안에는 현재 소개 중인 1명의 3D 캐릭터만 단독으로 표시하여 다른 선수와 절대 겹치지 않도록 보장!
            if (_preMatchPanel == null || _preMatchPanel.activeSelf)
                SetLineupSoloSpotlightVisibility(current.actorTransform);

            // 1. 3D 카메라를 해당 선수(골키퍼 #1 ~ 공격수 #11) 앞으로 즉시 이동시켜 화면 우측에 3D 캐릭터를 세워둠!
            if (DynamicSoccerCamera.Instance != null && current.actorTransform != null)
            {
                float hScale = current.profile != null ? current.profile.HeightScale : 1f;
                int attackSign = _isInspectingBlueTeam ? +1 : -1;
                DynamicSoccerCamera.Instance.SetPreMatchSpotlightTarget(current.actorTransform, hScale, attackSign);
            }

            // 2. 우측 3D 캐릭터 바로 밑에 뜨는 이름표(Lower-Third) & 포지션별 특화 스탯 업데이트!
            FillCard(_playerCard, current.profile);

            if (_spotlightStepLabel != null && current.profile != null)
            {
                string roleKor = current.profile.positionLine switch
                {
                    PositionLine.Goalkeeper => "골키퍼 (GK)",
                    PositionLine.Defender => "수비수 (DF)",
                    PositionLine.Midfielder => "미드필더 (MF)",
                    _ => "공격수 (FW)"
                };
                string teamTag = _isInspectingBlueTeam ? "🟦 대한민국 (BLUE)" : "🟥 AI 상대팀 (RED)";
                _spotlightStepLabel.text =
                    $"🎥 {teamTag} 선발 라인업 소개   <b>[ {_inspectedLineupIndex + 1} / {activeList.Count} — {roleKor} #{current.profile.jerseyNumber} {current.profile.playerName} ]</b>";
            }

            // 3. 좌측 11인 명단 보드 + 감독 + 중앙 2D 미니 전술판 하이라이트 갱신!
            RefreshBroadcastBoardVisuals();
        }

        private void SetLineupSoloSpotlightVisibility(Transform soloActor)
        {
            for (int i = 0; i < _blueLineupEntries.Count; i++)
            {
                Transform t = _blueLineupEntries[i].actorTransform;
                if (t == null) continue;
                PlayerVisualRig rig = t.GetComponent<PlayerVisualRig>();
                if (rig != null)
                    rig.SetBroadcastSoloVisible(soloActor == null || t == soloActor);
            }

            for (int i = 0; i < _redLineupEntries.Count; i++)
            {
                Transform t = _redLineupEntries[i].actorTransform;
                if (t == null) continue;
                PlayerVisualRig rig = t.GetComponent<PlayerVisualRig>();
                if (rig != null)
                    rig.SetBroadcastSoloVisible(soloActor == null || t == soloActor);
            }
        }

        private void RefreshBroadcastBoardVisuals()
        {
            List<LineupEntry> activeList = _isInspectingBlueTeam ? _blueLineupEntries : _redLineupEntries;
            FormationPreset activeForm = _isInspectingBlueTeam ? _cachedBlueFormation : _cachedRedFormation;
            string formName = activeForm != null ? activeForm.formationName : "4-3-3";
            string formTitle = activeForm != null ? activeForm.formationTitleKor : "4-3-3 어택";

            if (_broadcastHeaderBg != null)
            {
                _broadcastHeaderBg.color = _isInspectingBlueTeam
                    ? new Color(0.13f, 0.15f, 0.18f, 0.98f)
                    : new Color(0.18f, 0.14f, 0.15f, 0.98f);
            }

            if (_broadcastFlagAccent != null)
            {
                _broadcastFlagAccent.color = _isInspectingBlueTeam
                    ? new Color(0.40f, 0.50f, 0.62f, 1f)
                    : new Color(0.62f, 0.40f, 0.44f, 1f);
            }

            if (_broadcastFlagCodeText != null)
                _broadcastFlagCodeText.text = _isInspectingBlueTeam ? "KOR · 1P" : "CPU · AI";

            if (_broadcastTeamTitleText != null)
            {
                string teamName = _isInspectingBlueTeam ? "대한민국 (BLUE FC)" : "레드 유나이티드 (AI)";
                _broadcastTeamTitleText.text = $"{teamName}   <size=20><color=#C8D0DC>[포메이션 {formName}]</color></size>";
            }

            if (_switchTeamBtnLabel != null)
            {
                _switchTeamBtnLabel.text = _isInspectingBlueTeam
                    ? "🔄 상대팀(RED) 라인업 보기 [Tab]"
                    : "🔄 내 팀(BLUE) 라인업 보기 [Tab]";
            }

            if (_managerNameText != null)
                _managerNameText.text = _isInspectingBlueTeam ? _blueManagerName : _redManagerName;

            if (_formationTacticSubText != null)
                _formationTacticSubText.text = $"🎲 랜덤 배정 전술: {formTitle}";

            for (int i = 0; i < 11; i++)
            {
                if (i >= activeList.Count)
                    continue;

                LineupEntry entry = activeList[i];
                PlayerProfile prof = entry.profile;
                bool isSelected = (i == _inspectedLineupIndex);

                // 1) 좌측 11인 명단 행 업데이트 (무채색 다크 차콜 / 선택 시 플래티넘-슬레이트 하이라이트)
                if (_rosterRowBgs[i] != null)
                {
                    if (isSelected)
                    {
                        _rosterRowBgs[i].color = new Color(0.28f, 0.31f, 0.36f, 0.98f);
                    }
                    else
                    {
                        _rosterRowBgs[i].color = (i % 2 == 0)
                            ? new Color(0.13f, 0.14f, 0.16f, 0.92f)
                            : new Color(0.16f, 0.17f, 0.19f, 0.92f);
                    }
                }

                if (_rosterRowNumTexts[i] != null && prof != null)
                {
                    _rosterRowNumTexts[i].text = entry.isGoalkeeper ? $"🧤 {prof.jerseyNumber}" : $"{prof.jerseyNumber}";
                    _rosterRowNumTexts[i].color = isSelected
                        ? Color.white
                        : new Color(0.80f, 0.84f, 0.90f, 1f);
                }

                if (_rosterRowNameTexts[i] != null && prof != null)
                {
                    string prefix = isSelected ? "▶ " : "";
                    _rosterRowNameTexts[i].text = $"{prefix}{prof.playerName}";
                    _rosterRowNameTexts[i].color = isSelected
                        ? Color.white
                        : new Color(0.88f, 0.91f, 0.95f, 1f);
                }

                if (_rosterRowPosTexts[i] != null && prof != null)
                {
                    string specTag = entry.isGoalkeeper
                        ? "GK"
                        : $"{prof.positionCode} <size=13>(공{prof.AttackRating}/수{prof.DefenseRating})</size>";
                    _rosterRowPosTexts[i].text = specTag;
                    _rosterRowPosTexts[i].color = isSelected
                        ? new Color(0.85f, 0.88f, 0.92f, 1f)
                        : new Color(0.65f, 0.69f, 0.75f, 1f);
                }

                // 2) 중앙 2D 미니 전술판 위 등번호 배지 위치 & 하이라이트 업데이트
                if (_pitchBadgeRects[i] != null && prof != null)
                {
                    float px = entry.pitchNormPos.x * 155f;
                    float py = entry.pitchNormPos.y * 232f;
                    _pitchBadgeRects[i].anchoredPosition = new Vector2(px, py);
                    _pitchBadgeRects[i].localScale = isSelected ? new Vector3(1.30f, 1.30f, 1f) : Vector3.one;

                    if (_pitchBadgeWingBgs[i] != null)
                    {
                        _pitchBadgeWingBgs[i].color = isSelected
                            ? new Color(0.85f, 0.88f, 0.94f, 1f)
                            : new Color(0.20f, 0.22f, 0.26f, 0.95f);
                    }

                    if (_pitchBadgeInnerBgs[i] != null)
                    {
                        _pitchBadgeInnerBgs[i].color = isSelected
                            ? new Color(0.18f, 0.20f, 0.24f, 1f)
                            : new Color(0.14f, 0.15f, 0.18f, 0.98f);
                    }

                    if (_pitchBadgeNumTexts[i] != null)
                    {
                        _pitchBadgeNumTexts[i].text = prof.jerseyNumber.ToString();
                        _pitchBadgeNumTexts[i].color = isSelected
                            ? Color.white
                            : new Color(0.90f, 0.92f, 0.96f);
                    }

                    if (_pitchBadgeNameTexts[i] != null)
                    {
                        string shortKorName = prof.playerName.Split(' ')[0];
                        _pitchBadgeNameTexts[i].text = isSelected
                            ? $"★ {shortKorName} ({prof.positionCode})"
                            : shortKorName;
                        _pitchBadgeNameTexts[i].color = isSelected
                            ? Color.white
                            : new Color(0.80f, 0.84f, 0.90f);
                    }
                }
            }
        }

        public void PopulatePlayerProfiles(PlayerProfile playerProf, PlayerProfile aiProf)
        {
            if (_scoreLeftName != null && playerProf != null)
                _scoreLeftName.text = $"[{playerProf.positionCode}] {playerProf.playerName}";
            if (_scoreRightName != null && aiProf != null)
                _scoreRightName.text = $"[{aiProf.positionCode}] {aiProf.playerName} (AI)";
        }

        public void UpdateControlledPlayerHudInfo(SoccerPlayerController activePlayer)
        {
            if (activePlayer == null || activePlayer.Profile == null) return;

            if (_scoreLeftName != null)
            {
                string posTag = !string.IsNullOrEmpty(activePlayer.Profile.positionCode)
                    ? activePlayer.Profile.positionCode
                    : "1P";
                _scoreLeftName.text = $"[{posTag}] {activePlayer.Profile.playerName}";
            }
        }

        private void FillCard(StatCardRefs card, PlayerProfile p)
        {
            if (card == null || p == null) return;

            card.nameText.text = p.playerName;
            card.archetypeText.text = $"{p.archetypeTag}  —  {p.PositionSpecialtySummary}";
            card.ovrText.text = p.overall.ToString();

            Color roleAccent = new Color(0.18f, 0.20f, 0.24f, 0.98f);

            if (_spotlightPosBadgeBg != null)
                _spotlightPosBadgeBg.color = roleAccent;
            if (_spotlightPosNumText != null)
            {
                _spotlightPosNumText.text = $"{p.positionCode}\n#{p.jerseyNumber}";
                _spotlightPosNumText.color = Color.white;
            }
            if (card.ovrBadgeBg != null)
                card.ovrBadgeBg.color = roleAccent;

            string stars = new string('★', p.weakFootStars) + new string('☆', Mathf.Max(0, 5 - p.weakFootStars));
            card.physicalInfoText.text =
                $"신체: <b>{p.heightCm}cm / {p.weightKg}kg</b>   |   주발: <b>{p.PreferredFootKorean}</b> ({stars})   |   헤어: {p.HairStyleKorean}";

            card.feelSummaryText.text =
                $"포지션 특화 비교:  공격력 <b>{p.AttackRating}</b>   ·   패스/시야 <b>{p.passing}</b>   ·   수비력 <b>{p.DefenseRating}</b>";

            int[] vals =
            {
                p.pace, p.agility, p.finishing, p.shotPower,
                p.passing, p.curve, p.acrobatics, p.defense
            };

            for (int i = 0; i < 8; i++)
            {
                int v = vals[i];
                card.statValueTexts[i].text = v.ToString();
                card.statBarFills[i].fillAmount = Mathf.Clamp01(v / 100f);

                // 고채도 원색 대신 세련된 모노크롬/무채색 그라데이션
                Color statCol;
                if (v >= 85) statCol = new Color(0.92f, 0.94f, 0.98f);      // 최상급 (화이트-플래티넘)
                else if (v >= 70) statCol = new Color(0.65f, 0.69f, 0.75f); // 준수 (쿨 실버-그레이)
                else statCol = new Color(0.38f, 0.41f, 0.46f);              // 상대적 약점 (다크 슬레이트-차콜)

                card.statBarFills[i].color = statCol;
                card.statValueTexts[i].color = statCol;
            }
        }

        public void ShowPreMatchStats(bool isMidGamePeek = false)
        {
            if (_preMatchPanel == null) return;
            if (_matchEndPanel != null) _matchEndPanel.SetActive(false);
            _preMatchPanel.SetActive(true);
            _hudRoot.SetActive(false);
            _lineupAutoTimer = 0f;

            // 창이 열릴 때 현재 선택된 선수의 3D 스포트라이트 카메라 정렬 보장
            SelectLineupPlayerIndex(_inspectedLineupIndex, manualClick: false);

            if (_preMatchHeaderTitle != null)
            {
                _preMatchHeaderTitle.text = isMidGamePeek
                    ? "📺 STARTING XI — 경기 중 선발 11인 포메이션 & 선수별 3D 프로필 확인 (Space / Esc로 경기 재개)"
                    : "📺 STARTING XI — 골키퍼(GK)부터 공격수(FW)까지 선발 11인 1명씩 라인업 소개 중 (클릭 또는 ◀/▶ 이동)";
            }
            if (_closeBtnLabel != null)
            {
                _closeBtnLabel.text = isMidGamePeek
                    ? "▶ 라인업 창 닫기 & 경기 계속하기! (Space / Esc)"
                    : "⚽ 라인업 확인 완료 — 11 vs 11 경기 시작! (Space / Enter)";
            }
        }

        public void CloseStatWindowAndStart()
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            // 경기 시작 시 22명 전원 활성화 복구!
            SetLineupSoloSpotlightVisibility(null);

            if (_preMatchPanel != null)
                _preMatchPanel.SetActive(false);
            if (_hudRoot != null)
                _hudRoot.SetActive(true);

            _onCloseStatScreenCallback?.Invoke();
        }

        private void HandleCloseStatWindowClicked()
        {
            CloseStatWindowAndStart();
        }

        private int _lastDisplayedPlayerScore = -1;
        private int _lastDisplayedAiScore = -1;
        private int _lastDisplayedTotalSec = -1;

        private static void SetTextIfChanged(UnityEngine.UI.Text label, string newText)
        {
            if (label != null && label.text != newText)
                label.text = newText;
        }

        public void UpdateScoreAndTimer(int playerScore, int aiScore, float remainingSeconds)
        {
            if (_scoreNumbersText != null && (playerScore != _lastDisplayedPlayerScore || aiScore != _lastDisplayedAiScore))
            {
                _lastDisplayedPlayerScore = playerScore;
                _lastDisplayedAiScore = aiScore;
                _scoreNumbersText.text = $"{playerScore}  :  {aiScore}";
            }

            if (_timerText != null)
            {
                int totalSec = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
                if (totalSec != _lastDisplayedTotalSec)
                {
                    _lastDisplayedTotalSec = totalSec;
                    int mins = totalSec / 60;
                    int secs = totalSec % 60;
                    _timerText.text = $"{mins:00}:{secs:00}";
                }
            }
        }

        public void UpdatePlayerWorldHud(SoccerPlayerController player)
        {
            if (player == null || _playerTrackerRect == null || !_hudRoot.activeSelf)
                return;

            if (_mainCam == null) _mainCam = Camera.main;
            if (_mainCam == null) return;

            bool fkActive = _freeKickHudRoot != null && _freeKickHudRoot.activeSelf;
            bool hideCrosshair = fkActive || (DynamicSoccerCamera.Instance != null && DynamicSoccerCamera.Instance.UseThirdPersonBroadcast);
            if (_fpCrosshairRoot != null && _fpCrosshairRoot.activeSelf == hideCrosshair)
                _fpCrosshairRoot.SetActive(!hideCrosshair);

            // =====================================================================
            // 볼 소유(드리블) 시 1인칭 크로스헤어 골드/시안 링 + 하단 드리블 배지 점등!
            // =====================================================================
            bool hasBallPossession = player.ball != null && player.ball.CurrentOwner == player && !fkActive;
            if (_possessionCrosshairRingRect != null)
            {
                if (_possessionCrosshairRingRect.gameObject.activeSelf != hasBallPossession)
                    _possessionCrosshairRingRect.gameObject.SetActive(hasBallPossession);

                if (hasBallPossession)
                {
                    _possessionCrosshairRingRect.Rotate(0f, 0f, -155f * Time.unscaledDeltaTime);
                    float ringPulse = 1f + Mathf.Sin(Time.unscaledTime * 11f) * 0.08f;
                    _possessionCrosshairRingRect.localScale = new Vector3(ringPulse, ringPulse, 1f);
                    if (_possessionCrosshairRingImg != null)
                    {
                        float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f);
                        _possessionCrosshairRingImg.color = Color.Lerp(
                            new Color(0.18f, 0.96f, 1.0f, 0.85f),
                            new Color(1.0f, 0.88f, 0.22f, 0.95f),
                            t);
                    }
                }
            }

            if (_possessionHudBadge != null)
            {
                if (_possessionHudBadge.activeSelf != hasBallPossession)
                    _possessionHudBadge.SetActive(hasBallPossession);

                if (hasBallPossession)
                {
                    float badgePulse = 1f + Mathf.Sin(Time.unscaledTime * 10f) * 0.04f;
                    _possessionHudBadge.transform.localScale = new Vector3(badgePulse, badgePulse, 1f);
                    if (_possessionHudBadgeText != null && player.Profile != null)
                    {
                        SetTextIfChanged(_possessionHudBadgeText,
                            $"⚽ 볼 소유 중! [{player.Profile.positionCode} {player.Profile.playerName}] — D: 슛(Z+D 감아차기) | S: 패스 | W: 스루 | A: 크로스 | E: 달리기 | Shift: 치달");
                    }
                }
            }

            // 포켓몬고식 스핀 & 드래그 커브볼 비주얼 위젯 업데이트 (인플레이 및 프리킥 공통!)
            if (_pokeSpinWidgetRoot != null)
            {
                bool showPokeDrag = player.IsDraggingCurveShot;
                if (_pokeSpinWidgetRoot.activeSelf != showPokeDrag)
                    _pokeSpinWidgetRoot.SetActive(showPokeDrag);

                if (showPokeDrag)
                {
                    float curveBias = player.DragCurveBias;
                    bool curveReady = Mathf.Abs(curveBias) >= 0.70f;
                    Vector2 orbPos = Vector2.ClampMagnitude(player.DragCursorOffset * 0.85f, 82f);

                    if (_pokeDragOrbRect != null)
                    {
                        _pokeDragOrbRect.anchoredPosition = orbPos;
                        float orbScale = curveReady ? (1.22f + Mathf.Sin(Time.unscaledTime * 22f) * 0.12f) : 1.0f;
                        _pokeDragOrbRect.localScale = new Vector3(orbScale, orbScale, 1f);
                    }

                    Color spinCol = curveReady
                        ? (curveBias > 0f ? new Color(0.18f, 0.96f, 1.0f, 0.96f) : new Color(1.0f, 0.36f, 0.92f, 0.96f))
                        : new Color(1.0f, 0.88f, 0.25f, 0.88f);

                    if (_pokeDragOrbImg != null) _pokeDragOrbImg.color = spinCol;
                    if (_pokeSpinRingImg != null)
                        _pokeSpinRingImg.color = new Color(spinCol.r, spinCol.g, spinCol.b, curveReady ? 0.78f : 0.38f);

                    if (_pokeSpinRingRect != null)
                    {
                        float spinDir = curveBias >= 0f ? -1f : 1f;
                        float rotSpeed = curveReady ? 540f : 140f;
                        _pokeSpinRingRect.Rotate(0f, 0f, spinDir * rotSpeed * Time.unscaledDeltaTime);
                    }
                }
            }

            // 1인칭 360도 공 위치 추적 화살표 HUD (공이 시야 밖이거나 멀리 있을 때 크로스헤어 둘레에서 공 방향을 정확히 회전 지시!)
            if (_ballRadarRect != null && player.ball != null && !fkActive)
            {
                Vector3 ballPos = player.ball.transform.position;
                float distToBall = Vector3.Distance(player.transform.position, ballPos);
                bool playerOwnsBall = player.ball.CurrentOwner == player;

                Vector3 vp = _mainCam.WorldToViewportPoint(ballPos);
                bool offScreen = vp.z <= 0f || vp.x < 0.08f || vp.x > 0.92f || vp.y < 0.08f || vp.y > 0.92f;
                Vector3 toBallFromCam = (ballPos - _mainCam.transform.position).normalized;
                float viewAngle = Vector3.Angle(_mainCam.transform.forward, toBallFromCam);

                bool showPointer = !playerOwnsBall && (offScreen || viewAngle > 22f || distToBall > 18f);
                if (_ballRadarRect.gameObject.activeSelf != showPointer)
                    _ballRadarRect.gameObject.SetActive(showPointer);

                if (showPointer)
                {
                    Vector3 localBall = _mainCam.transform.InverseTransformPoint(ballPos);
                    Vector2 dir2D = new Vector2(localBall.x, localBall.y);
                    if (localBall.z < 0f)
                    {
                        dir2D.y = -Mathf.Max(0.35f, Mathf.Abs(dir2D.y));
                    }
                    if (dir2D.sqrMagnitude < 0.0001f)
                        dir2D = Vector2.down;
                    dir2D.Normalize();

                    _ballRadarRect.anchoredPosition = new Vector2(dir2D.x * 265f, dir2D.y * 185f);

                    if (_ballRadarArrowRect != null)
                    {
                        float angleDeg = Mathf.Atan2(dir2D.y, dir2D.x) * Mathf.Rad2Deg - 90f;
                        _ballRadarArrowRect.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
                    }

                    Color pointerCol;
                    string stateTag;
                    SoccerPlayerController owner = player.ball.CurrentOwner;
                    if (owner != null && owner.attackingGoalSign == player.attackingGoalSign)
                    {
                        pointerCol = new Color(0.25f, 1.0f, 0.68f, 0.98f);
                        stateTag = "동료볼 [C전환]";
                    }
                    else if (owner != null)
                    {
                        pointerCol = new Color(1.0f, 0.38f, 0.22f, 0.98f);
                        stateTag = "상대볼 [R태클]";
                    }
                    else
                    {
                        pointerCol = new Color(1.0f, 0.88f, 0.20f, 0.98f);
                        stateTag = "루즈볼";
                    }

                    if (_ballRadarArrowImg != null)
                        _ballRadarArrowImg.color = pointerCol;

                    int distM = Mathf.RoundToInt(distToBall);
                    SetTextIfChanged(_ballRadarText, $"⚽ {distM}m {stateTag}");
                    if (_ballRadarText.color != pointerCol)
                        _ballRadarText.color = pointerCol;
                }
            }
            else if (_ballRadarRect != null && _ballRadarRect.gameObject.activeSelf)
            {
                _ballRadarRect.gameObject.SetActive(false);
            }

            // 1인칭 크로스헤어 스마트 타겟팅 색상 및 동작 배지 업데이트
            if (_fpCrosshairParts != null && _fpCrosshairParts.Length > 0 && !fkActive)
            {
                Color chColor = new Color(1f, 1f, 1f, 0.82f);
                string aimText = "";

                if (player.IsDraggingCurveShot)
                {
                    float bias = player.DragCurveBias;
                    if (Mathf.Abs(bias) >= 0.70f)
                    {
                        chColor = bias > 0f ? new Color(0.20f, 0.96f, 1.0f, 1f) : new Color(1.0f, 0.42f, 0.95f, 1f);
                        aimText = bias > 0f
                            ? "🌀 [포켓몬고 감아차기] ↷ 우측 커브 스핀! (드롭 시 발사)"
                            : "🌀 [포켓몬고 감아차기] ↶ 좌측 커브 스핀! (드롭 시 발사)";
                    }
                    else
                    {
                        chColor = new Color(1.0f, 0.88f, 0.25f, 1f);
                        aimText = "💥 직선 파워 슛 (좌/우로 빠르게 드래그하면 감아차기!)";
                    }
                }
                else if (player.CurrentAimTargetType == AimTargetType.Teammate)
                {
                    chColor = new Color(0.20f, 1.0f, 0.65f, 1f); // 아군: 에메랄드 그린
                    string mateName = player.CurrentAimTargetPlayer != null && player.CurrentAimTargetPlayer.Profile != null
                        ? player.CurrentAimTargetPlayer.Profile.playerName : "동료";
                    bool mateHasBall = player.ball != null && player.ball.CurrentOwner == player.CurrentAimTargetPlayer;
                    aimText = mateHasBall
                        ? $"📢 {mateName}에게 패스 콜 [S] | 선수 전환 [C]"
                        : $"🎯 {mateName}에게 패스 [S] | 크로스 [A]";
                }
                else if (player.CurrentAimTargetType == AimTargetType.Opponent)
                {
                    chColor = new Color(1.0f, 0.28f, 0.20f, 1f); // 상대: 경고 레드
                    string oppName = player.CurrentAimTargetPlayer != null && player.CurrentAimTargetPlayer.Profile != null
                        ? player.CurrentAimTargetPlayer.Profile.playerName : "상대";
                    string tackleLabel = player.IsSprintingNow ? "슬라이딩 태클" : "태클";
                    aimText = $"💥 {oppName} {tackleLabel} [Space / D]  |  슬라이딩 [A]  |  몸싸움 [F]";
                }
                else if (player.CanDoAcrobaticNow && !player.IsPerformingAcrobatic)
                {
                    chColor = new Color(1.0f, 0.62f, 0.14f, 0.98f);
                    aimText = player.AvailableAcrobaticType == ShotType.OverheadKick
                        ? "🔥 오버헤드킥 찬스!! [D]"
                        : "⚡ 시저스 발리 찬스!! [D]";
                }
                else if (player.IsBallInAcrobaticRangeWithoutAim && !player.IsPerformingAcrobatic)
                {
                    chColor = new Color(1.0f, 0.90f, 0.35f, 0.92f);
                    aimText = "🎯 공중볼에 마우스 에임(+)을 맞추고 [좌클릭]하면 오버헤드킥!";
                }
                else if (player.BlitzDashCandidateTarget != null && player.BlitzDashCooldownRemaining <= 0.05f)
                {
                    chColor = new Color(0.32f, 0.95f, 1.0f, 0.95f);
                    string oppName = player.BlitzDashCandidateTarget.Profile != null
                        ? player.BlitzDashCandidateTarget.Profile.playerName : "상대";
                    aimText = $"⚡ {oppName} 순간 돌진 볼 탈취 가능! [Space 두번 연타]";
                }

                for (int i = 0; i < _fpCrosshairParts.Length; i++)
                {
                    if (_fpCrosshairParts[i] != null && _fpCrosshairParts[i].color != chColor)
                        _fpCrosshairParts[i].color = chColor;
                }

                if (_fpAimActionLabel != null)
                {
                    SetTextIfChanged(_fpAimActionLabel, aimText);
                    if (_fpAimActionLabel.color != chColor)
                        _fpAimActionLabel.color = chColor;
                }
            }

            // 슛 게이지 표시 (1인칭 크로스헤어 하단 중앙 고정)
            bool showGauge = player.IsChargingShot;
            if (_shotGaugeContainer.activeSelf != showGauge)
                _shotGaugeContainer.SetActive(showGauge);

            if (showGauge)
            {
                float c = player.CurrentShotCharge01;
                _shotGaugeFill.fillAmount = c;
                if (player.IsDraggingCurveShot && Mathf.Abs(player.DragCurveBias) >= 0.70f)
                {
                    _shotGaugeFill.color = new Color(0.20f, 0.92f, 1.0f);
                    string dir = player.DragCurveBias > 0f ? "우측 커브 ↷" : "좌측 커브 ↶";
                    SetTextIfChanged(_shotGaugeLabel, $"🌀 포켓몬고 감아차기 ({dir})");
                }
                else
                {
                    _shotGaugeFill.color = Color.Lerp(new Color(0.25f, 1f, 0.4f), new Color(1f, 0.30f, 0.15f), c);
                    SetTextIfChanged(_shotGaugeLabel, "💥 파워 슛 (좌우 드래그 시 감아차기)");
                }
            }

            // 공중볼 오버헤드킥 / 아크로바틱 찬스 즉시 알림 배지! (1인칭 중앙 상단 고정)
            bool showAcro = (player.CanDoAcrobaticNow || player.CanDoJumpHeaderNow()) && !player.IsPerformingAcrobatic;
            if (_acrobaticPromptBadge.activeSelf != showAcro)
                _acrobaticPromptBadge.SetActive(showAcro);

            if (showAcro)
            {
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 16f) * 0.08f;
                _acrobaticPromptBadge.transform.localScale = new Vector3(pulse, pulse, 1f);
                if (player.CanDoAcrobaticNow)
                {
                    SetTextIfChanged(_acrobaticPromptText, player.AvailableAcrobaticType == ShotType.OverheadKick
                        ? "🔥 오버헤드킥 찬스!! [D]"
                        : "⚡ 시저스 발리 찬스!! [D]");
                }
                else
                {
                    SetTextIfChanged(_acrobaticPromptText, "💥 헤딩 찬스!! [D]");
                }
            }
        }

        public void UpdateFreeKickHud(
            bool active,
            bool isHumanKicker,
            Vector3 worldAimPoint,
            float charge01,
            bool isCurveMode,
            SoccerPlayerController humanKicker = null,
            bool isPenaltyKick = false,
            bool isDistantPassKick = false,
            SoccerPlayerController suggestedPassMate = null)
        {
            if (_freeKickHudRoot == null) return;
            if (_freeKickHudRoot.activeSelf != active)
                _freeKickHudRoot.SetActive(active);

            if (!active)
            {
                if (_pokeSpinWidgetRoot != null && _pokeSpinWidgetRoot.activeSelf && (humanKicker == null || !humanKicker.IsDraggingCurveShot))
                    _pokeSpinWidgetRoot.SetActive(false);
                return;
            }

            if (isHumanKicker)
            {
                if (isPenaltyKick)
                {
                    _freeKickBannerText.text =
                        "⚽ <b>페널티킥(PK) 1대1 찬스!!</b>  방향키로 골문 구석을 조준하고 <b>[D]</b>로 슛 (<b>[Z+D]</b> 감아차기)!";
                }
                else if (isDistantPassKick)
                {
                    _freeKickBannerText.text =
                        "⚽ <b>프리킥 빌드업 (장거리 — 수비벽 없음)</b>  방향키로 동료 방향을 조준하고 <b>[S]</b> 패스 · <b>[A]</b> 롱크로스!";
                }
                else
                {
                    _freeKickBannerText.text =
                        "🎯 <b>직접 프리킥 찬스!</b>  [D] 직접슛 (Z+D 감아차기)  |  <b>[S] 동료 패스</b>  |  <b>[A] 침투 헤딩 크로스</b>";
                }

                SoccerPlayerController passTargetMate = (!isPenaltyKick && humanKicker != null && humanKicker.CurrentAimTargetType == AimTargetType.Teammate && humanKicker.CurrentAimTargetPlayer != null)
                    ? humanKicker.CurrentAimTargetPlayer
                    : (isDistantPassKick ? suggestedPassMate : null);

                if (passTargetMate != null)
                {
                    _freeKickReticleRect.gameObject.SetActive(true);
                    if (_mainCam == null) _mainCam = Camera.main;
                    if (_mainCam != null)
                    {
                        Vector3 sp = _mainCam.WorldToScreenPoint(passTargetMate.transform.position + Vector3.up * 1.15f);
                        if (sp.z > 0f)
                        {
                            RectTransform canvasRt = _mainCanvas.GetComponent<RectTransform>();
                            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, null, out Vector2 localPt);
                            _freeKickReticleRect.anchoredPosition = localPt;
                        }
                    }

                    Color passCol = new Color(0.20f, 1.0f, 0.65f);
                    if (_freeKickCrosshairParts != null)
                    {
                        for (int i = 0; i < _freeKickCrosshairParts.Length; i++)
                            if (_freeKickCrosshairParts[i] != null) _freeKickCrosshairParts[i].color = passCol;
                    }
                    string mName = passTargetMate.Profile != null ? passTargetMate.Profile.playerName : "동료";
                    if (_freeKickModeLabel != null)
                    {
                        _freeKickModeLabel.text = isDistantPassKick
                            ? $"🎯 {mName}에게 프리킥 패스 재개 [S]"
                            : $"🎯 {mName}에게 프리킥 패스 [S]";
                        _freeKickModeLabel.color = passCol;
                    }
                    if (_freeKickGaugeObj != null) _freeKickGaugeObj.SetActive(false);
                    return;
                }

                if (_mainCam == null) _mainCam = Camera.main;
                bool visibleInFront = false;
                if (_mainCam != null)
                {
                    Vector3 sp = _mainCam.WorldToScreenPoint(worldAimPoint);
                    if (sp.z > 0f)
                    {
                        visibleInFront = true;
                        RectTransform canvasRt = _mainCanvas.GetComponent<RectTransform>();
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, null, out Vector2 localPt);
                        _freeKickReticleRect.anchoredPosition = localPt;
                    }
                }
                _freeKickReticleRect.gameObject.SetActive(visibleInFront);

                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 10f) * 0.06f;
                _freeKickReticleRect.localScale = new Vector3(pulse, pulse, 1f);

                Color aimCol = isCurveMode ? new Color(0.85f, 0.90f, 0.98f) : new Color(0.95f, 0.96f, 0.98f);
                if (_freeKickCrosshairParts != null)
                {
                    for (int i = 0; i < _freeKickCrosshairParts.Length; i++)
                    {
                        if (_freeKickCrosshairParts[i] != null)
                            _freeKickCrosshairParts[i].color = aimCol;
                    }
                }

                if (_freeKickModeLabel != null)
                {
                    if (isPenaltyKick)
                    {
                        _freeKickModeLabel.text = isCurveMode
                            ? "🌀 포켓몬고 감아차기 페널티킥! (드롭 시 발사)"
                            : "⚽ 페널티킥(PK) 조준 ([D] 슛 | [Z+D] 감아차기)";
                    }
                    else
                    {
                        _freeKickModeLabel.text = isCurveMode
                            ? "🌀 포켓몬고 감아차기 프리킥! (드롭 시 발사)"
                            : "💥 프리킥 조준 ([D] 슛 | [Z+D] 감아차기)";
                    }
                    _freeKickModeLabel.color = aimCol;
                }

                bool charging = charge01 > 0.005f;
                if (_freeKickGaugeObj != null && _freeKickGaugeObj.activeSelf != charging)
                    _freeKickGaugeObj.SetActive(charging);

                if (charging && _freeKickGaugeFill != null)
                {
                    _freeKickGaugeFill.fillAmount = charge01;
                    _freeKickGaugeFill.color = isCurveMode
                        ? new Color(0.80f, 0.86f, 0.94f)
                        : Color.Lerp(new Color(0.68f, 0.72f, 0.78f), new Color(0.92f, 0.94f, 0.98f), charge01);
                }
            }
            else
            {
                if (isPenaltyKick)
                {
                    _freeKickBannerText.text =
                        "🧤 <b>상대 페널티킥(PK) 위기!</b>  우리 골키퍼의 선방을 지켜보세요!";
                }
                else if (isDistantPassKick)
                {
                    _freeKickBannerText.text =
                        "⚽ <b>상대 진영 프리킥 재개</b>  상대 팀이 패스로 경기를 재개합니다!";
                }
                else
                {
                    _freeKickBannerText.text =
                        "🧱 <b>상대 직접 프리킥 수비벽!</b>  키커가 발을 휘두를 때 <b>[Space]</b> 키를 눌러 점프 블로킹하세요!";
                }
                _freeKickReticleRect.gameObject.SetActive(false);
            }
        }

        public void ShowShotToast(string message, bool isWeakFootWarning)
        {
            if (_toastText == null || _toastGroup == null) return;
            _toastText.text = message;
            _toastBg.color = isWeakFootWarning
                ? new Color(0.22f, 0.17f, 0.17f, 0.95f)
                : new Color(0.12f, 0.13f, 0.16f, 0.95f);
            _toastGroup.alpha = 1f;
            _toastTimer = 3.1f;
        }

        public void ShowGoalCelebrationBanner(string mainHeadline, string subDetail)
        {
            if (_goalBannerRoot == null) return;
            _goalMainTitle.text = mainHeadline;
            _goalDetailSubtitle.text = subDetail;
            _goalBannerAnimTime = 0f;
            _goalBannerRoot.SetActive(true);
        }

        public void HideGoalCelebrationBanner()
        {
            if (_goalBannerRoot != null)
                _goalBannerRoot.SetActive(false);
        }

        public void ShowMatchEndScreen(int playerScore, int aiScore, string playerName, string aiName)
        {
            if (_matchEndPanel == null) return;
            _hudRoot.SetActive(false);
            _goalBannerRoot.SetActive(false);
            _matchEndPanel.SetActive(true);

            if (playerScore > aiScore)
                _matchEndTitle.text = $"🏆 승리!! ({playerName} 하드캐리!)";
            else if (playerScore < aiScore)
                _matchEndTitle.text = $"🔥 아쉬운 패배! ({aiName} 승리)";
            else
                _matchEndTitle.text = "🤝 명승부 무승부!";

            _matchEndScore.text = $"{playerScore}   :   {aiScore}";
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // 프리매치 SBS Sports 중계풍 선발 라인업 1명씩 소개 시퀀스 업데이트!
            if (_preMatchPanel != null && _preMatchPanel.activeSelf)
            {
                Keyboard kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
                        StepLineupPlayer(-1, manualClick: true);
                    else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
                        StepLineupPlayer(1, manualClick: true);
                    else if (kb.tabKey.wasPressedThisFrame)
                        ToggleInspectedTeam();
                }

                if (_autoAdvanceLineup)
                {
                    _lineupAutoTimer += dt;
                    if (_spotlightTimerFill != null)
                        _spotlightTimerFill.fillAmount = Mathf.Clamp01(_lineupAutoTimer / LineupAutoStepDuration);

                    if (_lineupAutoTimer >= LineupAutoStepDuration)
                    {
                        _lineupAutoTimer = 0f;
                        List<LineupEntry> activeList = _isInspectingBlueTeam ? _blueLineupEntries : _redLineupEntries;
                        if (activeList != null && _inspectedLineupIndex + 1 >= activeList.Count)
                        {
                            // 한 팀의 11명(GK~FW) 소개가 끝나면 자동으로 반대편 팀 선발 11인 소개로 전환!
                            _isInspectingBlueTeam = !_isInspectingBlueTeam;
                            SelectLineupPlayerIndex(0, manualClick: false);
                        }
                        else
                        {
                            StepLineupPlayer(1, manualClick: false);
                        }
                    }
                }
                else if (_spotlightTimerFill != null)
                {
                    _spotlightTimerFill.fillAmount = 1f;
                }

                // 2D 미니 전술판 위에서 현재 소개 중인 선수의 등번호 배지 펄스 애니메이션
                if (_inspectedLineupIndex >= 0 && _inspectedLineupIndex < _pitchBadgeRects.Length)
                {
                    RectTransform selBadge = _pitchBadgeRects[_inspectedLineupIndex];
                    if (selBadge != null)
                    {
                        float pulse = 1.32f + Mathf.Sin(Time.unscaledTime * 7.5f) * 0.10f;
                        selBadge.localScale = new Vector3(pulse, pulse, 1f);
                    }
                }
            }

            if (_toastTimer > 0f && _toastGroup != null)
            {
                _toastTimer -= dt;
                if (_toastTimer < 0.5f)
                    _toastGroup.alpha = Mathf.Clamp01(_toastTimer / 0.5f);
            }

            if (_goalBannerRoot != null && _goalBannerRoot.activeSelf && _goalBannerCard != null)
            {
                _goalBannerAnimTime += dt;
                float pop = _goalBannerAnimTime < 0.22f
                    ? Mathf.Lerp(0.4f, 1.08f, _goalBannerAnimTime / 0.22f)
                    : Mathf.Lerp(1.08f, 1.0f, Mathf.Clamp01((_goalBannerAnimTime - 0.22f) / 0.18f));
                _goalBannerCard.localScale = new Vector3(pop, pop, 1f);
            }
        }

        #region UI Factory Helpers (Fully-Qualified UnityEngine.UI Types)

        private enum PanelShape
        {
            Sharp,
            Rounded,
            Pill
        }

        private static GameObject CreateUiElement(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static GameObject CreatePanel(
            string name, Transform parent, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta,
            PanelShape shape = PanelShape.Sharp)
        {
            GameObject go = CreateUiElement(name, parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            UnityEngine.UI.Image img = go.AddComponent<UnityEngine.UI.Image>();
            switch (shape)
            {
                case PanelShape.Rounded:
                    img.sprite = MaterialHelper.GetRoundedRectSprite();
                    img.type = UnityEngine.UI.Image.Type.Sliced;
                    break;
                case PanelShape.Pill:
                    img.sprite = MaterialHelper.GetPillSprite();
                    img.type = UnityEngine.UI.Image.Type.Sliced;
                    break;
                default:
                    img.sprite = MaterialHelper.GetWhiteSprite();
                    img.type = UnityEngine.UI.Image.Type.Simple;
                    break;
            }
            img.color = color;
            img.raycastTarget = false;
            return go;
        }

        private UnityEngine.UI.Text CreateText(
            string name, Transform parent, string content, int fontSize, FontStyle style,
            Color color, TextAnchor alignment,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject go = CreateUiElement(name, parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;

            UnityEngine.UI.Text txt = go.AddComponent<UnityEngine.UI.Text>();
            txt.font = _koreanFont;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = alignment;
            txt.supportRichText = true;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;

            UnityEngine.UI.Outline outline = go.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.65f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);

            return txt;
        }

        private GameObject CreateButton(
            string name, Transform parent, string label, int fontSize,
            Color bgColor, Color textColor,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta,
            UnityEngine.Events.UnityAction onClick,
            PanelShape shape = PanelShape.Rounded)
        {
            GameObject btnObj = CreatePanel(name, parent, bgColor, anchorMin, anchorMax, pivot, anchoredPos, sizeDelta, shape);
            UnityEngine.UI.Image img = btnObj.GetComponent<UnityEngine.UI.Image>();
            img.raycastTarget = true;

            UnityEngine.UI.Button btn = btnObj.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = img;
            btn.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            UnityEngine.UI.ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.22f, 1.22f, 1.25f, 1f);
            cb.pressedColor = new Color(0.72f, 0.74f, 0.78f, 1f);
            btn.colors = cb;
            btn.onClick.AddListener(onClick);

            CreateText("BtnLabel", btnObj.transform, label, fontSize, FontStyle.Bold,
                textColor, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, new Vector2(8f, 2f), new Vector2(-8f, -2f));

            return btnObj;
        }

        #endregion
    }
}
