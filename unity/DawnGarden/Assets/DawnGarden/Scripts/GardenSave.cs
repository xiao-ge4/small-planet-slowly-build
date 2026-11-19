using System;
using System.IO;
using UnityEngine;

namespace DawnGarden
{
    [Serializable]
    public sealed class GardenProgress
    {
        public int schemaVersion = 1;
        public string homeId = "dawn-garden-starter-v1";
        public bool watered, chestOpened, starterRewardGranted;
        public int expansionPermits;
        public bool expansionOfferPending, firstExpansionUnlocked;
        public bool hasPosition;
        public Vector3 position;
        public float heading;
        public string lastWateredDay;
        public int wateringDays;
        // Version zero identifies pre-guardian saves; never revoke their built land.
        public int guardianLandVersion;
        public string guardianLandState, guardianEncounterId, guardianOccupationId;
        public string guardianLastEncounterId, guardianLastOutcome;
        public bool guardianLegacyOwnership, guardianReplay;
    }

    public static class GardenSave
    {
        public static string Today => DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd");
        public static bool WateredToday(GardenProgress state) => state.watered && state.lastWateredDay==Today;
        public static bool WaterToday(GardenProgress state,string day=null)
        {
            day=day??Today;
            if(!string.IsNullOrEmpty(state.lastWateredDay) && string.CompareOrdinal(day,state.lastWateredDay)<=0)return false;
            state.watered=true;state.lastWateredDay=day;state.wateringDays=Math.Max(0,state.wateringDays)+1;return true;
        }
        public static string DefaultPath => Path.Combine(Application.persistentDataPath, "DawnGarden", "garden-state-v1.json");
        public static string PathFor(string homeId)
        {
            if (homeId == "dawn-garden-starter-v1") return DefaultPath;
            if (homeId == "ember-camp-starter-v1") return Path.Combine(Application.persistentDataPath, "DawnGarden", "ember-camp-state-v1.json");
            throw new InvalidDataException("未知家园，无法选择存档。");
        }
        public static GardenProgress Load(string path, string homeId = "dawn-garden-starter-v1")
        {
            if (!File.Exists(path))
            {
                var fresh = new GardenProgress { homeId = homeId };
                GardenGuardianLand.Restore(fresh);
                return fresh;
            }
            var state = JsonUtility.FromJson<GardenProgress>(File.ReadAllText(path));
            if (state == null || state.schemaVersion != 1 || state.homeId != homeId
                || state.expansionPermits < 0 || (state.hasPosition && !Finite(state.position)))
                throw new InvalidDataException("家园存档格式无效，原文件已保留。");
            // Repair an interrupted chest reward once; added fields keep v1 saves compatible.
            GrantStarterReward(state);
            GardenGuardianLand.Restore(state);
            if(state.watered && string.IsNullOrEmpty(state.lastWateredDay)){state.lastWateredDay=Today;state.wateringDays=Math.Max(1,state.wateringDays);}
            return state;
        }
        public static void GrantStarterReward(GardenProgress state)
        {
            if (!state.chestOpened || state.starterRewardGranted) return;
            state.starterRewardGranted = true;
            state.expansionPermits++;
            state.expansionOfferPending = !state.firstExpansionUnlocked;
        }
        public static void Write(string path, GardenProgress state)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(state, true));
            SmallWorldBrowserStorage.Commit(temp, path, path + ".bak");
        }
        static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsInfinity(p.x)
            && !float.IsNaN(p.y) && !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z);
    }
}
