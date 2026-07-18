using UnityEngine;

public class CameraShakeImpulse : MonoBehaviour
{
    [SerializeField] private ThirdPersonActionCamera targetCamera;

    private void Awake()
    {
        if (targetCamera == null && Camera.main != null)
            targetCamera = Camera.main.GetComponent<ThirdPersonActionCamera>();
    }

    public void Shoot()
    {
        if (targetCamera != null)
            targetCamera.PlayShootShake();
    }

    public void Hit()
    {
        if (targetCamera != null)
            targetCamera.PlayHitShake();
    }
}
