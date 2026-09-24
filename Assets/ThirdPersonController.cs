using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging; // butuh package "Animation Rigging" (com.unity.animation.rigging)

// Simple Third Person Controller + Camera Look
// Requirement: Unity Input System package (Edit > Project Settings > Player > Active Input Handling = Input System Package (New))
//
// Setup singkat:
// 1. Buat GameObject "Player" -> tambahkan CharacterController component.
// 2. Attach script ini ke "Player".
// 3. Buat child GameObject "CameraPivot" di bawah Player, posisikan di sekitar dada (contoh: 0, 1.5, 0).
// 4. Buat Main Camera sebagai child dari "CameraPivot", mundurkan posisi camera (contoh: 0, 1.5, -4) menghadap ke pivot.
// 5. Di Inspector script ini, drag CameraPivot ke field "cameraPivot", dan Main Camera transform ke field "cameraTransform" (opsional, untuk collision-safe zoom nanti).
// 6. Buat Input Actions Asset baru (klik kanan Project > Create > Input Actions), atau biarkan kosong -
//    script ini sudah membaca input langsung lewat Keyboard.current & Mouse.current, jadi TIDAK WAJIB
//    membuat Input Actions Asset manual. Cukup pastikan package Input System sudah terpasang.

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4.5f;
    [SerializeField] private float runSpeedMultiplier = 1.8f; // kecepatan lari = moveSpeed * multiplier ini
    [SerializeField] private float rotationSmoothTime = 0.1f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -20f;

    [Header("Camera Look")]
    [SerializeField] private Transform cameraPivot;      // parent kosong tempat camera berputar (pitch)
    [SerializeField] private float mouseSensitivity = 2.5f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 60f;

    [Header("Sprint FOV Effect")]
    [SerializeField] private Camera playerCamera;        // drag Main Camera ke sini
    [SerializeField] private float normalFOV = 60f;
    [SerializeField] private float sprintFOV = 72f;
    [SerializeField] private float fovLerpSpeed = 8f;     // makin besar makin cepat transisinya

    [Header("Focus / Lock-On System")]
    [SerializeField] private Transform[] focusTargets;           // daftar target yang bisa di-lock (Target 1, Target 2, dst)
    [SerializeField] private MultiAimConstraint headAimConstraint; // Multi-Aim Constraint di kepala (Animation Rigging)
    [SerializeField] private float focusCameraLerpSpeed = 6f;    // seberapa cepat kamera "snap" ke target
    [SerializeField] private float headAimWeightLerpSpeed = 8f;  // seberapa cepat kepala mulai/berhenti menoleh
    [SerializeField] private float lockFacingLerpSpeed = 10f;    // seberapa cepat body player menghadap target saat fokus (dipakai buat orbit WASD)

    [Header("Shift Head IK")]
    [SerializeField] private TwoBoneIKConstraint shiftHeadIKFront; // aktif (weight 1) saat Shift + jalan ke depan (W)
    [SerializeField] private TwoBoneIKConstraint shiftHeadIKBack;  // aktif (weight 1) saat Shift + jalan mundur (S)
    [SerializeField] private float shiftHeadIKLerpSpeed = 8f; // seberapa cepat transisi weight-nya

    [Header("Boost / Stamina System")]
    [SerializeField] private float maxBoost = 100f;
    [SerializeField] private float boostDrainRate = 25f;  // berkurang per detik saat Shift dipakai buat boost
    [SerializeField] private float boostRegenRate = 15f;  // terisi per detik saat tidak dipakai & tidak cooldown
    [SerializeField] private float boostCooldownDuration = 2f; // detik, dipaksa nunggu setelah boost habis total (0)

    private CharacterController controller;
    private Vector3 velocity;
    private float verticalVelocity;
    private float playerYaw;    // rotasi PLAYER, selalu mengikuti mouse horizontal (apapun mode fokus kamera)
    private float cursorPitch;  // pitch mouse mentah, dipakai kamera saat mode fokus "cursor"
    private float cameraYaw;    // yaw AKTUAL yang diterapkan ke cameraPivot
    private float cameraPitch;  // pitch AKTUAL yang diterapkan ke cameraPivot
    private float rotationVelocity;
    private bool isSprinting;
    private int currentFocusIndex = -1; // -1 = none, 0 = target 1, 1 = target 2, dst

    private float currentBoost;   // sisa boost saat ini (0..maxBoost)
    private float cooldownTimer;  // hitung mundur cooldown saat boost habis total
    private bool isOnCooldown;
    private bool canSprint;       // true kalau boleh sprint frame ini (ada boost & tidak cooldown)

    // Dibaca script lain (mis. efek trail kaki) buat tahu apakah player sedang napak tanah
    public bool IsGrounded { get; private set; }

    // Dibaca script lain kalau perlu tahu sisa boost / status cooldown
    public float CurrentBoost => currentBoost;
    public float MaxBoost => maxBoost;
    public bool IsOnBoostCooldown => isOnCooldown;
    public float BoostCooldownRemaining => isOnCooldown ? cooldownTimer : 0f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerYaw = transform.eulerAngles.y;
        cameraYaw = playerYaw;

        currentBoost = maxBoost;

        if (playerCamera != null)
        {
            playerCamera.fieldOfView = normalFOV;
        }
    }

    private void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleFocusInput();
        HandleCameraLook();
        HandleBoostSystem();
        HandleMovementAndJump();
        HandleSprintFOV();
        HandleHeadAimWeight();
        HandleShiftHeadIK();
    }

    private void HandleCameraLook()
    {
        Mouse mouse = Mouse.current;
        Vector2 lookDelta = Vector2.zero;
        if (mouse != null)
        {
            lookDelta = mouse.delta.ReadValue();

            // Pitch mentah dari mouse, tetap dipakai kamera (baik mode cursor maupun sebagai basis mode target)
            cursorPitch -= lookDelta.y * mouseSensitivity * Time.deltaTime * 10f;
            cursorPitch = Mathf.Clamp(cursorPitch, minPitch, maxPitch);
        }

        Transform lockedTarget = GetCurrentFocusTarget();

        if (lockedTarget != null)
        {
            // Mode fokus TARGET: player otomatis menghadap target (bukan mouse) supaya
            // WASD bergerak relatif ke target -> W/S mendekat/menjauh, A/D orbit mengelilingi target.
            HandleLockedPlayerFacing(lockedTarget);
            HandleFocusCameraLook(lockedTarget);
        }
        else
        {
            // Mode fokus CURSOR (default): player mengikuti rotasi horizontal mouse seperti biasa
            playerYaw += lookDelta.x * mouseSensitivity * Time.deltaTime * 10f;
            cameraYaw = playerYaw;
            cameraPitch = cursorPitch;
        }

        // Player selalu diterapkan dari playerYaw (mouse saat bebas, atau arah-ke-target saat lock)
        transform.rotation = Quaternion.Euler(0f, playerYaw, 0f);

        if (cameraPivot != null)
        {
            cameraPivot.rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
        }
    }

    // Memutar body player secara halus supaya selalu menghadap target saat mode fokus aktif
    private void HandleLockedPlayerFacing(Transform target)
    {
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        float targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        playerYaw = Mathf.LerpAngle(playerYaw, targetYaw, lockFacingLerpSpeed * Time.deltaTime);
    }

    private void HandleFocusCameraLook(Transform target)
    {
        Vector3 originPos = cameraPivot != null ? cameraPivot.position : transform.position;
        Vector3 dir = target.position - originPos;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion lookRot = Quaternion.LookRotation(dir.normalized);
        Vector3 e = lookRot.eulerAngles;

        float targetYaw = e.y;
        float targetPitch = e.x;
        if (targetPitch > 180f) targetPitch -= 360f; // normalisasi ke -180..180 supaya clamp benar
        targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);

        // LerpAngle supaya transisi tetap halus walau melewati batas 0/360 derajat
        cameraYaw = Mathf.LerpAngle(cameraYaw, targetYaw, focusCameraLerpSpeed * Time.deltaTime);
        cameraPitch = Mathf.LerpAngle(cameraPitch, targetPitch, focusCameraLerpSpeed * Time.deltaTime);
    }

    private void HandleMovementAndJump()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        // Baca WASD dari New Input System
        Vector2 input = Vector2.zero;
        if (kb.wKey.isPressed) input.y += 1f;
        if (kb.sKey.isPressed) input.y -= 1f;
        if (kb.aKey.isPressed) input.x -= 1f;
        if (kb.dKey.isPressed) input.x += 1f;
        input = Vector2.ClampMagnitude(input, 1f);

        // Sprint hanya boleh kalau boost system mengizinkan (ada sisa boost & tidak cooldown)
        bool isRunning = canSprint;
        // Sprint FOV hanya aktif kalau player benar-benar sedang bergerak (bukan cuma nahan shift diam)
        isSprinting = isRunning && input.sqrMagnitude > 0.01f;
        float currentSpeed = isRunning ? moveSpeed * runSpeedMultiplier : moveSpeed;

        // Arah gerak relatif ke arah hadap player (mouse saat mode cursor, arah-ke-target saat mode fokus)
        Vector3 moveDir = (transform.forward * input.y + transform.right * input.x);
        moveDir = Vector3.ClampMagnitude(moveDir, 1f);

        // Gravity & ground check
        bool isGrounded = controller.isGrounded;
        IsGrounded = isGrounded;
        if (isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f; // menempel ke tanah
        }

        // Lompat dengan Space
        if (isGrounded && kb.spaceKey.wasPressedThisFrame)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 finalMove = moveDir * currentSpeed;
        finalMove.y = verticalVelocity;

        controller.Move(finalMove * Time.deltaTime);
    }

    private void HandleSprintFOV()
    {
        if (playerCamera == null) return;

        float targetFOV = isSprinting ? sprintFOV : normalFOV;
        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFOV,
            fovLerpSpeed * Time.deltaTime
        );
    }

    // ---------------- Boost / Stamina ----------------

    // Aturan:
    // - Shift ditahan -> boost berkurang (boostDrainRate/detik).
    // - Kalau boost habis sampai 0 -> masuk cooldown selama boostCooldownDuration detik, TIDAK bisa sprint selama itu.
    //   Begitu cooldown SELESAI, boost langsung diisi PENUH 100% (bukan bertahap).
    // - Regen bertahap (boostRegenRate/detik) HANYA berlaku kalau boost belum pernah sampai 0
    //   (mis. Shift dilepas saat boost masih tersisa sebagian) -> di luar jalur cooldown.
    private void HandleBoostSystem()
    {
        Keyboard kb = Keyboard.current;
        bool shiftHeld = kb != null && kb.leftShiftKey.isPressed;

        // Hitung mundur cooldown dulu
        if (isOnCooldown)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f)
            {
                isOnCooldown = false;
                cooldownTimer = 0f;
                currentBoost = maxBoost; // cooldown selesai -> langsung penuh 100%, bukan regen bertahap
            }
        }

        // Boleh sprint kalau: Shift ditahan, tidak sedang cooldown, dan masih ada sisa boost
        canSprint = shiftHeld && !isOnCooldown && currentBoost > 0f;

        if (canSprint)
        {
            currentBoost -= boostDrainRate * Time.deltaTime;

            if (currentBoost <= 0f)
            {
                currentBoost = 0f;
                isOnCooldown = true;
                cooldownTimer = boostCooldownDuration;
                canSprint = false; // begitu habis, langsung nonaktif di frame yang sama juga
            }
        }
        else if (!isOnCooldown)
        {
            // Tidak dipakai buat boost & tidak cooldown (belum pernah habis total) -> isi ulang bertahap
            currentBoost += boostRegenRate * Time.deltaTime;
            currentBoost = Mathf.Clamp(currentBoost, 0f, maxBoost);
        }
    }

    // ---------------- Focus / Lock-On ----------------

    private void HandleFocusInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.qKey.wasPressedThisFrame)
        {
            CycleFocusTarget();
        }
    }

    // Urutan cycle: None -> Target 1 -> Target 2 -> ... -> None (ulang dari awal)
    private void CycleFocusTarget()
    {
        if (focusTargets == null || focusTargets.Length == 0) return;

        currentFocusIndex++;
        if (currentFocusIndex >= focusTargets.Length)
        {
            currentFocusIndex = -1; // balik ke "none"
        }

        SetHeadAimSource(GetCurrentFocusTarget());
    }

    private Transform GetCurrentFocusTarget()
    {
        if (currentFocusIndex < 0 || focusTargets == null) return null;
        if (currentFocusIndex >= focusTargets.Length) return null;
        return focusTargets[currentFocusIndex];
    }

    // Mengganti source object Multi-Aim Constraint sesuai target yang sedang di-lock
    private void SetHeadAimSource(Transform target)
    {
        if (headAimConstraint == null) return;

        var sources = new WeightedTransformArray();
        if (target != null)
        {
            sources.Add(new WeightedTransform(target, 1f));
        }
        headAimConstraint.data.sourceObjects = sources;
    }

    // Blend weight constraint 0->1 (nyala) atau 1->0 (mati) supaya kepala tidak "snap" mendadak
    private void HandleHeadAimWeight()
    {
        if (headAimConstraint == null) return;

        float targetWeight = currentFocusIndex >= 0 ? 1f : 0f;
        headAimConstraint.weight = Mathf.Lerp(
            headAimConstraint.weight,
            targetWeight,
            headAimWeightLerpSpeed * Time.deltaTime
        );
    }

    // Weight IK front/back: Shift + maju (W) -> front = 1, back = 0. Shift + mundur (S) -> back = 1, front = 0.
    // Selain kondisi itu (tidak sprint, atau sprint tanpa maju/mundur) -> keduanya balik ke 0.
    private void HandleShiftHeadIK()
    {
        if (shiftHeadIKFront == null && shiftHeadIKBack == null) return;

        Keyboard kb = Keyboard.current;
        bool shiftHeld = kb != null && kb.leftShiftKey.isPressed;
        bool movingForward = kb != null && kb.wKey.isPressed;
        bool movingBackward = kb != null && kb.sKey.isPressed;

        float targetFrontWeight = 0f;
        float targetBackWeight = 0f;

        if (shiftHeld && movingForward)
        {
            targetFrontWeight = 1f;
        }
        else if (shiftHeld && movingBackward)
        {
            targetBackWeight = 1f;
        }

        if (shiftHeadIKFront != null)
        {
            shiftHeadIKFront.weight = Mathf.Lerp(shiftHeadIKFront.weight, targetFrontWeight, shiftHeadIKLerpSpeed * Time.deltaTime);
        }

        if (shiftHeadIKBack != null)
        {
            shiftHeadIKBack.weight = Mathf.Lerp(shiftHeadIKBack.weight, targetBackWeight, shiftHeadIKLerpSpeed * Time.deltaTime);
        }
    }
}