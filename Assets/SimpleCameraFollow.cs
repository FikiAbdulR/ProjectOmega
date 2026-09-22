using UnityEngine;

public class SimpleCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0, 5, -7);
    [SerializeField] private float smoothSpeed = 5f;

    [Header("Lean Offset Settings")]
    [SerializeField] private float leanAmount = 1f;      // seberapa jauh offset.x bergeser
    [SerializeField] private float leanSmoothSpeed = 5f; // kecepatan transisi lean

    private PlayerMovement playerMovement;
    private float currentOffsetX;

    private void Start()
    {
        // Ambil reference PlayerMovement dari target
        playerMovement = target.GetComponent<PlayerMovement>();
    }

    private void LateUpdate()
    {
        float targetOffsetX = 0f;

        if (playerMovement != null)
        {
            float xInput = playerMovement.MoveInput.x;

            if (xInput < 0f)
            {
                // Belok kiri (A) -> offset.x menuju +1
                targetOffsetX = leanAmount;
            }
            else if (xInput > 0f)
            {
                // Belok kanan (D) -> offset.x menuju -1
                targetOffsetX = -leanAmount;
            }
            // kalau xInput == 0, targetOffsetX tetap 0 (kembali ke tengah)
        }

        // Smooth transisi offset.x
        currentOffsetX = Mathf.Lerp(currentOffsetX, targetOffsetX, leanSmoothSpeed * Time.deltaTime);

        Vector3 dynamicOffset = new Vector3(currentOffsetX, offset.y, offset.z);
        Vector3 desiredPosition = target.position + dynamicOffset;

        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.LookAt(target);
    }
}