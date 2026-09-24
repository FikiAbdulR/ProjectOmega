using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Script UI terpisah: hanya baca data boost dari ThirdPersonController lalu tampilkan.
// ThirdPersonController TIDAK tahu-menahu soal Slider/UI apapun — pemisahan tanggung jawab.
//
// Setup:
// 1. Attach script ini ke GameObject UI (mis. Canvas atau child-nya).
// 2. Drag GameObject Player (yang punya ThirdPersonController) ke field "controller".
// 3. Drag UI Slider yang menampilkan sisa boost ke field "boostSlider".
// 4. (Opsional) Drag warna fill slider & Text/TMP buat status cooldown.
public class UiControl : MonoBehaviour
{
    [Header("Referensi")]
    [SerializeField] private ThirdPersonController controller; // Player yang punya ThirdPersonController
    [SerializeField] private Slider boostSlider;

    [Header("Teks Cooldown (opsional)")]
    [SerializeField] private GameObject cooldownTextObject; // GameObject yang berisi Text/TMP, di-nonaktifkan kalau tidak cooldown
    [SerializeField] private TMP_Text cooldownText;              // pakai ini kalau UI Text biasa (UnityEngine.UI)

    private void Reset()
    {
        controller = FindObjectOfType<ThirdPersonController>();
        boostSlider = GetComponentInChildren<Slider>();
    }

    private void Start()
    {
        if (boostSlider != null && controller != null)
        {
            boostSlider.minValue = 0f;
            boostSlider.maxValue = controller.MaxBoost;
        }
    }

    private void Update()
    {
        if (controller == null) return;

        UpdateSlider();
        UpdateCooldownText();
    }

    private void UpdateSlider()
    {
        if (boostSlider == null) return;
        boostSlider.value = controller.CurrentBoost;
    }

    private void UpdateCooldownText()
    {
        bool onCooldown = controller.IsOnBoostCooldown;

        if (cooldownTextObject != null)
        {
            cooldownTextObject.SetActive(onCooldown);
        }

        if (cooldownText != null && onCooldown)
        {
            cooldownText.text = controller.BoostCooldownRemaining.ToString("F1") + "s";
        }
    }
}