using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.XR.Templates.AR
{
    /// <summary>
    /// Запись QR совпадения - упрощенная версия
    /// </summary>
    [System.Serializable]
    public class QRMatchEntry
    {
        public string qrCodeMatchText = "";
        public string entryName = "QR Match";
    }

    /// <summary>
    /// MonoBehaviour компонент для хранения совпадений QR кодов
    /// Упрощенная версия - только основные поля
    /// </summary>
    public class QRMatches : MonoBehaviour
    {
        #region Поля и методы

        [Header("Записи QR совпадений")]
        public List<QRMatchEntry> matches = new List<QRMatchEntry>();

        /// <summary>
        /// Найти совпадающую запись для текста QR кода
        /// </summary>
        /// <param name="qrCodeText">Текст декодированный из QR кода</param>
        /// <returns>Совпадающая запись или null если не найдена</returns>
        public QRMatchEntry GetMatchForQR(string qrCodeText)
        {
            if (string.IsNullOrEmpty(qrCodeText))
                return null;

            // Сначала попробовать точное совпадение
            foreach (var match in matches)
            {
                if (qrCodeText.Equals(match.qrCodeMatchText, System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log($"QR совпадения: Найдено совпадение - {match.entryName}");
                    return match;
                }
            }

            // Попытаться с частичным совпадением (содержит)
            foreach (var match in matches)
            {
                if (!string.IsNullOrEmpty(match.qrCodeMatchText) &&
                    qrCodeText.Contains(match.qrCodeMatchText))
                {
                    Debug.Log($"QR совпадения: Найдено частичное совпадение - {match.entryName}");
                    return match;
                }
            }

            // Попытаться совпадением по имени записи
            foreach (var match in matches)
            {
                if (qrCodeText.Contains(match.entryName))
                {
                    Debug.Log($"QR совпадения: Найдено совпадение по имени - {match.entryName}");
                    return match;
                }
            }

            Debug.Log($"QR совпадения: Совпадение не найдено для '{qrCodeText}'");
            return null;
        }

        /// <summary>
        /// Проверить, существует ли совпадение для данного QR текста
        /// </summary>
        /// <param name="qrCodeText">Текст декодированный из QR кода</param>
        /// <returns>True если совпадение существует</returns>
        public bool HasMatchForQR(string qrCodeText)
        {
            return GetMatchForQR(qrCodeText) != null;
        }

        #endregion
    }
}