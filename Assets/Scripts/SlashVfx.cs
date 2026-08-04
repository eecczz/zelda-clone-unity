using UnityEngine;

/// <summary>
/// 프리팹 없이 코드로 즉석에서 만드는 처치 이펙트.
/// 인스펙터에서 파티클 프리팹을 연결하면 그쪽이 우선이고, 비워두면 이게 쓰인다.
/// </summary>
public static class SlashVfx
{
    private static Material sharedMaterial;

    /// <summary>지정 위치에 한 번 터지는 파티클을 만들고, 재생이 끝나면 스스로 파괴된다.</summary>
    public static void Play(Vector3 position, Color color, int particleCount = 24, float lifetime = 0.35f, float speed = 7f)
    {
        var go = new GameObject("SlashVFX");
        go.transform.position = position;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = lifetime;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.5f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startColor = color;
        main.gravityModifier = 0.6f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        // 재생이 끝나면 GameObject째로 정리
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)particleCount) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = GetSharedMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        ps.Play();
    }

    static Material GetSharedMaterial()
    {
        if (sharedMaterial != null) return sharedMaterial;

        // Sprites/Default는 항상 빌드에 포함되는 내장 셰이더라 Shader.Find가 안전하다.
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        sharedMaterial = new Material(shader) { mainTexture = Texture2D.whiteTexture };
        sharedMaterial.hideFlags = HideFlags.HideAndDontSave;
        return sharedMaterial;
    }
}
