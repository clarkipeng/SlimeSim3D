using UnityEngine;

public class OrbitCamera : MonoBehaviour
{
    public Transform target;
    public float rotateSpeed = 5.0f;
    public float distance = 10.0f; // Now actually used!
    public float zoomSpeed = 2.0f;

    [SerializeField]
    public Color gizmoColor;

    // Internal variables to track rotation
    private float _currentYaw;
    private float _currentPitch;

    void Start()
    {
        // Initialize angles to match current view if target exists
        if (target != null)
        {
            Vector3 angles = transform.eulerAngles;
            _currentYaw = angles.y;
            _currentPitch = angles.x;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (Input.GetMouseButton(0))
        {
            _currentYaw += Input.GetAxis("Mouse X") * rotateSpeed;
            _currentPitch -= Input.GetAxis("Mouse Y") * rotateSpeed;
        }
        float scrollDelta = Input.GetAxis("Mouse ScrollWheel");
        distance -= scrollDelta * zoomSpeed;
        distance = Mathf.Clamp(distance, 1.0f, 100.0f);

        Quaternion rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0);
        Vector3 positionOffset = rotation * new Vector3(0, 0, -distance);

        transform.position = target.position + positionOffset;
        transform.LookAt(target.position);
    }

    void OnDrawGizmos()
    {
        if (target != null)
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(target.position, distance);

            Gizmos.DrawLine(target.position, transform.position);
        }
    }
}