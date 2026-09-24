using UnityEngine;

// Efek TrailRenderer di kedua kaki, mengikuti status napak tanah dari ThirdPersonController.
// - Napak tanah (grounded)  -> trail.time = groundedTrailTime (default 1, trail terlihat)
// - Tidak napak (di udara)  -> trail.time = airborneTrailTime (default 0, trail hilang)
//
// Setup:
// 1. Pastikan kedua kaki sudah punya komponen TrailRenderer masing-masing.
// 2. Attach script ini di GameObject manapun (boleh di Player, atau GameObject terpisah).
// 3. Drag GameObject Player (yang punya komponen ThirdPersonController) ke field "controller".
// 4. Drag TrailRenderer kaki kiri & kanan ke field "leftFootTrail" / "rightFootTrail".
[DisallowMultipleComponent]
public class FootTrail : MonoBehaviour
{
    [Header("Referensi")]
    [SerializeField] private ThirdPersonController controller; // GameObject Player yang punya ThirdPersonController
    [SerializeField] private TrailRenderer leftFootTrail;
    [SerializeField] private TrailRenderer rightFootTrail;

    [Header("Trail Time")]
    [SerializeField] private float groundedTrailTime = 1f; // saat napak tanah
    [SerializeField] private float airborneTrailTime = 0f; // saat di udara (jump/jatuh)

    private void Reset()
    {
        // Auto-isi kalau script ini ditaruh sebagai child dari Player
        controller = GetComponentInParent<ThirdPersonController>();
    }

    private void Update()
    {
        if (controller == null) return;

        float targetTime = controller.IsGrounded ? groundedTrailTime : airborneTrailTime;

        ApplyTrailTime(leftFootTrail, targetTime);
        ApplyTrailTime(rightFootTrail, targetTime);
    }

    private void ApplyTrailTime(TrailRenderer trail, float time)
    {
        if (trail == null) return;
        trail.time = time;
    }
}