using UnityEngine;
using System.Collections.Generic;

public class SimpleCameraFollow : MonoBehaviour
{
    [Header("Follow (Position)")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0, 5, -7);
    [SerializeField] private float smoothSpeed = 5f;

    [Header("Look Targets (Rotation)")]
    [SerializeField] private PlayerMovement playerMovement; // untuk subscribe Q event
    [SerializeField] private List<Transform> lookTargets = new List<Transform>();
    [SerializeField] private float lookDetectionRange = 10f;
    [SerializeField] private float lookSmoothSpeed = 5f;

    private int currentTargetIndex = -1; // -1 berarti "no target" / fokus ke player

    private void OnEnable()
    {
        if (playerMovement != null)
            playerMovement.OnSwitchTargetPressed += CycleTarget;
    }

    private void OnDisable()
    {
        if (playerMovement != null)
            playerMovement.OnSwitchTargetPressed -= CycleTarget;
    }

    private void CycleTarget()
    {
        // Filter target yang valid & dalam range dulu
        List<Transform> validTargets = GetValidTargetsInRange();

        if (validTargets.Count == 0)
        {
            currentTargetIndex = -1; // fallback ke player
            return;
        }

        currentTargetIndex++;

        // Kalau sudah lewat "player slot" (index terakhir), wrap ke -1 (player) dulu baru target pertama lagi
        if (currentTargetIndex >= validTargets.Count)
        {
            currentTargetIndex = -1; // kembali fokus ke player dulu sebelum ulang ke target 0
        }
    }

    private List<Transform> GetValidTargetsInRange()
    {
        List<Transform> valid = new List<Transform>();
        foreach (var t in lookTargets)
        {
            if (t == null) continue;
            float distance = Vector3.Distance(target.position, t.position);
            if (distance <= lookDetectionRange)
                valid.Add(t);
        }
        return valid;
    }

    private void LateUpdate()
    {
        // Posisi kamera tetap follow player
        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        // Tentukan focus point
        Transform focusPoint = GetCurrentFocus();

        Vector3 direction = focusPoint.position - transform.position;
        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, lookSmoothSpeed * Time.deltaTime);
        }
    }

    private Transform GetCurrentFocus()
    {
        List<Transform> validTargets = GetValidTargetsInRange();

        if (currentTargetIndex >= 0 && currentTargetIndex < validTargets.Count)
        {
            return validTargets[currentTargetIndex];
        }

        return target; // fallback ke player
    }

    private void OnDrawGizmosSelected()
    {
        if (target == null) return;

        Gizmos.color = Color.yellow;
        DrawCircleGizmo(target.position, lookDetectionRange, 64);
    }

    private void DrawCircleGizmo(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angle = angleStep * i * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
}