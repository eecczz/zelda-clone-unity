using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 가장자리 방향 표시. 평소엔 가장 가까운 미클리어 거점을,
/// 필드 경계에 가까워지면 그쪽을 우선해 "돌아가라"를 가리킨다.
/// 화살표 이미지는 코드로 만들어 붙이므로 에디터에서 스프라이트를 넣을 필요가 없다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class CompassArrow : MonoBehaviour
{
    [Header("배치")]
    [Tooltip("화면 가장자리에서 띄울 여백 (픽셀)")]
    [SerializeField] private float edgePadding = 70f;
    [Tooltip("화살표 크기 (픽셀)")]
    [SerializeField] private float arrowSize = 46f;

    [Header("색")]
    [SerializeField] private Color campColor = new Color(0.722f, 0.910f, 1f, 0.85f);
    [SerializeField] private Color boundaryColor = new Color(1f, 0.35f, 0.3f, 0.95f);

    [Header("표시 조건")]
    [Tooltip("목표가 화면 안 이 여백보다 안쪽에 있으면 화살표를 숨긴다.")]
    [SerializeField] private float hideWhenVisibleMargin = 120f;

    [Header("참조")]
    [Tooltip("비워두면 Camera.main")]
    [SerializeField] private Camera viewCamera;
    [Tooltip("비워두면 Player 태그로 찾는다.")]
    [SerializeField] private Transform player;

    private RectTransform rect;
    private Image arrow;
    private Canvas canvas;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();

        if (viewCamera == null) viewCamera = Camera.main;

        BuildArrow();
    }

    void Start()
    {
        if (player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null) player = found.transform;
        }
    }

    void BuildArrow()
    {
        var go = new GameObject("Arrow", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        arrow = go.AddComponent<Image>();
        arrow.sprite = UiSprites.Triangle;
        arrow.raycastTarget = false;
        arrow.color = campColor;

        var arrowRect = arrow.rectTransform;
        arrowRect.sizeDelta = new Vector2(arrowSize, arrowSize);
        arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
        arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
        arrowRect.anchoredPosition = Vector2.zero;
    }

    void LateUpdate()
    {
        if (arrow == null) return;

        if (!TryResolveTarget(out Vector3 worldTarget, out Color color))
        {
            arrow.enabled = false;
            return;
        }

        PlaceArrow(worldTarget, color);
    }

    /// <summary>무엇을 가리킬지 정한다. 경계 경고가 거점보다 우선.</summary>
    bool TryResolveTarget(out Vector3 worldTarget, out Color color)
    {
        worldTarget = Vector3.zero;
        color = campColor;

        GameManager game = GameManager.Instance;
        if (game != null && !game.IsPlaying) return false;
        if (player == null || viewCamera == null) return false;

        FieldBounds bounds = FieldBounds.Instance;
        if (bounds != null && bounds.PlayerNearEdge)
        {
            worldTarget = bounds.Center;
            color = boundaryColor;
            return true;
        }

        EnemyCamp camp = EnemyCamp.NearestUncleared(player.position);
        if (camp == null) return false;

        worldTarget = camp.Center;
        return true;
    }

    void PlaceArrow(Vector3 worldTarget, Color color)
    {
        Vector3 screenPoint = viewCamera.WorldToScreenPoint(worldTarget);

        // 카메라 뒤쪽이면 좌표가 뒤집히므로 되돌려 놓는다
        if (screenPoint.z < 0f)
        {
            screenPoint.x = Screen.width - screenPoint.x;
            screenPoint.y = Screen.height - screenPoint.y;
        }

        Vector2 screenCenter = new Vector2(Screen.width, Screen.height) * 0.5f;
        Vector2 fromCenter = new Vector2(screenPoint.x, screenPoint.y) - screenCenter;

        bool behind = screenPoint.z < 0f;
        bool onScreen = !behind
                        && Mathf.Abs(fromCenter.x) < screenCenter.x - hideWhenVisibleMargin
                        && Mathf.Abs(fromCenter.y) < screenCenter.y - hideWhenVisibleMargin;

        if (onScreen)
        {
            arrow.enabled = false;
            return;
        }

        arrow.enabled = true;
        arrow.color = color;

        if (fromCenter.sqrMagnitude < 0.01f) fromCenter = Vector2.up;

        // 화면 가장자리(패딩만큼 안쪽)의 사각형에 방향 벡터를 투영한다
        Vector2 limit = screenCenter - Vector2.one * edgePadding;
        Vector2 dir = fromCenter.normalized;

        float scaleX = Mathf.Abs(dir.x) > 0.0001f ? limit.x / Mathf.Abs(dir.x) : float.MaxValue;
        float scaleY = Mathf.Abs(dir.y) > 0.0001f ? limit.y / Mathf.Abs(dir.y) : float.MaxValue;
        Vector2 edgePoint = dir * Mathf.Min(scaleX, scaleY);

        rect.position = ScreenToCanvas(screenCenter + edgePoint);

        // 스프라이트가 위를 향하므로 up 기준으로 각도를 잰다
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    Vector3 ScreenToCanvas(Vector2 screenPosition)
    {
        // Screen Space - Overlay면 스크린 좌표가 곧 월드 좌표다
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return new Vector3(screenPosition.x, screenPosition.y, 0f);

        Camera uiCamera = canvas.worldCamera != null ? canvas.worldCamera : viewCamera;
        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            rect.parent as RectTransform, screenPosition, uiCamera, out Vector3 world);
        return world;
    }
}
