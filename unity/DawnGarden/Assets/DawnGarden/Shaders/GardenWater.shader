Shader "Dawn Garden/Water"
{
    Properties
    {
        _BaseColor("Deep water", Color) = (.17,.47,.55,1)
        _ShallowColor("Light water", Color) = (.36,.73,.74,1)
        _WaveScale("Ripple scale", Float) = 2
        _Daylight("Daylight", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShallowColor;
                float _WaveScale;
                float _Daylight;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.positionWS.xz * _WaveScale;
                float t = _Time.y * .18;
                float wave = sin(p.x * 1.2 + sin(p.y * 1.7 + t) * .7 + t);
                float crossWave = sin(p.y * 1.6 + sin(p.x + t) * .5 - t);
                float blend = .42 + wave * crossWave * .07;
                half3 color = lerp(_BaseColor.rgb, _ShallowColor.rgb, blend);
                float sparkle = pow(saturate(wave * crossWave), 18) * .035;
                color *= lerp(half3(.16,.22,.40), half3(1,1,1), _Daylight);
                return half4(color + sparkle * lerp(.15,1,_Daylight), 1);
            }
            ENDHLSL
        }
    }
}
