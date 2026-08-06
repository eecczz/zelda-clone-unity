using UnityEngine;

/// <summary>
/// 플레이어 체력. 피격 시 짧은 무적 시간이 붙어 여러 적에게 동시에 갈려 죽지 않는다.
/// RockProjectile과 Enemy가 IDamageable로 찾아 데미지를 넣는다.
/// </summary>
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("체력")]
    [SerializeField] private int maxHealth = 3;
    [Tooltip("피격 후 무적 시간 (초)")]
    [SerializeField] private float invulnerabilityDuration = 0.8f;

    [Header("피격 연출")]
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.15f, 0.15f);
    [SerializeField] private float hitFlashAlpha = 0.35f;
    [SerializeField] private float hitFlashDuration = 0.25f;
    [Tooltip("무적 동안 메시를 깜빡이는 주기 (초). 0이면 깜빡이지 않는다.")]
    [SerializeField] private float invulnerabilityBlinkInterval = 0.1f;

    [Header("사망 연출")]
    [SerializeField] private Color deathVfxColor = LightningPalette.PlayerDeath;
    [SerializeField] private int deathVfxParticles = 40;
    [Tooltip("죽으면 플레이어 메시를 숨긴다.")]
    [SerializeField] private bool hideRendererOnDeath = true;

    private int health;
    private bool isDead;
    private float invulnerableUntil;
    private Renderer[] renderers;

    public bool IsDead => isDead;
    public int Health => health;
    public int MaxHealth => maxHealth;

    /// <summary>무적 시간이 남아 있는지. (unscaled 기준 — 히트스톱에 늘어나지 않게)</summary>
    public bool IsInvulnerable => Time.unscaledTime < invulnerableUntil;

    void Awake()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        health = maxHealth;
        renderers = GetComponentsInChildren<Renderer>();
    }

    void Update()
    {
        if (isDead || invulnerabilityBlinkInterval <= 0f) return;

        bool invulnerable = IsInvulnerable;

        // 무적이 끝나면 반드시 다시 보이게 되돌린다
        bool visible = !invulnerable
                       || Mathf.FloorToInt(Time.unscaledTime / invulnerabilityBlinkInterval) % 2 == 0;

        SetRenderersVisible(visible);
    }

    public void TakeDamage(int amount)
    {
        if (isDead) return;
        if (IsInvulnerable) return;

        // 시작 전/게임오버 후에는 무적
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

        health -= Mathf.Max(1, amount);

        if (health <= 0)
        {
            health = 0;
            Die();
            return;
        }

        invulnerableUntil = Time.unscaledTime + invulnerabilityDuration;
        ScreenFlash.Play(hitFlashColor, hitFlashAlpha, hitFlashDuration);
    }

    void SetRenderersVisible(bool visible)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;

            // 조준선은 PlayerDash가 켜고 끄므로 건드리지 않는다
            if (renderers[i] is LineRenderer) continue;

            renderers[i].enabled = visible;
        }
    }

    void Die()
    {
        isDead = true;

        SlashVfx.Play(transform.position + Vector3.up, deathVfxColor, deathVfxParticles, 0.6f, 5f);
        ScreenFlash.Play(hitFlashColor, hitFlashAlpha * 1.5f, hitFlashDuration * 2f);

        // 일반 이동만 끈다. PlayerDash는 GameManager가 CancelAll로 정리하므로
        // 여기서 비활성화하면 OnDisable이 사망 감속을 풀어버린다.
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null) movement.enabled = false;

        if (hideRendererOnDeath) SetRenderersVisible(false);

        if (GameManager.Instance != null) GameManager.Instance.ReportPlayerDeath();
    }
}
