using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace DawnGarden
{
    public static class SmallWorldApi
    {
        static string recordingBase;
        public static string Base
        {
            get
            {
                if(recordingBase!=null)return recordingBase;
#if UNITY_WEBGL && !UNITY_EDITOR
                if(Uri.TryCreate(Application.absoluteURL,UriKind.Absolute,out var page)
                    && (page.Scheme==Uri.UriSchemeHttp||page.Scheme==Uri.UriSchemeHttps))
                    return page.GetLeftPart(UriPartial.Authority);
#endif
                return "http://127.0.0.1:8780";
            }
        }
        public static void UseRecordingService(string address)
        {var uri=new Uri(address);if(uri.Scheme!="http"||uri.Host!="127.0.0.1"||uri.Port==8780)throw new ArgumentException("Recording requires a separate loopback service");recordingBase=address.TrimEnd('/');}
        [Serializable] class ErrorBody { public ErrorInfo error; }
        [Serializable] class ErrorInfo { public string message; }
        public static IEnumerator Request(string path,object body,Action<string> success,Action<string> failure=null)
        {
            using(var req=new UnityWebRequest(Base+"/api/game-v1/"+path,body==null?"GET":"POST"))
            {
                if(body!=null){req.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));req.SetRequestHeader("Content-Type","application/json");}
                req.downloadHandler=new DownloadHandlerBuffer();req.timeout=25;yield return req.SendWebRequest();
                if(req.result==UnityWebRequest.Result.Success)success?.Invoke(req.downloadHandler.text);
                else
                {
                    string message="向导服务暂时未连接，游戏进度仍会保存在本机。";
                    try{var error=JsonUtility.FromJson<ErrorBody>(req.downloadHandler.text);if(!string.IsNullOrEmpty(error?.error?.message))message=error.error.message;}catch(Exception){}
                    failure?.Invoke(message);
                }
            }
        }
        public static IEnumerator Creation(string path,object body,Action<string> success,Action<string> failure=null)
        {
            using(var req=new UnityWebRequest(Base+"/api/home-creation/"+path,body==null?"GET":"POST"))
            {
                if(body!=null){req.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));req.SetRequestHeader("Content-Type","application/json");}
                req.downloadHandler=new DownloadHandlerBuffer();req.timeout=25;yield return req.SendWebRequest();
                if(req.result==UnityWebRequest.Result.Success)success?.Invoke(req.downloadHandler.text);else failure?.Invoke("创作请求未完成。请查看已有任务后重试，勿重复提交付费生成。");
            }
        }
    }
}
