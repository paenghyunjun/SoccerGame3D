using System;
using UnityEngine;

namespace Soccer3D.Data
{
    public enum PreferredFoot
    {
        Right = 0,
        Left = 1
    }

    public enum HairStyleType
    {
        ShortCrop = 0,       // 깔끔한 숏컷
        SpikyCrest = 1,      // 스파이키 크레스트 (호날두풍)
        BigAfro = 2,         // 빅 아프로 (마르셀루/펠라이니풍)
        TopKnotPonytail = 3, // 포니테일 탑노트 (즐라탄/베일풍)
        LongDreads = 4,      // 장발 드레드 (호나우지뉴/굴리트풍)
        Mohawk = 5,          // 모히칸 (비달/발로텔리풍)
        SidePart = 6,        // 가르마 투블럭 (손흥민/베컴풍)
        HeadbandLong = 7     // 헤어밴드 장발 (홀란/그리즈만풍)
    }

    public enum ShotType
    {
        Normal = 0,
        FinesseCurve = 1,
        OverheadKick = 2,
        AcrobaticVolley = 3,
        LobPass = 4
    }

    public enum ImpactVfxType
    {
        BlazingFire = 0,     // 인페르노 불꽃
        NeonLaser = 1,       // 네온 레이저 빔
        CosmicPlasma = 2,    // 코즈믹 플라즈마
        GoldenLightning = 3, // 골든 썬더 번개
        EmeraldSonic = 4     // 에메랄드 소닉 블래스트
    }

    public enum PositionLine
    {
        Forward = 0,    // 공격수 (공격 스탯 최상, 수비 스탯 낮음)
        Midfielder = 1, // 미드필더 (패스·시야·볼배급 특화, 공수 밸런스)
        Defender = 2,   // 수비수 (수비·몸싸움 최상, 공격 결정력 낮음)
        Goalkeeper = 3  // 골키퍼 (선방 특화)
    }

    [Serializable]
    public struct FormationSlot
    {
        public string positionCode;   // 예: "ST", "LW", "RW", "CAM", "LCM", "RCM", "CDM", "LB", "LCB", "RCB", "RB"
        public PositionLine line;
        public float normDepthX;      // 인플레이 전술 기준 X 깊이 (-0.82 수비 ~ +0.42 최전방)
        public float normLaneZ;       // 인플레이 전술 기준 Z 좌우 폭 (-0.82 좌측면 ~ +0.82 우측면)
        public float kickoffDepthX;   // 킥오프 시 자기 진영 배치 X (-40m ~ -2m)
        public float kickoffLaneZ;    // 킥오프 시 좌우 배치 Z (-26m ~ +26m)

        public FormationSlot(string code, PositionLine posLine, float depthX, float laneZ, float koX, float koZ)
        {
            positionCode = code;
            line = posLine;
            normDepthX = depthX;
            normLaneZ = laneZ;
            kickoffDepthX = koX;
            kickoffLaneZ = koZ;
        }
    }

    [Serializable]
    public class FormationPreset
    {
        public string formationName;
        public string formationTitleKor;
        public string tacticalSummary;
        public FormationSlot[] outfieldSlots; // 10명 필드플레이어 슬롯 (0번 = 메인 스트라이커 1P 시작 포지션)
    }

    [Serializable]
    public class PlayerProfile
    {
        public string playerName;
        public string archetypeTag;
        public string positionCode = "ST";
        public PositionLine positionLine = PositionLine.Forward;
        public int jerseyNumber;

        // 신체 스펙
        public int heightCm;
        public int weightKg;
        public PreferredFoot preferredFoot;
        public int weakFootStars; // 1 ~ 5 (보통 2 ~ 4로 배정해 주발/약발 차이 체감)

        // 외형 랜덤 요소
        public HairStyleType hairStyle;
        public Color hairColor;
        public Color skinColor;
        public Color jerseyPrimaryColor;
        public Color jerseySecondaryColor;
        public Color shortsColor;
        public Color bootColor;

        // 핵심 능력치 (30 ~ 99) — 포지션별로 공격/미드/수비 특화 격차 부여!
        public int pace;        // 속력
        public int agility;     // 민첩성
        public int finishing;   // 골 결정력 (공격수 높음 / 수비수 낮음)
        public int shotPower;   // 슛 파워
        public int longShots;   // 중거리 슛
        public int curve;       // 커브 (감아차기 핵심)
        public int acrobatics;  // 아크로바틱 (오버헤드킥/발리 핵심)
        public int passing;     // 패스/시야 (미드필더 최상)
        public int defense;     // 수비력/태클 (수비수 최상 / 공격수 낮음)
        public int physical;    // 몸싸움/피지컬
        public int overall;     // 포지션 가중치 반영 종합 오버롤

        public int AttackRating => Mathf.RoundToInt(finishing * 0.42f + shotPower * 0.22f + acrobatics * 0.18f + curve * 0.18f);
        public int DefenseRating => Mathf.RoundToInt(defense * 0.65f + physical * 0.35f);

        // 신체 조건(키·몸무게)에 따른 체감 보정 계수
        public float HeightScale => heightCm / 180f;

        public float Bmi
        {
            get
            {
                float hm = heightCm * 0.01f;
                return weightKg / Mathf.Max(0.01f, hm * hm);
            }
        }

        /// <summary>
        /// 체중/BMI 기반 가로 체형 두께 배율 (마른 선수 ~0.86, 근육질/거구 ~1.18)
        /// </summary>
        public float BodyBuildScale => Mathf.Clamp(0.84f + (Bmi - 19.5f) * 0.042f, 0.84f, 1.20f);

        /// <summary>
        /// 키가 작고 가벼울수록 폭발적 가속 & 방향전환 민첩성 상승 (1.18x), 크고 무거울수록 묵직함 (0.82x)
        /// </summary>
        public float PhysicalNimbleness
        {
            get
            {
                float heightNorm = Mathf.InverseLerp(202f, 163f, heightCm); // 작을수록 1에 가까움
                float weightNorm = Mathf.InverseLerp(98f, 56f, weightKg);   // 가벼울수록 1에 가까움
                return Mathf.Lerp(0.80f, 1.22f, heightNorm * 0.55f + weightNorm * 0.45f);
            }
        }

        /// <summary>
        /// 실제 인게임 최고 이동 속도 (m/s) - 속력 스탯 + 신체 가벼움/보폭 반영
        /// </summary>
        public float EffectiveTopSpeed
        {
            get
            {
                float statSpeed = Mathf.Lerp(6.2f, 9.6f, Mathf.InverseLerp(45f, 99f, pace));
                float lightBonus = Mathf.Lerp(-0.45f, 0.65f, Mathf.InverseLerp(98f, 58f, weightKg));
                return Mathf.Clamp(statSpeed + lightBonus, 5.8f, 10.4f);
            }
        }

