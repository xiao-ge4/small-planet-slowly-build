Shader "Dawn Garden/Coast Sample Flow"
{
    Properties
    {
        _Shallow("Shallow water", Color) = (.22,.76,.74,1)
        _Deep("Deep water", Color) = (.055,.37,.44,1)
        _Mode("Flow type", Range(0,4)) = 0
        _Opacity("Opacity", Range(0,1)) = .96
        _Cull("Cull mode", Float) = 0
        _SkyMap("Morning sky reflection",2D)="black"{}
        [HideInInspector] _MouthProfile("Estuary origin and heading", Vector) = (0,0,0,0)
        [HideInInspector] _MouthLength("Estuary length", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Cull [_Cull]
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_SkyMap);SAMPLER(sampler_SkyMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _Shallow;
                half4 _Deep;
                float4 _MouthProfile;
                float _MouthLength;
                float _Mode;
                half _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fog : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings result;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS);
                result.positionCS = p.positionCS;
                result.positionWS = p.positionWS;
                result.normalWS = TransformObjectToWorldNormal(input.normalOS);
                result.uv = input.uv;
                result.fog = ComputeFogFactor(p.positionCS.z);
                return result;
            }

            float FlowCell(float2 cell)
            {
                float3 key = frac(cell.xyx * .1031);
                key += dot(key,key.yzx + 33.33);
                return frac((key.x + key.y) * key.z);
            }

            float FlowNoise(float2 sampleUV)
            {
                float2 cell = floor(sampleUV);
                float2 offset = frac(sampleUV);
                float2 mixWeight = offset * offset * (3 - 2 * offset);
                return lerp(lerp(FlowCell(cell),FlowCell(cell + float2(1,0)),mixWeight.x),
                    lerp(FlowCell(cell + float2(0,1)),FlowCell(cell + 1),mixWeight.x),mixWeight.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float across = input.uv.x * 2 - 1;
                float along = input.uv.y;
                float advected = along - _Time.y * ((_Mode > .5 && _Mode < 1.5) ? 1.90 : .65);
                float wave = sin(advected * 6.5 + sin(across * 8 + along * 2.1) * .6);
                float waveB = sin(advected * 13 + sin(across * 17 - along * 1.6) * .8);
                float bank = smoothstep(.6, .99, abs(across) + wave * .025);
                float scaleB=along*.66 -_Time.y*.24;
                float diagonalWave=sin(across*34 + scaleB*16 + sin(along*2.41)*.92);
                float crossWave=sin(across*15 - scaleB*22 + cos(along*5.7+across*17)*1.19);
                float net=abs(diagonalWave+crossWave*.77+sin(across*49+scaleB*27)*.26);
                float waterBreak = smoothstep(.10,.80,wave*.21+waveB*.25+.52);
                float caustic=(1 - smoothstep(.050,.13,net)) * (.23+.30*bank) * waterBreak;
                float glint = pow(saturate(wave * .58 + waveB * .26 + diagonalWave*.19), 7) * .26;
                float3 surfaceNormal = normalize(input.normalWS);
                float fresnel = pow(1 - saturate(abs(dot(surfaceNormal,
                    GetWorldSpaceNormalizeViewDir(input.positionWS)))), 4);
                float3 color = lerp(_Deep.rgb, _Shallow.rgb, saturate(.17 + .54 * bank + fresnel * .27 + glint));
                color+=caustic*half3(.022h,.083h,.067h);
                float ribbonEdge = smoothstep(.81,.977,abs(across)+waveB*.036);
                float foamBreak = smoothstep(-.35,.74,wave*.49+waveB*.22+crossWave*.26);
                float edgeFoam = ribbonEdge * (.07 + .69 * foamBreak);
                color = lerp(color, half3(.80,.96,.89), edgeFoam);
                float poolSeam = 1 - smoothstep(.886,1,abs(across)+waveB*.016) * (1 - foamBreak*.76);
                half alpha = _Opacity;
                if (_Mode > 3.5)
                {
                    float depth = input.uv.x;
                    float edgeWeight = input.uv.y;
                    float2 waterCells = input.positionWS.xz;
                    float wind = FlowNoise(waterCells * float2(1.7,4.1) + float2(_Time.y * .13,_Time.y * -.24));
                    float floorColor = smoothstep(.04,.97,depth);
                    color = lerp(_Shallow.rgb,_Deep.rgb,floorColor);
                    color += (wind - .5) * half3(.022,.043,.039);
                    float waveDepth = depth + (wind - .5) * .033;
                    float rim = smoothstep(.011,.040,waveDepth) * (1 - smoothstep(.058,.145,waveDepth));
                    float interrupted = smoothstep(.56,.85,FlowNoise(waterCells * float2(7.0,4.8) + _Time.y*.09));
                    color = lerp(color,half3(.76,.91,.79),rim * interrupted * .58);
                    alpha *= edgeWeight * smoothstep(.002,.06,depth) * (1 - floorColor * .65);
                }
                else if (_Mode > 2.5)
                {
                    float downhill = along - _Time.y * 1.90;
                    float drift = FlowNoise(float2(across * 2.9,downhill * 1.52));
                    float body = FlowNoise(float2(across * 4.8 + drift,downhill * 1.35));
                    float spray = FlowNoise(float2(across * 12 + .7 * drift,downhill * 3.6));
                    float shimmer = smoothstep(.44,.82,body * .72 + spray * .28);
                    float thrownWater = 1 - smoothstep(.80,.975,abs(surfaceNormal.y));
                    float tornEdge = smoothstep(.68,1,abs(across) + (body - .5) * .20);
                    float3 waterCore = lerp(half3(.18,.47,.50),half3(.81,.93,.90),shimmer*.70);
                    color = lerp(color,waterCore,thrownWater);
                    float waterfallOpacity = _Opacity * (.70 + .27 * shimmer) * (1 - tornEdge * .96);
                    alpha = lerp(_Opacity * poolSeam,waterfallOpacity,thrownWater);
                }
                else if (_Mode > 1.5)
                {
                    float2 local = input.uv * 2 - 1;
                    float2 cells = input.positionWS.xz * 27 + float2(0,_Time.y * 2.1);
                    float dapple = FlowNoise(cells);
                    float coverage = smoothstep(.39,.80,dapple);
                    float distanceToCenter = length(local);
                    float fade = 1 - smoothstep(.12,1,distanceToCenter);
                    color = lerp(_Deep.rgb,_Shallow.rgb,.48 + coverage * .44);
                    alpha *= fade * (.18 + .69 * coverage);
                }
                else if (_Mode > .5)
                {
                    float pulse = FlowNoise(float2(across * 5.2,advected * 1.4));
                    float streaks = FlowNoise(float2(across * 23.3 + pulse,advected * 3.4));
                    float filament = smoothstep(.44,.84,pulse * .58 + streaks * .42);
                    float strandEdge = smoothstep(.69,1.0,abs(across) + (pulse - .5) * .21);
                    color = lerp(_Deep.rgb,_Shallow.rgb,.36 + pulse * .22 + filament * .29 + fresnel * .10);
                    color=lerp(color,half3(.84h,.94h,.92h),filament*.43h);
                    alpha *= 1 - .88 * strandEdge;
                }
                else
                {
                    alpha *= poolSeam;
                    if (_MouthLength > .01)
                    {
                        float distanceFromMouth = dot(input.positionWS.xz - _MouthProfile.xy, _MouthProfile.zw);
                        alpha *= 1 - smoothstep(_MouthLength * .38, _MouthLength, distanceFromMouth);
                    }
                }
                if(_Mode<.5)
                {
                    float3 reflected=reflect(-GetWorldSpaceNormalizeViewDir(input.positionWS),surfaceNormal);
                    float2 skyUV=float2(atan2(reflected.x,reflected.z)/6.2831853*5+.80,
                        saturate(.5+atan(reflected.y*14)/3.14159265));
                    half3 sky=SAMPLE_TEXTURE2D(_SkyMap,sampler_SkyMap,skyUV).rgb;
                    color=lerp(color,sky,.10h+fresnel*.16h);
                }
                Light key = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half daylight = saturate(dot(key.color, half3(.38,.45,.17)) * .72);
                color *= lerp(half3(.27,.36,.55), half3(1,1,1), daylight);
                color*=lerp(.60h,1.0h,key.shadowAttenuation);
                return half4(MixFog(color, input.fog), alpha);
            }
            ENDHLSL
        }
    }
}
