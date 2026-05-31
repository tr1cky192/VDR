/*
 * ==============================================================================
 * MR Stability Logger for Meta Quest 3 (Unity)
 * ==============================================================================
 * Призначення: Позиціонування об'єкта в AR/MR просторі та логування стабільності
 * його прив'язки відносно користувача (Лабораторна робота №5).
 * 
 * ФУНКЦІОНАЛ:
 * 1. Розміщення об'єкта в точці погляду (Raycast на поверхню або фіксована відстань).
 * 2. Скасування/Скидання розміщення (кнопка B).
 * 3. Логування координат об'єкта та користувача у CSV для аналізу дрейфу.
 * 
 * ІНСТРУКЦІЯ:
 * 1. Додайте цей скрипт на об'єкт у Unity.
 * 2. Призначте "Virtual Object" (куб або маркер) та "Main Camera".
 * 3. Для роботи на Quest 3 переконайтеся, що активовано Passthrough та Scene Understanding.
 * ==============================================================================
 */

using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Text;

public class MRStabilityLogger : MonoBehaviour
{
    [Header("References")]
    public GameObject virtualObject;   // Об'єкт, що розміщується
    public Transform userCamera;       // Основна камера гарнітури

    [Header("Settings")]
    public string fileName = "mr_stability_data.csv";
    public float logInterval = 0.05f;  // Частота запису (20 Hz)

    private bool isPlaced = false;
    private List<string> logData = new List<string>();
    private float nextLogTime = 0.0f;
    private float nextSaveTime = 5.0f;

    void Start()
    {
        // Заголовок лог-файлу
        logData.Add("Timestamp;User_X;User_Y;User_Z;Obj_X;Obj_Y;Obj_Z;Rel_Dist");
        
        // Автоматично шукаємо камеру, якщо вона не призначена
        if (userCamera == null)
        {
            userCamera = Camera.main != null ? Camera.main.transform : null;
        }

        // Автоматичне лікування (Self-healing): якщо віртуальний об'єкт не призначено, створюємо кубик для тестів
        if (virtualObject == null)
        {
            virtualObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            virtualObject.name = "MR_VirtualObject_Fallback";
            virtualObject.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);
            
            // Знімемо колайдер, щоб об'єкт не штовхав гравця фізично
            Collider col = virtualObject.GetComponent<Collider>();
            if (col != null) Destroy(col);
            
            Debug.Log("[MRStabilityLogger] Virtual Object не призначено! Створено автоматичний тестовий куб.");
        }

        if (virtualObject != null)
        {
            virtualObject.SetActive(false);
        }

        // Авторозміщення при старті, щоб лог ніколи не залишався порожнім!
        Invoke("AutoPlaceOnStart", 0.5f);
    }

    private void AutoPlaceOnStart()
    {
        if (!isPlaced)
        {
            PlaceObjectInFront();
            Debug.Log("[MRStabilityLogger] Автоматично розміщено тестовий куб при старті для генерації стабільних логів.");
        }
    }

    void Update()
    {
        // Перевірка введення з VR-контролерів через нову Input System
        bool primaryButtonPressed = false;
        bool secondaryButtonPressed = false;

        // Шукаємо правий контролер серед пристроїв введення як універсальний InputDevice
        var rightHand = UnityEngine.InputSystem.InputSystem.GetDevice<UnityEngine.InputSystem.InputDevice>(
            UnityEngine.InputSystem.CommonUsages.RightHand
        );

        if (rightHand != null)
        {
            // Кнопка A (Primary Button)
            var primaryBtn = rightHand.GetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("primaryButton");
            if (primaryBtn != null && primaryBtn.wasPressedThisFrame)
            {
                primaryButtonPressed = true;
            }

            // Кнопка B (Secondary Button)
            var secondaryBtn = rightHand.GetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("secondaryButton");
            if (secondaryBtn != null && secondaryBtn.wasPressedThisFrame)
            {
                secondaryButtonPressed = true;
            }
        }

        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        bool pPressed = keyboard != null && keyboard.pKey.wasPressedThisFrame;
        bool cPressed = keyboard != null && keyboard.cKey.wasPressedThisFrame;
        bool spacePressed = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;

        // Кнопка A (VR) або клавіша P (Емулятор ПК) - Розмістити куб
        if (pPressed || primaryButtonPressed)
        {
            PlaceObjectInFront();
        }

        // Кнопка B (VR) або клавіша C (Емулятор ПК) - Прибрати куб
        if (cPressed || secondaryButtonPressed)
        {
            ResetObject();
        }

        // Реєстрація даних, якщо об'єкт розміщено
        if (isPlaced && Time.time >= nextLogTime)
        {
            nextLogTime = Time.time + logInterval;
            RecordData();
        }

        // Автоматичне збереження кожні 5 секунд
        if (Time.time > nextSaveTime)
        {
            nextSaveTime = Time.time + 5.0f;
            SaveLog();
        }

        // Зчитуємо кнопку X (primaryButton) ЛІВОГО контролера для збереження логу у VR
        bool vrSavePressed = false;
        var leftHand = UnityEngine.InputSystem.InputSystem.GetDevice<UnityEngine.InputSystem.InputDevice>(
            UnityEngine.InputSystem.CommonUsages.LeftHand
        );
        if (leftHand != null)
        {
            var primaryBtn = leftHand.GetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("primaryButton");
            if (primaryBtn != null && primaryBtn.wasPressedThisFrame)
            {
                vrSavePressed = true;
            }
        }

        // Збереження при натисканні Space або кнопки X на лівому контролері
        if (spacePressed || vrSavePressed)
        {
            SaveLog();
        }
    }

    private void PlaceObjectInFront()
    {
        if (userCamera == null || virtualObject == null)
        {
            Debug.LogError("[MRStabilityLogger] Неможливо розмістити: Камера або Об'єкт не призначені!");
            return;
        }

        // Розміщуємо об'єкт на відстані 1.2 метра перед камерою
        Vector3 spawnPosition = userCamera.position + userCamera.forward * 1.2f;
        
        // Вирівнюємо по висоті підлоги (якщо відомо) або просто фіксуємо
        virtualObject.transform.position = spawnPosition;
        virtualObject.transform.rotation = Quaternion.LookRotation(new Vector3(userCamera.forward.x, 0, userCamera.forward.z));
        
        virtualObject.SetActive(true);
        isPlaced = true;
        Debug.Log("Object Anchored at: " + spawnPosition);
    }

    private void ResetObject()
    {
        if (virtualObject != null)
        {
            virtualObject.SetActive(false);
        }
        isPlaced = false;
        Debug.Log("Placement Canceled");
    }

    private void RecordData()
    {
        StringBuilder sb = new StringBuilder();
        Vector3 uP = userCamera.position;
        Vector3 oP = virtualObject.transform.position;
        float dist = Vector3.Distance(uP, oP);

        sb.Append($"{Time.time:F3};");
        sb.Append($"{uP.x:F4};{uP.y:F4};{uP.z:F4};");
        sb.Append($"{oP.x:F4};{oP.y:F4};{oP.z:F4};");
        sb.Append($"{dist:F4}");

        logData.Add(sb.ToString());
    }

    public void SaveLog()
    {
        string path = Path.Combine(Application.persistentDataPath, fileName);
        File.WriteAllLines(path, logData);
        Debug.Log("Stability Log saved to: " + path);
    }

    void OnDisable()
    {
        SaveLog();
    }

    void OnApplicationQuit()
    {
        SaveLog();
    }
}