        /// <summary>
        /// 가속력 (작고 마른 선수가 순간 치고 나가는 속도가 훨씬 빠름)
        /// </summary>
        public float EffectiveAcceleration
        {
            get
            {
                float baseAccel = Mathf.Lerp(14f, 30f, Mathf.InverseLerp(45f, 99f, agility));
                return baseAccel * PhysicalNimbleness;
            }
        }

        /// <summary>
        /// 회전/방향전환 반응 속도 (체감 핵심!)
        /// </summary>
        public float EffectiveTurnSpeed
        {
            get
            {
                float baseTurn = Mathf.Lerp(7.5f, 17.5f, Mathf.InverseLerp(45f, 99f, agility));
                return baseTurn * PhysicalNimbleness;
            }
        }

        /// <summary>
        /// 키가 클수록 공중볼(오버헤드킥/발리) 타점 도달 범위 보너스
        /// </summary>
        public float AerialReachBonus => Mathf.Lerp(-0.10f, 0.38f, Mathf.InverseLerp(165f, 202f, heightCm));

        /// <summary>
        /// 체격이 클수록 슛 임팩트 시 실리는 체중 파워 보너스
        /// </summary>
        public float HeavyShotBonus => Mathf.Lerp(-0.03f, 0.09f, Mathf.InverseLerp(58f, 96f, weightKg));

        public string PreferredFootKorean => preferredFoot == PreferredFoot.Right ? "오른발" : "왼발";

        public string HairStyleKorean
        {
            get
            {
                switch (hairStyle)
                {
                    case HairStyleType.ShortCrop: return "클래식 숏컷";
                    case HairStyleType.SpikyCrest: return "스파이키 크레스트";
                    case HairStyleType.BigAfro: return "빅 아프로";
                    case HairStyleType.TopKnotPonytail: return "탑노트 포니테일";
                    case HairStyleType.LongDreads: return "장발 드레드";
                    case HairStyleType.Mohawk: return "와일드 모히칸";
                    case HairStyleType.SidePart: return "가르마 투블럭";
                    case HairStyleType.HeadbandLong: return "헤어밴드 장발";
                    default: return "커스텀 헤어";
                }
            }
        }

        public string BodyFeelSummary
        {
            get
            {
                if (heightCm <= 173 && weightKg <= 68)
                    return "초경량 테크니션 → 폭발적 가속 · 칼같은 방향전환 · 잔발 드리블 최상";
                if (heightCm <= 178 && weightKg <= 73)
                    return "날렵한 밸런스형 → 빠릿빠릿한 체감 · 민첩한 퍼스트터치";
                if (heightCm >= 190 && weightKg >= 86)
                    return "장신 헤비급 전차 → 묵직한 방향전환 · 대포알 슛파워 · 높은 오버헤드 타점";
                if (heightCm >= 187)
                    return "장신 타겟형 → 넓은 공중볼 타점 · 시원한 보폭 · 강한 몸싸움";
                if (weightKg >= 84)
                    return "단단한 근육질 → 강력한 슛 임팩트 · 몸싸움 우위 · 다소 묵직한 선회";
                return "표준 올라운더 체형 → 공수 밸런스와 안정적인 슈팅 밸런스";
            }
        }

        public string PositionSpecialtySummary
        {
            get
            {
                return positionLine switch
                {
                    PositionLine.Forward => "🔥 공격 특화 (결정력·슈팅 최상 / 수비력 낮음)",
                    PositionLine.Midfielder => "🎯 중원 특화 (패스·시야·볼배급 최상 / 공수 밸런스)",
                    PositionLine.Defender => "🛡️ 수비 특화 (태클·몸싸움 최상 / 공격력 낮음)",
                    _ => "🧤 골키퍼 특화 (슈퍼 세이브·반사신경·공중볼 장악 최상)"
                };
            }
        }
    }

    public static class PlayerProfileGenerator
    {
        private static readonly string[] FamousGoalkeeperNames =
        {
            "조현우 (Jo Hyeon-woo)", "알리송 베케르 (Alisson)", "티보 쿠르투아 (Courtois)",
            "마누엘 노이어 (Neuer)", "잔루이지 부폰 (Buffon)", "이케르 카시야스 (Casillas)",
            "이운재 (Lee Woon-jae)", "에데르송 (Ederson)", "얀 오블락 (Oblak)",
            "다비드 데 헤아 (De Gea)", "에밀리아노 마르티네스 (Dibu)", "잔루이지 돈나룸마 (Donnarumma)",
            "페트르 체흐 (Čech)", "올리버 칸 (Kahn)", "에드윈 판 데르 사르 (Van der Sar)"
        };

        private static readonly string[] FamousManagerNames =
        {
            "거스 히딩크 (Guus Hiddink)", "펩 과르디올라 (Pep Guardiola)", "위르겐 클롭 (Jürgen Klopp)",
            "카를로 안첼로티 (Carlo Ancelotti)", "알렉스 퍼거슨 (Sir Alex Ferguson)", "조세 무리뉴 (José Mourinho)",
            "파울루 벤투 (Paulo Bento)", "아르센 벵거 (Arsène Wenger)", "지네딘 지단 (Zinedine Zidane)",
            "루이스 엔리케 (Luis Enrique)", "토마스 투헬 (Thomas Tuchel)", "디에고 시메오네 (Diego Simeone)"
        };

        public static string PickRandomManagerName(string exclude = null)
        {
            string picked = FamousManagerNames[UnityEngine.Random.Range(0, FamousManagerNames.Length)];
            int guard = 0;
            while (!string.IsNullOrEmpty(exclude) && picked == exclude && guard++ < 20)
            {
                picked = FamousManagerNames[UnityEngine.Random.Range(0, FamousManagerNames.Length)];
            }
            return picked;
        }
        private static readonly string[] FamousNames =
        {
            "손흥민 (Son)", "크리스티아누 호날두 (Ronaldo)", "리오넬 메시 (Messi)",
            "킬리안 음바페 (Mbappé)", "엘링 홀란 (Haaland)", "즐라탄 이브라히모비치 (Zlatan)",
            "호나우지뉴 (Ronaldinho)", "비니시우스 Jr. (Vinícius)", "네이마르 Jr. (Neymar)",
            "웨인 루니 (Rooney)", "가레스 베일 (Bale)", "모하메드 살라 (Salah)",
            "라민 야말 (Yamal)", "티에리 앙리 (Henry)", "디디에 드록바 (Drogba)",
            "로베르트 레반도프스키 (Lewandowski)", "루이스 수아레스 (Suárez)", "카림 벤제마 (Benzema)",
            "앙투안 그리즈만 (Griezmann)", "해리 케인 (Kane)", "호나우두 (R9)", "사뮈엘 에투 (Eto'o)"
        };

