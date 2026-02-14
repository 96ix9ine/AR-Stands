using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace UnityEngine.XR.Templates.AR
{
    /// <summary>
    /// QR-сканер оптимизированный для AR, использующий камеру AR Foundation вместо EasyWebCam
    /// Решает конфликты между AR Foundation и EasyWebCam на мобильных устройствах
    /// </summary>
    public class ARQRCodeCameraManager : MonoBehaviour
    {
        [System.Serializable]
        public class QRCodeDetectedEvent : UnityEvent<string, Vector3> { }

        #region Поля и свойства

        [Header("Настройки QR сканирования")]
        [SerializeField] private ARCameraManager m_CameraManager;
        [SerializeField] private QRCodeDecodeController m_QRDecoder;

        [Header("Управление")]
        [SerializeField] private QRGameManager m_QRGameManager;

        [Header("3D UI префабы")]
        [SerializeField] private GameObject m_ARButtonPrefab;

        [Header("Настройки информационной панели")]
        [SerializeField] private GameObject m_CloseButtonName;
        [SerializeField] private float m_UIOffsetFromQR = 0.3f;
        [SerializeField] private float m_ARButtonOffsetRight = 0.8f;

        [Header("Настройки сканирования")]
        [SerializeField] private float m_ScanInterval = 0.5f;
        [SerializeField] private bool m_ShowDebugInfo = true;

        [Header("События")]
        public QRCodeDetectedEvent onQRCodeDetected;
        public UnityEvent<string> onQRCodeProcessed;

        // Приватные переменные
        private bool m_IsScanning = false;
        private bool m_QRCodeFound = false;
        private string m_LastQRData = "";
        private GameObject m_CurrentQRUI = null;
        private GameObject m_CurrentInfoPanel = null; // Ссылка на информационную панель
        private Camera m_ARCamera = null;
        private WaitForSeconds m_ScanWaitTime;
        private Texture2D m_CameraTexture = null;
        private int m_FrameCounter = 0;

        // Свойства
        public bool isScanning => m_IsScanning;
        public bool qrCodeFound => m_QRCodeFound;
        public string lastQRData => m_LastQRData;

        #endregion

        #region Методы Unity жизненного цикла

        void Awake()
        {
            m_ScanWaitTime = new WaitForSeconds(m_ScanInterval);

            // Найти ссылки, если они не назначены
            if (m_CameraManager == null)
            {
                m_CameraManager = FindFirstObjectByType<ARCameraManager>();
                Debug.Log($"ARCameraManager найден: {m_CameraManager != null}");
            }

            if (m_QRDecoder == null)
            {
                // Попробовать несколькими способами найти QR декодер
                m_QRDecoder = FindFirstObjectByType<QRCodeDecodeController>();

                if (m_QRDecoder == null)
                {
                    // Попробовать найти любой компонент с возможностью декодирования QR
                    var qrComponents = FindObjectsOfType<MonoBehaviour>();
                    foreach (var component in qrComponents)
                    {
                        if (component.GetType().Name.Contains("QR") || component.GetType().Name.Contains("Decode"))
                        {
                            Debug.Log($"Найден потенциальный QR компонент: {component.GetType().Name}");
                            // Пока используем статический метод напрямую
                            break;
                        }
                    }
                }

                Debug.Log($"QRCodeDecodeController найден: {m_QRDecoder != null}");
            }

            // Получить AR камеру
            if (m_ARCamera == null && m_CameraManager != null)
            {
                m_ARCamera = m_CameraManager.GetComponent<Camera>();
                Debug.Log($"AR камера найдена: {m_ARCamera != null}");
            }
        }

        void Start()
        {
            // Инициализировать текстуру AR камеры
            InitializeARCameraTexture();

            Debug.Log("AR QR менеджер камеры инициализирован");
        }

        void OnEnable()
        {
            // Подписаться на события кадров камеры
            if (m_CameraManager != null)
            {
                m_CameraManager.frameReceived += OnCameraFrameReceived;
            }

            // Подписаться на события QR декодера
            if (m_QRDecoder != null)
            {
                m_QRDecoder.onQRScanFinished.AddListener(OnQRCodeScanned);
            }
        }

        void OnDisable()
        {
            // Отписаться от событий камеры
            if (m_CameraManager != null)
            {
                m_CameraManager.frameReceived -= OnCameraFrameReceived;
            }

            // Отписаться от QR событий
            if (m_QRDecoder != null)
            {
                m_QRDecoder.onQRScanFinished.RemoveListener(OnQRCodeScanned);
            }

            StopScanning();
        }

        #endregion

        #region Инициализация и обработка камеры

        /// <summary>
        /// Инициализировать текстуру AR камеры для QR сканирования
        /// </summary>
        private void InitializeARCameraTexture()
        {
            if (m_CameraManager != null)
            {
                // AR Camera Manager будет предоставлять текстуры через событие frameReceived
                Debug.Log("Инициализация текстуры AR камеры начата");
            }
            else
            {
                Debug.LogError("AR Camera Manager не найден!");
            }
        }

        /// <summary>
        /// Обработать полученный кадр камеры от AR Foundation
        /// </summary>
        private void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
        {
            if (!m_IsScanning)
                return;

            // Обрабатывать кадры с интервалами для избежания проблем с производительностью
            m_FrameCounter++;
            if (m_FrameCounter % 15 != 0) // Обрабатывать каждый 15-й кадр
                return;

            try
            {
                // Попытаться получить изображение камеры от AR Foundation
                StartCoroutine(ProcessCameraFrameAsync());
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Ошибка обработки кадра камеры: {e.Message}");
            }
        }

        /// <summary>
        /// Асинхронно обработать кадр камеры для детекции QR
        /// </summary>
        private IEnumerator ProcessCameraFrameAsync()
        {
            Texture2D cameraTexture = null;

            try
            {
                // Попытаться получить текстуру камеры от AR Camera Manager
                if (m_CameraManager != null)
                {
                    cameraTexture = GetCameraTexture();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Ошибка QR обработки: {e.Message}");
                yield break;
            }

            if (cameraTexture != null)
            {
                yield return StartCoroutine(ProcessCameraFrame(cameraTexture));
            }
        }

        /// <summary>
        /// Получить текстуру камеры от AR Foundation
        /// </summary>
        private Texture2D GetCameraTexture()
        {
            try
            {
                if (m_ARCamera != null)
                {
                    // Создать текстуру рендеринга для захвата вида камеры
                    int width = Screen.width / 4; // Уменьшить разрешение для производительности
                    int height = Screen.height / 4;

                    RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.Default);
                    m_ARCamera.targetTexture = rt;

                    // Рендер в текстуру
                    m_ARCamera.Render();
                    m_ARCamera.targetTexture = null;

                    // Конвертировать в Texture2D с правильным форматом для ZXing
                    Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    RenderTexture.active = rt;
                    texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    texture.Apply();
                    RenderTexture.active = null;

                    Destroy(rt);

                    // Проверить текстуру перед возвратом
                    if (texture.width > 0 && texture.height > 0)
                    {
                        return texture;
                    }
                    else
                    {
                        Destroy(texture);
                        return null;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Ошибка захвата текстуры камеры: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// Обработать кадр камеры для детекции QR
        /// </summary>
        private IEnumerator ProcessCameraFrame(Texture2D cameraTexture)
        {
            if (cameraTexture == null) yield break;

            // Создать копию текстуры для обработки с тем же форматом
            if (m_CameraTexture == null || m_CameraTexture.width != cameraTexture.width)
            {
                m_CameraTexture = new Texture2D(cameraTexture.width, cameraTexture.height, TextureFormat.RGBA32, false);
            }

            // Скопировать текстуру камеры
            Graphics.CopyTexture(cameraTexture, m_CameraTexture);

            // Подождать кадр для завершения копирования
            yield return null;

            try
            {
                // Декодировать QR из текстуры
                DecodeQRFromTexture(m_CameraTexture);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Ошибка QR обработки: {e.Message}");
            }
            finally
            {
                // Очистить временную текстуру
                if (cameraTexture != null)
                {
                    Destroy(cameraTexture);
                }
            }
        }

        #endregion

        #region QR декодирование

        /// <summary>
        /// Декодировать QR код из текстуры
        /// </summary>
        private void DecodeQRFromTexture(Texture2D texture)
        {
            try
            {
                // Проверить текстуру перед декодированием
                if (texture == null)
                {
                    Debug.LogWarning("Ошибка QR декодирования: Текстура равна null");
                    return;
                }

                if (texture.width <= 0 || texture.height <= 0)
                {
                    Debug.LogWarning($"Ошибка QR декодирования: Недопустимый размер текстуры: {texture.width}x{texture.height}");
                    return;
                }

                // Проверить, есть ли у текстуры читаемые данные
                try
                {
                    var pixels = texture.GetPixels32();
                    if (pixels == null || pixels.Length == 0)
                    {
                        Debug.LogWarning("Ошибка QR декодирования: У текстуры нет читаемых данных пикселей");
                        return;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Ошибка QR декодирования: Невозможно прочитать пиксели текстуры: {e.Message}");
                    return;
                }

                // Использовать существующий статический метод QR декодера
                string qrData = QRCodeDecodeController.DecodeByStaticPic(texture);

                if (!string.IsNullOrEmpty(qrData) && qrData != "decode failed!")
                {
                    if (qrData != m_LastQRData)
                    {
                        OnQRCodeScanned(qrData);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Ошибка QR декодирования: {e.Message}");
                if (m_ShowDebugInfo)
                    Debug.LogWarning($"Стек вызовов: {e.StackTrace}");
            }
        }

        #endregion

        #region Управление сканированием

        /// <summary>
        /// Запустить режим сканирования QR кодов
        /// </summary>
        public void StartScanning()
        {
            if (m_IsScanning)
                return;

            m_IsScanning = true;
            m_QRCodeFound = false;
            m_LastQRData = "";
            m_FrameCounter = 0;

            // Показать интерфейс сканирования
            SetScanMode(true);

            // Примечание: AR камера уже запущена, нам нужно только обрабатывать кадры
            Debug.Log("AR QR сканирование запущено");
        }

        /// <summary>
        /// Остановить сканирование QR кодов
        /// </summary>
        public void StopScanning()
        {
            m_IsScanning = false;
            Debug.Log("AR QR сканирование остановлено");
        }

        /// <summary>
        /// Переключиться между режимом сканирования и режимом AR вида
        /// </summary>
        public void SetScanMode(bool scanMode)
        {
            if (scanMode)
            {

                // Скрыть существующий 3D интерфейс
                if (m_CurrentQRUI != null)
                    m_CurrentQRUI.SetActive(false);
            }
            else
            {
                // Показать 3D интерфейс если существует
                if (m_CurrentQRUI != null)
                    m_CurrentQRUI.SetActive(true);
            }
        }

              /// <summary>
        /// Обработать обнаружение QR кода
        /// </summary>
        private void OnQRCodeScanned(string qrData)
        {
            if (string.IsNullOrEmpty(qrData) || qrData == m_LastQRData)
                return;

            m_LastQRData = qrData;
            m_QRCodeFound = true;

            if (m_ShowDebugInfo)
                Debug.Log($"QR код обнаружен: {qrData}");

            // Вычислить позицию QR в AR пространстве
            Vector3 qrWorldPosition = CalculateQRWorldPosition();

            // Запустить события
            onQRCodeDetected?.Invoke(qrData, qrWorldPosition);
            onQRCodeProcessed?.Invoke(qrData);

            // Создать 3D интерфейс в позиции QR
            SpawnQRUI(qrData, qrWorldPosition);

            // Остановить сканирование и переключиться в AR вид
            StartCoroutine(SwitchToARViewAfterDelay());
        }

        #endregion

        #region Создание 3D интерфейса

        /// <summary>
        /// Вычислить приблизительную мировую позицию QR кода
        /// </summary>
        private Vector3 CalculateQRWorldPosition()
        {
            // Использовать направление вперед камеры и разместить интерфейс на разумном расстоянии
            if (m_ARCamera != null)
            {
                return m_ARCamera.transform.position + m_ARCamera.transform.forward * 2.0f;
            }

            return transform.position + transform.forward * 2.0f;
        }

        /// <summary>
        /// Создать 3D элементы интерфейса в локации QR кода на основе QRGameManager
        /// </summary>
        private void SpawnQRUI(string qrData, Vector3 worldPosition)
        {
            // Проверить, есть ли у QRGameManager конфигурация интерфейса для этого QR кода
            bool hasQRGameUI = m_QRGameManager != null && m_QRGameManager.HasUIForQR(qrData);

            // Удалить существующий интерфейс
            if (m_CurrentQRUI != null)
            {
                Destroy(m_CurrentQRUI);
            }

            // Создать родительский объект для QR интерфейса
            m_CurrentQRUI = new GameObject($"QR_UI_{System.DateTime.Now.Ticks}");
            m_CurrentQRUI.transform.position = worldPosition;
            m_CurrentQRUI.transform.LookAt(m_ARCamera != null ? m_ARCamera.transform : Camera.main.transform);

            // Получить элементы интерфейса от QRGameManager
            var (buttons, panels) = m_QRGameManager.GetUIForQR(qrData);

            // Создать кнопки от QRGameManager используя прямую инстанциацию
            if (buttons != null && buttons.Length > 0)
            {
                for (int i = 0; i < buttons.Length; i++)
                {
                    var buttonPrefab = buttons[i];
                    if (buttonPrefab != null && m_ARButtonPrefab != null)
                    {
                        // Вычислить позицию аналогично CreateButtonsFromCollection
                        float startX = m_ARButtonOffsetRight;
                        float buttonSpacing = 0.45f;
                        float xPos = startX + (buttons.Length - 1 - i) * buttonSpacing;
                        Vector3 position = new Vector3(xPos, m_UIOffsetFromQR, 0);

                        // Создать кнопку напрямую используя префаб от QRGameManager
                        GameObject button = Instantiate(buttonPrefab, m_CurrentQRUI.transform);
                        button.transform.localPosition = position;

                        // Повернуть к камере и исправить зеркалирование
                        button.transform.LookAt(m_ARCamera != null ? m_ARCamera.transform : Camera.main.transform);
                        button.transform.Rotate(0, 180, 0);

                        // Настроить Canvas для 3D пространства
                        Canvas canvas = button.GetComponentInChildren<Canvas>();
                        if (canvas != null)
                        {
                            canvas.renderMode = RenderMode.WorldSpace;
                            canvas.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);

                            UnityEngine.UI.CanvasScaler scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                            if (scaler != null)
                            {
                                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;
                                scaler.scaleFactor = 1f;
                                scaler.referenceResolution = new Vector2(1920, 1080);
                            }

                            UnityEngine.UI.GraphicRaycaster raycaster = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                            if (raycaster == null)
                            {
                                canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                            }
                        }

                        // Настроить обработчик клика кнопки - сопоставить кнопку с её панелью
                        Button btnComponent = button.GetComponent<Button>();
                        if (btnComponent != null)
                        {
                            // Найти соответствующую панель для этой конкретной кнопки
                            GameObject targetPanel = null;
                            if (panels != null && i < panels.Length)
                            {
                                targetPanel = m_QRGameManager.uiReferences.GetPanelForButton(m_LastQRData, i);
                            }

                            string panelContent = "";
                            if (targetPanel != null)
                            {
                                // Сначала попытаться получить существующий текст из панели
                                var panelTexts = targetPanel.GetComponentsInChildren<Text>();
                                var panelTMPs = targetPanel.GetComponentsInChildren<TextMeshProUGUI>();

                                // Использовать существующий контент по умолчанию (не перезаписывать, если пустой)
                                foreach (var text in panelTexts)
                                {
                                    if (text.name.ToLower().Contains("content") ||
                                        text.name.ToLower().Contains("description") ||
                                        text.name.ToLower().Contains("info"))
                                    {
                                        if (!string.IsNullOrEmpty(text.text))
                                        {
                                            panelContent = text.text;
                                            break;
                                        }
                                    }
                                }

                                if (string.IsNullOrEmpty(panelContent))
                                {
                                    foreach (var tmp in panelTMPs)
                                    {
                                        if (tmp.name.ToLower().Contains("content") ||
                                            tmp.name.ToLower().Contains("description") ||
                                            tmp.name.ToLower().Contains("info"))
                                        {
                                            if (!string.IsNullOrEmpty(tmp.text))
                                            {
                                                panelContent = tmp.text;
                                                break;
                                            }
                                        }
                                    }
                                }

                                // Если все еще пусто, использовать имя кнопки
                                if (string.IsNullOrEmpty(panelContent))
                                {
                                    panelContent = "Content for " + buttonPrefab.name;
                                }
                            }

                            btnComponent.onClick.AddListener(() => OnCollectionButtonClicked(buttonPrefab.name, string.IsNullOrEmpty(panelContent) ? "No content available" : panelContent));
                        }

                        button.SetActive(true);

                        if (m_ShowDebugInfo)
                        {
                            Debug.Log($"QRManager кнопка создана: {buttonPrefab.name} в позиции {position}");
                        }
                    }
                }
            }

            // Настроить информационные панели от QRGameManager
            if (panels != null && panels.Length > 0)
            {
                m_CurrentInfoPanel = panels[0];
                // Настроить кнопку закрытия для ВСЕХ панелей
                for (int i = 0; i < panels.Length; i++)
                {
                    if (panels[i] != null)
                    {
                        SetupCloseButtonForPanel(panels[i]);
                        panels[i].SetActive(false);
                    }
                }
            }

            if (m_ShowDebugInfo)
            {
                Debug.Log($"Создан QR интерфейс для '{qrData}': {buttons?.Length ?? 0} кнопок, {panels?.Length ?? 0} панелей");
            }
        }

        #endregion

        #region Управление интерфейсом

        /// <summary>
        /// Создать кнопки из коллекции
        /// Всегда располагает кнопки линейно слева направо, справа от QR кода
        /// </summary>
        private void CreateButtonsFromCollection(QRContentItem collection)
        {
            int buttonCount = collection.buttons.Count;

            // Начать позиционирование кнопок справа от QR кода
            float startX = m_ARButtonOffsetRight;
            float buttonSpacing = 0.45f; // Интервал между кнопками коллекции

            // Итерировать вперед, но располагать кнопки от правой к левой для правильного визуального порядка
            for (int i = 0; i < buttonCount; i++)
            {
                var buttonData = collection.buttons[i];

                // Вычислить позицию от правой к левой
                float xPos = startX + (buttonCount - 1 - i) * buttonSpacing;
                Vector3 position = new Vector3(xPos, m_UIOffsetFromQR, 0);
                position += collection.layoutOffset;
                position += buttonData.localPosition;

                CreateCollectionButton(buttonData, position, Quaternion.identity);
            }
        }

        /// <summary>
        /// Создать отдельную кнопку коллекции
        /// </summary>
        private void CreateCollectionButton(QRContentButton buttonData, Vector3 position, Quaternion rotation)
        {
            if (m_ARButtonPrefab == null)
            {
                Debug.LogWarning("AR Button префаб не назначен");
                return;
            }

            // Создать AR кнопку в указанной позиции
            GameObject button = Instantiate(m_ARButtonPrefab, m_CurrentQRUI.transform);
            button.transform.localPosition = position;

            // Использовать логику вращения - повернуть к камере и исправить отзеркаливание
            button.transform.LookAt(m_ARCamera != null ? m_ARCamera.transform : Camera.main.transform);
            button.transform.Rotate(0, 180, 0); // Исправить зеркальное отражение

            // Применить дополнительное вращение ТОЛЬКО если buttonData имеет конкретное вращение (не ноль)
            if (rotation != Quaternion.identity && buttonData.localRotation != Vector3.zero)
            {
                button.transform.Rotate(buttonData.localRotation);
            }

            // Настроить Canvas для 3D пространства
            Canvas canvas = button.GetComponentInChildren<Canvas>();
            if (canvas != null)
            {
                // Установить режим рендеринга World Space для 3D
                canvas.renderMode = RenderMode.WorldSpace;

                // Установить разумный размер Canvas (соответствует размеру кнопки в пикселях)
                canvas.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);

                // Настроить Canvas Scaler если существует
                UnityEngine.UI.CanvasScaler scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                if (scaler != null)
                {
                    scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;
                    scaler.scaleFactor = 1f;
                    scaler.referenceResolution = new Vector2(1920, 1080);
                }

                // Добавить GraphicRaycaster для обработки кликов в 3D пространстве
                UnityEngine.UI.GraphicRaycaster raycaster = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                if (raycaster == null)
                {
                    canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                    if (m_ShowDebugInfo)
                        Debug.Log("Добавлен GraphicRaycaster к Canvas для детекции кликов");
                }
            }

            // Применить масштаб кнопки если указан (переопределить масштаб по умолчанию)
            if (buttonData.localScale != Vector3.one)
            {
                button.transform.localScale = buttonData.localScale;
            }
            else
            {
                // Убедиться, что кнопка имеет правильный масштаб если buttonData.localScale по умолчанию
                if (button.transform.localScale == Vector3.zero)
                {
                    button.transform.localScale = Vector3.one;
                }
            }

            // Убедиться, что кнопка видима
            button.SetActive(true);

            // Настроить обработчик клика кнопки с данными кнопки
            Button btnComponent = button.GetComponent<Button>();
            if (btnComponent != null)
            {
                btnComponent.onClick.AddListener(() => OnCollectionButtonClicked(buttonData.buttonText, buttonData.description));
            }

            // Обновить текст кнопки - сначала попробовать TextMeshPro, затем обычный Text
            TextMeshProUGUI tmpText = button.GetComponentInChildren<TextMeshProUGUI>();
            if (tmpText != null)
            {
                tmpText.text = buttonData.buttonText;
            }
            else
            {
                Text buttonText = button.GetComponentInChildren<Text>();
                if (buttonText != null)
                {
                    buttonText.text = buttonData.buttonText;
                }
            }

            // Показать информационную панель немедленно если настроено
            if (buttonData.showImmediately)
            {
                ToggleInfoPanel(buttonData.description);
            }

            if (m_ShowDebugInfo)
            {
                Debug.Log($"Кнопка коллекции создана: {buttonData.buttonText} в позиции {position}");
            }
        }

        /// <summary>
        /// Настроить обработчик кнопки закрытия для конкретной информационной панели
        /// </summary>
        /// <param name="infoPanel">Панель для настройки кнопки закрытия</param>
        private void SetupCloseButtonForPanel(GameObject infoPanel)
        {
            if (infoPanel == null)
                return;

            Button closeButton = null;

            // Если GameObject кнопки закрытия назначен напрямую, использовать его
            if (m_CloseButtonName != null)
            {
                // Попытаться найти его как ребенка информационной панели
                Transform closeButtonTransform = infoPanel.transform.Find(m_CloseButtonName.name);
                if (closeButtonTransform != null)
                {
                    closeButton = closeButtonTransform.GetComponent<Button>();
                }
            }

            // Если не найден напрямую, попытаться найти путем поиска всех детей
            if (closeButton == null)
            {
                Button[] buttons = infoPanel.GetComponentsInChildren<Button>();
                foreach (Button btn in buttons)
                {
                    if (btn.name.ToLower().Contains("close") || btn.gameObject.name.ToLower().Contains("close"))
                    {
                        closeButton = btn;
                        break;
                    }
                }
            }

            if (closeButton != null)
            {
                // Сохранить конкретную ссылку на панель для этой кнопки закрытия
                GameObject targetPanel = infoPanel;
                closeButton.onClick.AddListener(() => {
                    if (targetPanel != null)
                    {
                        targetPanel.SetActive(false);
                        if (m_ShowDebugInfo)
                            Debug.Log($"Информационная панель {targetPanel.name} закрыта через кнопку закрытия");
                    }
                });

                if (m_ShowDebugInfo)
                    Debug.Log($"Настройка кнопки закрытия завершена для панели {infoPanel.name}: {closeButton.name}");
            }
            else
            {
                Debug.LogWarning($"Кнопка закрытия не найдена в информационной панели: {infoPanel.name}");
            }
        }

        /// <summary>
        /// Обработать клик кнопки коллекции с заголовком и описанием
        /// </summary>
        private void OnCollectionButtonClicked(string title, string description)
        {
            if (m_ShowDebugInfo)
                Debug.Log($"Кнопка коллекции нажата: {title} - {description}");

            // Обновить контент информационной панели и показать ее
            if (m_CurrentInfoPanel != null)
            {
                // При работе с QRGameManager, получить правильный контент от QRUIReferences
                if (m_QRGameManager != null && m_QRGameManager.HasUIForQR(m_LastQRData))
                {
                    var (buttons, panels) = m_QRGameManager.GetUIForQR(m_LastQRData);
                    if (panels != null && panels.Length > 0 && buttons != null && buttons.Length > 0)
                    {
                        // Найти индекс кнопки, который соответствует этому заголовку
                        int buttonIndex = -1;
                        for (int i = 0; i < buttons.Length; i++)
                        {
                            if (buttons[i] != null && buttons[i].name == title)
                            {
                                buttonIndex = i;
                                break;
                            }
                        }

                        // Если нашли соответствующий индекс кнопки, использовать соответствующую панель
                        if (buttonIndex >= 0 && buttonIndex < panels.Length && panels[buttonIndex] != null)
                        {
                            // Получить контент из соответствующей панели
                            string panelContent = GetContentFromPanel(panels[buttonIndex]);

                            // Использовать соответствующую панель с ее контентом
                            UpdateInfoPanelContent(panels[buttonIndex], title, !string.IsNullOrEmpty(panelContent) ? panelContent : description);

                            // Скрыть все другие панели и показать правильную
                            for (int i = 0; i < panels.Length; i++)
                            {
                                if (panels[i] != null)
                                {
                                    panels[i].SetActive(i == buttonIndex);
                                }
                            }

                            if (m_ShowDebugInfo)
                                Debug.Log($"Используется панель {buttonIndex} для кнопки '{title}' с контентом: {panelContent}");
                        }
                        else
                        {
                            // Запасной вариант: использовать описание и показать первую панель
                            UpdateInfoPanelContent(m_CurrentInfoPanel, title, description);
                            m_CurrentInfoPanel.SetActive(true);

                            // Скрыть все другие панели
                            for (int i = 1; i < panels.Length; i++)
                            {
                                if (panels[i] != null)
                                {
                                    panels[i].SetActive(false);
                                }
                            }

                            if (m_ShowDebugInfo)
                                Debug.Log($"Соответствующая панель не найдена для кнопки '{title}', используется первая панель с описанием");
                        }
                    }
                    else
                    {
                        UpdateInfoPanelContent(m_CurrentInfoPanel, title, "Нет доступного описания");
                        m_CurrentInfoPanel.SetActive(true);
                    }
                }
                else
                {
                    // Запасной вариант для интерфейса по умолчанию - использовать описание кнопки
                    UpdateInfoPanelContent(m_CurrentInfoPanel, title, description);
                    m_CurrentInfoPanel.SetActive(true);
                }

                if (m_ShowDebugInfo)
                    Debug.Log($"Информационная панель обновлена и показана для: {title}");
            }
            else
            {
                Debug.LogWarning("Нет доступной информационной панели для показа контента");
            }
        }

        /// <summary>
        /// Получить контент из конкретной панели
        /// </summary>
        /// <param name="panel">Панель для получения контента</param>
        /// <returns>Текст контента или пустая строка</returns>
        private string GetContentFromPanel(GameObject panel)
        {
            if (panel == null) return "";

            // Попробовать сначала Text компоненты
            var textComponents = panel.GetComponentsInChildren<Text>();
            foreach (var text in textComponents)
            {
                if (text.name.ToLower().Contains("content") ||
                    text.name.ToLower().Contains("description") ||
                    text.name.ToLower().Contains("info"))
                {
                    if (!string.IsNullOrEmpty(text.text))
                    {
                        if (m_ShowDebugInfo)
                            Debug.Log($"Найден контент панели из Text: {text.name} = {text.text}");
                        return text.text;
                    }
                }
            }

            // Попробовать TextMeshPro компоненты
            var tmpComponents = panel.GetComponentsInChildren<TextMeshProUGUI>();
            foreach (var tmp in tmpComponents)
            {
                if (tmp.name.ToLower().Contains("content") ||
                    tmp.name.ToLower().Contains("description") ||
                    tmp.name.ToLower().Contains("info"))
                {
                    if (!string.IsNullOrEmpty(tmp.text))
                    {
                        if (m_ShowDebugInfo)
                            Debug.Log($"Найден контент панели из TextMeshPro: {tmp.name} = {tmp.text}");
                        return tmp.text;
                    }
                }
            }

            // Если конкретный контент не найден, вернуть первый непустой текст
            foreach (var text in textComponents)
            {
                if (!string.IsNullOrEmpty(text.text))
                {
                    return text.text;
                }
            }

            foreach (var tmp in tmpComponents)
            {
                if (!string.IsNullOrEmpty(tmp.text))
                {
                    return tmp.text;
                }
            }

            return "";
        }

        /// <summary>
        /// Обновить контент информационной панели
        /// </summary>
        private void UpdateInfoPanelContent(GameObject infoPanel, string title, string content)
        {
            if (m_ShowDebugInfo)
                Debug.Log($"Обновление контента информационной панели - Заголовок: {title}, Контент: {content}");

            // Попытаться найти Text компоненты и обновить их
            Text[] textComponents = infoPanel.GetComponentsInChildren<Text>();

            if (m_ShowDebugInfo)
                Debug.Log($"Найдено {textComponents.Length} Text компонентов в информационной панели");

            foreach (var textComponent in textComponents)
            {
                if (m_ShowDebugInfo)
                    Debug.Log($"Обработка текстового компонента: {textComponent.name} - текущий текст: {textComponent.text}");

                if (textComponent.name.ToLower().Contains("title") || textComponent.name.ToLower().Contains("header"))
                {
                    textComponent.text = title;
                    if (m_ShowDebugInfo)
                        Debug.Log($"Обновлен заголовок: {textComponent.name} -> {title}");
                }
                else if (textComponent.name.ToLower().Contains("content") ||
                         textComponent.name.ToLower().Contains("info") ||
                         textComponent.name.ToLower().Contains("description"))
                {
                    textComponent.text = content;
                    if (m_ShowDebugInfo)
                        Debug.Log($"Обновлен контент: {textComponent.name} -> {content}");
                }
                else
                {
                    // Если не найдено конкретного именования, обновить первый текст как контент
                    if (textComponents.Length == 1)
                    {
                        textComponent.text = content;
                        if (m_ShowDebugInfo)
                            Debug.Log($"Обновлен единственный текстовый компонент -> {content}");
                    }
                }
            }

            // Также попробовать TextMeshPro компоненты
            TextMeshProUGUI[] tmpComponents = infoPanel.GetComponentsInChildren<TextMeshProUGUI>();
            if (m_ShowDebugInfo && tmpComponents.Length > 0)
                Debug.Log($"Найдено {tmpComponents.Length} TextMeshPro компонентов в информационной панели");

            foreach (var tmpComponent in tmpComponents)
            {
                if (m_ShowDebugInfo)
                    Debug.Log($"Обработка TMP компонента: {tmpComponent.name} - текущий текст: {tmpComponent.text}");

                if (tmpComponent.name.ToLower().Contains("title") || tmpComponent.name.ToLower().Contains("header"))
                {
                    tmpComponent.text = title;
                    if (m_ShowDebugInfo)
                        Debug.Log($"Обновлен TMP заголовок: {tmpComponent.name} -> {title}");
                }
                else if (tmpComponent.name.ToLower().Contains("content") ||
                         tmpComponent.name.ToLower().Contains("info") ||
                         tmpComponent.name.ToLower().Contains("description"))
                {
                    tmpComponent.text = content;
                    if (m_ShowDebugInfo)
                        Debug.Log($"Обновлен TMP контент: {tmpComponent.name} -> {content}");
                }
            }
        }

        /// <summary>
        /// Обработать клик QR кнопки
        /// </summary>
        private void OnQRButtonClicked(string title, string content)
        {
            if (m_ShowDebugInfo)
                Debug.Log($"Кнопка нажата: {title}");

            ToggleInfoPanel(content);
        }

        /// <summary>
        /// Переключить информационную панель с конкретным контентом
        /// </summary>
        private void ToggleInfoPanel(string content)
        {
            if (m_CurrentQRUI != null)
            {
                // Найти информационную панель с соответствующим контентом
                Transform[] children = m_CurrentQRUI.GetComponentsInChildren<Transform>();
                foreach (var child in children)
                {
                    if (child.gameObject.name.ToLower().Contains("info") || child.gameObject.name.ToLower().Contains("panel"))
                    {
                        var textComponents = child.gameObject.GetComponentsInChildren<Text>();
                        foreach (var text in textComponents)
                        {
                            if (text.text.Contains(content) || content.Contains(text.text))
                            {
                                bool isActive = !child.gameObject.activeSelf;
                                child.gameObject.SetActive(isActive);

                                if (m_ShowDebugInfo)
                                    Debug.Log($"Информационная панель {(isActive ? "показана" : "скрыта")} для: {content}");

                                return;
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Утилитарные методы

        /// <summary>
        /// Переключиться в режим AR вида после короткой задержки
        /// </summary>
        private IEnumerator SwitchToARViewAfterDelay()
        {
            yield return new WaitForSeconds(1.0f);

            StopScanning();
            SetScanMode(false);
        }

        /// <summary>
        /// Сбросить состояние QR сканера
        /// </summary>
        public void ResetScanner()
        {
            StopScanning();
            m_QRCodeFound = false;
            m_LastQRData = "";

            // Удалить существующий интерфейс
            if (m_CurrentQRUI != null)
            {
                Destroy(m_CurrentQRUI);
                m_CurrentQRUI = null;
            }
        }

        /// <summary>
        /// Очистить все созданные QR элементы интерфейса
        /// </summary>
        public void ClearQRUI()
        {
            if (m_CurrentQRUI != null)
            {
                Destroy(m_CurrentQRUI);
                m_CurrentQRUI = null;
            }
        }

        #endregion
    }
}