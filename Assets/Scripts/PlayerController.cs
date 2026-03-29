using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Audio — driving / engine")]
    [Tooltip("Looping engine sound. Leave empty to use a simple built-in tone.")]
    public AudioClip engineLoopClip;
    [Range(0f, 1f)] public float engineVolume = 0.45f;
    [Range(0.5f, 2f)] public float minEnginePitch = 0.75f;
    [Range(0.5f, 2f)] public float maxEnginePitch = 1.35f;
    [Tooltip("How much steering adds pitch (subtle rev feel).")]
    [Range(0f, 0.2f)] public float steerPitchBlend = 0.06f;

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
    AudioSource engineAudio;
    static AudioClip generatedEngineLoop;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        engineAudio = GetComponent<AudioSource>();
        if (engineAudio == null)
            engineAudio = gameObject.AddComponent<AudioSource>();
        engineAudio.playOnAwake = false;
        engineAudio.loop = true;
        engineAudio.spatialBlend = 0f;
        engineAudio.clip = engineLoopClip != null ? engineLoopClip : GetOrCreateGeneratedEngineClip();
        if (engineAudio.clip != null)
            engineAudio.Play();
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
        if (WasSwitchCameraPressedThisFrame() && thirdPersonCamera != null && driverCamera != null)
        {
            thirdPersonCamera.enabled = !thirdPersonCamera.enabled;
            driverCamera.enabled = !driverCamera.enabled;
        }
    }

    void FixedUpdate()
    {
        float forwardInput = ReadAxis(forwardAxis, vertical: true);
        float lateralInput = ReadAxis(lateralAxis, vertical: false);

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

        UpdateEngineAudio(forwardInput, lateralInput);
    }

    void UpdateEngineAudio(float forwardInput, float lateralInput)
    {
        if (engineAudio == null)
            return;

        float speedNorm = maxForwardSpeed > 0.01f
            ? Mathf.Clamp01(Mathf.Abs(currentSpeed) / maxForwardSpeed)
            : 0f;
        float inputNorm = Mathf.Clamp01(Mathf.Abs(forwardInput) + Mathf.Abs(lateralInput) * steerPitchBlend);
        float pitchBlend = Mathf.Max(speedNorm, inputNorm * 0.35f);
        float pitch = Mathf.Lerp(minEnginePitch, maxEnginePitch, pitchBlend);
        pitch += Mathf.Abs(lateralInput) * steerPitchBlend;
        engineAudio.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
        engineAudio.volume = engineVolume;
    }

    static AudioClip GetOrCreateGeneratedEngineClip()
    {
        if (generatedEngineLoop != null)
            return generatedEngineLoop;

        const int sampleRate = 44100;
        const float seconds = 1f;
        int n = (int)(sampleRate * seconds);
        var samples = new float[n];
        const float freq = 88f;
        float amp = 0.1f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sampleRate;
            float s = Mathf.Sin(2f * Mathf.PI * freq * t);
            s += 0.35f * Mathf.Sin(2f * Mathf.PI * freq * 2.1f * t);
            samples[i] = amp * s;
        }

        generatedEngineLoop = AudioClip.Create("GeneratedEngineLoop", n, 1, sampleRate, false);
        generatedEngineLoop.SetData(samples, 0);
        return generatedEngineLoop;
    }

    /// <summary>
    /// Uses legacy Input Manager when enabled; falls back to the new Input System (keyboard + gamepad)
    /// when the project is set to Input System only or the legacy axis is idle.
    /// </summary>
    float ReadAxis(string axisName, bool vertical)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        float v = Input.GetAxis(axisName);
#if ENABLE_INPUT_SYSTEM
        if (Mathf.Abs(v) > 0.01f)
            return Mathf.Clamp(v, -1f, 1f);
        return ReadAxisFromInputDevices(vertical);
#else
        return Mathf.Clamp(v, -1f, 1f);
#endif
#else
#if ENABLE_INPUT_SYSTEM
        return ReadAxisFromInputDevices(vertical);
#else
        return 0f;
#endif
#endif
    }

#if ENABLE_INPUT_SYSTEM
    static float ReadAxisFromInputDevices(bool vertical)
    {
        var gp = Gamepad.current;
        if (gp != null)
        {
            float stick = vertical ? gp.leftStick.y.ReadValue() : gp.leftStick.x.ReadValue();
            if (Mathf.Abs(stick) > 0.01f)
                return Mathf.Clamp(stick, -1f, 1f);
        }

        var kb = Keyboard.current;
        if (kb == null)
            return 0f;

        if (vertical)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) return 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) return -1f;
        }
        else
        {
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) return 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) return -1f;
        }

        return 0f;
    }
#endif

    bool WasSwitchCameraPressedThisFrame()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(switchCameraKey))
            return true;
#endif
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && switchCameraKey == KeyCode.C && kb.cKey.wasPressedThisFrame)
            return true;
#endif
        return false;
    }
}