        private static readonly string[] FamousMidfielderNames =
        {
            "케빈 더 브라위너 (De Bruyne)", "주드 벨링엄 (Bellingham)", "이강인 (Lee Kang-in)",
            "박지성 (Park Ji-sung)", "데이비드 베컴 (Beckham)", "루카 모드리치 (Modrić)",
            "지네딘 지단 (Zidane)", "안드레스 이니에스타 (Iniesta)", "토니 크로스 (Kroos)",
            "로드리 (Rodri)", "페데리코 발베르데 (Valverde)", "자말 무시알라 (Musiala)",
            "플로리안 비르츠 (Wirtz)", "페드리 (Pedri)", "마르틴 외데고르 (Ødegaard)",
            "안드레아 피를로 (Pirlo)", "은골로 캉테 (Kanté)", "카세미루 (Casemiro)"
        };

        private static readonly string[] FamousDefenderNames =
        {
            "김민재 (Kim Min-jae)", "버질 반 다이크 (Van Dijk)", "세르히오 라모스 (Ramos)",
            "파올로 말디니 (Maldini)", "안토니오 뤼디거 (Rüdiger)", "후벵 디아스 (Rúben Dias)",
            "윌리엄 살리바 (Saliba)", "요슈코 그바르디올 (Gvardiol)", "알폰소 데이비스 (Davies)",
            "아슈라프 하키미 (Hakimi)", "카를레스 푸욜 (Puyol)", "알레산드로 네스타 (Nesta)",
            "마르키뉴스 (Marquinhos)", "테오 에르난데스 (Theo)", "트렌트 알렉산더-아놀드 (Arnold)",
            "홍명보 (Hong Myung-bo)", "히카르두 카르발류 (Carvalho)", "존 테리 (Terry)"
        };

        private static readonly FormationPreset[] AllFormations =
        {
            new FormationPreset
            {
                formationName = "4-3-3",
                formationTitleKor = "4-3-3 어택 (정통 스리톱)",
                tacticalSummary = "윙포워드 침투와 역삼각형 미드필더의 공격 지향 전술",
                outfieldSlots = new[]
                {
                    new FormationSlot("ST",  PositionLine.Forward,    +0.36f,  0.00f, -2.2f,   0.0f),
                    new FormationSlot("LW",  PositionLine.Forward,    +0.26f, -0.68f, -6.5f, -19.5f),
                    new FormationSlot("RW",  PositionLine.Forward,    +0.26f, +0.68f, -6.5f, +19.5f),
                    new FormationSlot("LCM", PositionLine.Midfielder, -0.04f, -0.34f, -14.5f, -10.5f),
                    new FormationSlot("RCM", PositionLine.Midfielder, -0.04f, +0.34f, -14.5f, +10.5f),
                    new FormationSlot("CDM", PositionLine.Midfielder, -0.24f,  0.00f, -21.0f,   0.0f),
                    new FormationSlot("LB",  PositionLine.Defender,   -0.54f, -0.72f, -31.0f, -21.5f),
                    new FormationSlot("LCB", PositionLine.Defender,   -0.66f, -0.25f, -35.5f,  -7.8f),
                    new FormationSlot("RCB", PositionLine.Defender,   -0.66f, +0.25f, -35.5f,  +7.8f),
                    new FormationSlot("RB",  PositionLine.Defender,   -0.54f, +0.72f, -31.0f, +21.5f)
                }
            },
            new FormationPreset
            {
                formationName = "4-4-2",
                formationTitleKor = "4-4-2 클래식 (투톱 & 두줄 수비)",
                tacticalSummary = "투톱 연계와 탄탄한 4미드필더-4백 밸런스 전술",
                outfieldSlots = new[]
                {
                    new FormationSlot("LS",  PositionLine.Forward,    +0.35f, -0.22f, -2.2f,  -4.5f),
                    new FormationSlot("RS",  PositionLine.Forward,    +0.35f, +0.22f, -4.2f,  +5.5f),
                    new FormationSlot("LM",  PositionLine.Midfielder, +0.04f, -0.70f, -13.5f, -20.5f),
                    new FormationSlot("LCM", PositionLine.Midfielder, -0.12f, -0.24f, -16.5f,  -7.2f),
                    new FormationSlot("RCM", PositionLine.Midfielder, -0.12f, +0.24f, -16.5f,  +7.2f),
                    new FormationSlot("RM",  PositionLine.Midfielder, +0.04f, +0.70f, -13.5f, +20.5f),
                    new FormationSlot("LB",  PositionLine.Defender,   -0.55f, -0.72f, -31.5f, -21.5f),
                    new FormationSlot("LCB", PositionLine.Defender,   -0.66f, -0.25f, -35.5f,  -7.8f),
                    new FormationSlot("RCB", PositionLine.Defender,   -0.66f, +0.25f, -35.5f,  +7.8f),
                    new FormationSlot("RB",  PositionLine.Defender,   -0.55f, +0.72f, -31.5f, +21.5f)
                }
            },
            new FormationPreset
            {
                formationName = "4-2-3-1",
                formationTitleKor = "4-2-3-1 현대 밸런스 (더블 볼란치)",
                tacticalSummary = "2선 플레이메이커(CAM)와 더블 수비형 미드필더의 공수 조화",
                outfieldSlots = new[]
                {
                    new FormationSlot("ST",  PositionLine.Forward,    +0.38f,  0.00f, -2.2f,   0.0f),
                    new FormationSlot("LAM", PositionLine.Forward,    +0.16f, -0.64f, -9.5f, -18.5f),
                    new FormationSlot("CAM", PositionLine.Midfielder, +0.14f,  0.00f, -10.5f,   0.0f),
                    new FormationSlot("RAM", PositionLine.Forward,    +0.16f, +0.64f, -9.5f, +18.5f),
                    new FormationSlot("LDM", PositionLine.Midfielder, -0.22f, -0.28f, -20.5f,  -8.5f),
                    new FormationSlot("RDM", PositionLine.Midfielder, -0.22f, +0.28f, -20.5f,  +8.5f),
                    new FormationSlot("LB",  PositionLine.Defender,   -0.54f, -0.72f, -31.0f, -21.5f),
                    new FormationSlot("LCB", PositionLine.Defender,   -0.66f, -0.25f, -35.5f,  -7.8f),
                    new FormationSlot("RCB", PositionLine.Defender,   -0.66f, +0.25f, -35.5f,  +7.8f),
                    new FormationSlot("RB",  PositionLine.Defender,   -0.54f, +0.72f, -31.0f, +21.5f)
                }
            },
            new FormationPreset
            {
                formationName = "3-5-2",
                formationTitleKor = "3-5-2 스리백 & 윙백 오버래핑",
                tacticalSummary = "두터운 5미드필더 중원 장악과 투톱 카운터 어택",
                outfieldSlots = new[]
                {
                    new FormationSlot("LS",  PositionLine.Forward,    +0.36f, -0.22f, -2.2f,  -4.5f),
                    new FormationSlot("RS",  PositionLine.Forward,    +0.36f, +0.22f, -4.2f,  +5.5f),
                    new FormationSlot("CAM", PositionLine.Midfielder, +0.12f,  0.00f, -11.0f,   0.0f),
                    new FormationSlot("LWB", PositionLine.Midfielder, -0.08f, -0.76f, -16.5f, -23.0f),
                    new FormationSlot("RWB", PositionLine.Midfielder, -0.08f, +0.76f, -16.5f, +23.0f),
                    new FormationSlot("LCM", PositionLine.Midfielder, -0.20f, -0.32f, -19.5f,  -9.5f),
                    new FormationSlot("RCM", PositionLine.Midfielder, -0.20f, +0.32f, -19.5f,  +9.5f),
                    new FormationSlot("LCB", PositionLine.Defender,   -0.64f, -0.46f, -34.0f, -14.0f),
                    new FormationSlot("CB",  PositionLine.Defender,   -0.68f,  0.00f, -36.5f,   0.0f),
                    new FormationSlot("RCB", PositionLine.Defender,   -0.64f, +0.46f, -34.0f, +14.0f)
                }
            },
            new FormationPreset
            {
                formationName = "3-4-3",
                formationTitleKor = "3-4-3 하이프레싱 (초공격 스리백)",
                tacticalSummary = "전방 스리톱과 좌우 윙어의 파상공세 화력 전술",
                outfieldSlots = new[]
                {
                    new FormationSlot("ST",  PositionLine.Forward,    +0.38f,  0.00f, -2.2f,   0.0f),
                    new FormationSlot("LW",  PositionLine.Forward,    +0.28f, -0.66f, -6.5f, -19.0f),
                    new FormationSlot("RW",  PositionLine.Forward,    +0.28f, +0.66f, -6.5f, +19.0f),
                    new FormationSlot("LM",  PositionLine.Midfielder, -0.05f, -0.74f, -15.5f, -22.5f),
                    new FormationSlot("LCM", PositionLine.Midfielder, -0.14f, -0.25f, -17.5f,  -7.5f),
                    new FormationSlot("RCM", PositionLine.Midfielder, -0.14f, +0.25f, -17.5f,  +7.5f),
                    new FormationSlot("RM",  PositionLine.Midfielder, -0.05f, +0.74f, -15.5f, +22.5f),
                    new FormationSlot("LCB", PositionLine.Defender,   -0.64f, -0.46f, -34.0f, -14.0f),
                    new FormationSlot("CB",  PositionLine.Defender,   -0.68f,  0.00f, -36.5f,   0.0f),
                    new FormationSlot("RCB", PositionLine.Defender,   -0.64f, +0.46f, -34.0f, +14.0f)
                }
            }
        };

