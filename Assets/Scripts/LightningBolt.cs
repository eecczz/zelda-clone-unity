using System.Collections.Generic;
using UnityEngine;

/// <summary>번개 볼트 한 줄기의 생김새. 인스펙터에서 조정할 수 있게 직렬화한다.</summary>
[System.Serializable]
public class LightningBoltSettings
{
    [Header("형태")]
    [Tooltip("지그재그 세그먼트 수 (최소)")]
    public int minSegments = 8;
    [Tooltip("지그재그 세그먼트 수 (최대)")]
    public int maxSegments = 14;
    [Tooltip("중간점이 직선에서 벗어나는 최대 거리")]
    public float amplitude = 0.5f;

    [Header("굵기")]
    [Tooltip("겉광 두께")]
    public float glowWidth = 0.5f;
    [Tooltip("심지 두께")]
    public float coreWidth = 0.12f;

    [Header("수명")]
    public float minDuration = 0.15f;
    public float maxDuration = 0.25f;

    [Header("잔가지")]
    [Tooltip("잔가지가 하나라도 뻗을 확률")]
    [Range(0f, 1f)] public float branchChance = 0.7f;
    public int minBranches = 1;
    public int maxBranches = 3;
    [Tooltip("잔가지 길이 (본체 길이 대비 비율)")]
    public float branchLengthRatio = 0.35f;
    [Tooltip("잔가지 굵기 배율")]
    public float branchWidthScale = 0.5f;

    [Header("색")]
    public Color glowColor = LightningPalette.Glow;
    public Color coreColor = LightningPalette.Core;
}

/// <summary>
/// 코드로 생성하는 프로시저럴 번개. 겉광 + 심지 2겹 구조에 잔가지가 분기된다.
/// 생성 즉시 페이드아웃을 시작하고 끝나면 스스로 파괴된다.
/// </summary>
public class LightningBolt : MonoBehaviour
{
    [Tooltip("동시에 존재할 수 있는 볼트 수. 넘으면 오래된 것부터 지운다.")]
    public const int MaxActiveBolts = 20;

    /// <summary>정지 상태(timeScale 0)에서 볼트가 영원히 남지 않게 하는 안전장치 (실제 시간)</summary>
    const float RealTimeSafetyLifetime = 3f;

    private static readonly List<LightningBolt> Active = new List<LightningBolt>();
    private static Material sharedMaterial;

    private readonly List<LineRenderer> strands = new List<LineRenderer>();
    private readonly List<Color> strandColors = new List<Color>();

    private float duration;
    private float elapsed;
    private float realTimeDeadline;

    /// <summary>from → to를 잇는 번개를 만든다.</summary>
    public static LightningBolt Create(Vector3 from, Vector3 to, LightningBoltSettings settings)
    {
        if (!Application.isPlaying || settings == null) return null;

        EnforceBudget();

        var go = new GameObject("LightningBolt");
        go.transform.position = from;

        var bolt = go.AddComponent<LightningBolt>();
        bolt.Build(from, to, settings);

        Active.Add(bolt);
        return bolt;
    }

