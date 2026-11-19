Shader "Dawn Garden/Coast Morning Panorama"
{
    Properties
    {
        _SkyMap("Morning cloud panorama",2D)="white"{}
        _Day("Daylight",Float)=1
        _SunDir("Sun direction",Vector)=(.4,.6,-.4,0)
        _PlanetCenter("Planet center",Vector)=(7.5,-1136.68,-10.4,0)
        _PlanetRadius("Planet radius",Float)=1133.48
        _CloudRepeats("Cloud field angular density",Float)=5
        _Rotation("Panorama rotation",Float)=.80
    }
    SubShader
    {
        Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_SkyMap);SAMPLER(sampler_SkyMap);
            float _Day,_PlanetRadius,_CloudRepeats,_Rotation;
            float4 _SunDir,_PlanetCenter;
            struct A {float4 positionOS:POSITION;};
            struct V {float4 positionCS:SV_POSITION;float3 direction:TEXCOORD0;};
            V Vert(A a){V o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.direction=a.positionOS.xyz;return o;}
            half4 Frag(V i):SV_Target
            {
                float3 d=normalize(i.direction);
                float3 radial=_WorldSpaceCameraPos-_PlanetCenter.xyz;
                float eyeRadius=length(radial);
                float dip=sqrt(saturate(1-(_PlanetRadius*_PlanetRadius)/(eyeRadius*eyeRadius)));
                float elevation=dot(d,radial/eyeRadius)+dip;
                // Only the cloud belt is textured; the island silhouettes remain
                // lit world geometry. Horizon elevation follows the curved sea.
                float2 uv=float2(atan2(d.x,d.z)/6.2831853*_CloudRepeats+_Rotation+_Time.y*.000002,
                    saturate(.5+atan(elevation*14)/3.14159265));
                half3 day=SAMPLE_TEXTURE2D(_SkyMap,sampler_SkyMap,uv).rgb;
                half3 night=day*half3(.055,.075,.15)+half3(.006,.012,.035);
                return half4(lerp(night,day,_Day),1);
            }
            ENDHLSL
        }
    }
}