        public static FormationPreset PickRandomFormation()
        {
            return AllFormations[UnityEngine.Random.Range(0, AllFormations.Length)];
        }

        private static readonly Color[] SkinTones =
        {
            new Color(0.96f, 0.80f, 0.69f),
            new Color(0.91f, 0.73f, 0.58f),
            new Color(0.82f, 0.61f, 0.44f),
            new Color(0.65f, 0.45f, 0.31f),
            new Color(0.45f, 0.30f, 0.21f),
            new Color(0.32f, 0.21f, 0.15f)
        };

        private static readonly Color[] HairColors =
        {
            new Color(0.09f, 0.08f, 0.08f), // 흑발
            new Color(0.24f, 0.15f, 0.09f), // 다크 브라운
            new Color(0.45f, 0.28f, 0.14f), // 체스트넛 브라운
            new Color(0.92f, 0.80f, 0.38f), // 금발
            new Color(0.92f, 0.92f, 0.95f), // 플래티넘 실버
            new Color(0.85f, 0.25f, 0.35f), // 개성 레드/핑크
            new Color(0.20f, 0.65f, 0.95f), // 네온 블루
            new Color(0.95f, 0.55f, 0.15f)  // 오렌지 골드
        };

        private static readonly Color[] BootColors =
        {
            new Color(1.0f, 0.30f, 0.12f),
            new Color(0.15f, 0.95f, 0.55f),
            new Color(0.98f, 0.88f, 0.15f),
            new Color(0.25f, 0.70f, 1.0f),
            new Color(0.95f, 0.20f, 0.75f),
            new Color(0.96f, 0.96f, 0.96f)
        };

