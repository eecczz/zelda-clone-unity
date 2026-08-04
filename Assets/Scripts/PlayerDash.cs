using System.Collections;
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
    [Tooltip("대시에 걸리는 시간 (초, 실제 시간 기준)")]
    [SerializeField] private float dashDuration = 0.12f;

    [Header("쿨다운")]
    [Tooltip("대시 종료 후 다시 조준 가능해질 때까지의 시간 (초)")]
    [SerializeField] private float cooldown = 0.5f;

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

        // 거의 제자리면 대시를 생략하되 쿨다운은 동일하게 적용
        if (delta.sqrMagnitude > 0.0001f && dashDuration > 0f)
        {
            isDashing = true;

            transform.rotation = Quaternion.LookRotation(delta.normalized);

            Vector3 velocity = delta / dashDuration;
            float elapsed = 0f;

            while (elapsed < dashDuration)
            {
                // 남은 시간보다 더 나아가지 않도록 스텝을 잘라낸다.
                float step = Mathf.Min(Time.deltaTime, dashDuration - elapsed);
                cc.Move(velocity * step);
                elapsed += step;
                yield return null;
            }

            isDashing = false;
        }

        cooldownEndTime = Time.unscaledTime + cooldown;
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
        RestoreTimeScale();
    }
}
