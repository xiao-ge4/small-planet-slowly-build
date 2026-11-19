using System;
using System.Collections.Generic;
using UnityEngine;

namespace DawnGarden
{
    /// <summary>Shared illustrated UI marks, rendered as textures so scaled IMGUI never loses thin line geometry.</summary>
    public static class SmallWorldUiArt
    {
        static readonly Dictionary<string, Texture2D> textures = new();
        public static readonly Color Ink = new(.16f,.28f,.23f);
        public static readonly Color Paper = new(.98f,.96f,.88f,.98f);
        public static Texture2D Plate(Color fill, Color edge)
        {
            string key="plate"+fill+edge;if(textures.TryGetValue(key,out var cached))return cached;
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp};
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                float dx=Mathf.Max(0,Mathf.Abs(x-31.5f)-16),dy=Mathf.Max(0,Mathf.Abs(y-31.5f)-16);
                float distance=Mathf.Sqrt(dx*dx+dy*dy),alpha=Mathf.Clamp01(15.5f-distance);
                var color=Color.Lerp(fill,edge,Mathf.Clamp01(distance-13.2f));color.a*=alpha;
                texture.SetPixel(x,y,color);
            }
            texture.Apply();textures[key]=texture;return texture;
        }
        static float Segment(Vector2 p,Vector2 a,Vector2 b){var d=b-a;return Vector2.Distance(p,a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude));}
        public static Texture2D Icon(string kind)
        {
            if(textures.TryGetValue(kind,out var cached))return cached;
            const int n=128;var texture=new Texture2D(n,n,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp};
            Color color=kind=="fire"?new(.83f,.38f,.17f):kind=="star"?new(.36f,.65f,.77f):kind=="wind"?new(.40f,.64f,.45f):new(.44f,.58f,.35f);
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                Vector2 p=new((x-63.5f)/55,(y-63.5f)/55);float a=0,detail=0;
                if(kind=="star") {float theta=Mathf.Atan2(p.y,p.x);float radius=.46f+.28f*Mathf.Pow(Mathf.Abs(Mathf.Cos(2*theta)),9);a=Mathf.Clamp01((radius-p.magnitude)*55);detail=Mathf.Clamp01((.045f-Mathf.Abs(p.x+p.y*.2f))*40)*a;}
                else if(kind=="fire") {float width=.51f*(1-Mathf.Clamp01((p.y+.65f)/1.5f));float skew=.14f*Mathf.Sin(p.y*3);a=Mathf.Clamp01((width-Mathf.Abs(p.x-skew))*50)*Mathf.Clamp01((p.y+.7f)*30)*Mathf.Clamp01((.88f-p.y)*30);detail=a*Mathf.Clamp01((.2f-Mathf.Abs(p.x))*15)*Mathf.Clamp01((.2f-p.y)*6);}
                else if(kind=="wind") {float d=10;for(int row=-1;row<=1;row++){float yy=row*.34f+.14f*Mathf.Sin((p.x+.7f)*3);if(p.x>-.74f&&p.x<.65f)d=Mathf.Min(d,Mathf.Abs(p.y-yy));}a=Mathf.Clamp01((.065f-d)*45);}
                else if(kind=="shield") {float width=.61f-Mathf.Max(0,-p.y)*.5f;a=Mathf.Clamp01((width-Mathf.Abs(p.x))*45)*Mathf.Clamp01((.64f-p.y)*45)*Mathf.Clamp01((p.y+.8f)*45);detail=a*Mathf.Clamp01((.04f-Mathf.Abs(p.x))*30);}
                else if(kind=="permit") {a=Mathf.Clamp01((.65f-Mathf.Abs(p.x))*40)*Mathf.Clamp01((.80f-Mathf.Abs(p.y))*40);detail=(Segment(p,new(-.38f,.35f),new(.38f,.35f))<.035f||Segment(p,new(-.38f,.05f),new(.2f,.05f))<.035f||Vector2.Distance(p,new(.22f,-.35f))<.18f)?1:0;color=new(.85f,.68f,.36f);}
                else if(kind=="lamp") {a=(Mathf.Abs(p.x)<.32f&&Mathf.Abs(p.y)<.45f)||(Mathf.Abs(p.x)<.48f&&Mathf.Abs(p.y-.52f)<.08f)||(Mathf.Abs(p.x)<.42f&&Mathf.Abs(p.y+.55f)<.08f)?1:0;detail=a*(Mathf.Abs(p.x)<.19f&&p.y>-.25f&&p.y<.34f?1:0);color=new(.75f,.51f,.22f);}
                else {float u=(p.x+p.y)*.7071f,v=(p.y-p.x)*.7071f;a=Mathf.Clamp01((1-u*u/.78f-v*v/.24f)*35);detail=Mathf.Clamp01((.025f-Mathf.Abs(v))*30)*a;}
                var c=Color.Lerp(color,new Color(1,.94f,.72f),detail*.8f);c.a=a;texture.SetPixel(x,y,c);
            }
            texture.Apply();textures[kind]=texture;return texture;
        }
        public static Texture2D PortraitTexture(string id)
        {
            string key="portrait:"+id;if(textures.TryGetValue(key,out var cached))return cached;
            var loaded=Resources.Load<Texture2D>("CharacterPortraits/"+id);
            if(loaded!=null)textures[key]=loaded;
            return loaded??Icon(id=="ajin"?"fire":id=="lumi"?"star":id=="lanlan"?"wind":id=="guardian"?"shield":"leaf");
        }
        public static void Portrait(Rect rect,string id)
        {
            var plate=new GUIStyle(GUIStyle.none){border=new RectOffset(16,16,16,16)};
            plate.normal.background=Plate(new Color(.89f,.91f,.79f,1),new Color(.55f,.65f,.46f,.8f));
            GUI.Box(rect,GUIContent.none,plate);GUI.DrawTexture(new Rect(rect.x+4,rect.y+4,rect.width-8,rect.height-8),PortraitTexture(id),ScaleMode.ScaleToFit);
        }
    }
    public sealed partial class SmallWorldApp
    {
        bool gameSettingsOpen;
        GUIStyle secondaryButton,chipStyle;
        GUISkin storySkin;
        void PrepareIllustratedSkin(Font font)
        {
            panel.normal.background=SmallWorldUiArt.Plate(SmallWorldUiArt.Paper,new Color(.53f,.62f,.44f,.9f));panel.border=new RectOffset(16,16,16,16);
            panel.padding=new RectOffset(20,20,18,18);
            foreach(var s in new[]{button,coverPill,inputStyle})s.border=new RectOffset(16,16,16,16);
            button.normal.background=SmallWorldUiArt.Plate(new Color(.25f,.40f,.30f),new Color(.43f,.55f,.39f));
            button.hover.background=button.active.background=button.focused.background=SmallWorldUiArt.Plate(new Color(.35f,.51f,.36f),new Color(.71f,.76f,.49f));
            button.fontSize=16;button.fixedHeight=0;button.padding=new RectOffset(12,12,6,6);
            secondaryButton=new GUIStyle(button);secondaryButton.normal.background=SmallWorldUiArt.Plate(new Color(.88f,.88f,.77f),new Color(.68f,.73f,.57f));secondaryButton.normal.textColor=SmallWorldUiArt.Ink;
            chipStyle=new GUIStyle(muted){alignment=TextAnchor.MiddleCenter,normal={background=SmallWorldUiArt.Plate(new Color(.88f,.90f,.79f),Color.clear)},border=new RectOffset(16,16,16,16)};
            inputStyle.normal.background=inputStyle.focused.background=SmallWorldUiArt.Plate(new Color(1,.99f,.95f),new Color(.64f,.73f,.55f));
            toggleStyle.normal.background=SmallWorldUiArt.Plate(new Color(.90f,.92f,.83f),new Color(.48f,.60f,.42f));toggleStyle.onNormal.background=SmallWorldUiArt.Icon("leaf");
            toggleStyle.hover.background=toggleStyle.active.background=toggleStyle.normal.background;toggleStyle.onHover.background=toggleStyle.onActive.background=toggleStyle.onNormal.background;
            toggleStyle.border=new RectOffset(16,16,16,16);toggleStyle.fixedHeight=28;toggleStyle.imagePosition=ImagePosition.ImageLeft;
            storySkin=Instantiate(GUI.skin);storySkin.font=font;
            storySkin.verticalScrollbar=new GUIStyle(GUI.skin.verticalScrollbar){fixedWidth=9,normal={background=SmallWorldUiArt.Plate(new Color(.87f,.89f,.79f),Color.clear)},border=new RectOffset(16,16,16,16)};
            storySkin.verticalScrollbarThumb=new GUIStyle(GUI.skin.verticalScrollbarThumb){normal={background=SmallWorldUiArt.Plate(new Color(.51f,.65f,.45f),Color.clear)},border=new RectOffset(16,16,16,16)};
            foreach(var s in new[]{storySkin.verticalScrollbarUpButton,storySkin.verticalScrollbarDownButton,storySkin.horizontalScrollbarLeftButton,storySkin.horizontalScrollbarRightButton}){s.fixedHeight=0;s.fixedWidth=0;s.normal.background=null;}
            storySkin.horizontalSlider=new GUIStyle(GUI.skin.horizontalSlider){normal={background=SmallWorldUiArt.Plate(new Color(.77f,.82f,.67f),Color.clear)},border=new RectOffset(16,16,16,16),fixedHeight=12};
            storySkin.horizontalSliderThumb=new GUIStyle(GUI.skin.horizontalSliderThumb){normal={background=SmallWorldUiArt.Plate(new Color(.36f,.52f,.35f),new Color(.89f,.85f,.59f))},border=new RectOffset(16,16,16,16),fixedHeight=20,fixedWidth=20};
        }
        bool IllustratedToggle(bool value,string text)
        {
            var style=new GUIStyle(label){fontSize=16,padding=new RectOffset(31,0,5,5)};
            var rect=GUILayoutUtility.GetRect(new GUIContent(text),style,GUILayout.MinHeight(30),GUILayout.ExpandWidth(true));
            return IllustratedToggle(rect,value,text);
        }
        bool IllustratedToggle(Rect rect,bool value,string text)
        {
            bool result=GUI.Toggle(rect,value,GUIContent.none,GUIStyle.none);
            var box=new Rect(rect.x,rect.y+4,21,21);
            GUI.DrawTexture(box,SmallWorldUiArt.Plate(new Color(.90f,.92f,.83f),new Color(.47f,.60f,.42f)),ScaleMode.StretchToFill);
            if(result)GUI.DrawTexture(new Rect(box.x+2,box.y+2,17,17),SmallWorldUiArt.Icon("leaf"));
            GUI.Label(new Rect(rect.x+31,rect.y+3,rect.width-32,rect.height),text,new GUIStyle(label){fontSize=16});
            return result;
        }
        void DrawGameSettings()
        {
            GUI.Box(new Rect(916,102,342,400),GUIContent.none,panel);
            GUI.Label(new Rect(939,119,220,32),"小世界设置",title);
            if(GUI.Button(new Rect(1190,120,43,30),"×",secondaryButton)){gameSettingsOpen=false;UpdateControls();}
            GUILayout.BeginArea(new Rect(940,165,288,320));var a=Game.atmosphere;
            GUILayout.Label("音乐 · "+Mathf.RoundToInt(a.MusicVolume*100)+"%",label);var volume=GUILayout.HorizontalSlider(a.MusicVolume,0,1);if(Mathf.Abs(volume-a.MusicVolume)>.001f)a.SetVolume(volume);
            if(GUILayout.Button(a.Muted?"开启音乐":"静音",secondaryButton))a.SetMuted(!a.Muted);
            GUILayout.Space(12);GUILayout.Label("光影 · "+a.ClockLabel,label);var hour=GUILayout.HorizontalSlider(a.CurrentHour,0,23.999f);if(Mathf.Abs(hour-a.CurrentHour)>.02f)a.Preview(hour);
            GUILayout.BeginHorizontal();if(GUILayout.Button("晨光",secondaryButton))a.Preview(7.5f);if(GUILayout.Button("月夜",secondaryButton))a.Preview(22);GUILayout.EndHorizontal();
            if(GUILayout.Button("跟随现实时间",button))a.UseRealTime();
            if(GUILayout.Button("保存并返回标题",secondaryButton))ReturnSubmissionTitle();GUILayout.EndArea();
        }
    }
}
