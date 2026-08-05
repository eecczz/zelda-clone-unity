using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 원거리 적이 던지는 돌. 직선 등속으로 날아가며 수명이 다하면 사라진다.
/// 플레이어가 대시로 스치면 반사되어(Reflect) 적을 즉사시키는 아군 투사체가 된다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class RockProjectile : MonoBehaviour
{
    [Header("비행")]
    [SerializeField] private float speed = 8f;
    [SerializeField] private float lifetime = 5f;
    [Tooltip("명중 판정에 쓰는 반지름. 콜라이더 크기와 비슷하게 두면 된다.")]
    [SerializeField] private float hitRadius = 0.25f;
    [Tooltip("명중 판정 대상 레이어. 기본값(Everything)이어도 컴포넌트 유무로 다시 걸러낸다.")]
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("반사 후")]
    [Tooltip("반사되면 스스로도 조금 커져서 눈에 띄게 한다.")]
    [SerializeField] private float reflectedScaleMultiplier = 1.3f;
    [SerializeField] private Color reflectedColor = LightningPalette.Reflect;
    [Tooltip("반사된 돌이 적을 맞혔을 때 터지는 잔가지 번개")]
    [SerializeField] private LightningBoltSettings killSparkBolt = new LightningBoltSettings
    {
        minSegments = 4,
        maxSegments = 7,
        amplitude = 0.25f,
        glowWidth = 0.3f,
        coreWidth = 0.08f,
        minDuration = 0.12f,
        maxDuration = 0.2f,
        branchChance = 0.3f,
        branchLengthRatio = 0.4f,
    };

    [Header("피격 훅")]
    [Tooltip("플레이어에게 넣을 피해량. IDamageable 구현체가 없으면 로그만 남는다.")]
    [SerializeField] private int damageToPlayer = 1;
    [Tooltip("플레이어 명중 시 호출. 체력 시스템/UI를 인스펙터에서 연결할 수 있다.")]
    [SerializeField] private UnityEvent<GameObject> onHitPlayer;

    private Vector3 direction = Vector3.forward;
    private float despawnTime;
    private bool isReflected;
    private bool isConsumed;
    private readonly RaycastHit[] castBuffer = new RaycastHit[16];

    /// <summary>대시로 반사된 상태인지. 반사된 돌은 적을 죽이고 플레이어를 무시한다.</summary>
    public bool IsReflected => isReflected;

    void Start()
    {
        despawnTime = Time.time + lifetime;
    }

    /// <summary>발사 직후 방향을 지정한다. (원거리 적이 호출)</summary>
    public void Launch(Vector3 newDirection)
    {
        newDirection.y = 0f;
        if (newDirection.sqrMagnitude > 0.0001f)
        {
            direction = newDirection.normalized;
            transform.rotation = Quaternion.LookRotation(direction);
        }

        despawnTime = Time.time + lifetime;
    }

    /// <summary>
    /// 일섬 반격. 대시 방향으로 방향을 틀고 속도를 올리며, 이후로는 적을 즉사시킨다.
    /// </summary>
    public void Reflect(Vector3 newDirection, float speedMultiplier)
    {
        newDirection.y = 0f;
        if (newDirection.sqrMagnitude > 0.0001f)
        {
            direction = newDirection.normalized;
            transform.rotation = Quaternion.LookRotation(direction);
        }

        speed *= speedMultiplier;
        isReflected = true;

        // 반사된 돌은 수명을 새로 받는다 (막 던져진 돌이 곧 사라지면 허무하니까)
        despawnTime = Time.time + lifetime;

        transform.localScale *= reflectedScaleMultiplier;

        var renderer = GetComponentInChildren<Renderer>();
        if (renderer != null) renderer.material.color = reflectedColor;
    }

    void Update()
    {
        if (isConsumed) return;

        if (Time.time >= despawnTime)
        {
            Destroy(gameObject);
            return;
        }

        float step = speed * Time.deltaTime;
        Vector3 from = transform.position;

        // 이동 전에 이번 프레임 구간을 먼저 검사한다.
        // 빠른 돌이 얇은 대상을 뚫고 지나가는(터널링) 걸 막기 위해 SphereCast를 쓴다.
        if (step > 0f && SweepForHit(from, step)) return;

        transform.position = from + direction * step;
    }

    /// <summary>이번 프레임 이동 구간에 맞을 대상이 있으면 처리하고 true를 반환한다.</summary>
    bool SweepForHit(Vector3 from, float step)
    {
        int count = Physics.SphereCastNonAlloc(
            from, hitRadius, direction, castBuffer, step, hitMask, QueryTriggerInteraction.Collide);

        // 가장 가까운 유효 대상부터 처리해야 하므로 거리순으로 훑는다
        int bestIndex = -1;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider col = castBuffer[i].collider;
            if (col == null) continue;
            if (!IsValidTarget(col)) continue;

            if (castBuffer[i].distance < bestDistance)
            {
                bestDistance = castBuffer[i].distance;
                bestIndex = i;
            }
        }

        if (bestIndex < 0) return false;

        // 맞은 지점까지는 실제로 이동시켜 놓고 처리 (이펙트 위치가 자연스럽게)
        transform.position = from + direction * bestDistance;
        ResolveHit(castBuffer[bestIndex].collider);
        return true;
    }

    bool IsValidTarget(Collider col)
    {
        if (isReflected)
        {
            Enemy enemy = col.GetComponentInParent<Enemy>();
            return enemy != null && !enemy.IsDead;
        }

        // 적이 던진 돌은 플레이어만 노린다. 대시 중이면 무시 —
        // 그 경우는 PlayerDash의 캡슐 스윕이 반사로 처리한다.
        PlayerDash dash = col.GetComponentInParent<PlayerDash>();
        return dash != null && !dash.IsDashing;
    }

    void ResolveHit(Collider col)
    {
        isConsumed = true;

        if (isReflected)
        {
            Enemy enemy = col.GetComponentInParent<Enemy>();
            if (enemy != null && !enemy.IsDead)
            {
                Vector3 deathPosition = enemy.transform.position;
                enemy.Kill();

                if (ScoreSystem.Instance != null)
                    ScoreSystem.Instance.RegisterKill(KillSource.Reflect);

                SlashVfx.Play(deathPosition, reflectedColor);
                LightningBolt.Burst(deathPosition + Vector3.up * 0.5f, Random.Range(2, 5), 1.6f, killSparkBolt);
                ScreenFlash.Play(LightningPalette.Flash, 0.25f, 0.1f);
            }
        }
        else
        {
            PlayerDash dash = col.GetComponentInParent<PlayerDash>();
            if (dash == null) { Destroy(gameObject); return; }

            GameObject player = dash.gameObject;

            // 체력 시스템이 붙어 있으면 데미지를, 없으면 아직 로그만.
            IDamageable damageable = player.GetComponentInParent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
                damageable.TakeDamage(damageToPlayer);
            else
                Debug.Log($"[RockProjectile] 플레이어 피격! (체력 시스템 미연결, damage={damageToPlayer})", player);

            onHitPlayer?.Invoke(player);
        }

        Destroy(gameObject);
    }
}
