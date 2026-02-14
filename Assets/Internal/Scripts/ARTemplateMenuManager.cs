using UnityEngine.UI;
using UnityEngine;

namespace UnityEngine.XR.Templates.AR
{
    /// <summary>
    /// Simplified Menu Manager with QR code scanning functionality only.
    /// Handles QR code scanning without AR object placement or surface detection.
    /// </summary>
    public class ARTemplateMenuManager : MonoBehaviour
    {
        [Header("QR Code Integration")]
        [SerializeField]
        [Tooltip("Button that opens QR code scanning.")]
        Button m_QRScanButton;

        /// <summary>
        /// Button that opens QR code scanning.
        /// </summary>
        public Button qrScanButton
        {
            get => m_QRScanButton;
            set => m_QRScanButton = value;
        }

        [SerializeField]
        [Tooltip("The QR code camera manager component.")]
        ARQRCodeCameraManager m_QRCodeCameraManager;

        /// <summary>
        /// The QR code camera manager component.
        /// </summary>
        public ARQRCodeCameraManager qrCodeCameraManager
        {
            get => m_QRCodeCameraManager;
            set => m_QRCodeCameraManager = value;
        }

        [Header("UI References for QR")]
        [SerializeField]
        [Tooltip("The overlay for QR scanning interface.")]
        GameObject m_QRScanOverlay;

        /// <summary>
        /// The overlay for QR scanning interface.
        /// </summary>
        public GameObject qrScanOverlay
        {
            get => m_QRScanOverlay;
            set => m_QRScanOverlay = value;
        }

        [SerializeField]
        [Tooltip("The text component for QR status display.")]
        Text m_QRStatusText;

        /// <summary>
        /// The text component for QR status display.
        /// </summary>
        public Text qrStatusText
        {
            get => m_QRStatusText;
            set => m_QRStatusText = value;
        }

        bool m_ShowQRScan = false;

        /// <summary>
        /// See <see cref="MonoBehaviour"/>.
        /// </summary>
        void Awake()
        {
            // Find QR Manager if not assigned
            if (m_QRCodeCameraManager == null)
                m_QRCodeCameraManager = FindFirstObjectByType<ARQRCodeCameraManager>();

            // Find QR UI elements if not assigned
            if (m_QRScanOverlay == null && m_QRCodeCameraManager != null)
            {
                // Try to get from QR manager
                var qrOverlay = m_QRCodeCameraManager.gameObject.GetComponentInChildren<Canvas>(true);
                if (qrOverlay != null)
                    m_QRScanOverlay = qrOverlay.gameObject;
            }

            if (m_QRStatusText == null && m_QRScanOverlay != null)
            {
                m_QRStatusText = m_QRScanOverlay.GetComponentInChildren<Text>();
            }
        }

        /// <summary>
        /// See <see cref="MonoBehaviour"/>.
        /// </summary>
        void OnEnable()
        {
            // QR button subscription
            if (m_QRScanButton != null)
                m_QRScanButton.onClick.AddListener(ToggleQRScanning);

            // QR Manager event subscriptions
            if (m_QRCodeCameraManager != null)
            {
                m_QRCodeCameraManager.onQRCodeDetected.AddListener(OnQRCodeDetected);
                m_QRCodeCameraManager.onQRCodeProcessed.AddListener(OnQRCodeProcessed);
            }
        }

        /// <summary>
        /// See <see cref="MonoBehaviour"/>.
        /// </summary>
        void OnDisable()
        {
            m_ShowQRScan = false;

            // QR button unsubscription
            if (m_QRScanButton != null)
                m_QRScanButton.onClick.RemoveListener(ToggleQRScanning);

            // QR Manager event unsubscriptions
            if (m_QRCodeCameraManager != null)
            {
                m_QRCodeCameraManager.onQRCodeDetected.RemoveListener(OnQRCodeDetected);
                m_QRCodeCameraManager.onQRCodeProcessed.RemoveListener(OnQRCodeProcessed);
            }
        }

        /// <summary>
        /// See <see cref="MonoBehaviour"/>.
        /// </summary>
        void Start()
        {
            HideQRScan();
        }

        /// <summary>
        /// See <see cref="MonoBehaviour"/>.
        /// </summary>
        void Update()
        {
            // Handle QR status text updates
            UpdateQRStatusText();
        }

        /// <summary>
        /// Toggle QR code scanning mode
        /// </summary>
        public void ToggleQRScanning()
        {
            if (m_ShowQRScan)
            {
                HideQRScan();
            }
            else
            {
                ShowQRScan();
            }
        }

        /// <summary>
        /// Show QR scanning interface
        /// </summary>
        public void ShowQRScan()
        {
            m_ShowQRScan = true;

            // Show QR overlay
            if (m_QRScanOverlay != null)
                m_QRScanOverlay.SetActive(true);

            // Start QR scanning
            if (m_QRCodeCameraManager != null)
                m_QRCodeCameraManager.StartScanning();
        }

        /// <summary>
        /// Hide QR scanning interface
        /// </summary>
        public void HideQRScan()
        {
            m_ShowQRScan = false;

            // Hide QR overlay
            if (m_QRScanOverlay != null)
                m_QRScanOverlay.SetActive(false);

            // Stop QR scanning
            if (m_QRCodeCameraManager != null)
                m_QRCodeCameraManager.StopScanning();
        }

        /// <summary>
        /// Handle QR code detection event
        /// </summary>
        /// <param name="qrData">The decoded QR data</param>
        /// <param name="position">The position where QR was detected</param>
        private void OnQRCodeDetected(string qrData, Vector3 position)
        {
            Debug.Log($"QR Code detected at position: {position} with data: {qrData}");
        }

        /// <summary>
        /// Handle QR code processing completion
        /// </summary>
        /// <param name="qrData">The processed QR data</param>
        private void OnQRCodeProcessed(string qrData)
        {
            Debug.Log($"QR Code processed: {qrData}");

            // Hide QR scan automatically after successful processing
            if (!string.IsNullOrEmpty(qrData))
            {
                StartCoroutine(HideQRScanDelayed());
            }
        }

        /// <summary>
        /// Update QR status text
        /// </summary>
        private void UpdateQRStatusText()
        {
            if (m_QRStatusText != null && m_ShowQRScan)
            {
                if (m_QRCodeCameraManager != null)
                {
                    if (m_QRCodeCameraManager.qrCodeFound)
                    {
                        m_QRStatusText.text = $"QR код найден: {m_QRCodeCameraManager.lastQRData}";
                    }
                    else if (m_QRCodeCameraManager.isScanning)
                    {
                        m_QRStatusText.text = "Наведите камеру на QR код...";
                    }
                    else
                    {
                        m_QRStatusText.text = "Готов к сканированию";
                    }
                }
            }
        }

        /// <summary>
        /// Hide QR scan after delay
        /// </summary>
        /// <returns></returns>
        private System.Collections.IEnumerator HideQRScanDelayed()
        {
            yield return new WaitForSeconds(2.0f);
            HideQRScan();
        }

        /// <summary>
        /// Clear QR UI elements
        /// </summary>
        public void ClearQRUI()
        {
            if (m_QRCodeCameraManager != null)
                m_QRCodeCameraManager.ClearQRUI();
        }
    }
}