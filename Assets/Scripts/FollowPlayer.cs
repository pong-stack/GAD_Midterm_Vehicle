using UnityEngine;

public class FollowPlayer : MonoBehaviour
{
    public GameObject player;
    [Min(0.01f)]
    public float positionSmoothTime = 0.12f;

    Vector3 offset;
    Vector3 smoothVelocity;

    void Start()
    {
        if (player != null)
            offset = transform.position - player.transform.position;
    }

    void LateUpdate()
    {
        if (player == null)
            return;

        Vector3 target = player.transform.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, target, ref smoothVelocity, positionSmoothTime);
    }
}
