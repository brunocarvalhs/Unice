// Pixelates a character sprite: quantizes its UV to a coarse, aspect-correct grid for a chibi/retro look.
// Used only on the paper-doll/Unice layers (WorldView.EntView) — everything else keeps the normal unlit material.
Shader "Unice/PixelSprite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Pixels ("Pixel blocks across the longer side", Float) = 32
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Pixels;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                // ponytail: assumes a non-atlased _MainTex (true for every character layer today) so TexelSize matches the sprite itself.
                float texW = 1.0 / max(_MainTex_TexelSize.x, 1e-6);
                float texH = 1.0 / max(_MainTex_TexelSize.y, 1e-6);
                float longest = max(texW, texH);
                float2 blocks = max(round(_Pixels * float2(texW, texH) / longest), 1);
                float2 uv = (floor(i.uv * blocks) + 0.5) / blocks;
                return tex2D(_MainTex, uv) * i.color;
            }
            ENDCG
        }
    }
}
