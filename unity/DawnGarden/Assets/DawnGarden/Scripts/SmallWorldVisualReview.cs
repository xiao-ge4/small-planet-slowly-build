using System;
using System.Collections;
using System.Linq;
using UnityEngine;
namespace DawnGarden
{
    public sealed partial class SmallWorldApp
    {
        bool visualReviewBusy,visualReviewPassed,visualReviewChanged;
        bool visualOverrideAccepted,visualReviewFinished;
        string visualReviewMessage="",reviewedDraftId="";
        int reviewedHour=-1;
        [Serializable] class VisualReviewReply
        {
            public string runId,binding,state,action,summary,surfaceStyle;public string[] issues;
            public bool hasProposal;public int maxCorrections=2;public SmallWorldItem[] proposal;
        }
        PreviewRequest PreviewBody(byte[] picture)=>new PreviewRequest {clientId=Profile.clientId,runId=Profile.creationRunId??"",buildId=library.buildId,expectedVersion=committedLayout.version,instances=editItems.ToArray(),surfaceStyle=editSurfaceStyle,seamSoftened=editSeamSoftened,checks=PlacementChecks(),image=Convert.ToBase64String(picture),lightingHour=Mathf.FloorToInt(Game.atmosphere?.CurrentHour??12)};
        byte[] CaptureVisualHome(float distance=14.2f,Vector3? localFocus=null)
        {
            var cam=Game.worldCamera;var p=cam.transform.position;var r=cam.transform.rotation;
            try{var focus=Game.expansion.land.position+(localFocus??new Vector3(.3f,.35f,.3f));cam.transform.position=focus+Quaternion.Euler(43,-30,0)*Vector3.back*distance;cam.transform.LookAt(focus);return CaptureHome();}
            finally{cam.transform.SetPositionAndRotation(p,r);}
        }
        IEnumerator OpenSavedDraft()
        {
            float until=Time.realtimeSinceStartup+75;
            while((Game==null||loading||editorBusy||restoringOwnedLayout)&&Time.realtimeSinceStartup<until)yield return null;
            if(Game!=null&&System.IO.File.Exists(Profile.HomeSavePath("layout-draft")))OpenPlacement();
        }
        IEnumerator ReviewPlacement()
        {
            if(editorBusy||visualReviewBusy||!previewValid)yield break;
            visualReviewBusy=true;visualReviewFinished=false;visualReviewPassed=false;visualReviewChanged=false;visualReviewMessage="正在对照实际画面，检查地表、植物层次和景观组合…";
            yield return SyncState();int corrections=0;
            while(true)
            {
                SaveDraft();var before=DraftContextId();VisualReviewReply result=null;string error=null;
                yield return SmallWorldApi.Request("review-runtime",PreviewBody(CaptureVisualHome()),j=>result=JsonUtility.FromJson<VisualReviewReply>(j),e=>error=e);
                float until=Time.realtimeSinceStartup+95;
                while(result!=null&&result.state=="running"&&Time.realtimeSinceStartup<until)
                {yield return new WaitForSecondsRealtime(.8f);yield return SmallWorldApi.Request("visual-review/"+Profile.clientId+"/"+result.runId+"/"+result.binding,null,j=>result=JsonUtility.FromJson<VisualReviewReply>(j),e=>error=e);}
                if(error!=null||result==null||result.state!="complete"){visualReviewMessage=error??result?.summary??"画面检查尚未完成，草稿已保留。";break;}
                if(before!=DraftContextId()){visualReviewMessage="草稿已经变化，请按当前画面重新检查。";break;}
                visualReviewMessage=result.summary+(result.issues?.Length>0?" · "+string.Join("；",result.issues.Take(2)):"");
                if(result.action=="accept"){visualReviewPassed=true;reviewedDraftId=before;reviewedHour=Mathf.FloorToInt(Game.atmosphere?.CurrentHour??12);break;}
                int maxCorrections=Mathf.Clamp(result.maxCorrections,0,4);
                if(!result.hasProposal||corrections>=maxCorrections){visualReviewMessage+=" 你可以继续手动调整，或告诉小禾修改方向。";break;}
                var old=DraftJson();RememberUndo();editItems=result.proposal.ToList();editSurfaceStyle=result.surfaceStyle;yield return PreviewPlacement(false);
                if(!previewValid){ReadDraft(old);yield return PreviewPlacement(false);visualReviewMessage="建议未通过通路检查，已保留原草稿。";break;}
                visualReviewChanged=true;corrections++;visualReviewMessage="正在检查调整后的场景（"+corrections+" / "+maxCorrections+"）…";yield return new WaitForEndOfFrame();
            }
            visualReviewBusy=false;visualReviewFinished=true;Toast(visualReviewChanged?"画面调整已放入可撤销草稿，请查看后再决定是否应用。":visualReviewMessage);
        }
    }
}
