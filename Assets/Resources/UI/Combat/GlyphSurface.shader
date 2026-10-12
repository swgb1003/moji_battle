Shader "MojiBattle/GlyphSurface"
{
    Properties { [PerRendererData] _MainTex("Sprite",2D)="white"{} _Color("Tint",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;float4 _MainTex_TexelSize;fixed4 _Color;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Color;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 tex=tex2D(_MainTex,i.uv);
                float2 px=_MainTex_TexelSize.xy*1.4;
                float dx=tex2D(_MainTex,i.uv+float2(px.x,0)).a-tex2D(_MainTex,i.uv-float2(px.x,0)).a;
                float dy=tex2D(_MainTex,i.uv+float2(0,px.y)).a-tex2D(_MainTex,i.uv-float2(0,px.y)).a;
                float bevel=clamp(-dx*.65+dy*.8,-.32,.45);
                return fixed4(tex.rgb*i.color.rgb*(.96+bevel),tex.a*i.color.a);
            }
            ENDCG
        }
    }
}
