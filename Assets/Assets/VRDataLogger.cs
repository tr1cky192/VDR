/*
 * ==============================================================================
 * VR Data Logger for Meta Quest 3 (Unity)
 * ==============================================================================
 * Призначення: Реєстрація положення та орієнтації HMD (голови) та рук у часі 
 * для аналізу точності та повторюваності трекінгу (Лабораторна робота №3).
 * 
 * ІНСТРУКЦІЯ З ВИКОРИСТАННЯ:
 * 1. Створіть у Unity порожній об'єкт (Create Empty) та назвіть його "DataCollector".
 * 2. Перетягніть цей скрипт на об'єкт "DataCollector".
 * 3. В інспекторі (Inspector) перетягніть об'єкти "CenterEyeAnchor" (для голови) 
 *    та "RightHandAnchor" (для правої руки) у відповідні поля скрипта.
 * 4. Вкажіть ім'я файлу (наприклад, vr_log.csv).
 * 5. Під час роботи натисніть клавішу "Space" (або реалізуйте натискання кнопки на контролері),
 *    щоб зберегти зібрані дані у файл.
 * 6. Файл буде збережено за шляхом: 
 *    %AppData%\LocalLow\[YourCompanyName]\[YourProjectName]\vr_log.csv (на PC)
 *    або в папку програми на самому шоломі Quest.
 * ==============================================================================
 */

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class VRDataLogger : MonoBehaviour
{
    [Header("Tracking References")]
    public Transform headTransform;      // Ссылка на камеру (Center Eye)
    public Transform rightHandTransform; // Ссылка на правую руку/контроллер

    [Header("Settings")]
    public string fileName = "vr_tracking_data.csv";
    public float samplingRate = 0.02f;   // Частота дискретизації (0.02 = 50Hz)

    private List<string> logData = new List<string>();
    private float nextActionTime = 0.0f;
    private bool isLogging = true;

    void Awake()
    {
        // Додаємо заголовок CSV-файлу
        StringBuilder header = new StringBuilder();
        header.Append("Timestamp;");
        header.Append("Head_X;Head_Y;Head_Z;Head_RotX;Head_RotY;Head_RotZ;");
        header.Append("Hand_X;Hand_Y;Hand_Z;Hand_RotX;Hand_RotY;Hand_RotZ");
        logData.Add(header.ToString());
    }

    void Start()
    {
        // Самолікування: автопошук HMD камери
        if (headTransform == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                headTransform = mainCam.transform;
                Debug.Log("[VRDataLogger] headTransform автоматично прив'язано до Main Camera.");
            }
            else
            {
                GameObject centerEye = GameObject.Find("CenterEyeAnchor");
                if (centerEye != null)
                {
                    headTransform = centerEye.transform;
                    Debug.Log("[VRDataLogger] headTransform автоматично прив'язано до CenterEyeAnchor.");
                }
            }
        }

        // Самолікування: автопошук правого контролера
        if (rightHandTransform == null)
        {
            GameObject rightHand = GameObject.Find("RightHandAnchor") ?? GameObject.Find("RightHand Controller") ?? GameObject.Find("Right Controller") ?? GameObject.Find("RightHand");
            if (rightHand != null)
            {
                rightHandTransform = rightHand.transform;
                Debug.Log($"[VRDataLogger] rightHandTransform автоматично прив'язано до '{rightHand.name}'.");
            }
            else
            {
                // Якщо контролера взагалі немає на сцені, створюємо пусту заглушку, щоб логування не ламалося і писало координати
                GameObject dummyHand = new GameObject("Dummy_RightHand_Anchor");
                dummyHand.transform.position = new Vector3(0.3f, 1.0f, 0.5f); // типова позиція руки
                rightHandTransform = dummyHand.transform;
                Debug.LogWarning("[VRDataLogger] Справжнього контролера не знайдено на сцені. Створено симуляційну заглушку Dummy_RightHand_Anchor.");
            }
        }
    }

    private float nextSaveTime = 5.0f;

    void Update()
    {
        // Реєстрація даних з фіксованою періодичністю
        if (isLogging && Time.time > nextActionTime)
        {
            nextActionTime += samplingRate;
            RecordCurrentState();
        }

        // Періодичне автоматичне збереження кожні 5 секунд (для гарантії наявності даних)
        if (Time.time > nextSaveTime)
        {
            nextSaveTime = Time.time + 5.0f;
            SaveToFile();
        }

        // Гаряча клавіша для збереження (клавіатура Space або кнопка X на лівому контролері у VR)
        bool keyboardSpace = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame;
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

        if (keyboardSpace || vrSavePressed)
        {
            SaveToFile();
        }
    }

    void OnDisable()
    {
        SaveToFile();
    }

    void OnApplicationQuit()
    {
        SaveToFile();
    }

    private void RecordCurrentState()
    {
        if (headTransform == null || rightHandTransform == null) return;

        StringBuilder line = new StringBuilder();
        
        // Час від старту сцени
        line.Append(Time.time.ToString("F3") + ";");

        // Дані голови
        Vector3 hPos = headTransform.position;
        Vector3 hRot = headTransform.eulerAngles;
        line.Append($"{hPos.x:F4};{hPos.y:F4};{hPos.z:F4};");
        line.Append($"{hRot.x:F4};{hRot.y:F4};{hRot.z:F4};");

        // Дані руки
        Vector3 rPos = rightHandTransform.position;
        Vector3 rRot = rightHandTransform.eulerAngles;
        line.Append($"{rPos.x:F4};{rPos.y:F4};{rPos.z:F4};");
        line.Append($"{rRot.x:F4};{rRot.y:F4};{rRot.z:F4}");

        logData.Add(line.ToString());
    }

    public void SaveToFile()
    {
        string path = Path.Combine(Application.persistentDataPath, fileName);
        
        try
        {
            File.WriteAllLines(path, logData);
            Debug.Log($"<color=green>Успішно збережено {logData.Count} записів у: {path}</color>");
        }
        catch (IOException e)
        {
            Debug.LogError($"Помилка запису файлу: {e.Message}");
        }
    }
    
    // Зупинка/запуск логування зовні
    public void SetLogging(bool state)
    {
        isLogging = state;
    }
}
