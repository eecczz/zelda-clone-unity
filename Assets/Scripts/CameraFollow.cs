using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;                       // Player를 여기에 드래그
    public Vector3 offset = new Vector3(0f, 12f, -8f);
    public float smoothTime = 0.15f;

    private Vector3 velocity;

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 desired = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
        transform.LookAt(target.position + Vector3.up * 1f);
    }
}