using UnityEngine;

public class MultiModalController : MonoBehaviour
{
    [Header("Visual Feedback")]
    public Renderer targetRenderer;
    public Color normalColor = Color.white;
    public Color fixedColor = Color.green;
    public Color hintColor = Color.yellow;

    [Header("State")]
    public bool isFixed = false;
    public bool isInZone = false;

    void Start()
    {
        // Автоматичне лікування: автопризначення Renderer
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();
        }

        // Автоматично активуємо зону взаємодії при старті для тестів
        SetZoneStatus(true);
    }

    void Update()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        bool zPressed = keyboard != null && keyboard.zKey.wasPressedThisFrame;
        bool gPressed = keyboard != null && keyboard.gKey.wasPressedThisFrame;
        bool vPressed = keyboard != null && keyboard.vKey.wasPressedThisFrame;

        // 1. Симуляція входу/виходу з зони за допомогою клавіші Z (для тестування в редакторі)
        if (zPressed)
        {
            SetZoneStatus(!isInZone);
            Debug.Log($"[MultiModal] Симуляція зони: {(isInZone ? "<color=yellow>УВІЙШОВ У ЗОНУ</color>" : "<color=red>ВИЙШОВ З ЗОНИ</color>")}");
        }

        // Перевірка введення з VR-контролерів через нову Input System
        bool vrGesturePressed = false;
        bool vrVoicePressed = false;

        // Зчитуємо бічний тригер (Grip) ЛІВОГО контролера для симуляції Жесту
        var leftHand = UnityEngine.InputSystem.InputSystem.GetDevice<UnityEngine.InputSystem.InputDevice>(
            UnityEngine.InputSystem.CommonUsages.LeftHand
        );
        if (leftHand != null)
        {
            var leftGrip = leftHand.GetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("grip");
            if (leftGrip != null && leftGrip.wasPressedThisFrame)
            {
                vrGesturePressed = true;
            }
        }

        // Зчитуємо бічний тригер (Grip) ПРАВОГО контролера для симуляції Голосу
        var rightHand = UnityEngine.InputSystem.InputSystem.GetDevice<UnityEngine.InputSystem.InputDevice>(
            UnityEngine.InputSystem.CommonUsages.RightHand
        );
        if (rightHand != null)
        {
            var rightGrip = rightHand.GetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("grip");
            if (rightGrip != null && rightGrip.wasPressedThisFrame)
            {
                vrVoicePressed = true;
            }
        }

        // 2. Симуляція та обробка фіксації (Клавіатура ПК або бічні тригери Grip на контролерах у VR)
        if (gPressed || vrGesturePressed || vPressed || vrVoicePressed)
        {
            string source = (gPressed || vrGesturePressed) ? "Gesture" : "Voice";

            if (isFixed)
            {
                Debug.LogWarning("[MultiModal] Компонент вже успішно зафіксовано!");
                return;
            }

            if (isInZone)
            {
                ConfirmFixation(source);
            }
            else
            {
                Debug.LogWarning($"[MultiModal] Не вдалося зафіксувати через {source}! Ви перебуваєте ПОЗА ЗОНОЮ взаємодії. Натисніть клавішу 'Z', щоб увійти в зону.");
            }
        }
    }

    public void ConfirmFixation(string source)
    {
        isFixed = true;
        if (targetRenderer != null)
        {
            targetRenderer.material.color = fixedColor;
        }
        Debug.Log($">>> [MultiModal] Компонент зафіксовано через: {source}");
        
        // Тут можна додати відтворення звуку
    }

    public void SetZoneStatus(bool inside)
    {
        isInZone = inside;
        if (!isFixed && targetRenderer != null)
        {
            targetRenderer.material.color = inside ? hintColor : normalColor;
        }
    }
}