        public static PlayerProfile GenerateRandom(bool isPlayerTeam, string excludeName = null)
        {
            var p = new PlayerProfile();

            // 이름 중복 방지 랜덤 선택
            string chosenName = FamousNames[UnityEngine.Random.Range(0, FamousNames.Length)];
            int guard = 0;
            while (!string.IsNullOrEmpty(excludeName) && chosenName == excludeName && guard++ < 20)
            {
                chosenName = FamousNames[UnityEngine.Random.Range(0, FamousNames.Length)];
            }
            p.playerName = chosenName;
            p.jerseyNumber = UnityEngine.Random.Range(7, 31);

            // 아키타입 랜덤 결정 (매 판마다 개성 확실한 체감 부여)
            int archetype = UnityEngine.Random.Range(0, 6);
            switch (archetype)
            {
                case 0: // 작고 날렵한 크랙 드리블러
                    p.archetypeTag = "⚡ 초민첩 드리블러";
                    p.heightCm = UnityEngine.Random.Range(164, 175);
                    p.weightKg = Mathf.RoundToInt(Mathf.Lerp(57f, 68f, UnityEngine.Random.value));
                    p.pace = UnityEngine.Random.Range(86, 99);
                    p.agility = UnityEngine.Random.Range(88, 99);
                    p.finishing = UnityEngine.Random.Range(74, 93);
                    p.shotPower = UnityEngine.Random.Range(68, 86);
                    p.longShots = UnityEngine.Random.Range(68, 88);
                    p.curve = UnityEngine.Random.Range(78, 96);
                    p.acrobatics = UnityEngine.Random.Range(80, 96);
                    p.physical = UnityEngine.Random.Range(52, 74);
                    break;

                case 1: // 아크로바틱 스페셜리스트 (오버헤드킥 장인)
                    p.archetypeTag = "🤸 아크로바틱 폭격기";
                    p.heightCm = UnityEngine.Random.Range(176, 193);
                    p.weightKg = Mathf.RoundToInt(Mathf.Lerp(70f, 85f, UnityEngine.Random.value));
                    p.pace = UnityEngine.Random.Range(78, 94);
                    p.agility = UnityEngine.Random.Range(80, 95);
                    p.finishing = UnityEngine.Random.Range(84, 98);
                    p.shotPower = UnityEngine.Random.Range(82, 97);
                    p.longShots = UnityEngine.Random.Range(72, 90);
                    p.curve = UnityEngine.Random.Range(70, 90);
                    p.acrobatics = UnityEngine.Random.Range(90, 99);
                    p.physical = UnityEngine.Random.Range(74, 92);
                    break;

                case 2: // 감아차기 & 중거리 스나이퍼
                    p.archetypeTag = "🌀 감아차기 마에스트로";
                    p.heightCm = UnityEngine.Random.Range(170, 185);
                    p.weightKg = Mathf.RoundToInt(Mathf.Lerp(64f, 79f, UnityEngine.Random.value));
                    p.pace = UnityEngine.Random.Range(75, 92);
                    p.agility = UnityEngine.Random.Range(78, 94);
                    p.finishing = UnityEngine.Random.Range(80, 96);
                    p.shotPower = UnityEngine.Random.Range(78, 94);
                    p.longShots = UnityEngine.Random.Range(88, 99);
                    p.curve = UnityEngine.Random.Range(90, 99);
                    p.acrobatics = UnityEngine.Random.Range(74, 92);
                    p.physical = UnityEngine.Random.Range(64, 84);
                    break;

                case 3: // 장신 헤비급 전차 스트라이커
                    p.archetypeTag = "💥 헤비급 캐논 전차";
                    p.heightCm = UnityEngine.Random.Range(189, 202);
                    p.weightKg = Mathf.RoundToInt(Mathf.Lerp(86f, 98f, UnityEngine.Random.value));
                    p.pace = UnityEngine.Random.Range(72, 89);
                    p.agility = UnityEngine.Random.Range(56, 76);
                    p.finishing = UnityEngine.Random.Range(84, 98);
                    p.shotPower = UnityEngine.Random.Range(90, 99);
                    p.longShots = UnityEngine.Random.Range(78, 95);
                    p.curve = UnityEngine.Random.Range(60, 82);
                    p.acrobatics = UnityEngine.Random.Range(76, 95);
                    p.physical = UnityEngine.Random.Range(88, 99);
                    break;

                case 4: // 중거리 레이저 포격수
                    p.archetypeTag = "🚀 중거리 레이저포";
                    p.heightCm = UnityEngine.Random.Range(178, 190);
                    p.weightKg = Mathf.RoundToInt(Mathf.Lerp(73f, 86f, UnityEngine.Random.value));
                    p.pace = UnityEngine.Random.Range(74, 90);
                    p.agility = UnityEngine.Random.Range(70, 88);
                    p.finishing = UnityEngine.Random.Range(78, 94);
                    p.shotPower = UnityEngine.Random.Range(89, 99);
                    p.longShots = UnityEngine.Random.Range(89, 99);
                    p.curve = UnityEngine.Random.Range(78, 95);
                    p.acrobatics = UnityEngine.Random.Range(75, 92);
                    p.physical = UnityEngine.Random.Range(76, 92);
                    break;

                default: // 올라운더 밸런스형
                    p.archetypeTag = "👑 육각형 올라운더";
                    p.heightCm = UnityEngine.Random.Range(174, 188);
                    p.weightKg = Mathf.RoundToInt(Mathf.Lerp(68f, 83f, UnityEngine.Random.value));
                    p.pace = UnityEngine.Random.Range(76, 95);
                    p.agility = UnityEngine.Random.Range(76, 95);
                    p.finishing = UnityEngine.Random.Range(76, 96);
                    p.shotPower = UnityEngine.Random.Range(76, 96);
                    p.longShots = UnityEngine.Random.Range(75, 95);
                    p.curve = UnityEngine.Random.Range(75, 95);
                    p.acrobatics = UnityEngine.Random.Range(78, 96);
                    p.physical = UnityEngine.Random.Range(72, 92);
                    break;
            }

            // 주발 랜덤 (오른발 68%, 왼발 32%) & 약발 등급 (2~4성 위주)
            p.preferredFoot = UnityEngine.Random.value < 0.68f ? PreferredFoot.Right : PreferredFoot.Left;
            p.weakFootStars = UnityEngine.Random.Range(2, 5); // 2, 3, 4성

            // 외형 완전 랜덤
            var hairValues = (HairStyleType[])Enum.GetValues(typeof(HairStyleType));
            p.hairStyle = hairValues[UnityEngine.Random.Range(0, hairValues.Length)];
            p.hairColor = HairColors[UnityEngine.Random.Range(0, HairColors.Length)];
            p.skinColor = SkinTones[UnityEngine.Random.Range(0, SkinTones.Length)];
            p.bootColor = BootColors[UnityEngine.Random.Range(0, BootColors.Length)];

            if (isPlayerTeam)
            {
                // 플레이어 팀: 청량한 로얄 블루/시안/화이트 계열 랜덤 조합
                Color[] homePrimaries =
                {
                    new Color(0.10f, 0.42f, 0.95f),
                    new Color(0.08f, 0.68f, 0.88f),
                    new Color(0.16f, 0.22f, 0.68f),
                    new Color(0.95f, 0.96f, 0.99f)
                };
                p.jerseyPrimaryColor = homePrimaries[UnityEngine.Random.Range(0, homePrimaries.Length)];
                p.jerseySecondaryColor = new Color(0.98f, 0.84f, 0.22f);
                p.shortsColor = new Color(0.09f, 0.13f, 0.24f);
            }
            else
            {
                // AI 팀: 강렬한 크림슨 레드/오렌지/퍼플 계열 (플레이어와 확실히 구분)
                Color[] awayPrimaries =
                {
                    new Color(0.90f, 0.16f, 0.22f),
                    new Color(0.95f, 0.42f, 0.10f),
                    new Color(0.62f, 0.14f, 0.82f),
                    new Color(0.95f, 0.78f, 0.12f)
                };
                p.jerseyPrimaryColor = awayPrimaries[UnityEngine.Random.Range(0, awayPrimaries.Length)];
                p.jerseySecondaryColor = new Color(0.12f, 0.12f, 0.15f);
                p.shortsColor = new Color(0.14f, 0.10f, 0.14f);
            }

            p.passing = UnityEngine.Random.Range(72, 90);
            p.defense = UnityEngine.Random.Range(32, 52); // 공격수는 수비 스탯이 낮음!
            p.positionCode = "ST";
            p.positionLine = PositionLine.Forward;

            p.overall = Mathf.RoundToInt(
                (p.pace * 0.14f) +
                (p.agility * 0.13f) +
                (p.finishing * 0.22f) +
                (p.shotPower * 0.15f) +
                (p.longShots * 0.10f) +
                (p.curve * 0.11f) +
                (p.acrobatics * 0.10f) +
                (p.physical * 0.05f)
            );

            return p;
        }

