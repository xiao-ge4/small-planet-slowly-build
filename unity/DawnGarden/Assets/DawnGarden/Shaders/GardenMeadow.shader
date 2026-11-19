Shader "Dawn Garden/Living Meadow"
{
    Properties {
        _Grass("Meadow",Color)=(.46,.67,.16,1)
        _LightGrass("New grass",Color)=(.64,.76,.24,1)
        _Earth("Warm earth",Color)=(.74,.58,.36,1)
        _Origin("Land origin",Vector)=(0,0,0,0)
    }
    SubShader {
        Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
        Pass {
            Tags {"LightMode"="UniversalForwardOnly"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Grass,_LightGrass,_Earth;float4 _Origin;
            CBUFFER_END
            float4 _ContactPoints[32];int _ContactCount;
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;half4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half fog:TEXCOORD2;};
            Varyings vert(Attributes i) {Varyings o;VertexPositionInputs p=GetVertexPositionInputs(i.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.fog=ComputeFogFactor(p.positionCS.z);return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);}
            half4 frag(Varyings i):SV_Target {
                float2 p=i.positionWS.xz-_Origin.xz;
                float coarse=noise(p*.72)*.62+noise(p*2.1)*.38;
                float footprint=length(fwidth(p));
                float fleck=lerp(.5,noise(p*38),saturate(1-footprint*38));
                float grain=lerp(.5,noise(p*115),saturate(1-footprint*115));
                float pathZ=.35*sin(p.x*.65)+.13*(p.x+3);
                float path=1-smoothstep(.15,.74,abs(p.y-pathZ)+noise(p*5)*.22);
                path*=1-smoothstep(.7,3.5,p.x);
                // The visible grass boundary is irregular and follows broad soil
                // patches. The mesh itself still matches the unchanged collider.
                float radial=length(p/float2(6.35,5.40));
                float soilEdge=smoothstep(.62,.99,radial+(noise(p*.49+11)-.5)*.35);
                float bare=smoothstep(.68,.87,noise(p*.72+20))*.30;
                half3 grass=lerp(_Grass.rgb,_LightGrass.rgb,smoothstep(.22,.8,coarse));
                grass*=lerp(.88,1.08,fleck);
                half3 earth=_Earth.rgb*lerp(.87,1.14,noise(p*7));
                half3 albedo=lerp(grass,earth,saturate(path+bare+soilEdge*.88));
                albedo*=lerp(.95,1.035,grain);
                float contact=0;
                for(int k=0;k<32;k++){if(k>=_ContactCount)break;float d=distance(p,_ContactPoints[k].xz);contact=max(contact,(1-smoothstep(_ContactPoints[k].w*.35,_ContactPoints[k].w,d))*.21);}
                albedo*=1-contact;
                // A matte, broad diffuse response keeps painted grass readable in
                // daylight. Main light / ambient still follow the real day cycle.
                half3 normal=normalize(i.normalWS);
                Light light=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half diffuse=.35+.65*saturate(dot(normal,light.direction));
                half shadow=lerp(.55,1,light.shadowAttenuation);
                half3 illumination=SampleSH(normal)*.7+light.color*diffuse*shadow;
                // The saved garden surface must receive the same lantern as the
                // bench and character; otherwise a warm pool ends at the grass.
                InputData inputData=(InputData)0;
                inputData.positionWS=i.positionWS;
                inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                #if defined(_ADDITIONAL_LIGHTS)
                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);lightIndex++)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    Light lamp=GetAdditionalLight(lightIndex,i.positionWS);
                    illumination+=lamp.color*saturate(dot(normal,lamp.direction))*lamp.distanceAttenuation*lamp.shadowAttenuation;
                }
                #endif
                uint count=GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(count)
                    Light lamp=GetAdditionalLight(lightIndex,i.positionWS);
                    illumination+=lamp.color*saturate(dot(normal,lamp.direction))*lamp.distanceAttenuation*lamp.shadowAttenuation;
                LIGHT_LOOP_END
                #endif
                half3 color=albedo*illumination;return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
