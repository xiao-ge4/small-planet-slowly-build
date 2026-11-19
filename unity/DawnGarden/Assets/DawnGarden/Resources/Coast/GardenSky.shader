Shader "Dawn Garden/Coast Garden Sky"
{
    Properties { _Day("Daylight",Float)=1 _SunDir("Sun direction",Vector)=(.4,.6,-.4,0) _PlanetCenter("Planet center",Vector)=(7.5,-1136.68,-10.4,0) _PlanetRadius("Planet radius",Float)=1133.48 }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _Day; float4 _SunDir; float4 _PlanetCenter; float _PlanetRadius;
            struct A { float4 positionOS:POSITION; };
            struct V { float4 positionCS:SV_POSITION; float3 direction:TEXCOORD0; };
            V Vert(A a) { V o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.direction=a.positionOS.xyz;return o; }
            float H(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float N(float2 p)
            {
                float2 f=frac(p),i=floor(p);f=f*f*(3-2*f);
                return lerp(lerp(H(i),H(i+float2(1,0)),f.x),lerp(H(i+float2(0,1)),H(i+1),f.x),f.y);
            }
            float Clouds(float2 p) { return N(p)*.54+N(p*2.08+17)*.27+N(p*4.13+5)*.13+N(p*8.27)*.06; }
            half4 Frag(V i):SV_Target
            {
                float3 d=normalize(i.direction);
                float3 radial=_WorldSpaceCameraPos-_PlanetCenter.xyz;
                float eyeRadius=length(radial);
                float dip=sqrt(saturate(1-(_PlanetRadius*_PlanetRadius)/(eyeRadius*eyeRadius)));
                float elevation=dot(d,radial/eyeRadius)+dip;
                float h=saturate(elevation*3.5);
                float sunset=saturate(1-_SunDir.y*3)*_Day;
                float towardSun=pow(saturate(dot(normalize(d.xz),normalize(_SunDir.xz))*.5+.5),3);
                half3 horizon=lerp(half3(.52,.72,.93),half3(1,.79,.59),towardSun*.48+sunset*.32);
                half3 zenith=lerp(half3(.14,.40,.77),half3(.27,.33,.59),sunset*.5);
                half3 sky=lerp(horizon,zenith,pow(h,.52));
                sky=lerp(lerp(half3(.042,.065,.13),half3(.01,.025,.075),h),sky,_Day);
                // Cloud lobes are distributed in angular space. Unlike projecting
                // onto a clamped horizontal plane, they cannot streak vertically
                // as the overview looks toward the curved ocean horizon.
                float2 plane=float2(atan2(d.x,d.z)*14+_Time.y*.0006,elevation*18);
                float cloud=0,lightAmount=0;
                [unroll] for(int row=-1;row<=1;row++)
                [unroll] for(int col=-1;col<=1;col++)
                {
                    float2 cell=floor(plane)+float2(col,row);
                    float seed=H(cell+17);
                    float2 center=cell+float2(.3+H(cell+3)*.4,.25+H(cell+9)*.5);
                    float2 delta=plane-center;
                    float width=.30+seed*.33,height=.22+H(cell+7)*.24;
                    float shape=length(delta/float2(width,height));
                    shape=min(shape,length((delta-float2(-width*.35,height*.42))/float2(width*.52,height*.88)));
                    shape=min(shape,length((delta-float2(width*.16,height*.65))/float2(width*.55,height*1.04)));
                    shape=min(shape,length((delta-float2(width*.48,height*.13))/float2(width*.46,height*.70)));
                    float mask=1-smoothstep(.83,1.04,shape+(Clouds(plane*8)-.5)*.22);
                    mask*=smoothstep(.18,.28,seed)*smoothstep(.006,.024,elevation);
                    if(mask>cloud){cloud=mask;lightAmount=saturate(delta.y/height*.38+.68);}
                }
                half3 litCloud=lerp(half3(1,.95,.85),half3(1,.79,.71),towardSun*.45+sunset*.35);
                half3 cloudShade=lerp(half3(.43,.46,.70),litCloud,lightAmount);
                cloudShade=lerp(half3(.12,.16,.28),cloudShade,_Day);
                sky=lerp(sky,cloudShade,cloud*.92);
                float sun=pow(saturate(dot(d,normalize(_SunDir.xyz))),720);
                sky+=sun*half3(1,.77,.40)*_Day*.44*(1-cloud);
                return half4(sky,1);
            }
            ENDHLSL
        }
    }
}
