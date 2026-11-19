using System.Linq;
using UnityEngine;
namespace DawnGarden
{
    public sealed class SmallWorldLibrary : ScriptableObject
    {
        public SmallWorldAsset[] assets;
        public Font font;
        public string buildId;
        public SmallWorldAsset Find(string id,string hash)=>assets.FirstOrDefault(a=>a.id==id&&a.hash==hash);
    }
}
