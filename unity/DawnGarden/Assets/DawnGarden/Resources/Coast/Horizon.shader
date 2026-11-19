Shader "Dawn Garden/Coast Distant Horizon"
{
    Properties { _Day("Daylight",Range(0,1))=1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Day;
            CBUFFER_END
            struct A { float3 p:POSITION;float3 n:NORMAL; };
            struct V { float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 n:TEXCOORD1; };
            V Vert(A a) { V o; o.p=TransformObjectToHClip(a.p);o.world=TransformObjectToWorld(a.p);o.n=TransformObjectToWorldNormal(a.n);return o; }
            half4 Frag(V i):SV_Target
            {
                half relief=saturate(dot(normalize(i.n),normalize(float3(-.4,.8,.2))));
                half3 daylight=lerp(half3(.20,.32,.39),half3(.36,.47,.51),relief);
                half aerial=smoothstep(180,580,distance(i.world,_WorldSpaceCameraPos));
                daylight=lerp(daylight,half3(.44,.59,.66),aerial*.72);
                half3 night=lerp(half3(.015,.027,.045),half3(.035,.055,.085),relief);
                return half4(lerp(night,daylight,_Day),1);
            }
            ENDHLSL
        }
    }
}
