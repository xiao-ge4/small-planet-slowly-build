using System.IO;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace DawnGarden
{
    // Unity's browser filesystem is in memory until IDBFS has been synchronized.
    public static class SmallWorldBrowserStorage
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void SmallWorld_FlushPersistentData();
#endif

        public static void Flush()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            SmallWorld_FlushPersistentData();
#endif
        }

        public static void Commit(string temporary, string path, string backup = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // File.Replace relies on native filesystem support unavailable in Web builds.
            if (backup != null && File.Exists(path)) File.Copy(path, backup, true);
            File.Copy(temporary, path, true);
            File.Delete(temporary);
#else
            if (File.Exists(path)) File.Replace(temporary, path, backup);
            else File.Move(temporary, path);
#endif
            Flush();
        }
    }
}
