#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System.Reflection;
using System.Collections.Generic;
using System;

[InitializeOnLoad]
public class VRSceneHelper : EditorWindow
{
    static VRSceneHelper()
    {
        // Автоматичне перенаправлення Gradle User Home на чистий англійський шлях (всередині папки проекту),
        // щоб вирішити помилку CXX1429 (через кириличні символи в імені користувача 'Ігор' на Windows)
        try
        {
            string projectPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));
            string cleanGradleHome = System.IO.Path.Combine(projectPath, ".gradle_home").Replace("/", "\\");
            System.Environment.SetEnvironmentVariable("GRADLE_USER_HOME", cleanGradleHome);
            Debug.Log($"<color=cyan>[VR Tools] Автоматично налаштовано GRADLE_USER_HOME: {cleanGradleHome}</color>");
        }
        catch (Exception ex)
        {
            Debug.LogError("[VR Tools] Не вдалося налаштувати GRADLE_USER_HOME: " + ex.Message);
        }
    }

    [MenuItem("VR Tools/Fix and Clean Scene")]
    public static void FixAndCleanScene()
    {
        Debug.Log(">>> [VR Tools] Запуск автоматичного виправлення сцени...");
        
        // 0. Автоматичне створення всіх необхідних лабораторних скриптів, якщо вони відсутні на сцені
        EnsureAllLabComponentsExist();

        // 1. Очищення відсутніх скриптів (Missing Scripts)
        CleanMissingScripts();

        // 2. Автоналаштування Tracked Pose Driver для камери
        ConfigureTrackedPoseDriver();

        // 2.3 Виправлення ієрархії камер (перенесення TrackingSpace під Camera Offset)
        ConfigureCameraHierarchy();

        // 2.5 Автоматичне призначення камери до компонента XR Origin
        ConfigureXROrigin();

        // 3. Автоматичний пошук і зв'язування посилань для DataVisualizer та VRDataLogger
        AutoLinkReferences();

        // 4. Автоматичне знаходження та додавання XR Device Simulator на сцену
        ConfigureXRDeviceSimulator();

        // 5. Автоматичне налаштування InputActionManager для реєстрації введення в XR Origin
        ConfigureInputActionManager();

        Debug.Log(">>> [VR Tools] Виправлення завершено успішно! Збережіть сцену (Ctrl + S).");
    }

    private static void ConfigureInputActionManager()
    {
        Type managerType = FindType("UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager") 
            ?? FindType("UnityEngine.XR.Interaction.Toolkit.InputActionManager");

        if (managerType == null)
        {
            Debug.LogWarning("[VR Tools] Не вдалося знайти клас InputActionManager в системі.");
            return;
        }

        Component manager = GameObject.FindAnyObjectByType(managerType) as Component;
        if (manager == null)
        {
            GameObject go = GameObject.Find("XR Interaction Manager") ?? GameObject.Find("[VR_System_Hub]");
            if (go == null)
            {
                go = new GameObject("XR Input Action Manager");
            }
            manager = go.AddComponent(managerType);
            Debug.Log($"<color=green>[VR Tools] Додано {managerType.Name} до '{go.name}'!</color>");
        }

        // Автоматично додаємо Input Actions у список
        SerializedObject so = new SerializedObject(manager);
        SerializedProperty actionAssetsProp = so.FindProperty("m_ActionAssets") ?? so.FindProperty("actionAssets");

        if (actionAssetsProp != null && actionAssetsProp.isArray)
        {
            if (actionAssetsProp.arraySize == 0)
            {
                // Шукаємо InputSystem_Actions або XRI Default Input Actions в проекті
                string[] guids = AssetDatabase.FindAssets("InputSystem_Actions t:InputActionAsset") 
                    ?? AssetDatabase.FindAssets("XRI Default Input Actions t:InputActionAsset");

                if (guids != null && guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    if (asset != null)
                    {
                        actionAssetsProp.InsertArrayElementAtIndex(0);
                        actionAssetsProp.GetArrayElementAtIndex(0).objectReferenceValue = asset;
                        so.ApplyModifiedProperties();
                        Debug.Log($"<color=green>[VR Tools] Автоматично додано InputActionAsset '{asset.name}' до {managerType.Name}!</color>");
                    }
                }
            }
        }
    }

    private static void ConfigureXRDeviceSimulator()
    {
        // Перевіряємо чи є вже XR Device Simulator на сцені
        GameObject existingSimulator = GameObject.Find("XR Device Simulator") ?? GameObject.Find("XRDeviceSimulator") ?? GameObject.Find("[XR_Device_Simulator]");
        if (existingSimulator != null)
        {
            Debug.Log("[VR Tools] XR Device Simulator вже присутній на сцені.");
            return;
        }

        // Шукаємо файл префабу в проекті
        string[] guids = AssetDatabase.FindAssets("XR Device Simulator t:Prefab");
        string prefabPath = "";
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("XR Device Simulator.prefab"))
            {
                prefabPath = path;
                break;
            }
        }

        if (string.IsNullOrEmpty(prefabPath))
        {
            prefabPath = "Assets/Samples/XR Interaction Toolkit/3.4.1/XR Device Simulator/XR Device Simulator.prefab";
        }

        GameObject simulatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (simulatorPrefab != null)
        {
            GameObject instantiated = (GameObject)PrefabUtility.InstantiatePrefab(simulatorPrefab);
            instantiated.name = "XR Device Simulator";
            Undo.RegisterCreatedObjectUndo(instantiated, "Create XR Device Simulator");
            Debug.Log($"<color=green>[VR Tools] Успішно додано та налаштовано {instantiated.name} на сцені з префабу: {prefabPath}</color>");
        }
        else
        {
            Debug.LogWarning("[VR Tools] Не вдалося знайти або завантажити префаб XR Device Simulator. Переконайтеся, що ви імпортували Samples у Package Manager.");
        }
    }

    private static void EnsureAllLabComponentsExist()
    {
        // Шукаємо чи є вже хоча б один з головних контролерів на сцені
        IntegratedSystemController controller = GameObject.FindAnyObjectByType<IntegratedSystemController>();
        
        if (controller == null)
        {
            Debug.Log("[VR Tools] Скриптів лабораторних робіт не знайдено в активній сцені! Створення системного хабу...");
            
            // Шукаємо або створюємо об'єкт хабу [VR_System_Hub]
            GameObject hub = GameObject.Find("[VR_System_Hub]");
            if (hub == null)
            {
                hub = new GameObject("[VR_System_Hub]");
            }

            // Додаємо всі 6 компонентів до хабу, якщо вони відсутні
            if (hub.GetComponent<IntegratedSystemController>() == null) hub.AddComponent<IntegratedSystemController>();
            if (hub.GetComponent<VRDataLogger>() == null) hub.AddComponent<VRDataLogger>();
            if (hub.GetComponent<DataVisualizer>() == null) hub.AddComponent<DataVisualizer>();
            if (hub.GetComponent<MultiModalController>() == null) hub.AddComponent<MultiModalController>();
            if (hub.GetComponent<MRStabilityLogger>() == null) hub.AddComponent<MRStabilityLogger>();
            if (hub.GetComponent<TrackingAnomalySimulator>() == null) hub.AddComponent<TrackingAnomalySimulator>();

            // Додаємо LineRenderer для малювання спіралі (DataVisualizer)
            if (hub.GetComponent<LineRenderer>() == null)
            {
                LineRenderer lr = hub.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.widthMultiplier = 0.02f;
                
                // Спробуємо призначити стандартний гарний матеріал
                Shader defaultShader = Shader.Find("Sprites/Default") ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");
                if (defaultShader != null)
                {
                    lr.material = new Material(defaultShader);
                }
                
                lr.startColor = Color.cyan;
                lr.endColor = Color.magenta;
            }

            Debug.Log("<color=green>[VR Tools] Успішно створено об'єкт '[VR_System_Hub]' на сцені та підключено всі 6 лабораторних скриптів!</color>");
        }
        else
        {
            Debug.Log("[VR Tools] Лабораторні компоненти вже присутні в сцені.");
        }
    }

    private static void CleanMissingScripts()
    {
        GameObject[] allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        int totalRemoved = 0;

        foreach (GameObject go in allObjects)
        {
            // Стандартний метод Unity для видалення компонентів з відсутніми скриптами
            int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            if (count > 0)
            {
                totalRemoved += count;
                Debug.Log($"[VR Tools] Видалено {count} відсутніх компонентів-скриптів з об'єкта: '{go.name}'", go);
            }
        }

        if (totalRemoved > 0)
        {
            Debug.Log($"<color=green>[VR Tools] Успішно очищено {totalRemoved} зламаних посилань (Missing Scripts) на сцені!</color>");
        }
        else
        {
            Debug.Log("[VR Tools] Зламаних посилань (Missing Scripts) не знайдено.");
        }
    }

    private static Type FindType(string typeName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = assembly.GetType(typeName);
            if (t != null) return t;
        }
        return null;
    }

    private static void ConfigureTrackedPoseDriver()
    {
        Type legacyTpdType = FindType("UnityEngine.SpatialTracking.TrackedPoseDriver");
        Type newTpdType = FindType("UnityEngine.InputSystem.XR.TrackedPoseDriver");

        // Список всіх трьох Oculus анкорів та стандартних назв камер
        string[] targetCameraNames = { "CenterEyeAnchor", "LeftEyeAnchor", "RightEyeAnchor", "Main Camera", "MainCamera" };
        List<GameObject> camerasToConfigure = new List<GameObject>();

        foreach (string camName in targetCameraNames)
        {
            GameObject go = GameObject.Find(camName);
            if (go != null && go.GetComponent<Camera>() != null)
            {
                camerasToConfigure.Add(go);
            }
        }

        // Додаємо також Camera.main про всяк випадок
        if (Camera.main != null && !camerasToConfigure.Contains(Camera.main.gameObject))
        {
            camerasToConfigure.Add(Camera.main.gameObject);
        }

        foreach (GameObject camGo in camerasToConfigure)
        {
            // 1. Видаляємо застарілий Tracked Pose Driver, якщо він є
            if (legacyTpdType != null)
            {
                Component legacyTpd = camGo.GetComponent(legacyTpdType);
                if (legacyTpd != null)
                {
                    Debug.Log($"[VR Tools] Виявлено застарілий {legacyTpdType.Name} на камері '{camGo.name}'. Видаляємо для оновлення...");
                    Undo.DestroyObjectImmediate(legacyTpd);
                }
            }

            // 2. Додаємо сумісний Tracked Pose Driver (Input System)
            if (newTpdType != null)
            {
                Component newTpd = camGo.GetComponent(newTpdType);
                if (newTpd == null)
                {
                    newTpd = camGo.AddComponent(newTpdType);
                    Debug.Log($"<color=green>[VR Tools] Додано сумісний {newTpdType.Name} до камери '{camGo.name}'!</color>", camGo);
                }

                // Завжди примусово налаштовуємо біндінги, оскільки при додаванні через код вони часто залишаються пустими!
                SerializedObject so = new SerializedObject(newTpd);
                so.Update();

                // Налаштовуємо Position Action
                SerializedProperty positionInputProp = so.FindProperty("m_PositionInput") ?? so.FindProperty("positionAction");
                if (positionInputProp != null)
                {
                    SerializedProperty useReferenceProp = positionInputProp.FindPropertyRelative("m_UseReference");
                    if (useReferenceProp != null) useReferenceProp.boolValue = false;
                    
                    SerializedProperty actionProp = positionInputProp.FindPropertyRelative("m_Action");
                    if (actionProp != null)
                    {
                        SerializedProperty bindingsProp = actionProp.FindPropertyRelative("m_SingletonActionBindings") 
                            ?? actionProp.FindPropertyRelative("m_Bindings");
                        if (bindingsProp != null)
                        {
                            bindingsProp.ClearArray();
                            bindingsProp.InsertArrayElementAtIndex(0);
                            SerializedProperty binding = bindingsProp.GetArrayElementAtIndex(0);
                            SerializedProperty pathProp = binding.FindPropertyRelative("m_Path");
                            if (pathProp != null) pathProp.stringValue = "<XRHMD>/centerEyePosition";
                            
                            SerializedProperty idProp = binding.FindPropertyRelative("m_Id");
                            if (idProp != null && string.IsNullOrEmpty(idProp.stringValue))
                            {
                                idProp.stringValue = Guid.NewGuid().ToString();
                            }
                        }
                    }
                }

                // Налаштовуємо Rotation Action
                SerializedProperty rotationInputProp = so.FindProperty("m_RotationInput") ?? so.FindProperty("rotationAction");
                if (rotationInputProp != null)
                {
                    SerializedProperty useReferenceProp = rotationInputProp.FindPropertyRelative("m_UseReference");
                    if (useReferenceProp != null) useReferenceProp.boolValue = false;
                    
                    SerializedProperty actionProp = rotationInputProp.FindPropertyRelative("m_Action");
                    if (actionProp != null)
                    {
                        SerializedProperty bindingsProp = actionProp.FindPropertyRelative("m_SingletonActionBindings") 
                            ?? actionProp.FindPropertyRelative("m_Bindings");
                        if (bindingsProp != null)
                        {
                            bindingsProp.ClearArray();
                            bindingsProp.InsertArrayElementAtIndex(0);
                            SerializedProperty binding = bindingsProp.GetArrayElementAtIndex(0);
                            SerializedProperty pathProp = binding.FindPropertyRelative("m_Path");
                            if (pathProp != null) pathProp.stringValue = "<XRHMD>/centerEyeRotation";
                            
                            SerializedProperty idProp = binding.FindPropertyRelative("m_Id");
                            if (idProp != null && string.IsNullOrEmpty(idProp.stringValue))
                            {
                                idProp.stringValue = Guid.NewGuid().ToString();
                            }
                        }
                    }
                }

                so.ApplyModifiedProperties();
                Debug.Log($"[VR Tools] Конфігуровано HMD біндінги для {camGo.name}: Position -> <XRHMD>/centerEyePosition, Rotation -> <XRHMD>/centerEyeRotation");
            }
        }

        if (newTpdType == null)
        {
            Debug.LogWarning("[VR Tools] Не вдалося знайти сумісний клас UnityEngine.InputSystem.XR.TrackedPoseDriver. Переконайтеся, що пакет Input System встановлено.");
        }
    }

    private static void ConfigureXROrigin()
    {
        Type xrOriginType = FindType("Unity.XR.CoreUtils.XROrigin") 
            ?? FindType("UnityEngine.XR.Interaction.Toolkit.XROrigin");

        if (xrOriginType == null) return;

        Component origin = GameObject.FindAnyObjectByType(xrOriginType) as Component;
        if (origin != null)
        {
            SerializedObject so = new SerializedObject(origin);
            SerializedProperty camProp = so.FindProperty("m_Camera");
            if (camProp != null && camProp.objectReferenceValue == null)
            {
                // Надаємо перевагу CenterEyeAnchor, оскільки вона є реальною активною камерою рендерингу
                Camera targetCam = null;
                GameObject centerEye = GameObject.Find("CenterEyeAnchor");
                if (centerEye != null)
                {
                    targetCam = centerEye.GetComponent<Camera>();
                }

                if (targetCam == null)
                {
                    // Якщо немає CenterEyeAnchor, беремо будь-яку активну камеру
                    targetCam = Camera.main;
                }

                if (targetCam != null)
                {
                    camProp.objectReferenceValue = targetCam;
                    so.ApplyModifiedProperties();
                    Debug.Log($"<color=green>[VR Tools] Автоматично призначено камеру '{targetCam.name}' до компонента XR Origin!</color>");
                }
            }
        }
    }

    private static void ConfigureCameraHierarchy()
    {
        GameObject trackingSpace = GameObject.Find("TrackingSpace");
        GameObject cameraOffset = GameObject.Find("Camera Offset") ?? GameObject.Find("CameraOffset");

        if (trackingSpace != null && cameraOffset != null)
        {
            if (trackingSpace.transform.parent != cameraOffset.transform)
            {
                Undo.SetTransformParent(trackingSpace.transform, cameraOffset.transform, "Reparent TrackingSpace to Camera Offset");
                
                // Скидаємо локальні позиції для правильного вирівнювання з XR Origin
                trackingSpace.transform.localPosition = Vector3.zero;
                trackingSpace.transform.localRotation = Quaternion.identity;
                trackingSpace.transform.localScale = Vector3.one;

                Debug.Log("<color=green>[VR Tools] Успішно перенесено 'TrackingSpace' в ієрархію під 'Camera Offset' для правильної роботи XR Origin!</color>");
            }
            else
            {
                Debug.Log("[VR Tools] Ієрархія 'TrackingSpace' під 'Camera Offset' уже налаштована правильно.");
            }
        }
        else
        {
            Debug.LogWarning("[VR Tools] Не вдалося знайти 'TrackingSpace' або 'Camera Offset' для виправлення ієрархії.");
        }
    }

    [MenuItem("VR Tools/Analyze Camera and Input")]
    public static void AnalyzeCameraAndInput()
    {
        Debug.Log("=== [VR Tools] АНАЛІЗ НАЛАШТУВАНЬ ВВЕДЕННЯ ТА КАМЕРИ ===");

        // 1. Аналіз камер
        Camera[] cameras = GameObject.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        Debug.Log($"Знайдено камер на сцені: {cameras.Length}");
        foreach (var cam in cameras)
        {
            Debug.Log($"Камера: '{cam.name}' | Tag: '{cam.tag}' | Active: {cam.gameObject.activeInHierarchy}");
            
            foreach (var comp in cam.GetComponents<Component>())
            {
                string typeName = comp.GetType().FullName;
                if (typeName.Contains("TrackedPoseDriver") || typeName.Contains("SpatialTracking"))
                {
                    Debug.Log($"  -> Знайдено компонент трекінгу: {typeName} (Enabled: {((Behaviour)comp).enabled})");
                }
            }
        }

        // 2. Аналіз InputActionManager
        Type managerType = Type.GetType("UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager, Unity.XR.Interaction.Toolkit") 
            ?? Type.GetType("UnityEngine.XR.Interaction.Toolkit.InputActionManager, Unity.XR.Interaction.Toolkit");
        
        if (managerType != null)
        {
            Component[] managers = GameObject.FindObjectsByType(managerType, FindObjectsSortMode.None) as Component[];
            Debug.Log($"Знайдено InputActionManagers: {managers.Length}");
            foreach (var manager in managers)
            {
                SerializedObject so = new SerializedObject(manager);
                SerializedProperty actionAssetsProp = so.FindProperty("m_ActionAssets") ?? so.FindProperty("actionAssets");
                int assetsCount = actionAssetsProp != null ? actionAssetsProp.arraySize : 0;
                Debug.Log($"  -> Менеджер на об'єкті: '{manager.name}' | Active Assets: {assetsCount}");
                if (assetsCount > 0)
                {
                    for (int i = 0; i < assetsCount; i++)
                    {
                        var asset = actionAssetsProp.GetArrayElementAtIndex(i).objectReferenceValue;
                        Debug.Log($"     - Asset {i}: {(asset != null ? asset.name : "null")}");
                    }
                }
            }
        }
        else
        {
            Debug.Log("  -> InputActionManager клас не знайдено в проекті!");
        }

        // 3. Аналіз XR Device Simulator
        GameObject simulator = GameObject.Find("XR Device Simulator") ?? GameObject.Find("XRDeviceSimulator");
        if (simulator != null)
        {
            Debug.Log($"Знайдено XR Device Simulator на сцені: '{simulator.name}' | Active: {simulator.activeInHierarchy}");
        }
        else
        {
            Debug.Log("XR Device Simulator НЕ знайдено на сцені!");
        }

        Debug.Log("=== [VR Tools] АНАЛІЗ ЗАВЕРШЕНО ===");
    }

    private static void AutoLinkReferences()
    {
        // Пошук VRDataLogger
        VRDataLogger logger = GameObject.FindAnyObjectByType<VRDataLogger>();
        Camera mainCam = Camera.main ?? (GameObject.Find("CenterEyeAnchor")?.GetComponent<Camera>());
        
        if (logger != null && mainCam != null)
        {
            bool modified = false;
            SerializedObject so = new SerializedObject(logger);
            
            SerializedProperty headProp = so.FindProperty("headTransform");
            if (headProp != null && headProp.objectReferenceValue == null)
            {
                headProp.objectReferenceValue = mainCam.transform;
                modified = true;
                Debug.Log($"[VR Tools] Автоматично прив'язано '{mainCam.name}' як Head Transform для VRDataLogger.", logger);
            }

            SerializedProperty handProp = so.FindProperty("rightHandTransform");
            if (handProp != null && handProp.objectReferenceValue == null)
            {
                GameObject rightHand = GameObject.Find("RightHandAnchor") ?? GameObject.Find("RightHand Controller") ?? GameObject.Find("Right Controller");
                if (rightHand != null)
                {
                    handProp.objectReferenceValue = rightHand.transform;
                    modified = true;
                    Debug.Log($"[VR Tools] Автоматично прив'язано '{rightHand.name}' як Right Hand Transform для VRDataLogger.", logger);
                }
            }

            if (modified)
            {
                so.ApplyModifiedProperties();
            }
        }

        // Пошук IntegratedSystemController
        IntegratedSystemController controller = GameObject.FindAnyObjectByType<IntegratedSystemController>();
        if (controller != null)
        {
            SerializedObject so = new SerializedObject(controller);
            bool modified = false;

            string[] fields = { "trackingLogger", "dataVisualizer", "interactions", "mrManager", "security" };
            Type[] types = { typeof(VRDataLogger), typeof(DataVisualizer), typeof(MultiModalController), typeof(MRStabilityLogger), typeof(TrackingAnomalySimulator) };

            for (int i = 0; i < fields.Length; i++)
            {
                SerializedProperty prop = so.FindProperty(fields[i]);
                if (prop != null && prop.objectReferenceValue == null)
                {
                    UnityEngine.Object comp = GameObject.FindAnyObjectByType(types[i]);
                    if (comp != null)
                    {
                        prop.objectReferenceValue = comp;
                        modified = true;
                        Debug.Log($"[VR Tools] Автоматично прив'язано модуль '{types[i].Name}' до IntegratedSystemController.", controller);
                    }
                }
            }

            if (modified)
            {
                so.ApplyModifiedProperties();
            }
        }
        
        // Позначаємо сцену як змінену для збереження
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
}
#endif
