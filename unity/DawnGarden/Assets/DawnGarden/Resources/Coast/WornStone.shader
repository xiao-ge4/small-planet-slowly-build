Shader "Dawn Garden/Coast Worn Stone"
{
    Properties
    {
        [MainColor] _BaseColor("Pigment", Color)=(.7,.67,.56,1)
        [MainTexture] _BaseMap("Stone colour",2D)="white"{}
        _Smoothness("Smoothness",Range(0,1))=.18
        _Cull("Cull",Float)=2
        _Cutoff("Cutoff",Float)=.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull [_Cull]
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;float4 _BaseMap_ST;half _Smoothness;half _Cull;half _Cutoff;
            CBUFFER_END
            struct A { float3 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0; };
            struct V { float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;half fog:TEXCOORD3; };
            V Vert(A a)
            {
                V o;VertexPositionInputs p=GetVertexPositionInputs(a.p);o.p=p.positionCS;o.world=p.positionWS;
                o.normal=TransformObjectToWorldNormal(a.n);o.uv=TRANSFORM_TEX(a.uv,_BaseMap);o.fog=ComputeFogFactor(p.positionCS.z);return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 f=frac(p),i=floor(p);f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            float Stone(float3 p)
            {
                return Noise(p.xz*3.1+p.y*.31)*.45+Noise(p.zy*4.9+p.x*.39)*.31+Noise(p.xy*13.1+p.z*.53)*.24;
            }
            half4 Frag(V i, FRONT_FACE_TYPE facing:FRONT_FACE_SEMANTIC):SV_Target
            {
                float3 n=normalize(i.normal)*IS_FRONT_VFACE(facing,1,-1);
                float h=Stone(i.world);
                float3 slope=float3(Stone(i.world+float3(.025,0,0))-h,Stone(i.world+float3(0,.025,0))-h,Stone(i.world+float3(0,0,.025))-h)/.025;
                n=normalize(n-(slope-n*dot(n,slope))*.075);
                float large=Noise(i.world.xz*.63+i.world.y*.24);
                float strata=abs(sin(i.world.y*5.3+large*5+i.world.x*.27+i.world.z*.39));
                float seams=1-smoothstep(.025,.15,strata);
                half3 color=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb;
                color*=.89+h*.21+large*.10-seams*.065;
                color*=lerp(half3(.97,.99,1.02),half3(1.03,1.0,.94),large);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                half diffuse=saturate(dot(n,sun.direction));
                half3 light=SampleSH(n)+sun.color*(diffuse*.97+.02)*sun.shadowAttenuation;
                InputData inputData=(InputData)0;
                inputData.positionWS=i.world;
                inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);
                #if defined(_ADDITIONAL_LIGHTS)
                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);lightIndex++)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    Light lamp=GetAdditionalLight(lightIndex,i.world);
                    light+=lamp.color*saturate(dot(n,lamp.direction))*lamp.distanceAttenuation*lamp.shadowAttenuation;
                }
                #endif
                uint count=GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(count)
                    Light lamp=GetAdditionalLight(lightIndex,i.world);
                    light+=lamp.color*saturate(dot(n,lamp.direction))*lamp.distanceAttenuation*lamp.shadowAttenuation;
                LIGHT_LOOP_END
                #endif
                #if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor ao=GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
                light*=lerp(.45,1,ao.indirectAmbientOcclusion);
                #endif
                return half4(MixFog(color*light,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
