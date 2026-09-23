using UnityEngine;

public class GroundSnap : MonoBehaviour
{
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float rayHeight = 1f;   // titik mulai ray, di atas object
    [SerializeField] private float rayLength = 2f;   // jarak deteksi ke bawah
    [SerializeField] private float offset = 0f;      // jarak object dari permukaan (misal 0.5 kalau pivot di tengah cube)

    private void LateUpdate()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * rayHeight;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayHeight + rayLength, groundLayer))
        {
            Vector3 newPosition = transform.position;
            newPosition.y = hit.point.y + offset;
            transform.position = newPosition;

            Debug.DrawLine(rayOrigin, hit.point, Color.green);
        }
        else
        {
            Debug.DrawRay(rayOrigin, Vector3.down * (rayHeight + rayLength), Color.red);
        }
    }
}