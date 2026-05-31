/*
 * ==============================================================================
 * Engineering Data Visualizer for VR (Unity)
 * ==============================================================================
 * Призначення: Візуалізація 3D-траєкторій та реєстрація взаємодії з даними
 * (Лабораторна робота №7).
 * 
 * ФУНКЦІОНАЛ:
 * 1. Побудова траєкторії на основі масиву точок (LineRenderer).
 * 2. Динамічне масштабування (Scale) даних користувачем.
 * 3. Логування навігації та змін режимів у CSV-файл.
 * 
 * ІНСТРУКЦІЯ:
 * 1. Додайте LineRenderer на порожній об'єкт у сцені.
 * 2. Прикріпіть цей скрипт.
 * 3. Натискайте 'M' для зміни режиму, '=' та '-' для масштабування.
 * ==============================================================================
 */

using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class DataVisualizer : MonoBehaviour
{
    [Header("Visual Settings")]
    public LineRenderer lineRenderer;
    public Transform dataContainer; // Об'єкт-батько для даних

    [Header("Interaction Settings")]
    public float scaleSpeed = 0.5f;
    private float currentScale = 1.0f;
    
    private string logPath;
    private List<string> actionBuffer = new List<string>();

    void Start()
    {
        logPath = Path.Combine(Application.persistentDataPath, "data_interaction_log.csv");
        actionBuffer.Add("Timestamp;Action;Value");
        
        // Автоматичне лікування (Self-healing): якщо LineRenderer не заданий в інспекторі
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
                Debug.Log("[DataVisualizer] LineRenderer було автоматично додано до об'єкта.");
            }
        }

        // Автоматичне лікування: якщо dataContainer не призначено, використовуємо поточний трансформ
        if (dataContainer == null)
        {
            dataContainer = this.transform;
            Debug.Log("[DataVisualizer] dataContainer не призначено. Використовується поточний Transform як fallback.");
        }
        
        if (lineRenderer != null)
        {
            lineRenderer.useWorldSpace = false; // ВИМИКАЄМО світові координати, щоб масштабування трансформа взагалі ПРАЦЮВАЛО!
            LoadAndVisualizeData();
        }
    }

    private float nextSaveTime = 5.0f;

    void Update()
    {
        HandleInput();

        // Періодичне автоматичне збереження кожні 5 секунд
        if (Time.time > nextSaveTime)
        {
            nextSaveTime = Time.time + 5.0f;
            SaveLog();
        }
    }

    void OnDisable()
    {
        SaveLog();
    }

    private void HandleInput()
    {
        // 1. Зчитування клавіатури (для тестування на ПК в симуляторі)
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null)
        {
            // Масштабування
            bool scaleUp = keyboard.equalsKey.isPressed || keyboard.numpadPlusKey.isPressed;
            bool scaleDown = keyboard.minusKey.isPressed || keyboard.numpadMinusKey.isPressed;

            if (scaleUp) 
                UpdateScale(scaleSpeed * Time.deltaTime);
            if (scaleDown) 
                UpdateScale(-scaleSpeed * Time.deltaTime);

            // Перемикання товщини лінії
            if (keyboard.mKey.wasPressedThisFrame)
            {
                ToggleMode();
            }

            // Збереження логів
            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                SaveLog();
            }
        }

        // 2. Зчитування VR-контролерів (для гри безпосередньо в VR окулярах Quest)
        var rightHand = UnityEngine.InputSystem.InputSystem.GetDevice<UnityEngine.InputSystem.InputDevice>(
            UnityEngine.InputSystem.CommonUsages.RightHand
        );
        
        if (rightHand != null)
        {
            // Кнопка A (primaryButton) на правому контролері для зміни товщини спіралі
            var primaryBtn = rightHand.GetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("primaryButton");
            if (primaryBtn != null && primaryBtn.wasPressedThisFrame)
            {
                ToggleMode();
            }

            // Правый джойстик (primary2DAxis) для плавного масштабування спіралі (вгору/вниз)
            var thumbstick = rightHand.GetChildControl<UnityEngine.InputSystem.Controls.Vector2Control>("primary2DAxis");
            if (thumbstick != null)
            {
                Vector2 axisValue = thumbstick.ReadValue();
                if (axisValue.y > 0.2f)
                {
                    UpdateScale(scaleSpeed * Time.deltaTime * axisValue.y);
                }
                else if (axisValue.y < -0.2f)
                {
                    UpdateScale(-scaleSpeed * Time.deltaTime * Mathf.Abs(axisValue.y));
                }
            }
        }

        // 3. Зчитування ЛІВОГО контролера для збереження логів у VR (кнопка X / primaryButton)
        var leftHand = UnityEngine.InputSystem.InputSystem.GetDevice<UnityEngine.InputSystem.InputDevice>(
            UnityEngine.InputSystem.CommonUsages.LeftHand
        );
        if (leftHand != null)
        {
            var primaryBtn = leftHand.GetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("primaryButton");
            if (primaryBtn != null && primaryBtn.wasPressedThisFrame)
            {
                SaveLog();
            }
        }
    }

    private void LoadAndVisualizeData()
    {
        // Імітація інженерних даних (спіральна траєкторія)
        int pointCount = 100;
        lineRenderer.positionCount = pointCount;
        
        for (int i = 0; i < pointCount; i++)
        {
            float t = i * 0.2f;
            Vector3 point = new Vector3(Mathf.Cos(t), t * 0.1f, Mathf.Sin(t));
            lineRenderer.SetPosition(i, point);
        }
    }

    private void UpdateScale(float delta)
    {
        currentScale = Mathf.Clamp(currentScale + delta, 0.1f, 10.0f);
        dataContainer.localScale = Vector3.one * currentScale;
        
        // Логуємо лише суттєві зміни або по завершенню руху
        if (Mathf.Abs(delta) > 0.01f)
        {
            LogAction("ScaleChange", currentScale.ToString("F2"));
        }
    }

    private void ToggleMode()
    {
        lineRenderer.startWidth = (lineRenderer.startWidth > 0.01f) ? 0.005f : 0.02f;
        lineRenderer.endWidth = lineRenderer.startWidth;
        LogAction("WidthToggle", lineRenderer.startWidth.ToString());
    }

    private void LogAction(string action, string val)
    {
        string entry = $"{Time.time:F3};{action};{val}";
        actionBuffer.Add(entry);
    }

    public void SaveLog()
    {
        File.WriteAllLines(logPath, actionBuffer);
        Debug.Log("Interaction log saved to: " + logPath);
    }

    void OnApplicationQuit()
    {
        SaveLog();
    }
}
