Shader "RaftGame/Build Ghost"
{
    Properties { _Color ("Tint", Color) = (0.15, 0.9, 0.7, 0.35) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 position : SV_POSITION; float light : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.light = 0.65 + 0.35 * abs(UnityObjectToWorldNormal(v.normal).y);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target { return float4(_Color.rgb * i.light, _Color.a); }
            ENDCG
        }
    }
}
