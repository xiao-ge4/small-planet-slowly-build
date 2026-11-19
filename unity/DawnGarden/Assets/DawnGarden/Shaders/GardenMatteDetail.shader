Shader "Dawn Garden/Matte Detail"
{
    Properties { _BaseColor("Color",Color)=(.5,.6,.25,1) _BaseMap("Base",2D)="white"{} }
    SubShader {
        Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
        Cull Off
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
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
            struct V {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half fog:TEXCOORD2;};
            V vert(A i){V o;VertexPositionInputs p=GetVertexPositionInputs(i.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.fog=ComputeFogFactor(p.positionCS.z);return o;}
            half4 frag(V i):SV_Target {half3 n=normalize(i.normalWS);Light light=GetMainLight(TransformWorldToShadowCoord(i.positionWS));half diffuse=.4+.6*abs(dot(n,light.direction));half3 color=_BaseColor.rgb*(SampleSH(n)*.7+light.color*diffuse*lerp(.6,1,light.shadowAttenuation));return half4(MixFog(color,i.fog),1);}
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
