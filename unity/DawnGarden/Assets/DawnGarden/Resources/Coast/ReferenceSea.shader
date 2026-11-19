Shader "Dawn Garden/Reference Sea"
{
 Properties { _BaseColor("Offshore blue",Color)=(.035,.30,.47,1) _ShallowColor("Lagoon",Color)=(.10,.64,.59,1) _SkyMap("Cloud reflection",2D)="black"{} _Daylight("Daylight",Range(0,1))=1 _DebugDepth("Water depth diagnostic",Float)=0 _Bathymetry("Actual seabed world height",2D)="black"{} _BathymetryRect("Seabed XZ origin and extent",Vector)=(0,0,1,1) _UseBathymetry("Use surveyed seabed",Float)=0 }
 SubShader {
 Tags {"RenderType"="Transparent" "Queue"="Transparent-50" "RenderPipeline"="UniversalPipeline"}
 Pass {
 Tags {"LightMode"="UniversalForwardOnly"}
 ZWrite On Cull Back
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_fog
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
 // This surface samples the atlas in world space even when opaque objects use
 // a screen-space shadow pass. The water is drawn after that opaque pass.
 #define _SURFACE_TYPE_TRANSPARENT 1
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 TEXTURE2D(_SkyMap);SAMPLER(sampler_SkyMap);
 TEXTURE2D(_Bathymetry);SAMPLER(sampler_Bathymetry);
 CBUFFER_START(UnityPerMaterial)
 half4 _BaseColor; half4 _ShallowColor; float _Daylight; float _DebugDepth;
 float4 _BathymetryRect;float4 _Bathymetry_TexelSize;float _UseBathymetry;
 CBUFFER_END
 struct A{float4 positionOS:POSITION;};
 struct V{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float fog:TEXCOORD1;};
 V vert(A a){V o;o.world=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.fog=ComputeFogFactor(o.positionCS.z);return o;}
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 a=floor(p),t=frac(p);t=t*t*(3-2*t);return lerp(lerp(hash(a),hash(a+float2(1,0)),t.x),lerp(hash(a+float2(0,1)),hash(a+1),t.x),t.y);}
 float shortCrest(float2 world,float time)
 {
     // A short, slightly curved breaker fits wholly inside each staggered cell.
     // Its ends fade before the cell border; no long parallel stripe is drawn.
     float2 q=mul(float2x2(.94,.342,-.342,.94),world)/float2(1.12,.70);
     float2 cell=floor(q),local=frac(q)-.5;
     float seed=hash(cell+float2(71,19));
     local-=float2(hash(cell+4.1)-.5,hash(cell+9.3)-.5)*.20;
     float angle=(seed-.5)*1.1;
     local=mul(float2x2(cos(angle),-sin(angle),sin(angle),cos(angle)),local);
     float halfLength=.24+.10*hash(cell+27.1);
     float arc=.42*local.x*local.x+.055*sin(local.x*8+seed*6.28);
     float distance=abs(local.y-arc-.075*sin(time*.73+seed*6.28));
     float thickness=.012+.012*hash(cell+31.7);
     float crestMask=1-smoothstep(thickness,thickness+max(.009,fwidth(distance)),distance);
     float ends=1-smoothstep(halfLength*.58,halfLength,abs(local.x));
     return crestMask*ends*smoothstep(.44,.66,hash(cell+17.3));
 }
 half4 frag(V i):SV_Target
 {
 float2 uv=GetNormalizedScreenSpaceUV(i.positionCS);float raw=SampleSceneDepth(uv);
 float3 bottom=ComputeWorldSpacePosition(uv,raw,UNITY_MATRIX_I_VP);
 #if UNITY_REVERSED_Z
 bool hasBottom=raw>0.00001;
 #else
 bool hasBottom=raw<0.99999;
 #endif
 float screenDepth=hasBottom?clamp(i.world.y-bottom.y,0,25):25;
 float depth=screenDepth;
 if(_UseBathymetry>.5)
 {
     // A grazing camera ray hits a distant bank, not the bed immediately below
     // this fragment. Its projected shallow colour used to form a large cyan
     // slab in front of the island. Colour now follows the actual world-XZ bed.
     float2 surveyUV=(i.world.xz-_BathymetryRect.xy)/max(_BathymetryRect.zw,.001);
     float2 edgeMetres=min(surveyUV,1-surveyUV)*_BathymetryRect.zw;
     // Blend the survey's shallow plateau into deeper water over a wide,
     // irregular shore band instead of exposing its rectangular atlas edge.
     float edgeDistance=min(edgeMetres.x,edgeMetres.y);
     float transition=12+4*noise(i.world.xz*.085);
     float coverage=smoothstep(0,transition,edgeDistance);
     float2 texelUV=saturate(surveyUV)*(1-_Bathymetry_TexelSize.xy)+_Bathymetry_TexelSize.xy*.5;
     float bedHeight=SAMPLE_TEXTURE2D(_Bathymetry,sampler_Bathymetry,texelUV).r;
     float surveyedDepth=clamp(i.world.y-bedHeight,0,25);
     // Outside the local seabed, a ray can hit a distant bank. It must not
     // paint that bank's shallow colour into an unrelated foreground strip.
     depth=lerp(max(surveyedDepth,11),surveyedDepth,coverage);
 }
 if(_DebugDepth>.5&&_DebugDepth<1.5)return hasBottom?half4(saturate(screenDepth/4).xxx,1):half4(1,0,1,1);
 if(_DebugDepth>1.5&&_DebugDepth<2.5)return half4(SampleSceneColor(uv),1);
 if(_DebugDepth>3.5&&_DebugDepth<4.5)return half4(saturate(depth/4).xxx,1);
 float2 p=i.world.xz;float time=_Time.y*.12;
 float ocean=step(.5,_UseBathymetry);
 float footprint=length(fwidth(p));
 // Two-dimensional phase changes break the horizon-wide sine bands. Normals
 // below come from this same small height field, including its varying phase.
 float warp=noise(p*.17+float2(time*.07,-time*.04));
 float swell=sin(dot(p,float2(.64,.37))+time+warp*3.2)*(1-smoothstep(1.2,4.5,footprint));
 float ripple=sin(dot(p,float2(-1.38,2.04))-time*1.7+noise(p*.39+7.3)*2.1)*(1-smoothstep(.25,1.2,footprint));
 float detail=noise(p*.73+float2(time*.12,time*.04));
 float broadPatch=noise(p*.115+noise(p*.041)*2.6);
 float shallow=exp(-depth*.24);
 // Derive the middle water from the chosen material colours. The previous
 // hard-coded bright cyan middle overrode both colour-study swatches.
 half3 middle=lerp(_ShallowColor.rgb,_BaseColor.rgb,.40);
 half3 tint=depth<2.6?lerp(_ShallowColor.rgb,middle,smoothstep(.18,2.6,depth)):
     lerp(middle,_BaseColor.rgb,smoothstep(2.6,9,depth));
 if(ocean<.5)
 {
     middle=lerp(_ShallowColor.rgb,half3(.07,.46,.57),smoothstep(.25,2.4,depth));
     tint=lerp(middle,_BaseColor.rgb,smoothstep(2,8,depth));
 }
 tint*=.80+.23*broadPatch+.045*detail+.012*swell;
 float2 refractedUV=uv+float2(swell,ripple)*.00065*saturate(depth);
 half3 bedColor=SampleSceneColor(refractedUV);
 // Keep the actual scene sample for refraction, but a far bank cannot make a
 // genuinely deep foreground column transparent. Pond materials keep their
 // screen-depth behaviour because they do not enable the sea's height survey.
 float absorptionDepth=max(depth,screenDepth);
 float transmission=hasBottom&&_DebugDepth<2.5 ? .66*exp(-absorptionDepth*.64) : 0;
 tint=lerp(tint,bedColor*half3(.32,.84,.73),transmission);
 float resolved=1-smoothstep(.20,1.05,length(fwidth(p)));
 float breakers=shortCrest(p+float2(time*.22,-time*.10),time);
 breakers+=shortCrest(p.yx*float2(-.71,.71)+19.7,time*.81)*.52;
 float crestGroups=smoothstep(.29,.61,noise(p*.14+13.4));
 float whitecaps=saturate(breakers)*crestGroups*resolved*ocean*(.012+.024*shallow);
 // The beach foam touches real depth intersections, with broken gaps and
 // short advancing tips instead of a continuous white outline of the island.
 // Ocean foam must use the surveyed seabed. A near-zero screen-depth sample
 // (for example a depth-primed water surface) is not an island-wide shoreline.
 float contactDepth=lerp(screenDepth,depth,ocean);
 // Reject a zero-depth self intersection outright. Foam requires actual
 // shallow water; a failed depth sample must stay calm, never turn into lace.
 float edge=smoothstep(.025,.09,contactDepth)*(1-smoothstep(.15,.45,contactDepth));
 float froth=edge*shortCrest(p*.65+float2(time*.11,0),time)*.22;
 // Short broken shoreward fronts occupy the real shallow shelf. Their
 // breadth follows depth; deep ocean never receives a repeated foam grid.
 float shelf=smoothstep(.06,.18,depth)*(1-smoothstep(.38,.95,depth))*ocean;
 float front=1-smoothstep(.05,.23,abs(sin(depth*6.4-time*.72+noise(p*.46)*2.8)));
 froth+=shelf*front*shortCrest(p*.8+9.1,time)*.08;
 float3 view=normalize(_WorldSpaceCameraPos-i.world);
 float3 rippledWorld=i.world+float3(0,(.072*swell+.023*ripple)*lerp(.40,1,ocean),0);
 float3 normal=normalize(cross(ddy(rippledWorld),ddx(rippledWorld)));
 if(normal.y<0)normal=-normal;
 float3 reflected=reflect(-view,normal);
 float2 skyUV=float2(atan2(reflected.x,reflected.z)/6.2831853*5+.80,saturate(.5+atan(reflected.y*14)/3.14159265));
 half3 skyColor=SAMPLE_TEXTURE2D(_SkyMap,sampler_SkyMap,skyUV).rgb;
 float fresnel=pow(1-saturate(dot(normal,view)),3);
 tint=lerp(tint,skyColor,(.065+.15*fresnel)*smoothstep(-.03,.12,reflected.y));
 Light sun=GetMainLight(TransformWorldToShadowCoord(i.world),i.world,half4(1,1,1,1));
 // Retain sky fill in shadow, but let bridges, piers and the true coast cast
 // onto the water. Reflection and foam share that light, never an emission.
 float direct=lerp(.72,1,sun.shadowAttenuation);
 if(_DebugDepth>4.5&&_DebugDepth<5.5)return half4(sun.shadowAttenuation.xxx,1);
 tint*=direct;
 half3 foamColor=half3(.55,.72,.69)*direct;
 tint=lerp(tint,foamColor,min(.055,whitecaps+froth*.16));
 float halfDot=saturate(dot(normal,normalize(view+sun.direction)));
 float spec=pow(halfDot,48)*.14+pow(halfDot,10)*.018;
 tint+=sun.color*spec*_Daylight*sun.shadowAttenuation;
 tint=lerp(tint,half3(.36,.65,.75),pow(1-saturate(dot(normal,view)),4)*.08);
 tint*=lerp(half3(.14,.21,.39),half3(1,1,1),_Daylight);
 return half4(MixFog(tint,i.fog),1);
 }
 ENDHLSL
 }
 }
}
