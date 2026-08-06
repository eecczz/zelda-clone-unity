using UnityEngine;

/// <summary>
/// 코드로 만드는 UI 도형 스프라이트.
/// 기본 TMP 폰트 아틀라스에 ▲/◆ 같은 글리프가 없어 □로 깨지므로,
/// 화살표와 체력 표시는 문자 대신 그려서 쓴다.
/// </summary>
public static class UiSprites
{
    const int Resolution = 64;

    private static Sprite triangle;
    private static Sprite diamond;

    /// <summary>위를 향하는 삼각형. 나침반 화살표용.</summary>
    public static Sprite Triangle
    {
        get
        {
            if (triangle == null) triangle = Build(IsInsideTriangle);
            return triangle;
        }
    }

    /// <summary>마름모. 체력 표시용.</summary>
    public static Sprite Diamond
    {
        get
        {
            if (diamond == null) diamond = Build(IsInsideDiamond);
            return diamond;
        }
    }

    /// <summary>u, v는 0~1 정규화 좌표.</summary>
    delegate bool ShapeTest(float u, float v);

    static bool IsInsideTriangle(float u, float v)
    {
        // 위로 갈수록 좁아지는 폭 안에 있는지
        float halfWidth = (1f - v) * 0.5f;
        return Mathf.Abs(u - 0.5f) <= halfWidth;
    }

    static bool IsInsideDiamond(float u, float v)
    {
        return Mathf.Abs(u - 0.5f) + Mathf.Abs(v - 0.5f) <= 0.5f;
    }

    static Sprite Build(ShapeTest test)
    {
        var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };

        var pixels = new Color32[Resolution * Resolution];

        for (int y = 0; y < Resolution; y++)
        {
            for (int x = 0; x < Resolution; x++)
            {
                float u = (x + 0.5f) / Resolution;
                float v = (y + 0.5f) / Resolution;

                // 가장자리를 부드럽게 하려고 2x2로 나눠 커버리지를 센다
                int covered = 0;
                float step = 0.25f / Resolution;
                if (test(u - step, v - step)) covered++;
                if (test(u + step, v - step)) covered++;
                if (test(u - step, v + step)) covered++;
                if (test(u + step, v + step)) covered++;

                byte alpha = (byte)(covered * 255 / 4);
                pixels[y * Resolution + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        var sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, Resolution, Resolution),
            new Vector2(0.5f, 0.5f),
            Resolution);

        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
