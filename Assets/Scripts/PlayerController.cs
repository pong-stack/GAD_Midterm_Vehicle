using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float maxForwardSpeed = 24f;
    public float accelerationTime = 0.28f;
    public float decelerationTime = 0.45f;
    public float maxRotationSpeed = 72f;
    [Tooltip("Lets you steer a bit while coasting, not only when accelerating.")]
    [Range(0f, 1f)]
    public float steerWhileCoasting = 0.22f;

    [Header("Input")]
    public string forwardAxis = "Vertical";
    public string lateralAxis = "Horizontal";

    [Header("Cameras")]
    public Camera thirdPersonCamera;
    public Camera driverCamera;
    public KeyCode switchCameraKey = KeyCode.C;

    Rigidbody rb;
    float currentSpeed;
    float speedSmoothVelocity;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    void Start()
    {
        if (thirdPersonCamera != null)
            thirdPersonCamera.enabled = true;
        if (driverCamera != null)
            driverCamera.enabled = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(switchCameraKey) && thirdPersonCamera != null && driverCamera != null)
        {
            thirdPersonCamera.enabled = !thirdPersonCamera.enabled;
            driverCamera.enabled = !driverCamera.enabled;
        }
    }

    void FixedUpdate()
    {
        float forwardInput = Input.GetAxis(forwardAxis);
        float lateralInput = Input.GetAxis(lateralAxis);

        float targetSpeed = forwardInput * maxForwardSpeed;
        float smoothTime = Mathf.Abs(targetSpeed) > 0.01f ? accelerationTime : decelerationTime;
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedSmoothVelocity, smoothTime);

        Vector3 planarForward = transform.forward;
        planarForward.y = 0f;
        if (planarForward.sqrMagnitude < 0.0001f)
            planarForward = Vector3.forward;
        else
            planarForward.Normalize();

        Vector3 horizontalVelocity = planarForward * currentSpeed;
        rb.velocity = new Vector3(horizontalVelocity.x, rb.velocity.y, horizontalVelocity.z);

        float steerFactor = Mathf.Clamp01(Mathf.Abs(forwardInput) + steerWhileCoasting);
        float yawDegrees = lateralInput * maxRotationSpeed * steerFactor * Time.fixedDeltaTime;
        Quaternion deltaYaw = Quaternion.Euler(0f, yawDegrees, 0f);
        rb.MoveRotation(rb.rotation * deltaYaw);
    }
}
