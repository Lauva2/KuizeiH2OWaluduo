Shader "RaftGame/Submerged Fish"
{
    Properties
    {
        _FishColor ("Fish color", Color) = (.7, .83, .74, 1)
        _WaterTint ("Water tint", Color) = (.1, .5, .5, 1)
        _Daylight ("Daylight", Range(0, 1)) = 1
        _FishDepth ("Submerged depth", Float) = .55
        _OceanTime ("Managed ocean time", Float) = 0
        _SurfaceDetails ("Wave direction / ripple scale and strength", Vector) = (1, .35, 1.15, .45)
        _ReflectionColor ("Water reflection", Color) = (.29, .67, .8, 1)
        _FoamColor ("Water highlights", Color) = (.82, .96, .91, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            // Water stamps only its visible pixels. Fish stay hidden by the raft and other opaque objects.
            Stencil { Ref 1 Comp Equal Pass Keep }
            ZTest Always
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float3 normal : NORMAL; };
            struct v2f { float4 position : SV_POSITION; float4 color : COLOR; float3 world : TEXCOORD0; float3 normal : TEXCOORD1; };
            float4 _FishColor, _WaterTint, _SurfaceDetails, _ReflectionColor, _FoamColor;
            float _Daylight, _FishDepth, _OceanTime;
            v2f vert(appdata v)
            { v2f o; o.position = UnityObjectToClipPos(v.vertex); o.color = v.color; o.world = mul(unity_ObjectToWorld, v.vertex).xyz; o.normal = UnityObjectToWorldNormal(v.normal); return o; }
            float Hash(float2 p)
            { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float Noise(float2 p)
            {
                float2 cell = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x), lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), f.x), f.y);
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 view = normalize(lerp(_WorldSpaceCameraPos.xyz - i.world, UNITY_MATRIX_V[2].xyz, unity_OrthoParams.w));
                float rim = pow(1 - saturate(abs(dot(normalize(i.normal), view))), 2);
                // Red light is absorbed first; deeper fish take on their local, day-lit water color.
                float3 absorption = exp(-_FishDepth * float3(.7, .25, .12));
                float3 fish = _FishColor.rgb * i.color.rgb * absorption * lerp(.32, 1, _Daylight);
                float3 color = lerp(fish, _WaterTint.rgb, saturate(.36 + _FishDepth * .16 + rim * .12));
                // Reproject onto the water above the fish. Match the sea's sparse surface pattern,
                // so reflections cross the silhouette instead of making the fish look surface-mounted.
                float2 p = i.world.xz + view.xz * (_FishDepth / max(.3, view.y));
                float2 direction = normalize(_SurfaceDetails.xy), tangent = float2(-direction.y, direction.x);
                float2 uv = float2(dot(p, direction), dot(p, tangent)) * _SurfaceDetails.z;
                float breakup = Noise(uv * float2(.38, .52) + float2(-_OceanTime * .045, _OceanTime * .025));
                float warp = Noise(uv * .27 + float2(_OceanTime * .025, -_OceanTime * .018));
                float phase = uv.x * 1.9 + sin(uv.y * .85 + _OceanTime * .22) * .8 + warp * 3.2 - _OceanTime * .7;
                float visible = 1 - smoothstep(.35, 1.2, fwidth(phase));
                float ripple = pow(saturate(sin(phase) * .5 + .5), 10);
                float waterLine = ripple * smoothstep(.48, .78, breakup) * _SurfaceDetails.w * visible;
                color = lerp(color, _WaterTint.rgb, .18 + rim * .15 + waterLine * .3);
                color = lerp(color, _ReflectionColor.rgb, waterLine * .16);
                color = lerp(color, _FoamColor.rgb, ripple * ripple * smoothstep(.62, .84, breakup) * _SurfaceDetails.w * .10 * visible);
                float opacity = i.color.a * .74 * exp(-_FishDepth * .18) * (1 - rim * .22);
                return fixed4(color, opacity);
            }
            ENDCG
        }
    }
    Fallback Off
}