        /// <summary>
        /// 11 vs 11 포메이션 슬롯(공격수 / 미드필더 / 수비수)에 맞춰 포지션별 특화 스탯(공격수는 공격 높고 수비 낮음, 수비수는 수비 높고 공격 낮음, 미드필더는 패스·시야 특화) 생성!
        /// </summary>
        public static PlayerProfile GenerateProfileForSlot(
            bool isPlayerTeam,
            FormationSlot slot,
            int slotIndex,
            System.Collections.Generic.HashSet<string> usedNames,
            PlayerProfile teamLeaderProfile = null)
        {
            if (slotIndex == 0 && teamLeaderProfile != null)
            {
                teamLeaderProfile.positionCode = slot.positionCode;
                teamLeaderProfile.positionLine = slot.line;
                return teamLeaderProfile;
            }

            if (slot.line == PositionLine.Forward)
            {
                PlayerProfile fwd = GenerateRandom(isPlayerTeam);
                string chosen = PickUniqueName(FamousNames, usedNames);
                fwd.playerName = chosen;
                fwd.positionCode = slot.positionCode;
                fwd.positionLine = PositionLine.Forward;

                int[] fwdNumbers = { 7, 9, 10, 11, 14, 17, 20, 21 };
                fwd.jerseyNumber = fwdNumbers[(slotIndex * 2 + UnityEngine.Random.Range(0, 3)) % fwdNumbers.Length];

                // 공격수 특화: 공격 스탯(결정력·슛파워·아크로바틱·커브) 매우 높고, 수비 스탯(defense)은 확실히 낮음!
                fwd.finishing = UnityEngine.Random.Range(82, 98);
                fwd.shotPower = UnityEngine.Random.Range(80, 97);
                fwd.acrobatics = UnityEngine.Random.Range(80, 98);
                fwd.passing = UnityEngine.Random.Range(70, 86);
                fwd.defense = UnityEngine.Random.Range(30, 50);

                ApplyTeamKitColors(fwd, isPlayerTeam, teamLeaderProfile);
                fwd.overall = Mathf.RoundToInt(
                    fwd.pace * 0.16f + fwd.agility * 0.14f + fwd.finishing * 0.26f +
                    fwd.shotPower * 0.16f + fwd.curve * 0.12f + fwd.acrobatics * 0.10f + fwd.physical * 0.06f);
                return fwd;
            }

            if (slot.line == PositionLine.Midfielder)
            {
                var mid = new PlayerProfile();
                mid.playerName = PickUniqueName(FamousMidfielderNames, usedNames);
                mid.positionCode = slot.positionCode;
                mid.positionLine = PositionLine.Midfielder;

                int[] midNumbers = { 6, 8, 10, 13, 16, 18, 22, 25 };
                mid.jerseyNumber = midNumbers[(slotIndex * 3 + UnityEngine.Random.Range(0, 3)) % midNumbers.Length];

                bool isDefensiveMid = slot.positionCode.Contains("DM");
                bool isAttackingMid = slot.positionCode.Contains("AM");
                bool isWideMid = slot.positionCode == "LM" || slot.positionCode == "RM" || slot.positionCode == "LWB" || slot.positionCode == "RWB";

                if (isDefensiveMid)
                {
                    mid.archetypeTag = "🛡️ 중원 진공청소기 (수비형 MF)";
                    mid.heightCm = UnityEngine.Random.Range(180, 192);
                    mid.weightKg = UnityEngine.Random.Range(75, 87);
                    mid.pace = UnityEngine.Random.Range(74, 87);
                    mid.agility = UnityEngine.Random.Range(75, 88);
                    mid.passing = UnityEngine.Random.Range(84, 96);
                    mid.defense = UnityEngine.Random.Range(80, 93);
                    mid.physical = UnityEngine.Random.Range(80, 94);
                    mid.finishing = UnityEngine.Random.Range(46, 65);
                    mid.shotPower = UnityEngine.Random.Range(74, 90);
                    mid.longShots = UnityEngine.Random.Range(72, 89);
                    mid.curve = UnityEngine.Random.Range(70, 86);
                    mid.acrobatics = UnityEngine.Random.Range(55, 74);
                }
                else if (isAttackingMid)
                {
                    mid.archetypeTag = "🎨 천재 플레이메이커 (공격형 MF)";
                    mid.heightCm = UnityEngine.Random.Range(170, 184);
                    mid.weightKg = UnityEngine.Random.Range(64, 77);
                    mid.pace = UnityEngine.Random.Range(78, 92);
                    mid.agility = UnityEngine.Random.Range(86, 98);
                    mid.passing = UnityEngine.Random.Range(88, 99);
                    mid.curve = UnityEngine.Random.Range(85, 98);
                    mid.longShots = UnityEngine.Random.Range(82, 96);
                    mid.finishing = UnityEngine.Random.Range(74, 88);
                    mid.shotPower = UnityEngine.Random.Range(75, 90);
                    mid.acrobatics = UnityEngine.Random.Range(74, 90);
                    mid.defense = UnityEngine.Random.Range(44, 62);
                    mid.physical = UnityEngine.Random.Range(60, 76);
                }
                else if (isWideMid)
                {
                    mid.archetypeTag = "⚡ 측면 크로스 스페셜리스트";
                    mid.heightCm = UnityEngine.Random.Range(171, 183);
                    mid.weightKg = UnityEngine.Random.Range(65, 76);
                    mid.pace = UnityEngine.Random.Range(85, 97);
                    mid.agility = UnityEngine.Random.Range(84, 96);
                    mid.passing = UnityEngine.Random.Range(82, 95);
                    mid.curve = UnityEngine.Random.Range(82, 96);
                    mid.finishing = UnityEngine.Random.Range(66, 82);
                    mid.shotPower = UnityEngine.Random.Range(72, 88);
                    mid.longShots = UnityEngine.Random.Range(70, 86);
                    mid.acrobatics = UnityEngine.Random.Range(70, 86);
                    mid.defense = UnityEngine.Random.Range(58, 76);
                    mid.physical = UnityEngine.Random.Range(64, 80);
                }
                else
                {
                    mid.archetypeTag = "🧭 육각형 박스토박스 미드필더";
                    mid.heightCm = UnityEngine.Random.Range(175, 187);
                    mid.weightKg = UnityEngine.Random.Range(70, 82);
                    mid.pace = UnityEngine.Random.Range(77, 90);
                    mid.agility = UnityEngine.Random.Range(80, 93);
                    mid.passing = UnityEngine.Random.Range(85, 97);
                    mid.curve = UnityEngine.Random.Range(78, 93);
                    mid.longShots = UnityEngine.Random.Range(78, 94);
                    mid.shotPower = UnityEngine.Random.Range(76, 92);
                    mid.finishing = UnityEngine.Random.Range(64, 79);
                    mid.acrobatics = UnityEngine.Random.Range(66, 82);
                    mid.defense = UnityEngine.Random.Range(68, 82);
                    mid.physical = UnityEngine.Random.Range(72, 88);
                }

                mid.preferredFoot = slot.normLaneZ < -0.25f
                    ? PreferredFoot.Left
                    : (slot.normLaneZ > 0.25f ? PreferredFoot.Right : (UnityEngine.Random.value < 0.7f ? PreferredFoot.Right : PreferredFoot.Left));
                mid.weakFootStars = UnityEngine.Random.Range(3, 5);

                var hairValues = (HairStyleType[])Enum.GetValues(typeof(HairStyleType));
                mid.hairStyle = hairValues[UnityEngine.Random.Range(0, hairValues.Length)];
                mid.hairColor = HairColors[UnityEngine.Random.Range(0, HairColors.Length)];
                mid.skinColor = SkinTones[UnityEngine.Random.Range(0, SkinTones.Length)];
                mid.bootColor = BootColors[UnityEngine.Random.Range(0, BootColors.Length)];

                ApplyTeamKitColors(mid, isPlayerTeam, teamLeaderProfile);
                mid.overall = Mathf.RoundToInt(
                    mid.passing * 0.26f + mid.agility * 0.16f + mid.pace * 0.14f +
                    mid.curve * 0.14f + mid.longShots * 0.12f + mid.defense * 0.10f + mid.physical * 0.08f);
                return mid;
            }

            // Defender (수비수: 수비력·몸싸움 최상급, 공격 스탯은 확실히 낮음!)
            PlayerProfile def = GenerateRandomDefender(isPlayerTeam, usedNames, teamLeaderProfile, slotIndex);
            def.positionCode = slot.positionCode;
            def.positionLine = PositionLine.Defender;
            return def;
        }

