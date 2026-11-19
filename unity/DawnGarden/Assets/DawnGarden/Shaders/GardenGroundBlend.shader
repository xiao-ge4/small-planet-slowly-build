Shader "Dawn Garden/Soft Ground Detail"
{
    Properties { _BaseColor("Earth tint",Color)=(.50,.35,.17,.55) }
    SubShader {
        Tags {"RenderType"="Transparent" "Queue"="Transparent-20" "RenderPipeline"="UniversalPipeline"}
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Offset -1,-1
        Pass {
            Tags {"LightMode"="UniversalForwardOnly"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;half4 color:COLOR;};
            struct V{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half fog:TEXCOORD2;half alpha:TEXCOORD3;};
            V vert(A i){V o;VertexPositionInputs p=GetVertexPositionInputs(i.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.fog=ComputeFogFactor(p.positionCS.z);o.alpha=i.color.a;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);}
            half4 frag(V i):SV_Target {
                float grain=lerp(.5,noise(i.positionWS.xz*32),saturate(1-length(fwidth(i.positionWS.xz))*32));
                float soft=noise(i.positionWS.xz*8);
                half alpha=saturate(i.alpha*1.35-(1-soft)*.32)*_BaseColor.a;
                half3 n=normalize(i.normalWS);Light light=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 illumination=SampleSH(n)*.7+light.color*(.35+.65*saturate(dot(n,light.direction)))*lerp(.6,1,light.shadowAttenuation);
                half3 color=_BaseColor.rgb*lerp(.9,1.1,grain)*illumination;
                return half4(MixFog(color,i.fog),alpha);
            }
            ENDHLSL
        }
    }
}
