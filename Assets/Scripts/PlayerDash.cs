using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 일섬 대시 — 마우스 좌클릭을 누르고 있는 동안 시간이 느려지고 조준선이 표시되며,
/// 버튼을 놓으면 조준한 지점까지 순간적으로 돌진한다.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerDash : MonoBehaviour
{
    [Header("감속 (조준)")]
    [Tooltip("조준 중 적용할 Time.timeScale 값")]
    [SerializeField, Range(0.01f, 1f)] private float slowMotionScale = 0.15f;

    [Header("대시")]
    [Tooltip("대시 최대 거리 (유닛)")]
    [SerializeField] private float maxDashDistance = 8f;
    [Tooltip("대시에 걸리는 시간 (초). 히트스톱 동안은 대시도 함께 멈춘다.")]
    [SerializeField] private float dashDuration = 0.12f;

    [Header("쿨다운")]
    [Tooltip("대시 종료 후 다시 조준 가능해질 때까지의 시간 (초)")]
    [SerializeField] private float cooldown = 0.5f;
    [Tooltip("대시로 적을 하나라도 베면 쿨다운을 즉시 초기화해 연속 베기를 허용")]
    [SerializeField] private bool resetCooldownOnKill = true;

    [Header("베기 판정")]
    [Tooltip("이동 경로 주변 이 반경 안의 적을 벤다. (캐릭터 반지름에 더해진다)")]
    [SerializeField] private float slashRadius = 0.6f;
    [Tooltip("적/투사체가 속한 레이어. 기본값(Everything)이어도 컴포넌트 유무로 다시 걸러낸다.")]
    [SerializeField] private LayerMask enemyMask = ~0;

    [Header("일섬 반격")]
    [Tooltip("대시로 스친 투사체를 되받아친다.")]
    [SerializeField] private bool reflectProjectiles = true;
    [Tooltip("반사된 투사체의 속도 배율")]
    [SerializeField] private float reflectSpeedMultiplier = 1.5f;
    [Tooltip("반격 이펙트 색")]
    [SerializeField] private Color reflectVfxColor = new Color(1f, 0.85f, 0.3f);

    [Header("타격감")]
    [Tooltip("적 처치 순간 적용할 Time.timeScale")]
    [SerializeField, Range(0f, 1f)] private float hitStopScale = 0.05f;
    [Tooltip("히트스톱 지속 시간 (초, 실제 시간 기준)")]
    [SerializeField] private float hitStopDuration = 0.06f;
    [Tooltip("처치 이펙트 프리팹. 비워두면 코드로 생성한 파티클이 재생된다.")]
    [SerializeField] private GameObject hitVfxPrefab;
    [Tooltip("코드 생성 파티클의 색")]
    [SerializeField] private Color hitVfxColor = new Color(1f, 0.35f, 0.2f);

    [Header("참조")]
    [Tooltip("조준선. 비워두면 자식/본인에서 자동으로 찾는다.")]
    [SerializeField] private LineRenderer aimLine;
    [Tooltip("조준용 레이캐스트에 사용할 카메라. 비워두면 Camera.main")]
    [SerializeField] private Camera aimCamera;
    [Tooltip("조준선을 띄울 높이 (플레이어 발밑 기준 오프셋)")]
    [SerializeField] private float aimLineHeight = 0.1f;

    private CharacterController cc;

    private bool isAiming;
    private bool isDashing;
    private float cooldownEndTime;      // unscaled time 기준
    private float defaultFixedDeltaTime;
    private Vector3 aimTarget;

    // 한 번의 대시에서 이미 벤 적 — 같은 적을 여러 프레임에 걸쳐 중복 판정하지 않게
    private readonly HashSet<Enemy> hitThisDash = new HashSet<Enemy>();
    // 한 번의 대시에서 이미 되받아친 투사체 — 같은 돌을 매 프레임 다시 튕기지 않게
    private readonly HashSet<RockProjectile> reflectedThisDash = new HashSet<RockProjectile>();
    private readonly Collider[] overlapBuffer = new Collider[32];
    private Coroutine hitStopRoutine;
    private int killsThisDash;
    private Vector3 dashDirection = Vector3.forward;

    /// <summary>돌진 중인지 여부. PlayerMovement가 일반 이동을 막는 데 사용.</summary>
    public bool IsDashing => isDashing;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        defaultFixedDeltaTime = Time.fixedDeltaTime;

        if (aimCamera == null) aimCamera = Camera.main;
        if (aimLine == null) aimLine = GetComponentInChildren<LineRenderer>();

        if (aimLine != null)
        {
            aimLine.useWorldSpace = true;
            aimLine.positionCount = 2;
            aimLine.enabled = false;
        }
    }

    void Update()
    {
        if (isDashing) return;

        bool onCooldown = Time.unscaledTime < cooldownEndTime;

        // 조준 시작 — 쿨다운 중에는 아예 시작되지 않는다.
        if (!isAiming && Input.GetMouseButtonDown(0) && !onCooldown)
        {
            BeginAim();
        }

        if (isAiming)
        {
            if (Input.GetMouseButton(0))
            {
                aimTarget = ResolveAimTarget();
                DrawAimLine(transform.position, aimTarget);
            }
            else
            {
                // 버튼을 놓는 순간 발사
                EndAim();
                StartCoroutine(DashRoutine(aimTarget));
            }
        }
    }

    void BeginAim()
    {
        isAiming = true;
        aimTarget = ResolveAimTarget();

        // 연속 베기로 히트스톱이 아직 남아있으면 취소한다.
        // (그대로 두면 히트스톱이 끝나면서 조준 슬로모션을 풀어버린다)
        if (hitStopRoutine != null)
        {
            StopCoroutine(hitStopRoutine);
            hitStopRoutine = null;
        }

        Time.timeScale = slowMotionScale;
        Time.fixedDeltaTime = defaultFixedDeltaTime * slowMotionScale;

        if (aimLine != null) aimLine.enabled = true;
        DrawAimLine(transform.position, aimTarget);
    }

    void EndAim()
    {
        isAiming = false;
        RestoreTimeScale();
        if (aimLine != null) aimLine.enabled = false;
    }

    void RestoreTimeScale()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDeltaTime;
    }

    /// <summary>
    /// 카메라에서 마우스 위치로 레이를 쏴 플레이어 높이의 수평면과 만나는 점을 구하고,
    /// 최대 대시 거리로 클램프한다.
    /// </summary>
    Vector3 ResolveAimTarget()
    {
        Vector3 origin = transform.position;
        if (aimCamera == null) return origin;

        // 플레이어 발 높이를 기준으로 하는 바닥 평면
        Plane ground = new Plane(Vector3.up, origin);
        Ray ray = aimCamera.ScreenPointToRay(Input.mousePosition);

        if (!ground.Raycast(ray, out float enter)) return origin;

        Vector3 hit = ray.GetPoint(enter);
        Vector3 flat = new Vector3(hit.x - origin.x, 0f, hit.z - origin.z);

        if (flat.magnitude > maxDashDistance)
            flat = flat.normalized * maxDashDistance;

        return origin + flat;
    }

    void DrawAimLine(Vector3 from, Vector3 to)
    {
        if (aimLine == null) return;
        Vector3 up = Vector3.up * aimLineHeight;
        aimLine.SetPosition(0, from + up);
        aimLine.SetPosition(1, to + up);
    }

    IEnumerator DashRoutine(Vector3 target)
    {
        Vector3 delta = target - transform.position;
        delta.y = 0f;

        hitThisDash.Clear();
        reflectedThisDash.Clear();
        killsThisDash = 0;

        // 거의 제자리면 대시를 생략하되 쿨다운은 동일하게 적용
        if (delta.sqrMagnitude > 0.0001f && dashDuration > 0f)
        {
            isDashing = true;

            dashDirection = delta.normalized;
            transform.rotation = Quaternion.LookRotation(dashDirection);

            Vector3 velocity = delta / dashDuration;
            float elapsed = 0f;

            while (elapsed < dashDuration)
            {
                // 남은 시간보다 더 나아가지 않도록 스텝을 잘라낸다.
                float step = Mathf.Min(Time.deltaTime, dashDuration - elapsed);
                Vector3 previousPosition = transform.position;

                cc.Move(velocity * step);
                elapsed += step;

                // 이번 프레임에 지나온 구간을 베기 판정
                SlashAlong(previousPosition, transform.position);

                yield return null;
            }

            isDashing = false;
        }

        // 하나라도 베었으면 쿨다운 없이 바로 다음 조준 가능 (연속 베기 체인)
        bool chain = resetCooldownOnKill && killsThisDash > 0;
        cooldownEndTime = chain ? 0f : Time.unscaledTime + cooldown;

        hitThisDash.Clear();
        reflectedThisDash.Clear();
    }

    /// <summary>
    /// 이전 위치 → 현재 위치 구간을 감싸는 캡슐로 적을 훑어 즉사시킨다.
    /// 프레임 사이를 건너뛰어도 통과한 적을 놓치지 않는다.
    /// </summary>
    void SlashAlong(Vector3 from, Vector3 to)
    {
        // 캐릭터 몸통 중심 높이에서 이동 구간을 잇는 캡슐
        Vector3 center = cc.center;
        Vector3 p0 = from + center;
        Vector3 p1 = to + center;
        float radius = cc.radius + slashRadius;

        int count = Physics.OverlapCapsuleNonAlloc(
            p0, p1, radius, overlapBuffer, enemyMask, QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null) continue;

            // 자기 자신/바닥 등은 Enemy/RockProjectile 컴포넌트가 없으므로 자연히 걸러진다
            Enemy enemy = col.GetComponentInParent<Enemy>();
            if (enemy != null)
            {
                SlashEnemy(enemy);
                continue;
            }

            if (!reflectProjectiles) continue;

            RockProjectile rock = col.GetComponentInParent<RockProjectile>();
            if (rock != null) ReflectProjectile(rock);
        }
    }

    void SlashEnemy(Enemy enemy)
    {
        if (enemy.IsDead) return;
        if (!hitThisDash.Add(enemy)) return;   // 이미 이번 대시에서 벤 적

        Vector3 deathPosition = enemy.transform.position;
        enemy.Kill();

        killsThisDash++;
        PlayHitVfx(deathPosition);
        TriggerHitStop();
    }

    /// <summary>일섬 반격 — 스친 돌을 대시 방향으로 되받아친다.</summary>
    void ReflectProjectile(RockProjectile rock)
    {
        if (rock.IsReflected) return;              // 이미 아군 투사체
        if (!reflectedThisDash.Add(rock)) return;  // 같은 대시에서 중복 반사 방지

        rock.Reflect(dashDirection, reflectSpeedMultiplier);

        // 반격도 처치와 같은 타격감을 준다 (쿨다운 리셋은 처치에만)
        SlashVfx.Play(rock.transform.position, reflectVfxColor);
        TriggerHitStop();
    }

    void PlayHitVfx(Vector3 position)
    {
        if (hitVfxPrefab != null)
        {
            GameObject vfx = Instantiate(hitVfxPrefab, position, Quaternion.identity);

            // 파티클 프리팹이면 재생이 끝날 때 스스로 사라지게, 아니면 넉넉히 잡고 정리
            ParticleSystem ps = vfx.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                main.stopAction = ParticleSystemStopAction.Destroy;
            }
            else
            {
                Destroy(vfx, 3f);
            }
            return;
        }

        SlashVfx.Play(position, hitVfxColor);
    }

    /// <summary>
    /// 처치 순간 아주 짧게 시간을 얼린다. 연속 처치 시에는 타이머를 다시 시작한다.
    /// </summary>
    void TriggerHitStop()
    {
        if (hitStopDuration <= 0f) return;

        if (hitStopRoutine != null) StopCoroutine(hitStopRoutine);
        hitStopRoutine = StartCoroutine(HitStopRoutine());
    }

    IEnumerator HitStopRoutine()
    {
        Time.timeScale = hitStopScale;
        Time.fixedDeltaTime = defaultFixedDeltaTime * Mathf.Max(hitStopScale, 0.0001f);

        // 실제 시간 기준이라 timeScale이 0에 가까워도 정확히 풀린다
        yield return new WaitForSecondsRealtime(hitStopDuration);

        RestoreTimeScale();
        hitStopRoutine = null;
    }

    void OnDisable()
    {
        // 조준 중 비활성화되어도 시간이 느려진 채 남지 않도록
        if (isAiming)
        {
            isAiming = false;
            if (aimLine != null) aimLine.enabled = false;
        }
        isDashing = false;
        hitStopRoutine = null;   // 컴포넌트가 꺼지면 코루틴도 함께 중단된다
        hitThisDash.Clear();
        reflectedThisDash.Clear();
        RestoreTimeScale();
    }
}