        private static string PickUniqueName(string[] pool, System.Collections.Generic.HashSet<string> usedNames)
        {
            string chosen = pool[UnityEngine.Random.Range(0, pool.Length)];
            int guard = 0;
            while (usedNames != null && usedNames.Contains(chosen) && guard++ < 50)
            {
                chosen = pool[UnityEngine.Random.Range(0, pool.Length)];
            }
            usedNames?.Add(chosen);
            return chosen;
        }

        private static void ApplyTeamKitColors(PlayerProfile p, bool isPlayerTeam, PlayerProfile teamLeaderProfile)
        {
            if (teamLeaderProfile != null)
            {
                p.jerseyPrimaryColor = teamLeaderProfile.jerseyPrimaryColor;
                p.jerseySecondaryColor = teamLeaderProfile.jerseySecondaryColor;
                p.shortsColor = teamLeaderProfile.shortsColor;
            }
            else if (isPlayerTeam)
            {
                p.jerseyPrimaryColor = new Color(0.10f, 0.42f, 0.95f);
                p.jerseySecondaryColor = new Color(0.98f, 0.84f, 0.22f);
                p.shortsColor = new Color(0.09f, 0.13f, 0.24f);
            }
            else
            {
                p.jerseyPrimaryColor = new Color(0.90f, 0.16f, 0.22f);
                p.jerseySecondaryColor = new Color(0.12f, 0.12f, 0.15f);
                p.shortsColor = new Color(0.14f, 0.10f, 0.14f);
            }
        }

