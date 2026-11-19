Shader "Dawn Garden/Layered Sand"
{
    Properties
    {
        _BaseMap("Fine sand color",2D)="white"{}
        _ReliefMap("Sand grain normals",2D)="bump"{}
        _Tint("Sand tint",Color)=(1,1,1,1)
        _Scale("Texture meters",Float)=.48
        _Relief("Grain depth",Range(0,1))=.45
    }
    SubShader
    {
        Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
        Pass
        {
            Tags {"LightMode"="UniversalForwardOnly"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            TEXTURE2D(_ReliefMap);SAMPLER(sampler_ReliefMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;float _Scale;half _Relief;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;half4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half4 color:COLOR;half fog:TEXCOORD2;};
            Varyings vert(Attributes i)
            {
                Varyings o;VertexPositionInputs p=GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(i.normalOS);
                o.color=i.color;o.fog=ComputeFogFactor(p.positionCS.z);return o;
            }
            float noise(float2 p)
            {
                float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);
                float4 h=frac(sin(float4(dot(a,float2(127.1,311.7)),dot(a+float2(1,0),float2(127.1,311.7)),dot(a+float2(0,1),float2(127.1,311.7)),dot(a+1,float2(127.1,311.7))))*43758.5453);
                return lerp(lerp(h.x,h.y,b.x),lerp(h.z,h.w,b.x),b.y);
            }
            half4 frag(Varyings i):SV_Target
            {
                float2 p=i.positionWS.xz;float2 uv=p*_Scale;
                half3 sand=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).rgb;
                half3 second=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv*.71+float2(.37,.82)).rgb;
                sand=lerp(sand,second,.12);
                float m=noise(p*.65)*.65+noise(p*2.3)*.35;
                sand*=lerp(half3(.86,.75,.65),half3(1.06,1.03,.94),m);
                sand=lerp(sand,sand*half3(.91,.82,.71),i.color.r*.35);
                half3 grain=SAMPLE_TEXTURE2D(_ReliefMap,sampler_ReliefMap,p*_Scale).rgb*2-1;
                half3 normal=normalize(i.normalWS+half3(grain.x,0,grain.y)*_Relief);
                SurfaceData s=(SurfaceData)0;s.albedo=sand*_Tint.rgb;s.alpha=1;s.metallic=0;s.specular=half3(.04,.04,.04);s.smoothness=.04;s.occlusion=lerp(.85,1,m);s.normalTS=half3(0,0,1);
                InputData input=(InputData)0;input.positionWS=i.positionWS;input.normalWS=normal;input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord=TransformWorldToShadowCoord(i.positionWS);input.bakedGI=SampleSH(normal);input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);input.shadowMask=half4(1,1,1,1);
                half4 color=UniversalFragmentPBR(input,s);color.rgb=MixFog(color.rgb,i.fog);return color;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
