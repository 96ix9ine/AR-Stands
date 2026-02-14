using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.XR.Templates.AR
{
    /// <summary>
    /// Запись UI ссылки для совпадения QR кода
    /// Упрощенная версия с поддержкой массивов
    /// </summary>
    [System.Serializable]
    public class QRUIEntry
    {
        [Header("Имя записи")]
        public string entryName = "";

        [Header("Связанные UI объекты")]
        public GameObject[] buttonPrefabs;
        public GameObject[] infoPanels;
    }

    /// <summary>
    /// MonoBehaviour компонент для хранения UI ссылок для совпадений QR
    /// Упрощенная версия - связывает QR совпадения с реальными UI объектами
    /// </summary>
    public class QRUIReferences : MonoBehaviour
    {
        #region Поля и методы

        [Header("UI ссылки")]
        public List<QRUIEntry> uiEntries = new List<QRUIEntry>();

        /// <summary>
        /// Получить UI ссылки для конкретного ключа совпадения QR
        /// </summary>
        /// <param name="entryName">Ключ из записи QR совпадения</param>
        /// <returns>Запись UI или null если не найдена</returns>
        public QRUIEntry GetUIForMatch(string entryName)
        {
            if (string.IsNullOrEmpty(entryName))
                return null;

            foreach (var entry in uiEntries)
            {
                if (entryName.Equals(entry.entryName, System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log($"QR UI ссылки: Найден UI для совпадения - {entryName}");
                    return entry;
                }
            }

            Debug.Log($"QR UI ссылки: UI не найден для совпадения - {entryName}");
            return null;
        }

        /// <summary>
        /// Получить префабы кнопок для ключа совпадения QR
        /// </summary>
        /// <param name="entryName">Ключ из записи QR совпадения</param>
        /// <returns>Массив префабов кнопок или пустой массив</returns>
        public GameObject[] GetButtonsForMatch(string entryName)
        {
            QRUIEntry entry = GetUIForMatch(entryName);
            if (entry != null && entry.buttonPrefabs != null && entry.buttonPrefabs.Length > 0)
            {
                return entry.buttonPrefabs;
            }

            return new GameObject[0];
        }

        /// <summary>
        /// Получить информационные панели для ключа совпадения QR с поддержкой индексов
        /// </summary>
        /// <param name="entryName">Ключ из записи QR совпадения</param>
        /// <param name="buttonIndex">Индекс кнопки (начиная с 0)</param>
        /// <returns>Информационная панель для конкретного индекса кнопки или первая панель</returns>
        public GameObject[] GetPanelsForMatch(string entryName)
        {
            QRUIEntry entry = GetUIForMatch(entryName);
            if (entry != null && entry.infoPanels != null && entry.infoPanels.Length > 0)
            {
                return entry.infoPanels;
            }

            return new GameObject[0];
        }

        /// <summary>
        /// Получить конкретную информационную панель для индекса кнопки
        /// </summary>
        /// <param name="entryName">Ключ из записи QR совпадения</param>
        /// <param name="buttonIndex">Индекс кнопки (начиная с 0)</param>
        /// <returns>Информационная панель для конкретного индекса кнопки</returns>
        public GameObject GetPanelForButton(string entryName, int buttonIndex)
        {
            QRUIEntry entry = GetUIForMatch(entryName);
            if (entry != null && entry.infoPanels != null && entry.infoPanels.Length > buttonIndex)
            {
                return entry.infoPanels[buttonIndex];
            }

            return entry.infoPanels != null && entry.infoPanels.Length > 0 ? entry.infoPanels[0] : null;
        }

        /// <summary>
        /// Добавить новую запись UI для ключа совпадения
        /// </summary>
        /// <param name="entryName">Ключ из записи QR совпадения</param>
        /// <param name="buttons">Префабы кнопок</param>
        /// <param name="panels">Информационные панели</param>
        public void AddUIEntry(string entryName, GameObject[] buttons, GameObject[] panels)
        {
            QRUIEntry newEntry = new QRUIEntry
            {
                entryName = entryName,
                buttonPrefabs = buttons,
                infoPanels = panels
            };
            uiEntries.Add(newEntry);
            Debug.Log($"QR UI ссылки: Добавлена запись UI для - {entryName}");
        }

        /// <summary>
        /// Проверить, существует ли UI для ключа совпадения
        /// </summary>
        /// <param name="entryName">Ключ из записи QR совпадения</param>
        /// <returns>True если запись UI существует</returns>
        public bool HasUIForMatch(string entryName)
        {
            return GetUIForMatch(entryName) != null;
        }

        #endregion
    }
}