using UnityEngine;

public class OrbitCamera : MonoBehaviour
{
    public Transform target;

    [Header("Settings")]
    public float rotateSpeed = 5.0f;
    public float zoomSpeed = 2.0f;
    public float minDistance = 1.0f;
    public float maxDistance = 100.0f;

    [Header("Current State")]
    public float distance = 10.0f;
    public float yaw = 0f;   // Previously _currentYaw
    public float pitch = 0f; // Previously _currentPitch

    [SerializeField]
    public Color gizmoColor = Color.cyan;

    void Start()
    {
        // Sync public attributes with initial transform if we have a target
        if (target != null)
        {
            Vector3 angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = angles.x;
            distance = Vector3.Distance(transform.position, target.position);
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Mouse Input modifies the public attributes directly
        if (Input.GetMouseButton(0))
        {
            yaw += Input.GetAxis("Mouse X") * rotateSpeed;
            pitch -= Input.GetAxis("Mouse Y") * rotateSpeed;
        }

        float scrollDelta = Input.GetAxis("Mouse ScrollWheel");
        distance -= scrollDelta * zoomSpeed;
        distance = Mathf.Clamp(distance, minDistance, maxDistance);

        // Apply the attributes to the Transform
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        Vector3 positionOffset = rotation * new Vector3(0, 0, -distance);

        transform.position = target.position + positionOffset;
        transform.LookAt(target.position);
    }

    void OnDrawGizmos()
    {
        if (target != null)
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(target.position, distance);
            Gizmos.DrawLine(target.position, transform.position);
        }
    }
}