Shader "Dawn Garden/Hot Spring Steam"
{
    Properties
    {
        _BaseMap("Soft steam", 2D) = "white" {}
        _Daylight("Daylight", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float _Daylight;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            Varyings vert(Attributes i)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz);
                o.uv=i.uv; o.color=i.color; return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                half alpha=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a*i.color.a;
                half3 tint=lerp(half3(.44,.55,.69),half3(.96,.96,.91),_Daylight);
                return half4(tint*i.color.rgb,alpha);
            }
            ENDHLSL
        }
    }
}
