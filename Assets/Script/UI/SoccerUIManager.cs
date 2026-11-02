using System;
using Soccer3D.Data;
using Soccer3D.Gameplay;
using Soccer3D.Visuals;
using UnityEngine;
using UnityEngine.EventSystems;
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

        private Font _koreanFont;
        private Canvas _mainCanvas;
        private Camera _mainCam;

        // 프리매치 스탯 확인창
        private GameObject _preMatchPanel;
        private UnityEngine.UI.Text _preMatchHeaderTitle;
        private UnityEngine.UI.Text _closeBtnLabel;
        private StatCardRefs _playerCard;
        private StatCardRefs _aiCard;

        // 인게임 HUD
        private GameObject _hudRoot;
        private UnityEngine.UI.Text _scoreLeftName;
        private UnityEngine.UI.Text _scoreRightName;
        private UnityEngine.UI.Text _scoreNumbersText;
        private UnityEngine.UI.Text _timerText;

        // 선수 머리 위 추적 게이지 & 오버헤드킥 알림 배지
        private RectTransform _playerTrackerRect;
        private GameObject _shotGaugeContainer;
        private UnityEngine.UI.Image _shotGaugeFill;
        private UnityEngine.UI.Text _shotGaugeLabel;
        private GameObject _acrobaticPromptBadge;
        private UnityEngine.UI.Text _acrobaticPromptText;

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
            "골 결정력 (FINISH)",
            "슛 파워 (POWER)",
            "중거리 슛 (LONG)",
            "커브·감아차기 (CURVE)",
            "아크로바틱 (ACRO)",
            "몸싸움 (PHYSICAL)"
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

            // 1. 상단 중계 스코어보드
            GameObject topBar = CreatePanel("TopScoreboard", _hudRoot.transform,
                new Color(0.05f, 0.08f, 0.15f, 0.88f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -18f), new Vector2(880f, 74f));

            // 왼쪽 블루 팀 스트라이프
            CreatePanel("BlueAccent", topBar.transform, new Color(0.15f, 0.58f, 1.0f, 1f),
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(10f, 0f));

            // 오른쪽 레드 팀 스트라이프
            CreatePanel("RedAccent", topBar.transform, new Color(0.95f, 0.22f, 0.28f, 1f),
                new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                Vector2.zero, new Vector2(10f, 0f));

            _scoreLeftName = CreateText("PlayerNameScore", topBar.transform, "손흥민 (ME)", 23, FontStyle.Bold,
                new Color(0.65f, 0.88f, 1f), TextAnchor.MiddleRight,
                new Vector2(0f, 0f), new Vector2(0.36f, 1f), new Vector2(18f, 0f), new Vector2(-12f, 0f));

            _scoreNumbersText = CreateText("ScoreDigits", topBar.transform, "0  :  0", 36, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.36f, 0.22f), new Vector2(0.64f, 1f), Vector2.zero, Vector2.zero);

            _timerText = CreateText("MatchTimer", topBar.transform, "02:00", 18, FontStyle.Bold,
                new Color(1f, 0.86f, 0.28f), TextAnchor.LowerCenter,
                new Vector2(0.36f, 0.04f), new Vector2(0.64f, 0.36f), Vector2.zero, Vector2.zero);

            _scoreRightName = CreateText("AiNameScore", topBar.transform, "호날두 (AI)", 23, FontStyle.Bold,
                new Color(1f, 0.72f, 0.72f), TextAnchor.MiddleLeft,
                new Vector2(0.64f, 0f), new Vector2(1f, 1f), new Vector2(12f, 0f), new Vector2(-18f, 0f));

            // 우측 상단 미니 버튼들 (스탯 다시 보기 / 새 판 랜덤선수)
            CreateButton("BtnPeekStats", _hudRoot.transform, "📊 선수 스탯 보기 [Tab]", 18,
                new Color(0.14f, 0.24f, 0.42f, 0.90f), Color.white,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -20f), new Vector2(230f, 44f),
                () => ShowPreMatchStats(isMidGamePeek: true));

            CreateButton("BtnNewRandomMatch", _hudRoot.transform, "🎲 새 판 (랜덤 스탯) [R]", 18,
                new Color(0.22f, 0.16f, 0.38f, 0.90f), new Color(1f, 0.88f, 0.42f),
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -72f), new Vector2(230f, 44f),
                () => _onNextMatchCallback?.Invoke());

            // 2. 좌측 하단 피파 조작키 미니 가이드
            GameObject keyGuideBox = CreatePanel("ControlsMiniBox", _hudRoot.transform,
                new Color(0.04f, 0.07f, 0.13f, 0.78f),
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(22f, 22f), new Vector2(510f, 220f));

            string guideStr =
                "<color=#FFD94A><b>[피파 조작키 가이드]</b></color>\n" +
                "• <b>방향키 (↑↓←→)</b> : 이동   |   <b>E</b> : 질주(달리기)\n" +
                "• <b>D</b> : 슛 (게이지)   |   <b>Z + D</b> : <color=#58E6FF>감아차기 슛</color>\n" +
                "• <b>Q</b> (또는 <b>A</b>) : <color=#FF9E44>볼 띄우기 (오버헤드킥 셋업!)</color>\n" +
                "• <color=#FFE866><b>공중볼 근처에서 D</b> : <b>오버헤드킥 / 시저스 발리!!</b></color>\n" +
                "• <b>Shift</b> : 치고 달리기   |   <b>Space</b> : 스탠딩 태클(수비)\n" +
                "• <b>A</b>(수비) : 슬라이딩 태클   |   <b>C</b> : 등지기 자세\n" +
                "• <color=#FF8888>위험한 백태클·슬라이딩 시 반칙 & 직접 프리킥 선언!</color>";

            CreateText("ControlsGuideText", keyGuideBox.transform, guideStr, 17, FontStyle.Normal,
                new Color(0.92f, 0.95f, 1f), TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.one, new Vector2(16f, 10f), new Vector2(-12f, -10f));

            // 3. 선수 머리 위 월드-스크린 추적 위젯 (슛 게이지 + 오버헤드킥 찬스 배지!)
            GameObject trackerObj = CreateUiElement("PlayerWorldTracker", _hudRoot.transform);
            _playerTrackerRect = trackerObj.GetComponent<RectTransform>();
            _playerTrackerRect.sizeDelta = new Vector2(260f, 90f);

            _shotGaugeContainer = CreatePanel("ShotGaugeBg", trackerObj.transform,
                new Color(0.05f, 0.06f, 0.09f, 0.92f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 0f), new Vector2(168f, 22f));

            GameObject fillObj = CreatePanel("ShotGaugeFill", _shotGaugeContainer.transform,
                new Color(0.25f, 1.0f, 0.45f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f),
                new Vector2(3f, 3f), new Vector2(-6f, -6f));
            _shotGaugeFill = fillObj.GetComponent<UnityEngine.UI.Image>();
            _shotGaugeFill.type = UnityEngine.UI.Image.Type.Filled;
            _shotGaugeFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            _shotGaugeFill.fillOrigin = 0;

            _shotGaugeLabel = CreateText("GaugeTypeText", _shotGaugeContainer.transform, "POWER SHOT", 13, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _shotGaugeContainer.SetActive(false);

            _acrobaticPromptBadge = CreatePanel("AcrobaticChanceBadge", trackerObj.transform,
                new Color(0.96f, 0.36f, 0.08f, 0.96f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 0f), new Vector2(256f, 40f));

            _acrobaticPromptText = CreateText("AcrobaticPromptLabel", _acrobaticPromptBadge.transform,
                "⚡ 오버헤드킥 찬스! [D]", 19, FontStyle.Bold,
                new Color(1f, 0.98f, 0.78f), TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _acrobaticPromptBadge.SetActive(false);

            // 4. 직접 프리킥 전용 골문 타겟 조준선 & 상단 가이드 바
            _freeKickHudRoot = CreateUiElement("FreeKickHUD", _hudRoot.transform);
            StretchFull(_freeKickHudRoot.GetComponent<RectTransform>());

            GameObject fkTopInfo = CreatePanel("FreeKickInstructionBar", _freeKickHudRoot.transform,
                new Color(0.08f, 0.14f, 0.28f, 0.94f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -104f), new Vector2(980f, 50f));
            _freeKickBannerText = CreateText("FreeKickInstructionText", fkTopInfo.transform, "", 20, FontStyle.Bold,
                new Color(1f, 0.94f, 0.45f), TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, new Vector2(14f, 2f), new Vector2(-14f, -2f));

            GameObject reticleRoot = CreateUiElement("FreeKickAimReticle", _freeKickHudRoot.transform);
            _freeKickReticleRect = reticleRoot.GetComponent<RectTransform>();
            _freeKickReticleRect.sizeDelta = new Vector2(90f, 90f);

            _freeKickCrosshairParts = new UnityEngine.UI.Image[5];
            _freeKickCrosshairParts[0] = CreatePanel("CenterDot", reticleRoot.transform, Color.yellow,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(10f, 10f)).GetComponent<UnityEngine.UI.Image>();
            _freeKickCrosshairParts[1] = CreatePanel("TickTop", reticleRoot.transform, Color.yellow,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 24f), new Vector2(5f, 20f)).GetComponent<UnityEngine.UI.Image>();
            _freeKickCrosshairParts[2] = CreatePanel("TickBottom", reticleRoot.transform, Color.yellow,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -24f), new Vector2(5f, 20f)).GetComponent<UnityEngine.UI.Image>();
            _freeKickCrosshairParts[3] = CreatePanel("TickLeft", reticleRoot.transform, Color.yellow,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-24f, 0f), new Vector2(20f, 5f)).GetComponent<UnityEngine.UI.Image>();
            _freeKickCrosshairParts[4] = CreatePanel("TickRight", reticleRoot.transform, Color.yellow,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(24f, 0f), new Vector2(20f, 5f)).GetComponent<UnityEngine.UI.Image>();

            _freeKickModeLabel = CreateText("FkModeBadge", reticleRoot.transform, "🎯 파워 프리킥 [D]", 16, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-115f, 38f), new Vector2(115f, 66f));

            _freeKickGaugeObj = CreatePanel("FkChargeBarBg", reticleRoot.transform,
                new Color(0.05f, 0.07f, 0.12f, 0.94f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -52f), new Vector2(176f, 22f));
            GameObject fkFillObj = CreatePanel("FkChargeFill", _freeKickGaugeObj.transform,
                new Color(0.25f, 1.0f, 0.45f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f),
                new Vector2(3f, 3f), new Vector2(-6f, -6f));
            _freeKickGaugeFill = fkFillObj.GetComponent<UnityEngine.UI.Image>();
            _freeKickGaugeFill.type = UnityEngine.UI.Image.Type.Filled;
            _freeKickGaugeFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            _freeKickGaugeFill.fillOrigin = 0;

            _freeKickHudRoot.SetActive(false);

            // 5. 하단 중앙 슈팅 임팩트 & 약발 판정 실시간 피드백 토스트
            GameObject toastObj = CreatePanel("ShotFeedbackToast", _hudRoot.transform,
                new Color(0.06f, 0.11f, 0.22f, 0.92f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 34f), new Vector2(820f, 54f));
            _toastBg = toastObj.GetComponent<UnityEngine.UI.Image>();
            _toastGroup = toastObj.AddComponent<CanvasGroup>();
            _toastGroup.alpha = 0f;
            _toastGroup.blocksRaycasts = false;

            _toastText = CreateText("ToastMessage", toastObj.transform, "", 21, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(16f, 4f), new Vector2(-16f, -4f));
        }

        private void BuildGoalCelebrationBanner(Transform canvasRoot)
        {
            _goalBannerRoot = CreateUiElement("GoalCelebrationBanner", canvasRoot);
            StretchFull(_goalBannerRoot.GetComponent<RectTransform>());

            GameObject card = CreatePanel("BannerCard", _goalBannerRoot.transform,
                new Color(0.06f, 0.09f, 0.20f, 0.94f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 110f), new Vector2(1060f, 195f));
            _goalBannerCard = card.GetComponent<RectTransform>();

            // 골드 상하 라인 장식
            CreatePanel("TopGoldTrim", card.transform, new Color(1f, 0.82f, 0.18f, 1f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 7f));
            CreatePanel("BottomGoldTrim", card.transform, new Color(1f, 0.82f, 0.18f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 7f));

            _goalMainTitle = CreateText("GoalMainTitle", card.transform, "⚽ GOOOAAAL!!", 68, FontStyle.Bold,
                new Color(1f, 0.90f, 0.25f), TextAnchor.MiddleCenter,
                new Vector2(0f, 0.38f), new Vector2(1f, 0.96f), Vector2.zero, Vector2.zero);

            _goalDetailSubtitle = CreateText("GoalDetailSub", card.transform, "", 26, FontStyle.Bold,
                new Color(0.75f, 0.95f, 1f), TextAnchor.MiddleCenter,
                new Vector2(0f, 0.06f), new Vector2(1f, 0.42f), new Vector2(20f, 0f), new Vector2(-20f, 0f));

            _goalBannerRoot.SetActive(false);
        }

        private void BuildPreMatchStatsOverlay(Transform canvasRoot)
        {
            _preMatchPanel = CreatePanel("PreMatchStatsOverlay", canvasRoot,
                new Color(0.03f, 0.05f, 0.11f, 0.88f),
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _preMatchPanel.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;

            GameObject window = CreatePanel("StatsMainWindow", _preMatchPanel.transform,
                new Color(0.07f, 0.11f, 0.20f, 0.97f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0f), new Vector2(1540f, 920f));

            // 상단 헤더 바
            GameObject header = CreatePanel("WindowHeader", window.transform,
                new Color(0.10f, 0.17f, 0.32f, 1f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 82f));

            _preMatchHeaderTitle = CreateText("HeaderTitle", header.transform,
                "⚽ 1 vs 1 매치업 — 랜덤 선수 스탯 & 체감 프로필", 32, FontStyle.Bold,
                Color.white, TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(0.75f, 1f), new Vector2(32f, 0f), Vector2.zero);

            // 우측 상단 [✕ 나가기 (게임 시작)] 버튼!
            CreateButton("BtnTopRightExitStart", header.transform, "✕ 나가기 (게임 시작)", 21,
                new Color(0.92f, 0.24f, 0.28f, 1f), Color.white,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-22f, 0f), new Vector2(245f, 52f),
                HandleCloseStatWindowClicked);

            // 좌측: 내 선수 카드 / 우측: AI 상대 선수 카드
            _playerCard = BuildPlayerStatCard(window.transform, "MyPlayerCard", "🟦 내 선수 (PLAYER 1)",
                new Color(0.12f, 0.45f, 0.95f), new Vector2(-375f, 22f));

            _aiCard = BuildPlayerStatCard(window.transform, "AiPlayerCard", "🟥 AI 상대 (CPU)",
                new Color(0.92f, 0.20f, 0.26f), new Vector2(375f, 22f));

            // VS 배지
            GameObject vsBadge = CreatePanel("VsCenterBadge", window.transform,
                new Color(1f, 0.80f, 0.16f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 45f), new Vector2(74f, 74f));
            CreateText("VsText", vsBadge.transform, "VS", 30, FontStyle.Bold,
                new Color(0.08f, 0.10f, 0.16f), TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // 하단 안내 및 버튼 영역
            CreateText("StatTipFooter", window.transform,
                "💡 <b>체감 & 슈팅 팁</b>: 키가 작고 가벼울수록 방향전환·가속이 빠르고, 감아차기(<b>Z+D</b>)는 <b>커브+중거리</b>, 오버헤드킥(<b>Q로 띄운 뒤 D</b>)은 <b>아크로바틱+결정력</b>이 높을수록 원더골 확률 급상승! (주발 아닌 발로 차면 정확도 하락)",
                18, FontStyle.Normal, new Color(0.80f, 0.90f, 1.0f), TextAnchor.MiddleCenter,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(36f, 92f), new Vector2(-36f, 138f));

            // 다시 뽑기 버튼
            CreateButton("BtnRerollRandom", window.transform, "🎲 선수 & 스탯 다시 랜덤 뽑기", 22,
                new Color(0.20f, 0.29f, 0.48f, 1f), new Color(0.85f, 0.95f, 1f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-240f, 24f), new Vector2(360f, 60f),
                () => _onRerollPlayersCallback?.Invoke());

            // 메인 나가기 & 경기 시작 버튼
            GameObject mainStartBtn = CreateButton("BtnExitAndStartMatch", window.transform,
                "▶ 창 나가기 & 경기 시작! (Space / Enter)", 24,
                new Color(0.14f, 0.78f, 0.42f, 1f), Color.white,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(210f, 24f), new Vector2(470f, 60f),
                HandleCloseStatWindowClicked);
            _closeBtnLabel = mainStartBtn.GetComponentInChildren<UnityEngine.UI.Text>();
        }

        private StatCardRefs BuildPlayerStatCard(Transform parent, string objName, string teamRoleLabel, Color accentColor, Vector2 centerOffset)
        {
            StatCardRefs refs = new StatCardRefs();

            GameObject card = CreatePanel(objName, parent,
                new Color(0.09f, 0.14f, 0.25f, 0.98f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                centerOffset, new Vector2(685f, 665f));

            CreatePanel("TeamTopBar", card.transform, accentColor,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 8f));

            CreateText("TeamRoleTitle", card.transform, teamRoleLabel, 20, FontStyle.Bold,
                Color.Lerp(accentColor, Color.white, 0.45f), TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0.75f, 1f), new Vector2(24f, -42f), new Vector2(0f, -12f));

            refs.nameText = CreateText("PlayerFamousName", card.transform, "손흥민 (Son)", 31, FontStyle.Bold,
                Color.white, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0.78f, 1f), new Vector2(24f, -88f), new Vector2(0f, -42f));

            refs.archetypeText = CreateText("ArchetypeBadge", card.transform, "⚡ 초민첩 드리블러", 19, FontStyle.Bold,
                new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0.78f, 1f), new Vector2(24f, -120f), new Vector2(0f, -88f));

            // OVR 배지
            GameObject ovrBox = CreatePanel("OvrBox", card.transform, accentColor,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -22f), new Vector2(108f, 94f));
            refs.ovrBadgeBg = ovrBox.GetComponent<UnityEngine.UI.Image>();

            CreateText("OvrSmall", ovrBox.transform, "OVR", 16, FontStyle.Bold,
                new Color(1f, 1f, 1f, 0.85f), TextAnchor.UpperCenter,
                Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(0f, -8f));

            refs.ovrText = CreateText("OvrNum", ovrBox.transform, "89", 44, FontStyle.Bold,
                Color.white, TextAnchor.LowerCenter,
                Vector2.zero, Vector2.one, new Vector2(0f, 6f), Vector2.zero);

            // 신체 조건 & 주발 & 헤어 요약 박스
            GameObject bioBox = CreatePanel("BioInfoBox", card.transform,
                new Color(0.05f, 0.08f, 0.16f, 0.92f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -132f), new Vector2(-44f, 106f));

            refs.physicalInfoText = CreateText("PhysicalInfo", bioBox.transform, "", 19, FontStyle.Bold,
                new Color(0.92f, 0.96f, 1f), TextAnchor.UpperLeft,
                Vector2.zero, Vector2.one, new Vector2(16f, 8f), new Vector2(-16f, -10f));

            refs.feelSummaryText = CreateText("FeelSummary", bioBox.transform, "", 17, FontStyle.Normal,
                new Color(0.48f, 0.95f, 0.72f), TextAnchor.LowerLeft,
                Vector2.zero, Vector2.one, new Vector2(16f, 10f), new Vector2(-16f, -48f));

            // 8개 세부 스탯 게이지 행
            for (int i = 0; i < 8; i++)
            {
                float rowY = -258f - i * 48f;
                GameObject row = CreateUiElement("StatRow_" + i, card.transform);
                RectTransform rt = row.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, rowY);
                rt.sizeDelta = new Vector2(-48f, 38f);

                CreateText("StatName", row.transform, StatLabels[i], 18, FontStyle.Bold,
                    new Color(0.82f, 0.88f, 0.96f), TextAnchor.MiddleLeft,
                    new Vector2(0f, 0f), new Vector2(0.40f, 1f), Vector2.zero, Vector2.zero);

                GameObject barBg = CreatePanel("BarBg", row.transform,
                    new Color(0.04f, 0.06f, 0.12f, 1f),
                    new Vector2(0.41f, 0.22f), new Vector2(0.87f, 0.78f), new Vector2(0f, 0.5f),
                    Vector2.zero, Vector2.zero);

                GameObject barFill = CreatePanel("BarFill", barBg.transform,
                    new Color(0.2f, 0.85f, 0.5f, 1f),
                    Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
                UnityEngine.UI.Image fillImg = barFill.GetComponent<UnityEngine.UI.Image>();
                fillImg.type = UnityEngine.UI.Image.Type.Filled;
                fillImg.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
                fillImg.fillOrigin = 0;
                refs.statBarFills[i] = fillImg;

                refs.statValueTexts[i] = CreateText("StatVal", row.transform, "85", 22, FontStyle.Bold,
                    Color.white, TextAnchor.MiddleRight,
                    new Vector2(0.88f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            }

            return refs;
        }

        private void BuildMatchEndOverlay(Transform canvasRoot)
        {
            _matchEndPanel = CreatePanel("MatchEndOverlay", canvasRoot,
                new Color(0.02f, 0.04f, 0.10f, 0.86f),
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _matchEndPanel.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;

            GameObject box = CreatePanel("ResultCard", _matchEndPanel.transform,
                new Color(0.08f, 0.13f, 0.24f, 0.98f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(740f, 430f));

            _matchEndTitle = CreateText("ResultTitle", box.transform, "🏆 경기 종료 — 승리!", 44, FontStyle.Bold,
                new Color(1f, 0.88f, 0.25f), TextAnchor.MiddleCenter,
                new Vector2(0f, 0.65f), new Vector2(1f, 0.92f), Vector2.zero, Vector2.zero);

            _matchEndScore = CreateText("ResultScore", box.transform, "3  :  1", 62, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.36f), new Vector2(1f, 0.66f), Vector2.zero, Vector2.zero);

            CreateButton("BtnPlayNextRandomMatch", box.transform,
                "🎲 다음 판 하기 (새 랜덤 선수 스탯 배정!)", 24,
                new Color(0.15f, 0.76f, 0.42f, 1f), Color.white,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 44f), new Vector2(520f, 68f),
                () =>
                {
                    _matchEndPanel.SetActive(false);
                    _onNextMatchCallback?.Invoke();
                });

            _matchEndPanel.SetActive(false);
        }

        public void PopulatePlayerProfiles(PlayerProfile playerProf, PlayerProfile aiProf)
        {
            FillCard(_playerCard, playerProf);
            FillCard(_aiCard, aiProf);

            if (_scoreLeftName != null && playerProf != null)
                _scoreLeftName.text = $"{playerProf.playerName}";
            if (_scoreRightName != null && aiProf != null)
                _scoreRightName.text = $"{aiProf.playerName} (AI)";
        }

        private void FillCard(StatCardRefs card, PlayerProfile p)
        {
            if (card == null || p == null) return;

            card.nameText.text = $"#{p.jerseyNumber}  {p.playerName}";
            card.archetypeText.text = p.archetypeTag;
            card.ovrText.text = p.overall.ToString();

            string stars = new string('★', p.weakFootStars) + new string('☆', Mathf.Max(0, 5 - p.weakFootStars));
            card.physicalInfoText.text =
                $"신체: <b>{p.heightCm} cm / {p.weightKg} kg</b>   |   헤어: <b>{p.HairStyleKorean}</b>\n" +
                $"주발: <color=#FFE048><b>{p.PreferredFootKorean}</b></color>   |   약발 정확도: {stars}";

            card.feelSummaryText.text = $"체감: {p.BodyFeelSummary}";

            int[] vals =
            {
                p.pace, p.agility, p.finishing, p.shotPower,
                p.longShots, p.curve, p.acrobatics, p.physical
            };

            for (int i = 0; i < 8; i++)
            {
                int v = vals[i];
                card.statValueTexts[i].text = v.ToString();
                card.statBarFills[i].fillAmount = Mathf.Clamp01(v / 100f);

                Color statCol;
                if (v >= 90) statCol = new Color(0.20f, 0.95f, 0.92f);      // S급 시안
                else if (v >= 82) statCol = new Color(0.25f, 0.92f, 0.45f); // A급 그린
                else if (v >= 72) statCol = new Color(0.98f, 0.80f, 0.22f); // B급 골드
                else statCol = new Color(0.95f, 0.48f, 0.25f);              // C급 오렌지

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

            if (_preMatchHeaderTitle != null)
            {
                _preMatchHeaderTitle.text = isMidGamePeek
                    ? "📊 현재 매치 선수 스탯 확인 (나가기 버튼을 누르면 경기 재개)"
                    : "⚽ 경기 입장 전 — 랜덤 배정 선수 스탯 & 체감 확인";
            }
            if (_closeBtnLabel != null)
            {
                _closeBtnLabel.text = isMidGamePeek
                    ? "▶ 창 나가기 & 경기 계속하기! (Space / Esc)"
                    : "▶ 창 나가기 & 1vs1 경기 시작! (Space / Enter)";
            }
        }

        public void CloseStatWindowAndStart()
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

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

        public void UpdateScoreAndTimer(int playerScore, int aiScore, float remainingSeconds)
        {
            if (_scoreNumbersText != null)
                _scoreNumbersText.text = $"{playerScore}  :  {aiScore}";

            if (_timerText != null)
            {
                int totalSec = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
                int mins = totalSec / 60;
                int secs = totalSec % 60;
                _timerText.text = $"{mins:00}:{secs:00}";
            }
        }

        public void UpdatePlayerWorldHud(SoccerPlayerController player)
        {
            if (player == null || _playerTrackerRect == null || !_hudRoot.activeSelf)
                return;

            if (_mainCam == null) _mainCam = Camera.main;
            if (_mainCam == null) return;

            Vector3 headWorld = player.transform.position + Vector3.up * (2.25f * (player.Profile != null ? player.Profile.HeightScale : 1f));
            Vector3 screenPt = _mainCam.WorldToScreenPoint(headWorld);

            if (screenPt.z > 0f)
            {
                RectTransform canvasRt = _mainCanvas.GetComponent<RectTransform>();
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screenPt, null, out Vector2 localPt);
                _playerTrackerRect.anchoredPosition = localPt;
            }

            // 슛 게이지 표시
            bool showGauge = player.IsChargingShot;
            if (_shotGaugeContainer.activeSelf != showGauge)
                _shotGaugeContainer.SetActive(showGauge);

            if (showGauge)
            {
                float c = player.CurrentShotCharge01;
                _shotGaugeFill.fillAmount = c;
                if (player.IsFinesseCharge)
                {
                    _shotGaugeFill.color = new Color(0.20f, 0.92f, 1.0f);
                    _shotGaugeLabel.text = "🌀 감아차기 (Z+D)";
                }
                else
                {
                    _shotGaugeFill.color = Color.Lerp(new Color(0.25f, 1f, 0.4f), new Color(1f, 0.30f, 0.15f), c);
                    _shotGaugeLabel.text = "💥 파워 슛 (D)";
                }
            }

            // 공중볼 오버헤드킥 / 아크로바틱 찬스 즉시 알림 배지!
            bool showAcro = player.CanDoAcrobaticNow && !player.IsPerformingAcrobatic;
            if (_acrobaticPromptBadge.activeSelf != showAcro)
                _acrobaticPromptBadge.SetActive(showAcro);

            if (showAcro)
            {
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 16f) * 0.08f;
                _acrobaticPromptBadge.transform.localScale = new Vector3(pulse, pulse, 1f);
                _acrobaticPromptText.text = player.AvailableAcrobaticType == ShotType.OverheadKick
                    ? "🔥 오버헤드킥 찬스!! [D]"
                    : "⚡ 시저스 발리 찬스!! [D]";
            }
        }

        public void UpdateFreeKickHud(bool active, bool isHumanKicker, Vector3 worldAimPoint, float charge01, bool isCurveMode)
        {
            if (_freeKickHudRoot == null) return;
            if (_freeKickHudRoot.activeSelf != active)
                _freeKickHudRoot.SetActive(active);

            if (!active) return;

            if (isHumanKicker)
            {
                _freeKickBannerText.text =
                    "🎯 <b>직접 프리킥 찬스!</b>  방향키(↑↓←→): 골문 코스 조준  |  <b>[D]</b> 파워 프리킥  |  <b>[Z + D]</b> <color=#4EF0FF>감아차기 프리킥</color>";
                _freeKickReticleRect.gameObject.SetActive(true);

                if (_mainCam == null) _mainCam = Camera.main;
                if (_mainCam != null)
                {
                    Vector3 sp = _mainCam.WorldToScreenPoint(worldAimPoint);
                    if (sp.z > 0f)
                    {
                        RectTransform canvasRt = _mainCanvas.GetComponent<RectTransform>();
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, null, out Vector2 localPt);
                        _freeKickReticleRect.anchoredPosition = localPt;
                    }
                }

                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 10f) * 0.06f;
                _freeKickReticleRect.localScale = new Vector3(pulse, pulse, 1f);

                Color aimCol = isCurveMode ? new Color(0.22f, 0.94f, 1.0f) : new Color(1.0f, 0.88f, 0.22f);
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
                    _freeKickModeLabel.text = isCurveMode ? "🌀 감아차기 프리킥 [Z+D]" : "💥 무회전 파워 프리킥 [D]";
                    _freeKickModeLabel.color = aimCol;
                }

                bool charging = charge01 > 0.005f;
                if (_freeKickGaugeObj != null && _freeKickGaugeObj.activeSelf != charging)
                    _freeKickGaugeObj.SetActive(charging);

                if (charging && _freeKickGaugeFill != null)
                {
                    _freeKickGaugeFill.fillAmount = charge01;
                    _freeKickGaugeFill.color = isCurveMode
                        ? new Color(0.20f, 0.92f, 1.0f)
                        : Color.Lerp(new Color(0.25f, 1f, 0.45f), new Color(1f, 0.28f, 0.16f), charge01);
                }
            }
            else
            {
                _freeKickBannerText.text =
                    "🧱 <b>상대 직접 프리킥 수비벽!</b>  키커가 발을 휘두를 때 <b>[Space]</b> 키를 눌러 점프 블로킹하세요!";
                _freeKickReticleRect.gameObject.SetActive(false);
            }
        }

        public void ShowShotToast(string message, bool isWeakFootWarning)
        {
            if (_toastText == null || _toastGroup == null) return;
            _toastText.text = message;
            _toastBg.color = isWeakFootWarning
                ? new Color(0.45f, 0.18f, 0.08f, 0.94f)
                : new Color(0.06f, 0.15f, 0.32f, 0.94f);
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
            Vector2 anchoredPos, Vector2 sizeDelta)
        {
            GameObject go = CreateUiElement(name, parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            UnityEngine.UI.Image img = go.AddComponent<UnityEngine.UI.Image>();
            img.sprite = MaterialHelper.GetWhiteSprite();
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
            outline.effectColor = new Color(0f, 0f, 0f, 0.72f);
            outline.effectDistance = new Vector2(1.4f, -1.4f);

            return txt;
        }

        private GameObject CreateButton(
            string name, Transform parent, string label, int fontSize,
            Color bgColor, Color textColor,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnObj = CreatePanel(name, parent, bgColor, anchorMin, anchorMax, pivot, anchoredPos, sizeDelta);
            UnityEngine.UI.Image img = btnObj.GetComponent<UnityEngine.UI.Image>();
            img.raycastTarget = true;

            UnityEngine.UI.Button btn = btnObj.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = img;
            btn.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            UnityEngine.UI.ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            cb.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
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
