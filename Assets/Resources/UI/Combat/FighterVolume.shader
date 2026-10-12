Shader "MojiBattle/FighterVolume"
{
    Properties { _ZWrite("Depth write",Float)=1 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite [_ZWrite]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; fixed4 color:COLOR; };
            struct v2f { float4 pos:SV_POSITION; float3 normal:TEXCOORD0; fixed4 color:COLOR; };
            v2f vert(appdata v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex);
                o.normal=dot(v.normal,v.normal)<.001?float3(0,0,0):UnityObjectToWorldNormal(v.normal);o.color=v.color;return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                if(dot(i.normal,i.normal)<.01)return i.color;
                float3 n=normalize(i.normal);
                float diffuse=saturate(dot(n,normalize(float3(-.55,.8,-.9))));
                float spec=pow(saturate(dot(n,normalize(float3(-.25,.45,-1)))),16);
                float rim=pow(1-abs(n.z),3)*.022;
                return fixed4(i.color.rgb*(.48+.8*diffuse)+spec*.09+rim,i.color.a);
            }
            ENDCG
        }
    }
}
