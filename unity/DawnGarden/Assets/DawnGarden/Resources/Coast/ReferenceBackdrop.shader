Shader "Dawn Garden/Coast Distant Rock Shore"
{
    Properties
    {
        _BaseColor("Color multiplier",Color)=(1,1,1,1)
        _BaseMap("Fine stone grain",2D)="white"{}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; half3 normalWS:TEXCOORD0; float2 uv:TEXCOORD1; half4 color:COLOR; half fog:TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.uv=input.uv;output.color=input.color;
                output.fog=ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half3 normal=normalize(input.normalWS);
                Light light=GetMainLight();
                half3 ambient=max(SampleSH(normal),half3(.012h,.015h,.022h));
                half diffuse=saturate(dot(normal,light.direction));
                half3 lighting=ambient*.83h+light.color*(.15h+diffuse*.72h)*light.distanceAttenuation;
                // These mesh swatches are authored as display colours in C#.
                // Unlike Material Color properties, vertex colours are not
                // automatically decoded for a linear-light render pipeline.
                half3 vertexTint=input.color.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                vertexTint=SRGBToLinear(vertexTint);
                #endif
                half3 albedo=vertexTint*_BaseColor.rgb*SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb;
                return half4(MixFog(albedo*lighting,input.fog),1);
            }
            ENDHLSL
        }
    }
}
