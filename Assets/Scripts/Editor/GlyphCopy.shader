// フォントアトラスの字形アルファを、そのまま白マスクとして書き出す（ブレンドしない）。GlyphBaker 専用。
Shader "Hidden/MojiBattle/GlyphCopy"
{
    Properties { _MainTex ("Font Texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue" = "Overlay" }
        Cull Off ZWrite Off ZTest Always Blend Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }
            fixed4 frag (v2f i) : SV_Target { return fixed4(1, 1, 1, tex2D(_MainTex, i.uv).a); }
            ENDCG
        }
    }
}
