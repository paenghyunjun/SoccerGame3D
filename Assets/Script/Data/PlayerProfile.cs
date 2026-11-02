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

    [Serializable]
    public class PlayerProfile
    {
        public string playerName;
        public string archetypeTag;
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

        // 핵심 능력치 (45 ~ 99)
        public int pace;        // 속력
        public int agility;     // 민첩성
        public int finishing;   // 골 결정력
        public int shotPower;   // 슛 파워
        public int longShots;   // 중거리 슛
        public int curve;       // 커브 (감아차기 핵심)
        public int acrobatics;  // 아크로바틱 (오버헤드킥/발리 핵심)
        public int physical;    // 몸싸움/밸런스
        public int overall;     // 종합 오버롤

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
    }

    public static class PlayerProfileGenerator
    {
        private static readonly string[] FamousNames =
        {
            "손흥민 (Son)", "크리스티아누 호날두 (Ronaldo)", "리오넬 메시 (Messi)",
            "킬리안 음바페 (Mbappé)", "엘링 홀란 (Haaland)", "즐라탄 이브라히모비치 (Zlatan)",
            "호나우지뉴 (Ronaldinho)", "주드 벨링엄 (Bellingham)", "비니시우스 Jr. (Vinícius)",
            "네이마르 Jr. (Neymar)", "웨인 루니 (Rooney)", "가레스 베일 (Bale)",
            "이강인 (Lee Kang-in)", "박지성 (Park Ji-sung)", "케빈 더 브라위너 (De Bruyne)",
            "모하메드 살라 (Salah)", "라민 야말 (Yamal)", "티에리 앙리 (Henry)",
            "디디에 드록바 (Drogba)", "로베르트 레반도프스키 (Lewandowski)", "루이스 수아레스 (Suárez)",
            "데이비드 베컴 (Beckham)", "카림 벤제마 (Benzema)", "앙투안 그리즈만 (Griezmann)"
        };

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

            p.overall = Mathf.RoundToInt(
                (p.pace * 0.13f) +
                (p.agility * 0.13f) +
                (p.finishing * 0.18f) +
                (p.shotPower * 0.14f) +
                (p.longShots * 0.12f) +
                (p.curve * 0.11f) +
                (p.acrobatics * 0.12f) +
                (p.physical * 0.07f)
            );

            return p;
        }
    }
}

