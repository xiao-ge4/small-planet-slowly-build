using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace DawnGarden
{
    public sealed class EnvironmentProbe : MonoBehaviour
    {
        private int errorCount;
        private bool verificationRunning;
        private int framesObserved;

        private void OnEnable() { Application.logMessageReceived += CountErrors; }
        private void OnDisable() { Application.logMessageReceived -= CountErrors; }

        private void Start()
        {
            Debug.Log("Dawn Garden environment check running. Unity " + Application.unityVersion);
        }

        public void BeginVerification(string outputDirectory)
        {
            if (verificationRunning) return;
            verificationRunning = true;
            StartCoroutine(CaptureVerification(outputDirectory));
        }

        private IEnumerator CaptureVerification(string outputDirectory)
        {
            for (var frame = 0; frame < 45; frame++)
            {
                yield return new WaitForEndOfFrame();
                framesObserved++;
            }

            Directory.CreateDirectory(outputDirectory);
            var screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            var screenshotPath = Path.Combine(outputDirectory, "environment-game-view.png");
            if (screenshot != null)
                File.WriteAllBytes(screenshotPath, screenshot.EncodeToPNG());

            var pipeline = GraphicsSettings.currentRenderPipeline;
            var report = new RuntimeReport
            {
                utc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                renderPipeline = pipeline != null ? pipeline.GetType().FullName : "Built-in",
                graphicsDevice = SystemInfo.graphicsDeviceName,
                framesObserved = framesObserved,
                width = screenshot != null ? screenshot.width : 0,
                height = screenshot != null ? screenshot.height : 0,
                errorCount = errorCount,
                passed = pipeline != null && Camera.main != null && screenshot != null && errorCount == 0,
                screenshot = screenshotPath
            };
            if (screenshot != null) Destroy(screenshot);
            File.WriteAllText(Path.Combine(outputDirectory, "environment-runtime.json"), JsonUtility.ToJson(report, true));
            Debug.Log("Dawn Garden runtime verification completed: " + report.passed);
        }

        private void CountErrors(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errorCount++;
        }

        [Serializable]
        private sealed class RuntimeReport
        {
            public string utc;
            public string unityVersion;
            public string platform;
            public string renderPipeline;
            public string graphicsDevice;
            public int framesObserved;
            public int width;
            public int height;
            public int errorCount;
            public bool passed;
            public string screenshot;
        }
    }
}
