using System;
using System.IO;
using UnityEngine;

namespace DawnGarden
{
    [Serializable] public class SmallWorldProfile
    {
        public int schemaVersion=1;
        public string clientId,theme="",createdAt,pendingTurnId,pendingResidentId,creationRunId;
        public int travelEnergy=3,sequence;
        public bool remindersMuted;
        public bool homecomingLampLit;
        public string energyWaterDay;
        public string creationIdeaDraft,pendingGuideActionJson,previewHandledKey,inspirationSeenHash;
        public string[] appliedGuideTurns=new string[0];
        public int worldSeed,ownContinent;
        public static string TestRoot;
        public static string Root => string.IsNullOrEmpty(TestRoot)?Path.Combine(Application.persistentDataPath,"SmallWorld-v1"):TestRoot;
        public static string ProfilePath=>Path.Combine(Root,"profile.json");
        public static SmallWorldProfile Load()
        {
            Directory.CreateDirectory(Root);
            if(File.Exists(ProfilePath))
            {
                try {var p=JsonUtility.FromJson<SmallWorldProfile>(File.ReadAllText(ProfilePath));if(p.schemaVersion==1 && Guid.TryParseExact(p.clientId,"N",out _) && p.travelEnergy>=0 && p.travelEnergy<=10 && (p.theme==""||p.theme=="dawn"||p.theme=="ember"))return p;}
                catch(Exception){ }
                // Preserve a failed save for inspection; start in an isolated replacement slot.
                File.Copy(ProfilePath,ProfilePath+".invalid-"+DateTime.UtcNow.Ticks,true);
            }
            var id=Guid.NewGuid().ToString("N");var profile=new SmallWorldProfile {clientId=id,createdAt=DateTime.UtcNow.ToString("o"),worldSeed=id.GetHashCode()&int.MaxValue};
            profile.ownContinent=profile.worldSeed%4;profile.Save();return profile;
        }
        public void Save()
        {
            Directory.CreateDirectory(Root);var temp=ProfilePath+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(this,true));
            SmallWorldBrowserStorage.Commit(temp,ProfilePath,ProfilePath+".bak");
        }
        public string HomeSavePath(string plot)
        {
            var safe=plot.Replace(':','-');foreach(var c in Path.GetInvalidFileNameChars())safe=safe.Replace(c,'-');
            var folder=Path.Combine(Root,clientId);Directory.CreateDirectory(folder);return Path.Combine(folder,safe+".json");
        }
    }
}
