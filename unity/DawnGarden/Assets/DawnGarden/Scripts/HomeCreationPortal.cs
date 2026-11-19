using System;
using System.IO;
using UnityEngine;

namespace DawnGarden
{
    public static class HomeCreationPortal
    {
        [Serializable] public class PlotGrant
        {
            public string token, homeId, plotId="first-expansion", createdAt;
            public bool unlocked;
        }
        public static void Open(DawnGardenGame game)
        {
            if(SmallWorldApp.Instance!=null){SmallWorldApp.Instance.OpenCreator();return;}
            if(!GardenGuardianLand.CanCreate(game.Progress) || game.isVisitor || game.expansion.IsRising || game.expansion.IsGuardianRevealPending)return;
            if(!game.SaveNow()){game.SetStatus("请先成功保存家园，再进入土地创作。");return;}
#if UNITY_EDITOR
            var token=Guid.NewGuid().ToString("N");
            var directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../artifacts/home-creation/game-grants"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory,token+".json"),JsonUtility.ToJson(new PlotGrant {token=token,homeId=game.homeId,unlocked=true,createdAt=DateTime.UtcNow.ToString("o")},true));
            Application.OpenURL("http://127.0.0.1:8780/?grant="+token);
            game.SetStatus("土地创作台已打开。这片土地不会重复消耗扩建许可。");
#else
            game.SetStatus("当前创作服务用于本机 Unity Demo，独立客户端将在部署服务后接入。");
#endif
        }
    }
}
