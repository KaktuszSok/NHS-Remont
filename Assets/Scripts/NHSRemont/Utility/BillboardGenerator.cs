using UnityEngine;

namespace NHSRemont.Utility
{
    /// <summary>
    /// Uses RuntimePreviewGenerator to create a billboard for any object
    /// </summary>
    public static class BillboardGenerator
    {
        private static readonly RuntimePreviewGenerator generator = new RuntimePreviewGenerator
        {
            BackgroundColor = Color.clear,
            PreviewDirection = Vector3.forward,
            Padding = 0f,
            OrthographicMode = true,
            GenerateMipMaps = true,
            MipmapDebugMode = false
        };

        /// <summary>
        /// Create a billboard for the given gameObject
        /// </summary>
        /// <param name="gameObject">The gameObject to billboard (can be a prefab)</param>
        /// <param name="width">The width of the billboard image, in pixels</param>
        /// <param name="height">The height of the billboard image, in pixels</param>
        /// <param name="filterMode">The filter mode of the billboard image</param>
        public static Texture2D GenerateBillboard(GameObject gameObject, int width, int height, FilterMode filterMode = FilterMode.Bilinear)
        {
            GameObject light = GameObject.Find("Directional Light");
            Vector3 fwd = light == null ? Vector3.forward : light.transform.forward;
            fwd.y = 0;
            generator.PreviewDirection = fwd;
            
            Transform previewModel = Object.Instantiate(gameObject, null, false).transform;
            previewModel.transform.position = RuntimePreviewGenerator.PREVIEW_POSITION;
            previewModel.gameObject.layer = RuntimePreviewGenerator.PREVIEW_LAYER;
            previewModel.gameObject.hideFlags = HideFlags.HideAndDontSave;
            Texture2D billboardTexture = generator.GenerateModelPreview(previewModel, width, height, false, false);
            billboardTexture.filterMode = filterMode;
            previewModel.gameObject.SetActive(false);
            Object.Destroy(previewModel.gameObject);
            return billboardTexture;
        }
    }
}