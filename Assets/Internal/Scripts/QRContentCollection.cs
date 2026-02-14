using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.XR.Templates.AR
{
    /// <summary>
    /// Represents a button that will be created in 3D space after QR code scanning
    /// </summary>
    [System.Serializable]
    public class QRContentButton
    {
        [Header("Button Settings")]
        public string buttonText = "Кнопка";
        public string description = "Описание информации";

        [Header("UI Settings")]
        public bool showImmediately = false;
        public Vector3 localPosition = Vector3.zero;
        public Vector3 localRotation = Vector3.zero;
        public Vector3 localScale = Vector3.one;
    }

    /// <summary>
    /// Collection of QR content buttons with matching criteria
    /// </summary>
    [System.Serializable]
    public class QRContentItem
    {
        [Header("Identification")]
        public string collectionName = "Название коллекции";
        public string qrCodeMatchText = "";

        [Header("Content")]
        public List<QRContentButton> buttons = new List<QRContentButton>();

        [Header("Layout Settings")]
        public bool arrangeInCircle = true;
        public float circleRadius = 1.0f;
        public float buttonSpacing = 0.5f;
        public Vector3 layoutOffset = Vector3.zero;
    }

    /// <summary>
    /// Manages QR content collections and matches QR codes to content
    /// </summary>
    [CreateAssetMenu(fileName = "QRContentCollection", menuName = "AR/QR Content Collection")]
    public class QRContentCollection : ScriptableObject
    {
        [Header("Collections")]
        public List<QRContentItem> collections = new List<QRContentItem>();

        [Header("Default Settings")]
        public QRContentItem defaultCollection;

        /// <summary>
        /// Find matching collection for QR code text
        /// </summary>
        /// <param name="qrCodeText">Text decoded from QR code</param>
        /// <returns>Matching collection or default collection</returns>
        public QRContentItem GetCollectionForQR(string qrCodeText)
        {
            if (string.IsNullOrEmpty(qrCodeText))
                return defaultCollection;

            // First try exact match
            foreach (var collection in collections)
            {
                if (qrCodeText.Equals(collection.qrCodeMatchText, System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log($"Exact match found: {collection.collectionName}");
                    return collection;
                }
            }

            // Then try partial match (contains)
            foreach (var collection in collections)
            {
                if (!string.IsNullOrEmpty(collection.qrCodeMatchText) &&
                    qrCodeText.Contains(collection.qrCodeMatchText))
                {
                    Debug.Log($"Partial match found: {collection.collectionName}");
                    return collection;
                }
            }

            // Then try match by collection name
            foreach (var collection in collections)
            {
                if (qrCodeText.Contains(collection.collectionName))
                {
                    Debug.Log($"Collection name match found: {collection.collectionName}");
                    return collection;
                }
            }

            Debug.Log($"No match found for QR: '{qrCodeText}', using default collection");
            return defaultCollection;
        }
    }
}