    /// <summary>한 지점에서 사방으로 짧은 잔가지 볼트를 터뜨린다. (처치 연출용)</summary>
    public static void Burst(Vector3 center, int count, float length, LightningBoltSettings settings)
    {
        if (!Application.isPlaying || settings == null) return;

        for (int i = 0; i < count; i++)
        {
            // 위쪽으로 치우친 랜덤 방향 — 탑다운에서도 뻗는 게 보이게 수평 성분을 남긴다
            Vector3 dir = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(0.2f, 1.2f),
                Random.Range(-1f, 1f)).normalized;

            Create(center, center + dir * length * Random.Range(0.7f, 1.3f), settings);
        }
    }

    /// <summary>상한을 넘지 않도록 오래된 볼트부터 정리한다.</summary>
    static void EnforceBudget()
    {
        // 파괴된 항목(null) 먼저 청소 — 씬 리로드 후 정적 리스트에 남아있을 수 있다
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            if (Active[i] == null) Active.RemoveAt(i);
        }

        while (Active.Count >= MaxActiveBolts)
        {
            LightningBolt oldest = Active[0];
            // Destroy는 프레임 끝에 반영되므로 리스트에서는 지금 빼야 무한 루프가 안 난다
            Active.RemoveAt(0);
            if (oldest != null) Destroy(oldest.gameObject);
        }
    }

    void Build(Vector3 from, Vector3 to, LightningBoltSettings s)
    {
        duration = Random.Range(s.minDuration, s.maxDuration);
        realTimeDeadline = Time.unscaledTime + RealTimeSafetyLifetime;

        Vector3 axis = to - from;
        float length = axis.magnitude;
        if (length < 0.0001f)
        {
            // axis / length가 단위 벡터로 유지되도록 길이에 맞춰 넣는다
            length = 0.01f;
            axis = Vector3.forward * length;
        }

        int segments = Random.Range(s.minSegments, s.maxSegments + 1);
        List<Vector3> points = GeneratePath(from, to, segments, s.amplitude);

        AddStrandPair(points, s, 1f);

        // 잔가지 — 본체 중간점 아무 데서나 뻗어나간다
        if (Random.value <= s.branchChance && points.Count > 2)
        {
            int branchCount = Random.Range(s.minBranches, s.maxBranches + 1);
            Vector3 dir = axis / length;

            for (int i = 0; i < branchCount; i++)
            {
                Vector3 origin = points[Random.Range(1, points.Count - 1)];

                // 본체에서 비스듬히 갈라져 나가는 방향
                Vector3 perpendicular = Perpendicular(dir);
                Vector3 branchDir = (dir * Random.Range(-0.4f, 0.9f)
                                     + perpendicular * Random.Range(-1f, 1f)
                                     + Vector3.up * Random.Range(-0.2f, 0.5f)).normalized;

                float branchLength = length * s.branchLengthRatio * Random.Range(0.6f, 1.2f);
                List<Vector3> branchPoints = GeneratePath(
                    origin, origin + branchDir * branchLength,
                    Mathf.Max(3, segments / 2), s.amplitude * 0.6f);

                AddStrandPair(branchPoints, s, s.branchWidthScale);
            }
        }
    }

    /// <summary>직선을 세그먼트로 쪼개고 각 중간점을 수직 방향으로 흔들어 지그재그를 만든다.</summary>
    static List<Vector3> GeneratePath(Vector3 from, Vector3 to, int segments, float amplitude)
    {
        var points = new List<Vector3>(segments + 1);

        Vector3 axis = to - from;
        Vector3 dir = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.forward;

        Vector3 perpendicular = Perpendicular(dir);
        Vector3 binormal = Vector3.Cross(dir, perpendicular).normalized;

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 basePoint = Vector3.Lerp(from, to, t);

            // 양 끝은 정확히 시작/도착점에 붙어야 하므로 흔들림을 0으로 좁힌다
            float taper = Mathf.Sin(t * Mathf.PI);
            float offsetA = Random.Range(-amplitude, amplitude) * taper;
            float offsetB = Random.Range(-amplitude, amplitude) * taper * 0.4f;

            points.Add(basePoint + perpendicular * offsetA + binormal * offsetB);
        }

        return points;
    }

    /// <summary>진행 방향에 수직인 벡터. 탑다운에서 잘 보이도록 수평면 쪽을 우선한다.</summary>
    static Vector3 Perpendicular(Vector3 dir)
    {
        Vector3 perpendicular = Vector3.Cross(dir, Vector3.up);
        if (perpendicular.sqrMagnitude < 0.0001f) perpendicular = Vector3.Cross(dir, Vector3.forward);
        return perpendicular.normalized;
    }

    /// <summary>같은 경로에 굵은 겉광과 가는 심지를 겹쳐 깐다.</summary>
    void AddStrandPair(List<Vector3> points, LightningBoltSettings s, float widthScale)
    {
        AddStrand(points, s.glowWidth * widthScale, s.glowColor, 0);
        AddStrand(points, s.coreWidth * widthScale, s.coreColor, 1);
    }

    void AddStrand(List<Vector3> points, float width, Color color, int sortingOrder)
    {
        var go = new GameObject(sortingOrder == 0 ? "Glow" : "Core");
        go.transform.SetParent(transform, false);

        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++) line.SetPosition(i, points[i]);

        line.widthMultiplier = width;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.alignment = LineAlignment.View;   // 탑다운 카메라를 항상 마주보게
        line.textureMode = LineTextureMode.Stretch;
        line.sharedMaterial = GetSharedMaterial();
        line.sortingOrder = sortingOrder;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.startColor = color;
        line.endColor = color;

        strands.Add(line);
        strandColors.Add(color);
    }

    void Update()
    {
        // 정지 중에도 언젠가는 사라지게
        if (Time.unscaledTime >= realTimeDeadline)
        {
            Destroy(gameObject);
            return;
        }

        elapsed += Time.deltaTime;
        float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;

        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        // 제곱으로 떨어뜨려 초반에 확 어두워지는 급격한 페이드
        float alpha = (1f - t) * (1f - t);

        for (int i = 0; i < strands.Count; i++)
        {
            LineRenderer line = strands[i];
            if (line == null) continue;

            Color c = strandColors[i];
            c.a *= alpha;
            line.startColor = c;
            line.endColor = c;
        }
    }

    void OnDestroy()
    {
        Active.Remove(this);
    }

    /// <summary>
    /// 가산합성 계열 머티리얼. 내장 셰이더 이름이 버전마다 달라 후보를 순서대로 찾고,
    /// 전부 없으면 항상 포함되는 Sprites/Default로 떨어진다.
    /// </summary>
    static Material GetSharedMaterial()
    {
        if (sharedMaterial != null) return sharedMaterial;

        string[] candidates =
        {
            "Legacy Shaders/Particles/Additive",
            "Particles/Additive",
            "Mobile/Particles/Additive",
            "Sprites/Default",
        };

        Shader shader = null;
        foreach (string name in candidates)
        {
            shader = Shader.Find(name);
            if (shader != null) break;
        }

        sharedMaterial = new Material(shader) { mainTexture = Texture2D.whiteTexture };

        // 레거시 파티클 셰이더는 _Color가 아니라 _TintColor를 쓰고 기본값이 반투명 회색이라
        // 그냥 두면 색이 반으로 죽는다. 실제 색은 LineRenderer의 정점 색으로 들어간다.
        if (sharedMaterial.HasProperty("_TintColor"))
            sharedMaterial.SetColor("_TintColor", Color.white);

        sharedMaterial.renderQueue = 3000;
        sharedMaterial.hideFlags = HideFlags.HideAndDontSave;
        return sharedMaterial;
    }
}
