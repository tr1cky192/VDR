/*
 * ==============================================================================
 * INTEGRATED SYSTEM CONTROLLER - FINAL PROJECT (Lab 8)
 * ==============================================================================
 * Цей скрипт є центральним вузлом вашої VR-системи, що об'єднує:
 * - Логування трекінгу (Lab 1, 3)
 * - Візуалізацію даних (Lab 7)
 * - Мультимодальну взаємодію (Lab 4)
 * - AR/MR стабільність (Lab 5)
 * - Моніторинг безпеки (Lab 6)
 * ==============================================================================
 */

using UnityEngine;

public class IntegratedSystemController : MonoBehaviour
{
    [Header("Core Modules")]
    public VRDataLogger trackingLogger;        // ЛР 3
    public DataVisualizer dataVisualizer;      // ЛР 7
    public MultiModalController interactions;  // ЛР 4
    public MRStabilityLogger mrManager;        // ЛР 5
    public TrackingAnomalySimulator security;  // ЛР 6

    [Header("Status")]
    public bool isSystemReady = false;

    void Start()
    {
        InitializeSystem();
    }

    private void InitializeSystem()
    {
        Debug.Log(">>> Запуск інтегрованої системи...");
        
        // 1. Активація логування
        if(trackingLogger != null) trackingLogger.SetLogging(true);
        
        // 2. Налаштування безпеки
        if(security != null) security.isAttackActive = false;
        
        isSystemReady = true;
        Debug.Log(">>> Система готова до роботи.");
    }

    void Update()
    {
        if (!isSystemReady) return;

        // Постійний моніторинг аномалій (Лаб 6)
        if (security != null && security.isAttackActive)
        {
            CheckSystemIntegrity();
        }
    }

    private void CheckSystemIntegrity()
    {
        if (security.DetectAnomaly())
        {
            // Візуальне сповіщення (Лаб 4)
            Debug.LogWarning("ALERT: Spatial Integrity Compromised!");
            // Тут можна додати зміну кольору UI на червоний
        }
    }

    // Метод для виклику з UI (Лаб 5)
    public void ResetAllSystems()
    {
        if (mrManager != null) mrManager.SaveLog();
        if (trackingLogger != null) trackingLogger.SaveToFile();
        if (dataVisualizer != null) dataVisualizer.SaveLog();
        
        Debug.Log("Всі дані збережено. Систему перезавантажено.");
    }
}
