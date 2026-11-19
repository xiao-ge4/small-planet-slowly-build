using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DawnGarden.Editor
{
    [InitializeOnLoad]
    public static class EmberCampTestRunner
    {
        const string Key="DawnGarden.EmberVerification";
        static double deadline;
        static EmberCampTestRunner()
        {
            deadline=EditorApplication.timeSinceStartup+210;
            EditorApplication.playModeStateChanged+=state=>{
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {
                    var game=UnityEngine.Object.FindFirstObjectByType<DawnGardenGame>();
                    game.gameObject.AddComponent<EmberCampVerification>().Begin(game,GardenBootstrap.Output);
                }
                if(state==PlayModeStateChange.EnteredEditMode)SessionState.SetBool(Key,false);
            };
            EditorApplication.update+=()=>{
                if(!SessionState.GetBool(Key,false))return;
                if(File.Exists(Path.Combine(GardenBootstrap.Output,"ember-runtime.json"))){SessionState.SetBool(Key,false);EditorApplication.ExitPlaymode();}
                else if(EditorApplication.timeSinceStartup>deadline){SessionState.SetBool(Key,false);Debug.LogError("Ember verification timed out");EditorApplication.ExitPlaymode();}
            };
        }
        [MenuItem("Dawn Garden/Ember Camp/Run Verification")]
        public static void Verify()
        {
            EmberCampSetup.Open();Directory.CreateDirectory(GardenBootstrap.Output);
            var report=Path.Combine(GardenBootstrap.Output,"ember-runtime.json");if(File.Exists(report))File.Delete(report);
            deadline=EditorApplication.timeSinceStartup+210;SessionState.SetBool(Key,true);
            ConfigureGameView();
            EditorApplication.EnterPlaymode();
        }
        public static void ConfigureGameView()
        {
            var asm=typeof(UnityEditor.Editor).Assembly;var sizesType=asm.GetType("UnityEditor.GameViewSizes");
            var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes=singleton.GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null);
            var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
            var group=sizesType.GetProperty("currentGroup",flags).GetValue(sizes);var type=group.GetType();
            var sizeType=asm.GetType("UnityEditor.GameViewSize");var count=(int)type.GetMethod("GetTotalCount",flags).Invoke(group,null);int chosen=-1;
            for(var i=0;i<count;i++)
            {
                var size=type.GetMethod("GetGameViewSize",flags).Invoke(group,new object[]{i});
                if((int)sizeType.GetProperty("width",flags).GetValue(size)==1440&&(int)sizeType.GetProperty("height",flags).GetValue(size)==810){chosen=i;break;}
            }
            if(chosen<0)
            {
                var sizeEnum=asm.GetType("UnityEditor.GameViewSizeType");
                var size=Activator.CreateInstance(sizeType,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,new object[]{Enum.Parse(sizeEnum,"FixedResolution"),1440,810,"Camp Review 1440x810"},null);
                type.GetMethod("AddCustomSize",flags).Invoke(group,new[]{size});chosen=count;
            }
            var viewType=asm.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex",flags).SetValue(view,chosen);
            view.Focus();view.Repaint();
        }
    }
}
