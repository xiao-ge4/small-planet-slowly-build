Shader "SmallWorld/StoryBoundaryMist"
{
 Properties { _BaseColor("Sea mist", Color) = (.66,.76,.73,.54) }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
  Pass
  {
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A { float3 positionOS:POSITION; float2 uv:TEXCOORD0; float2 size:TEXCOORD1; };
   struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float2 uv:TEXCOORD1; };
   half4 _BaseColor;float4 _RevealBounds;float _RevealProgress;
   V vert(A a)
   {
    V o; float3 center=TransformObjectToWorld(a.positionOS);
    float3 right=UNITY_MATRIX_V[0].xyz,up=UNITY_MATRIX_V[1].xyz;
    o.world=center+right*(a.uv.x-.5)*a.size.x+up*(a.uv.y-.5)*a.size.y;
    o.positionCS=TransformWorldToHClip(o.world);o.uv=a.uv;return o;
   }
   half4 frag(V i):SV_Target
   {
    float revealFade=1-smoothstep(0,1,_RevealProgress);
    // Fade fully to transparent before the plot boundary instead of clipping
    // billboard fragments into a hard rectangular sheet over the sea.
    float edge=min(min(i.world.x-_RevealBounds.x,_RevealBounds.z-i.world.x),
                   min(i.world.z-_RevealBounds.y,_RevealBounds.w-i.world.z));
    float edgeFade=smoothstep(0,1.6,edge);
    float d=dot((i.uv-.5)*2,(i.uv-.5)*2);
    float alpha=saturate(1-d);alpha=alpha*alpha*revealFade*edgeFade;
    return half4(_BaseColor.rgb,_BaseColor.a*alpha);
   }
   ENDHLSL
  }
 }
}