        /// <summary>
        /// 5대5 매치용 팀당 3명의 자동 수비수 AI 프로필 랜덤 생성 (유명 수비수 이름 + 팀 유니폼 통일 + 수비 체격·피지컬 특화)
        /// </summary>
        public static PlayerProfile GenerateRandomDefender(
            bool isPlayerTeam,
            System.Collections.Generic.HashSet<string> usedNames,
            PlayerProfile teamLeaderProfile,
            int defenderIndex)
        {
            var p = new PlayerProfile();

            string chosenName = FamousDefenderNames[UnityEngine.Random.Range(0, FamousDefenderNames.Length)];
            int guard = 0;
            while (usedNames != null && usedNames.Contains(chosenName) && guard++ < 40)
            {
                chosenName = FamousDefenderNames[UnityEngine.Random.Range(0, FamousDefenderNames.Length)];
            }
            usedNames?.Add(chosenName);
            p.playerName = chosenName;

            int[] defNumbers = { 2, 3, 4, 5, 6, 15, 19, 23, 24 };
            p.jerseyNumber = defNumbers[(defenderIndex * 3 + UnityEngine.Random.Range(0, 3)) % defNumbers.Length];

            int defArchetype = UnityEngine.Random.Range(0, 3);
            switch (defArchetype)
            {
                case 0: // 철벽 센터백 (피지컬 & 몸싸움 최강)
                    p.archetypeTag = "🛡️ 철벽 파이터 수비수";
                    p.heightCm = UnityEngine.Random.Range(186, 197);
                    p.weightKg = UnityEngine.Random.Range(82, 94);
                    p.pace = UnityEngine.Random.Range(76, 89);
                    p.agility = UnityEngine.Random.Range(68, 83);
                    p.physical = UnityEngine.Random.Range(86, 98);
                    break;
                case 1: // 초고속 풀백/스토퍼 (대인 추격 특화)
                    p.archetypeTag = "⚡ 초고속 추격 수비수";
                    p.heightCm = UnityEngine.Random.Range(176, 185);
                    p.weightKg = UnityEngine.Random.Range(70, 80);
                    p.pace = UnityEngine.Random.Range(86, 97);
                    p.agility = UnityEngine.Random.Range(82, 94);
                    p.physical = UnityEngine.Random.Range(74, 88);
                    break;
                default: // 빌드업 수비형 미드필더/스위퍼
                    p.archetypeTag = "🧭 빌드업 커맨더 수비";
                    p.heightCm = UnityEngine.Random.Range(180, 190);
                    p.weightKg = UnityEngine.Random.Range(75, 86);
                    p.pace = UnityEngine.Random.Range(78, 91);
                    p.agility = UnityEngine.Random.Range(76, 90);
                    p.physical = UnityEngine.Random.Range(80, 94);
                    break;
            }

            // 수비수 특화: 수비력(defense 85~98)과 몸싸움은 최상급이지만, 공격 스탯(결정력 32~52, 아크로바틱 38~56)은 확실히 낮음!
            p.defense = UnityEngine.Random.Range(85, 98);
            p.passing = UnityEngine.Random.Range(68, 84);
            p.finishing = UnityEngine.Random.Range(32, 52);
            p.shotPower = UnityEngine.Random.Range(64, 82);
            p.longShots = UnityEngine.Random.Range(38, 58);
            p.curve = UnityEngine.Random.Range(42, 64);
            p.acrobatics = UnityEngine.Random.Range(38, 56);
            p.positionCode = "CB";
            p.positionLine = PositionLine.Defender;

            p.preferredFoot = defenderIndex == 0
                ? PreferredFoot.Left
                : (defenderIndex == 2 ? PreferredFoot.Right : (UnityEngine.Random.value < 0.7f ? PreferredFoot.Right : PreferredFoot.Left));
            p.weakFootStars = UnityEngine.Random.Range(2, 5);

            var hairValues = (HairStyleType[])Enum.GetValues(typeof(HairStyleType));
            p.hairStyle = hairValues[UnityEngine.Random.Range(0, hairValues.Length)];
            p.hairColor = HairColors[UnityEngine.Random.Range(0, HairColors.Length)];
            p.skinColor = SkinTones[UnityEngine.Random.Range(0, SkinTones.Length)];
            p.bootColor = BootColors[UnityEngine.Random.Range(0, BootColors.Length)];

            // 1인칭 시점에서 아군/적군이 0.1초 만에 구분되도록 팀 대표 선수와 유니폼 컬러 통일!
            if (teamLeaderProfile != null)
            {
                p.jerseyPrimaryColor = teamLeaderProfile.jerseyPrimaryColor;
                p.jerseySecondaryColor = teamLeaderProfile.jerseySecondaryColor;
                p.shortsColor = teamLeaderProfile.shortsColor;
            }
            else if (isPlayerTeam)
            {
                p.jerseyPrimaryColor = new Color(0.10f, 0.42f, 0.95f);
                p.jerseySecondaryColor = new Color(0.98f, 0.84f, 0.22f);
                p.shortsColor = new Color(0.09f, 0.13f, 0.24f);
            }
            else
            {
                p.jerseyPrimaryColor = new Color(0.90f, 0.16f, 0.22f);
                p.jerseySecondaryColor = new Color(0.12f, 0.12f, 0.15f);
                p.shortsColor = new Color(0.14f, 0.10f, 0.14f);
            }

            p.overall = Mathf.RoundToInt(
                (p.defense * 0.36f) +
                (p.physical * 0.26f) +
                (p.pace * 0.18f) +
                (p.agility * 0.10f) +
                (p.passing * 0.10f)
            );

            return p;
        }

        /// <summary>
        /// 11 vs 11 매치용 골키퍼(#1 GK) 랜덤 프로필 생성 (선발 11인 중 첫 번째로 소개!)
        /// </summary>
        public static PlayerProfile GenerateGoalkeeperProfile(
            bool isPlayerTeam,
            System.Collections.Generic.HashSet<string> usedNames = null)
        {
            var p = new PlayerProfile();

            string chosenName = FamousGoalkeeperNames[UnityEngine.Random.Range(0, FamousGoalkeeperNames.Length)];
            int guard = 0;
            while (usedNames != null && usedNames.Contains(chosenName) && guard++ < 40)
            {
                chosenName = FamousGoalkeeperNames[UnityEngine.Random.Range(0, FamousGoalkeeperNames.Length)];
            }
            usedNames?.Add(chosenName);

            p.playerName = chosenName;
            p.jerseyNumber = 1;
            p.positionCode = "GK";
            p.positionLine = PositionLine.Goalkeeper;
            p.archetypeTag = "🧤 월드클래스 수문장 (GK)";

            p.heightCm = UnityEngine.Random.Range(188, 200);
            p.weightKg = UnityEngine.Random.Range(82, 94);
            p.preferredFoot = UnityEngine.Random.value < 0.78f ? PreferredFoot.Right : PreferredFoot.Left;
            p.weakFootStars = UnityEngine.Random.Range(3, 5);

            // 골키퍼 특화 스탯: 수비(선방·위치선정), 민첩성(반사신경), 피지컬(공중볼), 패스(롱킥 배급) 높음 / 공격 결정력 낮음
            p.defense = UnityEngine.Random.Range(88, 98);
            p.agility = UnityEngine.Random.Range(85, 97);
            p.physical = UnityEngine.Random.Range(84, 96);
            p.passing = UnityEngine.Random.Range(78, 92);
            p.shotPower = UnityEngine.Random.Range(82, 95);
            p.pace = UnityEngine.Random.Range(64, 78);
            p.finishing = UnityEngine.Random.Range(25, 42);
            p.longShots = UnityEngine.Random.Range(30, 48);
            p.curve = UnityEngine.Random.Range(55, 76);
            p.acrobatics = UnityEngine.Random.Range(82, 95);

            var hairValues = (HairStyleType[])Enum.GetValues(typeof(HairStyleType));
            p.hairStyle = hairValues[UnityEngine.Random.Range(0, hairValues.Length)];
            p.hairColor = HairColors[UnityEngine.Random.Range(0, HairColors.Length)];
            p.skinColor = SkinTones[UnityEngine.Random.Range(0, SkinTones.Length)];
            p.bootColor = BootColors[UnityEngine.Random.Range(0, BootColors.Length)];

            if (isPlayerTeam)
            {
                p.jerseyPrimaryColor = new Color(0.14f, 0.82f, 0.42f);
                p.jerseySecondaryColor = new Color(0.98f, 0.90f, 0.25f);
                p.shortsColor = new Color(0.08f, 0.16f, 0.12f);
            }
            else
            {
                p.jerseyPrimaryColor = new Color(0.96f, 0.56f, 0.12f);
                p.jerseySecondaryColor = new Color(0.15f, 0.15f, 0.18f);
                p.shortsColor = new Color(0.12f, 0.12f, 0.15f);
            }

            p.overall = Mathf.RoundToInt(
                (p.defense * 0.42f) +
                (p.agility * 0.26f) +
                (p.physical * 0.18f) +
                (p.passing * 0.14f)
            );

            return p;
        }
    }
}

