using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float movespeed = 5f;
    [SerializeField] private float sprintMultiplier = 2f;
    [SerializeField] private float verticalSpeed = 5f;

    [Header("Ground Check (Raycast)")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float raycastDistance = 1.1f; // sedikit lebih dari jarak minimum ke lantai
    [SerializeField] private float groundOffset = 0.5f;    // jarak minimal cube dari lantai (misal setengah tinggi cube)

    private PlayerControls controls;
    private Vector2 moveInput;
    private float verticalInput;
    private bool isSprinting;
    private bool isGrounded;

    public Vector2 MoveInput => moveInput;
    public bool IsSprinting => isSprinting;

    private void Awake()
    {
        controls = new PlayerControls();
    }

    private void OnEnable()
    {
        controls.Player.Enable();

        controls.Player.Move.performed += OnMove;
        controls.Player.Move.canceled += OnMove;

        controls.Player.Sprint.performed += OnSprint;
        controls.Player.Sprint.canceled += OnSprint;

        controls.Player.Vertical.performed += OnVertical;
        controls.Player.Vertical.canceled += OnVertical;

        controls.Player.SwitchTarget.performed += OnSwitchTarget;
    }

    private void OnDisable()
    {
        controls.Player.Move.performed -= OnMove;
        controls.Player.Move.canceled -= OnMove;

        controls.Player.Sprint.performed -= OnSprint;
        controls.Player.Sprint.canceled -= OnSprint;

        controls.Player.Vertical.performed -= OnVertical;
        controls.Player.Vertical.canceled -= OnVertical;

        controls.Player.SwitchTarget.performed -= OnSwitchTarget;

        controls.Player.Disable();
    }

    // Event yang dilempar keluar, supaya script lain (kamera) bisa subscribe
    public event System.Action OnSwitchTargetPressed;

    private void OnSwitchTarget(InputAction.CallbackContext context)
    {
        OnSwitchTargetPressed?.Invoke();
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void OnSprint(InputAction.CallbackContext context)
    {
        isSprinting = context.ReadValueAsButton();
    }

    private void OnVertical(InputAction.CallbackContext context)
    {
        verticalInput = context.ReadValue<float>();
    }

    private void CheckGround()
    {
        // Cast ray dari posisi cube ke bawah
        Ray ray = new Ray(transform.position, Vector3.down);
        isGrounded = Physics.Raycast(ray, out RaycastHit hit, raycastDistance, groundLayer);

        // Debug visual di Scene view (opsional, bisa dihapus)
        Debug.DrawRay(transform.position, Vector3.down * raycastDistance, isGrounded ? Color.green : Color.red);
    }

    void Update()
    {
        CheckGround();

        float currentSpeed = isSprinting ? movespeed * sprintMultiplier : movespeed;

        Vector3 horizontalMove = new Vector3(moveInput.x, 0f, moveInput.y) * currentSpeed;

        float verticalMoveY = verticalInput * verticalSpeed;

        // Kalau grounded dan mencoba turun (input negatif), blokir gerakan turun
        if (isGrounded && verticalMoveY < 0f)
        {
            verticalMoveY = 0f;
        }

        Vector3 verticalMove = Vector3.up * verticalMoveY;
        Vector3 finalMove = horizontalMove + verticalMove;

        transform.Translate(finalMove * Time.deltaTime, Space.World);
    }
}