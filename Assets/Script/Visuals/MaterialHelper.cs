using UnityEngine;
using UnityEngine.Rendering;

namespace Soccer3D.Visuals
{
    public static class MaterialHelper
    {
        private static Texture2D _softGlowTexture;
        private static Texture2D _ringGlowTexture;
        private static Texture2D _soccerBallTexture;
        private static Texture2D _goalNetTexture;
        private static Sprite _whiteSprite;
        private static Sprite _circleSprite;
        private static Sprite _circleRingSprite;
        private static Sprite _arrowSprite;
        private static Sprite _roundedRectSprite;
        private static Sprite _pillSprite;

        public static Shader GetLitShader()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (s == null) s = Shader.Find("Standard");
            return s;
        }

        public static Shader GetUnlitShader()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Unlit/Color");
            if (s == null) s = Shader.Find("Sprites/Default");
            return s;
        }

        public static Shader GetParticleShader()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (s == null) s = Shader.Find("Particles/Standard Unlit");
            if (s == null) s = Shader.Find("Sprites/Default");
            return s;
        }

        public static Material CreateLitMaterial(Color baseColor, float smoothness = 0.35f, float metallic = 0f, Color? emissionHdr = null)
        {
            Material mat = new Material(GetLitShader());
            mat.enableInstancing = true;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);

            if (emissionHdr.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", emissionHdr.Value);
            }
            return mat;
        }

        public static Material CreateHdrUnlitMaterial(Color baseColor, float hdrIntensity = 3.0f)
        {
            Material mat = new Material(GetUnlitShader());
            mat.enableInstancing = true;
            Color hdr = new Color(
                baseColor.r * hdrIntensity,
                baseColor.g * hdrIntensity,
                baseColor.b * hdrIntensity,
                baseColor.a
            );
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", hdr);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", hdr);
            return mat;
        }

        public static Material CreateAdditiveVfxMaterial(Color tint, float hdrMultiplier = 2.8f, bool useRing = false)
        {
            Shader shader = Shader.Find("Sprites/Default");
            Material mat = new Material(shader != null ? shader : GetParticleShader());
            mat.enableInstancing = true;

            Texture2D tex = useRing ? GetRingGlowTexture() : GetSoftGlowTexture();
            mat.mainTexture = tex;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);

            Color hdr = new Color(tint.r * hdrMultiplier, tint.g * hdrMultiplier, tint.b * hdrMultiplier, tint.a);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", hdr);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", hdr);

            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)BlendMode.One);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3100;
            return mat;
        }

        public static Texture2D GetSoftGlowTexture()
        {
            if (_softGlowTexture != null) return _softGlowTexture;

            int size = 64;
            _softGlowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _softGlowTexture.wrapMode = TextureWrapMode.Clamp;
            _softGlowTexture.filterMode = FilterMode.Bilinear;

            float half = (size - 1) * 0.5f;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * (3f - 2f * a);
                    float core = Mathf.Pow(a, 1.6f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, core);
                }
            }
            _softGlowTexture.SetPixels(pixels);
            _softGlowTexture.Apply();
            return _softGlowTexture;
        }

        public static Texture2D GetRingGlowTexture()
        {
            if (_ringGlowTexture != null) return _ringGlowTexture;

            int size = 64;
            _ringGlowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _ringGlowTexture.wrapMode = TextureWrapMode.Clamp;
            _ringGlowTexture.filterMode = FilterMode.Bilinear;

            float half = (size - 1) * 0.5f;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ring = 1f - Mathf.Clamp01(Mathf.Abs(r - 0.72f) / 0.22f);
                    ring = ring * ring;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, ring);
                }
            }
            _ringGlowTexture.SetPixels(pixels);
            _ringGlowTexture.Apply();
            return _ringGlowTexture;
        }

        public static Texture2D GetGoalNetTexture()
        {
            if (_goalNetTexture != null) return _goalNetTexture;

            int size = 64;
            _goalNetTexture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            _goalNetTexture.wrapMode = TextureWrapMode.Repeat;
            _goalNetTexture.filterMode = FilterMode.Trilinear;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 다이아몬드 & 정사각형 직조 그물 패턴
                    int d1 = Mathf.Abs((x + y) % 16 - 8);
                    int d2 = Mathf.Abs((x - y + 64) % 16 - 8);
                    bool cord = d1 >= 6 || d2 >= 6;
                    pixels[y * size + x] = cord
                        ? new Color(0.95f, 0.97f, 1.0f, 0.78f)
                        : new Color(0.90f, 0.94f, 1.0f, 0.06f);
                }
            }
            _goalNetTexture.SetPixels(pixels);
            _goalNetTexture.Apply();
            return _goalNetTexture;
        }

        public static Sprite GetWhiteSprite()
        {
            if (_whiteSprite != null) return _whiteSprite;
            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color[] cols = new Color[16];
            for (int i = 0; i < 16; i++) cols[i] = Color.white;
            tex.SetPixels(cols);
            tex.Apply();
            _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            return _whiteSprite;
        }

        public static Sprite GetCircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float half = (size - 1) * 0.5f;
            Color[] cols = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((0.95f - r) * 14f);
                    cols[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _circleSprite;
        }

        public static Sprite GetCircleRingSprite()
        {
            if (_circleRingSprite != null) return _circleRingSprite;
            int size = 96;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float half = (size - 1) * 0.5f;
            Color[] cols = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float distFromRing = Mathf.Abs(r - 0.88f);
                    float alpha = Mathf.Clamp01((0.065f - distFromRing) * 24f);
                    cols[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            _circleRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _circleRingSprite;
        }

        /// <summary>
        /// 9-슬라이스(9-Sliced) 매끄러운 둥근 모서리 스프라이트 (패널, 카드, 버튼 등 각진 네모 제거용)
        /// </summary>
        public static Sprite GetRoundedRectSprite()
        {
            if (_roundedRectSprite != null) return _roundedRectSprite;
            int size = 64;
            int r = 16;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color[] cols = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = x < r ? r : (x >= size - r ? size - 1 - r : x);
                    float cy = y < r ? r : (y >= size - r ? size - 1 - r : y);
                    float dx = x - cx;
                    float dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((r - dist) * 1.6f + 0.5f);
                    cols[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            _roundedRectSprite = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(r, r, r, r)
            );
            return _roundedRectSprite;
        }

        /// <summary>
        /// 9-슬라이스(9-Sliced) 완전 둥근 캡슐/알약(Pill) 스프라이트 (배지, 태그, 게이지 바 등)
        /// </summary>
        public static Sprite GetPillSprite()
        {
            if (_pillSprite != null) return _pillSprite;
            int size = 64;
            int r = 31;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color[] cols = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = x < r ? r : (x >= size - r ? size - 1 - r : x);
                    float cy = y < r ? r : (y >= size - r ? size - 1 - r : y);
                    float dx = x - cx;
                    float dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((r - dist) * 1.6f + 0.5f);
                    cols[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            _pillSprite = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(r, r, r, r)
            );
            return _pillSprite;
        }

        /// <summary>
        /// +Y(위쪽) 방향을 가리키는 선명한 전술 화살표 스프라이트 (미니맵 시야 및 HUD 공 추적 화살표용)
        /// </summary>
        public static Sprite GetArrowSprite()
        {
            if (_arrowSprite != null) return _arrowSprite;
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color[] cols = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = (y + 0.5f) / size; // 0(bottom) .. 1(top)
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size - 0.5f; // -0.5 .. +0.5
                    float ax = Mathf.Abs(nx);
                    // 날렵한 셰브론/화살촉 형태
                    float maxHalfWidth = (0.94f - ny) * 0.56f;
                    bool insideOuter = ny <= 0.94f && ny >= 0.10f && ax <= maxHalfWidth;
                    bool notchedBase = ny < 0.30f && ax < (0.30f - ny) * 0.85f;
                    float edgeDist = maxHalfWidth - ax;
                    float alpha = (insideOuter && !notchedBase) ? Mathf.Clamp01(edgeDist * 28f) : 0f;
                    cols[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            _arrowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _arrowSprite;
        }

        /// <summary>
        /// 잔디 위에서도 한눈에 확 띄는 프리미어리그 윈터 볼 스타일 (형광 옐로우-오렌지 + 크림슨 레드 + 네이비 스타 패치)
        /// </summary>
        public static Texture2D GetSoccerBallTexture()
        {
            if (_soccerBallTexture != null) return _soccerBallTexture;

            int w = 256;
            int h = 128;
            _soccerBallTexture = new Texture2D(w, h, TextureFormat.RGBA32, true);
            _soccerBallTexture.wrapMode = TextureWrapMode.Repeat;
            _soccerBallTexture.filterMode = FilterMode.Trilinear;

            Vector3[] patchDirs =
            {
                new Vector3(0, 1, 0),
                new Vector3(0, -1, 0),
                new Vector3(1, 0, 0),
                new Vector3(-1, 0, 0),
                new Vector3(0, 0, 1),
                new Vector3(0, 0, -1),
                new Vector3(0.577f, 0.577f, 0.577f),
                new Vector3(-0.577f, 0.577f, 0.577f),
                new Vector3(0.577f, -0.577f, 0.577f),
                new Vector3(-0.577f, -0.577f, 0.577f),
                new Vector3(0.577f, 0.577f, -0.577f),
                new Vector3(-0.577f, -0.577f, -0.577f)
            };

            Color[] pixels = new Color[w * h];
            Color hiVisYellow = new Color(1.00f, 0.90f, 0.08f);
            Color fluoOrangeRing = new Color(1.00f, 0.30f, 0.05f);
            Color navyCore = new Color(0.08f, 0.11f, 0.28f);

            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                float theta = v * Mathf.PI;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float phi = u * Mathf.PI * 2f;

                    Vector3 dir = new Vector3(
                        Mathf.Sin(theta) * Mathf.Cos(phi),
                        Mathf.Cos(theta),
                        Mathf.Sin(theta) * Mathf.Sin(phi)
                    ).normalized;

                    float maxDot = -1f;
                    for (int i = 0; i < patchDirs.Length; i++)
                    {
                        float d = Vector3.Dot(dir, patchDirs[i]);
                        if (d > maxDot) maxDot = d;
                    }

                    Color c = hiVisYellow;
                    if (maxDot > 0.915f)
                        c = navyCore;
                    else if (maxDot > 0.855f)
                        c = fluoOrangeRing;

                    pixels[y * w + x] = c;
                }
            }

            _soccerBallTexture.SetPixels(pixels);
            _soccerBallTexture.Apply();
            return _soccerBallTexture;
        }

        /// <summary>
        /// 사실적인 천연 잔디 구장 알베도 + 마이크로 잔디 결 노멀맵 동시 생성 (FIFA 정규 105m x 68m 라인 규격 지원)
        /// </summary>
        public static void CreateRealisticPitchTextures(
            float fieldLength,
            float fieldWidth,
            float marginLength,
            float marginWidth,
            out Texture2D albedoTex,
            out Texture2D normalTex)
        {
            int w = 1536;
            int h = 1024;
            albedoTex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            albedoTex.wrapMode = TextureWrapMode.Clamp;
            albedoTex.filterMode = FilterMode.Trilinear;
            albedoTex.anisoLevel = 8;

            normalTex = new Texture2D(w, h, TextureFormat.RGBA32, true, linear: true);
            normalTex.wrapMode = TextureWrapMode.Clamp;
            normalTex.filterMode = FilterMode.Trilinear;
            normalTex.anisoLevel = 8;

            float totalX = fieldLength + marginLength * 2f;
            float totalZ = fieldWidth + marginWidth * 2f;
            float halfL = fieldLength * 0.5f;
            float halfW = fieldWidth * 0.5f;
            bool fullRegulation = fieldLength >= 90f;

            // 과장된 원색 대신 유럽 야간 구장 특유의 깊고 사실적인 천연 잔디 색감
            Color grassMowLight = new Color(0.16f, 0.44f, 0.15f);
            Color grassMowDark = new Color(0.11f, 0.35f, 0.12f);
            Color grassCrossTint = new Color(0.14f, 0.40f, 0.14f);
            Color trackApron = new Color(0.15f, 0.19f, 0.16f);
            Color chalkWhite = new Color(0.95f, 0.97f, 0.95f);

            float lineThickness = fullRegulation ? 0.16f : 0.15f;
            float centerCircleRadius = fullRegulation ? 9.15f : 5.2f;
            float penDepth = fullRegulation ? 16.5f : 9.5f;
            float penHalfW = fullRegulation ? 20.16f : 9.5f;
            float goalBoxDepth = fullRegulation ? 5.5f : 3.6f;
            float goalBoxHalfW = fullRegulation ? 9.16f : 5.25f;
            float penSpotX = halfL - (fullRegulation ? 11.0f : 6.8f);
            float penArcRadius = fullRegulation ? 9.15f : 4.5f;

            Color[] pixels = new Color[w * h];
            Color[] normals = new Color[w * h];

            for (int py = 0; py < h; py++)
            {
                float nz = (py + 0.5f) / h - 0.5f;
                float z = nz * totalZ;

                for (int px = 0; px < w; px++)
                {
                    float nx = (px + 0.5f) / w - 0.5f;
                    float x = nx * totalX;

                    bool insideTurf = Mathf.Abs(x) <= halfL + 2.6f && Mathf.Abs(z) <= halfW + 2.2f;
                    Color baseCol;

                    if (!insideTurf)
                    {
                        // 육상트랙/사이드 매트 다크 슬레이트 톤
                        float trackNoise = Mathf.PerlinNoise(px * 0.35f, py * 0.35f) * 0.03f;
                        baseCol = new Color(trackApron.r + trackNoise, trackApron.g + trackNoise, trackApron.b + trackNoise, 1f);
                    }
                    else
                    {
                        // 세로 18등분 + 가로 12등분 교차 잔디 깎기 패턴 (실제 정규 축구장 체크무늬 잔디 결)
                        int stripeX = Mathf.FloorToInt((x + halfL + 6f) / (fieldLength / 18f));
                        int stripeZ = Mathf.FloorToInt((z + halfW + 6f) / (fieldWidth / 12f));

                        baseCol = (stripeX % 2 == 0) ? grassMowLight : grassMowDark;
                        if (stripeZ % 2 == 0)
                            baseCol = Color.Lerp(baseCol, grassCrossTint, 0.25f);

                        // 잔디 클럼프 디테일 & 골문 앞 자연스러운 잔디 마모 질감
                        float macroNoise = Mathf.PerlinNoise(px * 0.045f, py * 0.045f) * 0.055f - 0.027f;
                        float bladeNoise = Mathf.PerlinNoise(px * 0.42f, py * 0.42f) * 0.05f - 0.025f;
                        baseCol.r = Mathf.Clamp01(baseCol.r + macroNoise * 0.8f + bladeNoise * 0.6f);
                        baseCol.g = Mathf.Clamp01(baseCol.g + macroNoise + bladeNoise);
                        baseCol.b = Mathf.Clamp01(baseCol.b + macroNoise * 0.5f + bladeNoise * 0.4f);

                        float goalMouthDist = Mathf.Min(
                            Vector2.Distance(new Vector2(x, z), new Vector2(-halfL + 1.4f, 0f)),
                            Vector2.Distance(new Vector2(x, z), new Vector2(halfL - 1.4f, 0f))
                        );
                        if (goalMouthDist < 4.5f)
                        {
                            float wear = (1f - goalMouthDist / 4.5f) * 0.22f * Mathf.PerlinNoise(px * 0.18f, py * 0.18f);
                            baseCol = Color.Lerp(baseCol, new Color(0.32f, 0.28f, 0.18f), wear);
                        }
                    }

                    bool isLine = false;
                    float ax = Mathf.Abs(x);
                    float az = Mathf.Abs(z);

                    if (az <= halfW + lineThickness && Mathf.Abs(ax - halfL) <= lineThickness)
                        isLine = true;
                    if (ax <= halfL + lineThickness && Mathf.Abs(az - halfW) <= lineThickness)
                        isLine = true;
                    if (az <= halfW && ax <= lineThickness)
                        isLine = true;

                    float centerDist = Mathf.Sqrt(x * x + z * z);
                    if (Mathf.Abs(centerDist - centerCircleRadius) <= lineThickness || centerDist <= 0.34f)
                        isLine = true;

                    if (ax >= halfL - penDepth - lineThickness && ax <= halfL)
                    {
                        if (Mathf.Abs(az - penHalfW) <= lineThickness) isLine = true;
                        if (Mathf.Abs(ax - (halfL - penDepth)) <= lineThickness && az <= penHalfW + lineThickness) isLine = true;
                    }

                    if (ax >= halfL - goalBoxDepth - lineThickness && ax <= halfL)
                    {
                        if (Mathf.Abs(az - goalBoxHalfW) <= lineThickness) isLine = true;
                        if (Mathf.Abs(ax - (halfL - goalBoxDepth)) <= lineThickness && az <= goalBoxHalfW + lineThickness) isLine = true;
                    }

                    float distLeftSpot = Mathf.Sqrt((x + penSpotX) * (x + penSpotX) + z * z);
                    float distRightSpot = Mathf.Sqrt((x - penSpotX) * (x - penSpotX) + z * z);
                    if (distLeftSpot <= 0.28f || distRightSpot <= 0.28f)
                        isLine = true;
                    if (x > -(halfL - penDepth) && Mathf.Abs(distLeftSpot - penArcRadius) <= lineThickness)
                        isLine = true;
                    if (x < (halfL - penDepth) && Mathf.Abs(distRightSpot - penArcRadius) <= lineThickness)
                        isLine = true;

                    // 코너 아크 (반경 1m)
                    if (ax <= halfL && az <= halfW)
                    {
                        float cornerDist = Mathf.Sqrt((halfL - ax) * (halfL - ax) + (halfW - az) * (halfW - az));
                        if (Mathf.Abs(cornerDist - 1.0f) <= lineThickness)
                            isLine = true;
                    }

                    pixels[py * w + px] = isLine ? Color.Lerp(baseCol, chalkWhite, 0.93f) : baseCol;

                    // 노멀맵 샘플 (잔디 미세 요철 표현)
                    float n1 = Mathf.PerlinNoise((px + 1f) * 0.55f, py * 0.55f) - Mathf.PerlinNoise((px - 1f) * 0.55f, py * 0.55f);
                    float n2 = Mathf.PerlinNoise(px * 0.55f, (py + 1f) * 0.55f) - Mathf.PerlinNoise(px * 0.55f, (py - 1f) * 0.55f);
                    Vector3 nVec = new Vector3(-n1 * 1.4f, -n2 * 1.4f, 1f).normalized;
                    normals[py * w + px] = new Color(nVec.x * 0.5f + 0.5f, nVec.y * 0.5f + 0.5f, nVec.z * 0.5f + 0.5f, 1f);
                }
            }

            albedoTex.SetPixels(pixels);
            albedoTex.Apply();

            normalTex.SetPixels(normals);
            normalTex.Apply();
        }
    }
}
