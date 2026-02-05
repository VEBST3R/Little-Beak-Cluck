using System;
using System.IO;
using UnityEngine;

namespace HenPatrol.Utility
{
    // Simple utility: Press P to save a PNG screenshot at the desired resolution
    public class ScreenshotCapture : MonoBehaviour
    {
        [Header("Capture Settings")]
        [SerializeField] private int width = 1284;
        [SerializeField] private int height = 2778;
        [SerializeField] private KeyCode triggerKey = KeyCode.P;
        [Tooltip("Save folder; defaults to <persistentDataPath>/Screenshots if empty")]
        [SerializeField] private string saveFolder = "";
        [SerializeField] private string filenamePrefix = "screenshot_";
        [Header("Orientation Fix")]
        [Tooltip("Flip image vertically before saving")][SerializeField] private bool flipVertical = true;
        [Tooltip("Flip image horizontally (mirror) before saving")][SerializeField] private bool flipHorizontal = true;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            if (Input.GetKeyDown(triggerKey))
            {
                StartCoroutine(CaptureRoutine());
            }
        }

        System.Collections.IEnumerator CaptureRoutine()
        {
            // Wait until end of frame for a fully rendered frame
            yield return new WaitForEndOfFrame();

            string folder = string.IsNullOrWhiteSpace(saveFolder)
                ? Path.Combine(Application.persistentDataPath, "Screenshots")
                : saveFolder;
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filePath = Path.Combine(folder, $"{filenamePrefix}{width}x{height}_{timestamp}.png");

            // Try modern API first to capture the whole Game View (including overlay UI)
#if UNITY_2020_1_OR_NEWER
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            try
            {
                // Capture the current game view into our RT
                ScreenCapture.CaptureScreenshotIntoRenderTexture(rt);

                var prevActive = RenderTexture.active;
                try
                {
                    RenderTexture.active = rt;
                    var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    tex.Apply(false, false);

                    FixOrientation(tex);
                    var png = tex.EncodeToPNG();
                    File.WriteAllBytes(filePath, png);
                    Destroy(tex);
                }
                finally
                {
                    RenderTexture.active = prevActive;
                }
            }
            finally
            {
                if (rt != null)
                {
                    rt.Release();
                    Destroy(rt);
                }
            }
#else
            // Fallback for older Unity versions: render from the main camera.
            // Note: This may not include Screen Space - Overlay UI.
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("ScreenshotCapture: No main camera found to render from.");
                yield break;
            }

            var prevTarget = cam.targetTexture;
            var rt2 = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            try
            {
                cam.targetTexture = rt2;
                cam.Render();

                var prevActive = RenderTexture.active;
                try
                {
                    RenderTexture.active = rt2;
                    var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    tex.Apply(false, false);

                    FixOrientation(tex);
                    var png = tex.EncodeToPNG();
                    File.WriteAllBytes(filePath, png);
                    Destroy(tex);
                }
                finally
                {
                    RenderTexture.active = prevActive;
                    cam.targetTexture = prevTarget;
                }
            }
            finally
            {
                if (rt2 != null)
                {
                    rt2.Release();
                    Destroy(rt2);
                }
            }
#endif
            Debug.Log($"Screenshot saved: {filePath}");
        }

        private void FixOrientation(Texture2D tex)
        {
            if (!flipHorizontal && !flipVertical) return;

            int w = tex.width;
            int h = tex.height;
            var src = tex.GetPixels32();
            var dst = new Color32[src.Length];

            for (int y = 0; y < h; y++)
            {
                int ny = flipVertical ? (h - 1 - y) : y;
                int srcRow = y * w;
                int dstRow = ny * w;
                for (int x = 0; x < w; x++)
                {
                    int nx = flipHorizontal ? (w - 1 - x) : x;
                    dst[dstRow + nx] = src[srcRow + x];
                }
            }

            tex.SetPixels32(dst);
            tex.Apply(false, false);
        }
    }
}
