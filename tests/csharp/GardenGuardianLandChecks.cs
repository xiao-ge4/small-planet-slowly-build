// Runs the actual save/rule sources in Mono without opening or refreshing Unity.
using System;
using System.IO;
using System.Web.Script.Serialization;
using DawnGarden;

namespace UnityEngine
{
    public struct Vector3 { public float x, y, z; }
    public static class Application { public static string persistentDataPath; }
    public static class JsonUtility
    {
        public static T FromJson<T>(string text) => new JavaScriptSerializer().Deserialize<T>(text);
        public static string ToJson(object value, bool pretty) => new JavaScriptSerializer().Serialize(value);
    }
}

public static class GardenGuardianLandChecks
{
    static int checks;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static void Invalid(Action action, string message)
    {
        try { action(); } catch (InvalidDataException) { checks++; return; }
        throw new Exception(message);
    }
    public static void Main(string[] args)
    {
        // The runner owns this temporary directory; never use Application's real save path.
        string root=args[0], path=Path.Combine(root,"garden.json");
        UnityEngine.Application.persistentDataPath=root;
        var state=GardenSave.Load(path);
        Check(state.guardianLandState==GardenGuardianLand.Guarded,"fresh land guarded");
        Check(!GardenGuardianLand.Begin(state,false,out _),"permit required");
        state.chestOpened=true;GardenSave.GrantStarterReward(state);GardenSave.GrantStarterReward(state);
        Check(state.expansionPermits==1,"one starter permit");
        state.expansionPermits=3;
        Check(GardenGuardianLand.Begin(state,false,out var first),"begin challenge");
        Check(state.expansionPermits==3&&!state.firstExpansionUnlocked&&!GardenGuardianLand.CanCreate(state),"starting keeps permit and gate");
        Check(!GardenGuardianLand.Begin(state,false,out _),"no overlapping challenge");
        Check(GardenGuardianLand.Resolve(state,Guid.NewGuid().ToString("N"),true)==GardenGuardianLand.Resolution.Rejected,"wrong encounter cannot win");
        Check(GardenGuardianLand.Resolve(state,first,false)==GardenGuardianLand.Resolution.Defeat,"retreat");
        Check(state.expansionPermits==3&&!state.firstExpansionUnlocked,"retreat has no cost");
        Check(GardenGuardianLand.Resolve(state,first,true)==GardenGuardianLand.Resolution.Rejected,"failed battle cannot become victory");
        Check(GardenGuardianLand.Begin(state,false,out var interrupted),"second attempt");
        GardenSave.Write(path,state);state=GardenSave.Load(path);
        Check(state.guardianLandState==GardenGuardianLand.Guarded&&state.guardianLastOutcome=="interrupted"&&state.expansionPermits==3,"reload interrupted challenge safely");
        Check(GardenGuardianLand.Resolve(state,interrupted,true)==GardenGuardianLand.Resolution.Rejected,"stale interrupted result rejected");
        Check(GardenGuardianLand.Begin(state,false,out var winning),"retry after interruption");
        var checkpoint=new GardenGuardianLand.Checkpoint(state);
        Check(GardenGuardianLand.Resolve(state,winning,true)==GardenGuardianLand.Resolution.Occupied,"first victory owns land");
        Check(state.expansionPermits==2&&GardenGuardianLand.CanCreate(state),"consume exactly one and allow co-creation");
        Check(GardenGuardianLand.Resolve(state,winning,true)==GardenGuardianLand.Resolution.AlreadyResolved&&state.expansionPermits==2,"duplicate victory idempotent");
        checkpoint.Apply(state);
        Check(state.expansionPermits==3&&!state.firstExpansionUnlocked&&GardenGuardianLand.IsChallenging(state),"failed save checkpoint restores all progression");
        Check(GardenGuardianLand.Resolve(state,winning,true)==GardenGuardianLand.Resolution.Occupied,"save can retry same victory");
        GardenSave.Write(path,state);state=GardenSave.Load(path);
        Check(state.firstExpansionUnlocked&&state.guardianOccupationId==winning&&state.expansionPermits==2,"occupation restores after interrupted rise");
        Check(GardenGuardianLand.Resolve(state,winning,true)==GardenGuardianLand.Resolution.AlreadyResolved&&state.expansionPermits==2,"victory receipt survives exit before reveal and cannot debit again");
        Check(!GardenGuardianLand.Begin(state,false,out _),"cannot acquire same land again");
        Check(GardenGuardianLand.Begin(state,true,out var replay),"owned land can replay");
        Check(state.firstExpansionUnlocked&&!GardenGuardianLand.CanCreate(state),"replay keeps terrain but suspends edits");
        Check(GardenGuardianLand.Resolve(state,replay,true)==GardenGuardianLand.Resolution.ReplayVictory,"replay victory");
        Check(state.expansionPermits==2&&state.guardianOccupationId==winning,"replay has no reward and preserves receipt");
        Check(GardenGuardianLand.Begin(state,true,out replay),"replay again");
        GardenSave.Write(path,state);state=GardenSave.Load(path);
        Check(state.firstExpansionUnlocked&&GardenGuardianLand.CanCreate(state)&&state.guardianOccupationId==winning,"interrupted replay cannot revoke ownership");
        File.WriteAllText(path,"{\"schemaVersion\":1,\"homeId\":\"dawn-garden-starter-v1\",\"firstExpansionUnlocked\":true,\"expansionPermits\":4}");
        var legacy=GardenSave.Load(path);
        Check(legacy.guardianLegacyOwnership&&legacy.firstExpansionUnlocked&&legacy.expansionPermits==4&&GardenGuardianLand.CanCreate(legacy),"legacy built land and inventory grandfathered");
        Check(legacy.guardianOccupationId==GardenSave.Load(path).guardianOccupationId,"legacy receipt stable before first write");
        Check(GardenGuardianLand.Begin(legacy,true,out replay),"legacy replay");
        GardenGuardianLand.Resolve(legacy,replay,false);
        Check(legacy.firstExpansionUnlocked&&legacy.expansionPermits==4,"legacy failed replay preserves land");
        var malformed=new GardenProgress{guardianLandVersion=1,guardianLandState="occupied"};
        Invalid(()=>GardenGuardianLand.Validate(malformed),"malformed owned state accepted");
        malformed=new GardenProgress{guardianLandVersion=1,guardianLandState="guarded",guardianEncounterId=Guid.NewGuid().ToString("N")};
        Invalid(()=>GardenGuardianLand.Validate(malformed),"orphan encounter accepted");
        Console.WriteLine("PASS: "+checks+" guardian save/rule checks (temporary storage only)");
    }
}
