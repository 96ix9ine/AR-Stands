using UnityEngine;

namespace UnityEngine.XR.Templates.AR
{
    /// <summary>
    /// Простой компонент-коннектор, который связывает QR совпадения с UI ссылками
    /// Действует как мост между QRMatchEntries и их связанными UI объектами
    /// </summary>
    public class QRGameManager : MonoBehaviour
    {
        #region Поля и свойства

        [Header("Компонент QR совпадений")]
        [SerializeField] private QRMatches m_QRMatches;

        [Header("Компонент UI ссылок")]
        [SerializeField] private QRUIReferences m_UIReferences;

        [Header("Ссылка на камеру")]
        [SerializeField] private Camera m_ARCamera;

        [Header("Отладка")]
        [SerializeField] private bool m_ShowDebugInfo = true;

        // Свойства для легкого доступа
        public QRMatches qrMatches => m_QRMatches;
        public QRUIReferences uiReferences => m_UIReferences;
        public Camera arCamera => m_ARCamera;
        public bool showDebugInfo => m_ShowDebugInfo;

        #endregion

        #region Методы Unity жизненного цикла

        void Start()
        {
            if (m_ShowDebugInfo)
                Debug.Log("QR Game Manager инициализирован как простой коннектор");
        }

        #endregion

        #region Управление UI для QR

        /// <summary>
        /// Получить UI ссылки для QR совпадения
        /// </summary>
        /// <param name="qrData">Текстовые данные QR</param>
        /// <returns>Кортеж с кнопками и панелями</returns>
        public (GameObject[] buttons, GameObject[] panels) GetUIForQR(string qrData)
        {
            if (m_QRMatches == null || m_UIReferences == null)
            {
                Debug.LogWarning("QR совпадения или UI ссылки не назначены!");
                return (null, null);
            }

            // Найти запись QR совпадения
            QRMatchEntry match = m_QRMatches.GetMatchForQR(qrData);
            if (match == null)
            {
                Debug.Log($"QR совпадение не найдено для: {qrData}");
                return (null, null);
            }

            // Получить UI ссылки для этого совпадения
            GameObject[] buttons = m_UIReferences.GetButtonsForMatch(match.entryName);
            GameObject[] panels = m_UIReferences.GetPanelsForMatch(match.entryName);

            if (m_ShowDebugInfo)
                Debug.Log($"Найден UI для QR '{qrData}' -> '{match.entryName}': {buttons?.Length ?? 0} кнопок, {panels?.Length ?? 0} панелей");

            return (buttons, panels);
        }

        /// <summary>
        /// Проверить, существует ли UI для данных QR
        /// </summary>
        /// <param name="qrData">Текстовые данные QR</param>
        /// <returns>True если конфигурация UI существует</returns>
        public bool HasUIForQR(string qrData)
        {
            if (m_QRMatches == null || m_UIReferences == null)
                return false;

            QRMatchEntry match = m_QRMatches.GetMatchForQR(qrData);
            if (match == null)
                return false;

            return m_UIReferences.HasUIForMatch(match.entryName);
        }

        #endregion
    }
}