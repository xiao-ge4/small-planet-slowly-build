Shader "Dawn Garden/Ember Lava"
{
    Properties
    {
        _DeepRed("Deep red water",Color)=(.34,.018,.022,1)
        _WarmRed("Warm red water",Color)=(.62,.045,.018,1)
        _Orange("Soft orange water",Color)=(.88,.20,.028,1)
        _FlowTime("Preview flow time (-1 = live)",Float)=-1
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
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _DeepRed, _WarmRed, _Orange;
                float _FlowTime;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;};
            struct V {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half fog:TEXCOORD1;};
            V vert(A i){V o;o.positionWS=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.fog=ComputeFogFactor(o.positionCS.z);return o;}
            float2 hash(float2 p){return frac(sin(float2(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3))))*43758.5453);}
            float noise(float2 p)
            {
                float2 c=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(hash(c).x,hash(c+float2(1,0)).x,f.x),lerp(hash(c+float2(0,1)).x,hash(c+1).x,f.x),f.y);
            }
            half4 frag(V i):SV_Target
            {
                float t=_FlowTime<0?_Time.y:_FlowTime;
                float2 p=i.positionWS.xz;
                // Broad, softly distorted color patches, like red-orange water.
                // No cracks, emissive lines, highlights, or added illumination.
                float2 drift=float2(t*.014,t*.008);
                float2 q=p*.105;
                float2 warp=float2(noise(q+float2(4.3,8.1)),noise(q+float2(17.2,2.6)))-.5;
                float broad=noise(q+warp*1.15+drift);
                float medium=noise(p*.20-warp*.55-drift*.58);
                float cloud=broad*.72+medium*.28;
                half3 col=lerp(_DeepRed.rgb,_WarmRed.rgb,smoothstep(.20,.78,cloud));
                col=lerp(col,_Orange.rgb,smoothstep(.52,.82,cloud)*.72);
                return half4(MixFog(col,i.fog),1);
            }
            ENDHLSL
        }
    }
}
