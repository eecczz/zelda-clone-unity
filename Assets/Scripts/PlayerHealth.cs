using UnityEngine;

/// <summary>
/// 플레이어 체력. 기본 1 — 한 방 게임.
/// RockProjectile과 Enemy가 IDamageable로 찾아 데미지를 넣는다.
/// </summary>
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxHealth = 1;

    [Header("사망 연출")]
    [SerializeField] private Color deathVfxColor = LightningPalette.PlayerDeath;
    [SerializeField] private int deathVfxParticles = 40;
    [Tooltip("죽으면 플레이어 메시를 숨긴다.")]
    [SerializeField] private bool hideRendererOnDeath = true;

    private int health;
    private bool isDead;

    public bool IsDead => isDead;
    public int Health => health;

    void Awake()
    {
        health = Mathf.Max(1, maxHealth);
    }

    public void TakeDamage(int amount)
    {
        if (isDead) return;

        // 시작 전/게임오버 후에는 무적 (사망 연출 도중 두 번 죽지 않게)
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

        health -= Mathf.Max(1, amount);
        if (health <= 0) Die();
    }

    void Die()
    {
        isDead = true;

        SlashVfx.Play(transform.position + Vector3.up, deathVfxColor, deathVfxParticles, 0.6f, 5f);

        // 일반 이동만 끈다. PlayerDash는 GameManager가 CancelAll로 정리하므로
        // 여기서 비활성화하면 OnDisable이 사망 감속을 풀어버린다.
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null) movement.enabled = false;

        if (hideRendererOnDeath)
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                // 조준선은 어차피 꺼지지만, 메시만 골라 끄기보다 전부 끄는 게 단순하다
                r.enabled = false;
            }
        }

        if (GameManager.Instance != null) GameManager.Instance.ReportPlayerDeath();
    }
}
