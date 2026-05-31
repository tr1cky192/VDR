/*
 * ==============================================================================
 * Tracking Anomaly Simulator for Meta Quest 3 (Unity)
 * ==============================================================================
 * Призначення: Демонстрація аномалій трекінгу (Spoofing/Freezing) для аналізу 
 * вразливостей VR-систем (Лабораторна робота №6).
 * 
 * ФУНКЦІОНАЛ:
 * 1. Jitter Attack: Додавання випадкового високочастотного шуму до позиції.
 * 2. Drift Attack: Поступове несанкціоноване зміщення об'єкта відносно руки.
 * 3. Freeze Attack: Призупинення оновлення координат (Tracking Loss Simulation).
 * 
 * ІНСТРУКЦІЯ:
 * 1. Додайте скрипт на об'єкт "VirtualHand" або "Tool".
 * 2. Вкажіть реальну "Hand Transform" (Anchor) для порівняння.
 * 3. Активуйте `isAttackActive` через інспектор або скрипт під час виконання.
 * ==============================================================================
 */

using UnityEngine;

public class TrackingAnomalySimulator : MonoBehaviour
{
    [Header("Target Tracking")]
    public Transform realHandAnchor; // Справжнє положення від SDK

    [Header("Attack Settings")]
    public bool isAttackActive = false;
    public AttackType currentAttack = AttackType.Jitter;
    
    public float intensity = 0.05f;  // Амплітуда атаки
    public float driftSpeed = 0.1f;  // Швидкість дрейфу (м/с)

    public enum AttackType { Jitter, Drift, Freeze }

    private Vector3 driftOffset = Vector3.zero;
    private Vector3 frozenPosition;

    void Update()
    {
        if (!isAttackActive)
        {
            // Повертаємо об'єкт до реального трекінгу
            if (realHandAnchor != null)
            {
                transform.position = realHandAnchor.position;
                transform.rotation = realHandAnchor.rotation;
            }
            driftOffset = Vector3.zero;
            return;
        }

        ApplyMaliciousEffect();
    }

    private void ApplyMaliciousEffect()
    {
        switch (currentAttack)
        {
            case AttackType.Jitter:
                // Додаємо випадковий шум до кожної координати
                Vector3 noise = new Vector3(
                    Random.Range(-intensity, intensity),
                    Random.Range(-intensity, intensity),
                    Random.Range(-intensity, intensity)
                );
                transform.position = realHandAnchor.position + noise;
                break;

            case AttackType.Drift:
                // Поступове зміщення в сторону
                driftOffset += Vector3.right * driftSpeed * Time.deltaTime;
                transform.position = realHandAnchor.position + driftOffset;
                break;

            case AttackType.Freeze:
                // Об'єкт не змінює позицію (імітація зависання)
                // transform.position залишається попереднім
                break;
        }
    }

    // Метод для виявлення (Detection Logic)
    public bool DetectAnomaly()
    {
        float diff = Vector3.Distance(transform.position, realHandAnchor.position);
        if (diff > 0.05f) // Поріг детекції 5 см
        {
            Debug.LogError($"[SECURITY ALERT] Tracking anomaly detected! Offset: {diff:F3}m");
            return true;
        }
        return false;
    }
}
