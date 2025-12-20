using UnityEngine;

public class OrbitCamera : MonoBehaviour
{
    public Transform target;

    [Header("Settings")]
    public float rotateSpeed = 5.0f;
    public float zoomSpeed = 2.0f;
    public float minRadius = 1.0f;
    public float maxRadius = 100.0f;

    [Range(1f, 20f)]
    public float smoothSpeed = 10f;

    [Header("Current State")]
    public float radius = 10.0f;
    public float yaw = 0f;
    public float pitch = 0f;
    private float targetRadius;
    private float targetYaw;
    private float targetPitch;

    [SerializeField]
    public Color gizmoColor = Color.cyan;

    void Start()
    {
        // Sync public attributes with initial transform if we have a target
        if (target != null)
        {
            Vector3 angles = transform.eulerAngles;
            yaw = targetYaw = angles.y;
            pitch = targetPitch = angles.x;


            if (pitch > 180) pitch -= 360;

            radius = targetRadius = Vector3.Distance(transform.position, target.position);
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Mouse Input modifies the public attributes directly
        if (Input.GetMouseButton(0))
        {
            targetYaw += Input.GetAxis("Mouse X") * rotateSpeed;
            targetPitch -= Input.GetAxis("Mouse Y") * rotateSpeed;

            targetPitch = Mathf.Clamp(targetPitch, -89f, 89f);
        }

        float scrollDelta = Input.GetAxis("Mouse ScrollWheel");
        targetRadius -= scrollDelta * zoomSpeed;
        targetRadius = Mathf.Clamp(targetRadius, minRadius, maxRadius);

        float lerpFactor = Time.deltaTime * smoothSpeed;
        yaw = Mathf.Lerp(yaw, targetYaw, lerpFactor);
        pitch = Mathf.Lerp(pitch, targetPitch, lerpFactor);
        radius = Mathf.Lerp(radius, targetRadius, lerpFactor);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        Vector3 positionOffset = rotation * new Vector3(0, 0, -radius);

        transform.position = target.position + positionOffset;
        transform.rotation = rotation;
    }

    void OnDrawGizmos()
    {
        if (target != null)
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(target.position, radius);
            Gizmos.DrawLine(target.position, transform.position);
        }
    }


    public void setYaw(float x)
    {
        yaw = targetYaw = x;
    }
    public void setPitch(float x)
    {
        pitch = targetPitch = x;
    }
    public void setRadius(float x)
    {
        radius = targetRadius = x;
    }
}