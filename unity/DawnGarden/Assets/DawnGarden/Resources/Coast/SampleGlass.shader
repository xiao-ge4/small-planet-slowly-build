Shader "Dawn Garden/Coast Greenhouse Glass"
{
    Properties { _Color("Glass tint", Color) = (.38,.70,.64,.40) _SkyMap("Morning sky reflection",2D)="black"{} _Daylight("Daylight",Range(0,1))=1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_SkyMap);SAMPLER(sampler_SkyMap);
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half _Daylight;
            CBUFFER_END
            struct Input { float4 positionOS: POSITION; float3 normalOS: NORMAL; };
            struct Output { float4 positionCS: SV_POSITION; float3 positionWS: TEXCOORD0; half3 normalWS: TEXCOORD1; };
            Output Vert(Input input)
            {
                Output output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 Frag(Output input): SV_Target
            {
                float3 eyeDir=GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 surface=normalize(input.normalWS);
                half face=abs(dot(surface,eyeDir));
                half edge=pow(1-face,3);
                half3 normal=dot(surface,eyeDir)<0 ? -surface : surface;
                half3 reflected=reflect(-eyeDir,normal);
                half horizon=smoothstep(-.24h,.83h,reflected.y);
                half grain=sin(input.positionWS.x*9+input.positionWS.z*3) *
                    sin(input.positionWS.y*11+input.positionWS.z*4);
                half3 green=_Color.rgb*lerp(.79h,1.16h,horizon);
                half shine=pow(saturate(dot(reflected,normalize(half3(-.51h,.69h,.51h)))),17);
                half3 glass=lerp(green,half3(.48h,.79h,.83h),edge*.46h+horizon*.28h);
                glass+= (shine*.22h+grain*.011h) * half3(.82h,.98h,1.0h);
                float2 skyUV=float2(atan2(reflected.x,reflected.z)/6.2831853*5+.80,
                    saturate(.5+atan(reflected.y*14)/3.14159265));
                half3 sky=SAMPLE_TEXTURE2D(_SkyMap,sampler_SkyMap,skyUV).rgb;
                half roof=smoothstep(.12h,.78h,abs(surface.y));
                // Keep a coherent green roof between the copper ribs. A heavy
                // blue sky reflection previously made it vanish against the sea.
                half reflectionWeight=lerp(.10h,.18h,roof)+edge*.06h;
                glass=lerp(glass,sky,reflectionWeight);
                glass*=lerp(half3(.16h,.24h,.40h),half3(1,1,1),_Daylight);
                half nearFade=smoothstep(.11h,.68h,distance(input.positionWS,GetCameraPositionWS()));
                return half4(glass,saturate(_Color.a*(lerp(.68h,1.0h,roof)+edge*.12h)*nearFade));
            }
            ENDHLSL
        }
    }
}
