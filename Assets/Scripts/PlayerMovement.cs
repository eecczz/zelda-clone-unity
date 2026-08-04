using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("이동")]
    public float moveSpeed = 6f;
    public float rotationSpeed = 12f;

    private CharacterController cc;
    private PlayerDash dash;
    private float verticalVelocity;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        dash = GetComponent<PlayerDash>();
    }

    void Update()
    {
        // 일섬 대시 중에는 일반 이동 입력을 무시 (대시가 이동을 전담)
        if (dash != null && dash.IsDashing) return;

        float h = Input.GetAxisRaw("Horizontal"); // A/D, ←/→
        float v = Input.GetAxisRaw("Vertical");   // W/S, ↑/↓
        Vector3 dir = new Vector3(h, 0f, v).normalized;

        // 중력 (땅에 붙어있게)
        if (cc.isGrounded) verticalVelocity = -1f;
        else verticalVelocity -= 20f * Time.deltaTime;

        Vector3 motion = dir * moveSpeed + Vector3.up * verticalVelocity;
        cc.Move(motion * Time.deltaTime);

        // 이동 방향으로 부드럽게 회전
        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, rotationSpeed * Time.deltaTime);
        }
    }
}