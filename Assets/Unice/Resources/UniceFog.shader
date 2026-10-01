// The darkness around the player: a mesh with per-vertex color and alpha, nothing else.
Shader "Unice/Fog"
{
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 pos : SV_POSITION; fixed4 col : COLOR; };
            v2f vert(float4 v : POSITION, fixed4 c : COLOR) { v2f o; o.pos = UnityObjectToClipPos(v); o.col = c; return o; }
            fixed4 frag(v2f i) : SV_Target { return i.col; }
            ENDCG
        }
    }
}
