using UnityEngine;

/// <summary>
/// 소프트 경계. 보이지 않는 벽 대신 가장자리에서 안쪽으로 밀어낸다.
/// 밀림의 세기는 얼마나 나갔는지에 비례하므로, 살짝 나가면 살짝만 되돌아온다.
/// </summary>
public class FieldBounds : MonoBehaviour
{
    public static FieldBounds Instance { get; private set; }

    [Header("필드")]
    [Tooltip("정사각형 필드 한 변의 길이 (유닛). Plane 스케일과 맞출 것.")]
    [SerializeField] private float fieldSize = 200f;
    [Tooltip("경계 안쪽 이 폭부터 경고와 밀어내기가 시작된다.")]
    [SerializeField] private float softMargin = 18f;

    [Header("밀어내기")]
    [Tooltip("경계에 완전히 닿았을 때의 되돌리는 속도 (유닛/초)")]
    [SerializeField] private float pushSpeed = 10f;
    [Tooltip("경계 바깥으로 나갔을 때 추가로 곱하는 배율")]
    [SerializeField] private float outsidePushMultiplier = 3f;

    [Header("참조")]
    [Tooltip("비워두면 Player 태그로 찾는다.")]
    [SerializeField] private Transform player;

    private CharacterController playerController;

    public Vector3 Center => transform.position;
    public float HalfSize => fieldSize * 0.5f;

    /// <summary>경계에 얼마나 가까운지 0~1. 0이면 안전, 1이면 경계선.</summary>
    public float EdgeSeverity { get; private set; }

    /// <summary>경고를 띄워야 하는 상태인지.</summary>
    public bool PlayerNearEdge => EdgeSeverity > 0.01f;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null) player = found.transform;
        }

        if (player != null) playerController = player.GetComponent<CharacterController>();
    }

    // 이동/대시가 끝난 뒤에 보정해야 밀림이 씹히지 않는다
    void LateUpdate()
    {
        if (player == null) return;

        Vector3 offset = player.position - Center;
        float limit = HalfSize;
        float inner = Mathf.Max(0f, limit - softMargin);

        Vector3 push = Vector3.zero;
        float severity = 0f;

        severity = Mathf.Max(severity, AxisPush(offset.x, inner, limit, out float pushX));
        severity = Mathf.Max(severity, AxisPush(offset.z, inner, limit, out float pushZ));

        push.x = pushX;
        push.z = pushZ;

        EdgeSeverity = severity;

        if (push.sqrMagnitude < 0.000001f) return;

        Vector3 motion = push * pushSpeed * Time.deltaTime;

        if (playerController != null && playerController.enabled) playerController.Move(motion);
        else player.position += motion;
    }

    /// <summary>한 축의 밀림 방향/세기와 경계 근접도를 계산한다.</summary>
    float AxisPush(float value, float inner, float limit, out float push)
    {
        push = 0f;

        float distance = Mathf.Abs(value);
        if (distance <= inner) return 0f;

        float sign = Mathf.Sign(value);
        float severity = Mathf.Clamp01((distance - inner) / Mathf.Max(0.001f, limit - inner));

        push = -sign * severity;

        // 완전히 나가버렸으면 확실하게 되돌린다
        if (distance > limit) push *= outsidePushMultiplier;

        return severity;
    }

    /// <summary>필드 중심으로 돌아가는 방향 (수평).</summary>
    public Vector3 DirectionToCenter(Vector3 from)
    {
        Vector3 toCenter = Center - from;
        toCenter.y = 0f;
        return toCenter.sqrMagnitude > 0.0001f ? toCenter.normalized : Vector3.forward;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(center, new Vector3(fieldSize, 0.1f, fieldSize));

        Gizmos.color = Color.yellow;
        float innerSize = Mathf.Max(0f, fieldSize - softMargin * 2f);
        Gizmos.DrawWireCube(center, new Vector3(innerSize, 0.1f, innerSize));
    }
}
