using UnityEngine;
using System.Linq;
namespace DawnGarden
{
    public sealed class SmallWorldGrowth:MonoBehaviour
    {
        DawnGardenGame game;GameObject bloom;Renderer[] plantRenderers;Transform plantVisual;int lastStage=-1;
        public void Bind(DawnGardenGame g,SmallWorldLibrary library)
        {
            game=g;plantVisual=game.FindInteraction(GardenInteractionKind.Water).visual;plantRenderers=plantVisual.GetComponentsInChildren<Renderer>();var asset=library.assets.FirstOrDefault(a=>a.id=="meadow-flower");if(asset?.prefab==null)return;
            bloom=Instantiate(asset.prefab,game.FindInteraction(GardenInteractionKind.Water).transform);
            bloom.name="照料后绽放的花";bloom.transform.localPosition=new Vector3(.16f,.06f,.12f);bloom.transform.localScale=Vector3.one*.34f;
            foreach(var c in bloom.GetComponentsInChildren<Collider>())Destroy(c);bloom.SetActive(false);game.GameEvent+=React;
        }
        void Update(){if(game?.Progress==null||bloom==null)return;int stage=Mathf.Min(3,game.Progress.wateringDays);if(stage==lastStage)return;lastStage=stage;bloom.SetActive(stage>=2);bloom.transform.localScale=Vector3.one*(stage>=3?.55f:.23f);}
        void LateUpdate()
        {
            if(bloom==null||!bloom.activeSelf||plantRenderers.Length==0)return;
            var bounds=plantRenderers[0].bounds;foreach(var r in plantRenderers)bounds.Encapsulate(r.bounds);
            bloom.transform.position=new Vector3(plantVisual.position.x+.03f,bounds.max.y-.015f,plantVisual.position.z);
        }
        void React(string kind)
        {
            if(kind!="water")return;
            SmallWorldPetResponse.Chime(game,game.homeId.Contains("ember")?"ember":game.homeId.Contains("wind")?"wind":"dew","grow");
        }
    }
}
