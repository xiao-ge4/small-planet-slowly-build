using System;
using System.Collections.Generic;
using System.Linq;

namespace DawnGarden
{
    // Pure progression model. Serialized only inside the isolated story profile.
    [Serializable]
    public sealed class StoryTerritoryLedger
    {
        [Serializable] public sealed class Entry
        {
            public string id, evidence, awardedUtc;
            public Entry() { }
            public Entry(string key,string source) {id=key;evidence=source;awardedUtc=DateTime.UtcNow.ToString("o");}
        }
        public int schemaVersion=1;
        public string provenance="demo-authored-territory-rewards; prerequisites checked against actual gameplay";
        public List<Entry> entries=new List<Entry>{new Entry("courtyard","initial-home")};
        static readonly string[] Keys={"courtyard","rest-garden","nursery-lower","nursery-upper","workshop-shore","ridge-garden"};
        public string[] OwnedIds => entries.Select(e=>e.id).ToArray();
        public bool Contains(string id)=>entries.Any(e=>e.id==id);
        static string Parent(string id)
        {
            switch(id)
            {
                case "rest-garden":return "courtyard";
                case "nursery-lower":return "rest-garden";
                case "nursery-upper":return "nursery-lower";
                case "workshop-shore":return "rest-garden";
                case "ridge-garden":return "workshop-shore";
                default:return null;
            }
        }
        static bool ValidEvidence(string id,string evidence)
        {
            if(string.IsNullOrEmpty(evidence))return false;
            switch(id)
            {
                case "courtyard":return evidence=="initial-home";
                case "rest-garden":return evidence.StartsWith("guardian-victory:",StringComparison.Ordinal)&&evidence.Length>17;
                case "nursery-lower":return evidence.StartsWith("garden-patrol:",StringComparison.Ordinal)&&evidence.Length>14;
                case "nursery-upper":return evidence=="neighbor-gift:lumi-flowers";
                case "workshop-shore":return evidence=="neighbor-gift:ajin-lantern";
                case "ridge-garden":return evidence=="neighbor-gift:lanlan-stone";
                default:return false;
            }
        }
        public bool Grant(string id,string evidence)
        {
            Validate();
            var existing=entries.FirstOrDefault(e=>e.id==id);
            if(existing!=null)
            {
                if(existing.evidence!=evidence)throw new InvalidOperationException("Territory already has a different reward source.");
                return false;
            }
            if(!Keys.Contains(id)||!ValidEvidence(id,evidence))throw new InvalidOperationException("Missing or invalid territory reward evidence.");
            if(!Contains(Parent(id)))throw new InvalidOperationException("Unlock the neighboring territory first.");
            entries.Add(new Entry(id,evidence));return true;
        }
        public void Validate()
        {
            if(schemaVersion!=1||entries==null||entries.Count==0||entries[0]?.id!="courtyard")throw new InvalidOperationException("Invalid story territory ledger.");
            var seen=new HashSet<string>();
            foreach(var entry in entries)
            {
                if(entry==null||!Keys.Contains(entry.id)||!seen.Add(entry.id)||!ValidEvidence(entry.id,entry.evidence))throw new InvalidOperationException("Invalid or duplicate territory entry.");
                var parent=Parent(entry.id);
                if(parent!=null&&!seen.Contains(parent))throw new InvalidOperationException("Territory reward is not adjacent to an earlier owned district.");
            }
        }
    }
}
