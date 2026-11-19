Shader "Dawn Garden/Coast Sample Ground"
{
    Properties
    {
        _BaseColor("Export swatch", Color) = (.54,.62,.34,1)
        _PathColor("Warm pavers", Color) = (.78,.66,.49,1)
        _StoneColor("Local layered rock", Color) = (.57,.55,.46,1)
        _PaverScale("Pavers per metre", Vector) = (1.7,2.0,0,0)
        _MortarTone("Mortar brightness", Float) = .63
        _GroutWidth("Mortar width", Float) = .055
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
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

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _PathColor;
                half4 _StoneColor;
                float4 _PaverScale;
                float _MortarTone; float _GroutWidth;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tintAndPath : COLOR;
                float2 wetAndRock : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float4 tintAndPath : TEXCOORD2;
                float2 wetAndRock : TEXCOORD3;
                half fog : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tintAndPath = input.tintAndPath;
                output.wetAndRock = input.wetAndRock;
                output.fog = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            float Hash(float2 v) { return frac(sin(dot(v, float2(12.9898, 78.233))) * 43758.5453); }

            float Grain(float2 v)
            {
                float2 baseCell = floor(v), t = frac(v);
                t = t * t * (3 - 2 * t);
                float a = lerp(Hash(baseCell), Hash(baseCell + float2(1,0)), t.x);
                float b = lerp(Hash(baseCell + float2(0,1)), Hash(baseCell + float2(1,1)), t.x);
                return lerp(a,b,t.y);
            }

            // Irregular broken beds, not horizontal stripes over a smooth wall.
            // Returns crack separation and a stable per-rock colour variation.
            float2 RockBeds(float2 p)
            {
                p=float2(p.x*.82+p.y*.17,p.y*1.08-p.x*.10);
                p+=float2(Grain(p*.47+17),Grain(p*.43+53))*.28;
                float2 cell=floor(p),local=frac(p);float nearest=20,second=20,tone=.5;
                [unroll] for(int y=-1;y<=1;y++)
                [unroll] for(int x=-1;x<=1;x++)
                {
                    float2 offset=float2(x,y),key=cell+offset;
                    float2 seed=.18+.64*float2(Hash(key),Hash(key+37.4));
                    float2 delta=offset+seed-local;float d=dot(delta,delta);
                    if(d<nearest){second=nearest;nearest=d;tone=Hash(key+91.7);}
                    else second=min(second,d);
                }
                return float2(sqrt(second)-sqrt(nearest),tone);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 position = i.positionWS.xz;
                float variation = Grain(position * .72) * .66 + Grain(position * 4.1) * .34;
                float footprint = length(fwidth(position));
                float grain = lerp(Grain(position * 12), .5, saturate(footprint * 12));
                half3 normal = normalize(i.normalWS);
                float rock = saturate(i.wetAndRock.y + smoothstep(.15,.56,1-normal.y) * .85);
                float2 beds=float2(.3,.5);
                if(rock>.025)
                {
                    float3 projection=pow(abs(normal),4);projection/=max(.001,projection.x+projection.y+projection.z);
                    beds=RockBeds(i.positionWS.zy)*projection.x+
                        RockBeds(i.positionWS.xz)*projection.y+RockBeds(i.positionWS.xy)*projection.z;
                }
                float crack=smoothstep(.01,.065+min(footprint*.08,.06),beds.x);
                float ledge=smoothstep(.02,.24,beds.x);
                half3 stoneWarmth=lerp(half3(.86,.92,1.04),half3(1.07,1.02,.91),beds.y);
                half3 stone = _StoneColor.rgb * stoneWarmth * (.78 + variation*.16 + beds.y*.14) * lerp(.83,1,crack);
                half3 baseTint = i.tintAndPath.rgb;
                // Mesh swatches use the same sRGB palette as the stone assets.
                // Decode them before lighting, rather than compensating with an
                // arbitrary grass tint which left the sand and cliffs washed out.
                #ifndef UNITY_COLORSPACE_GAMMA
                baseTint=SRGBToLinear(baseTint);
                #endif
                half3 albedo = lerp(baseTint * (.79 + variation * .27 + grain * .18), stone, rock);

                // Paver size follows metres at any camera angle. A derivative-sized edge keeps the
                // mortar legible in the ground-level frame without aliasing in the overview.
                float2 paving = position * _PaverScale.xy;
                paving += float2(Grain(position*3.7),Grain(position*4.3+19))*.15;
                paving.x += frac(floor(paving.y) * .5);
                float2 cell = frac(paving);
                float groutDistance = min(min(cell.x,1-cell.x),min(cell.y,1-cell.y));
                float grout = smoothstep(_GroutWidth*.35, _GroutWidth + min(footprint * .20,.15), groutDistance);
                float paverTone = .77 + Hash(floor(paving)) * .28 + grain * .10;
                half3 warmVariation = lerp(half3(.94,.96,1.0),half3(1.05,.98,.88),Hash(floor(paving)+41));
                half3 pavers = lerp(_PathColor.rgb * _MortarTone, _PathColor.rgb * paverTone * warmVariation, grout);
                float path = smoothstep(.06,.94,i.tintAndPath.a + (grain-.5) * .16) * (1-i.wetAndRock.x * .91);
                // Paving belongs on the tread. Blended vertex alpha used to
                // paint it down the supporting bank, hiding the rock material.
                path*=1-smoothstep(.18,.74,rock);
                float submerged=1-smoothstep(-3.25,-3.10,i.positionWS.y);
                path*=1-submerged;
                albedo = lerp(albedo,pavers,path);
                // The shallow bay reveals sand, not a submerged copy of the path grid.
                half3 seaBed=lerp(half3(.55,.57,.43),half3(.17,.31,.23),
                    smoothstep(.44,.71,Grain(position*.43+7))*.72)*(.87+variation*.19);
                albedo=lerp(albedo,seaBed,submerged);
                albedo *= lerp(half3(1,1,1), half3(.65,.75,.73), saturate(i.wetAndRock.x));

                // A few centimetres of stone bevel, and millimetres of soil grain, rather
                // than a dark painted grid. This remains stable at the player's eye level.
                float relief = lerp(grain * .012 + variation * .018,
                    grout * .035 + grain * .008, path);
                relief += rock * (ledge*.065 + grain*.006);
                relief*=1-submerged;
                float3 dx = ddx(i.positionWS), dy = ddy(i.positionWS);
                float3 rx = cross(dy,normal), ry = cross(normal,dx);
                float determinant = dot(dx,rx);
                float3 gradient = sign(determinant) * (ddx(relief)*rx + ddy(relief)*ry);
                normal = normalize(max(abs(determinant),.000001)*normal-gradient);

                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 ambient = SampleSH(normal) * .72;
                half direct = (.08 + .92 * saturate(dot(normal,sun.direction))) * sun.shadowAttenuation;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor ao=GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS));
                ambient*=ao.indirectAmbientOcclusion;
                direct*=ao.directAmbientOcclusion;
                #endif
                half daylight = saturate(dot(sun.color,half3(.35,.48,.17)));
                half3 light = (ambient + sun.color * direct) * lerp(1, .80, daylight);
                InputData inputData=(InputData)0;
                inputData.positionWS=i.positionWS;
                inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                #if defined(_ADDITIONAL_LIGHTS)
                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);lightIndex++)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    Light lamp=GetAdditionalLight(lightIndex,i.positionWS);
                    light+=lamp.color*saturate(dot(normal,lamp.direction))*lamp.distanceAttenuation*lamp.shadowAttenuation;
                }
                #endif
                uint count=GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(count)
                    Light lamp=GetAdditionalLight(lightIndex,i.positionWS);
                    light+=lamp.color*saturate(dot(normal,lamp.direction))*lamp.distanceAttenuation*lamp.shadowAttenuation;
                LIGHT_LOOP_END
                #endif
                return half4(MixFog(albedo * light, i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
