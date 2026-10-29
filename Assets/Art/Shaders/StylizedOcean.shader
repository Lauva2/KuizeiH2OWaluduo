Shader "RaftGame/Stylized Ocean"
{
    Properties
    {
        _DeepColor ("Deep sea blue", Color) = (0.025, 0.16, 0.34, 1)
        _ShallowColor ("Shallow sea turquoise", Color) = (0.12, 0.70, 0.66, 1)
        _ShallowArea ("Shallow center X/Z, radii X/Z", Vector) = (-28, 12, 29, 40)
        _DepthTransition ("Transition width, boundary variation", Vector) = (14, 4, 0, 0)
        _FoamColor ("Foam", Color) = (0.82, 0.96, 0.91, 1)
        _ReflectionColor ("Sky reflection", Color) = (0.48, 0.80, 0.83, 1)
        _WaveA ("Wave A: direction X/Z, length, amplitude", Vector) = (1, 0.35, 12, 0.14)
        _WaveB ("Wave B", Vector) = (-0.5, 1, 6.5, 0.075)
        _WaveC ("Wave C", Vector) = (0.2, 1, 3.1, 0.03)
        _WaveSpeed ("Wave speed", Float) = 0.45
        _RippleStrength ("Ripple strength", Range(0, 1)) = 0.32
        _RippleScale ("Ripple scale", Float) = 0.8
        _CrestFoam ("Sparse crest foam", Range(0, 1)) = 0.22
        _ContactFoamWidth ("Contact foam width", Float) = 0.5
        _ContactFoamStrength ("Contact foam opacity", Range(0, 1)) = 0.75
        _SunGlint ("Sun glint", Range(0, 1)) = 0.32
        [HideInInspector] _OceanTime ("Managed time (-1 = editor time)", Float) = -1
    }

    SubShader
    {
        // Draw after opaque objects so their depth can define the foam at the waterline.
        Tags { "Queue" = "Transparent-10" "RenderType" = "Transparent" }
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            ZWrite On
            Cull Back

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _DeepColor, _ShallowColor, _FoamColor, _ReflectionColor;
            float4 _WaveA, _WaveB, _WaveC;
            float4 _ShallowArea, _DepthTransition;
            float _WaveSpeed, _RippleStrength, _RippleScale, _CrestFoam;
            float _ContactFoamWidth, _ContactFoamStrength, _SunGlint, _OceanTime;

            struct appdata { float4 vertex : POSITION; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPosition : TEXCOORD0;
                float4 screenPosition : TEXCOORD1;
                float3 waveNormal : TEXCOORD2;
                float eyeDepth : TEXCOORD3;
                SHADOW_COORDS(4)
                UNITY_FOG_COORDS(5)
            };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), f.x), f.y);
            }

            float OceanTime() { return _OceanTime < 0 ? _Time.y : _OceanTime; }

            // World-fixed bathymetry, independent of animated surface noise or the camera depth texture.
            float DeepWaterBlend(float2 p)
            {
                float2 radii = max(float2(1, 1), _ShallowArea.zw);
                float edgeDistance = (length((p - _ShallowArea.xy) / radii) - 1) * min(radii.x, radii.y);
                float variation = (sin(p.x * 0.085 + p.y * 0.035) +
                    0.5 * sin(p.y * 0.13 - p.x * 0.04)) * _DepthTransition.y;
                float width = max(0.1, _DepthTransition.x);
                return smoothstep(-width * 0.5, width * 0.5, edgeDistance + variation);
            }

            void EvaluateWave(float4 wave, float2 p, float time, inout float height, inout float2 slope)
            {
                float2 direction = normalize(wave.xy);
                float frequency = UNITY_TWO_PI / max(0.5, wave.z);
                float phase = frequency * dot(direction, p) - time * _WaveSpeed * sqrt(9.81 * frequency);
                height += wave.w * sin(phase);
                slope += direction * frequency * wave.w * cos(phase);
            }

            v2f vert(appdata v)
            {
                v2f o;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                float height = 0;
                float2 slope = 0;
                float time = OceanTime();
                EvaluateWave(_WaveA, world.xz, time, height, slope);
                EvaluateWave(_WaveB, world.xz, time, height, slope);
                EvaluateWave(_WaveC, world.xz, time, height, slope);
                world.y += height;
                v.vertex = mul(unity_WorldToObject, float4(world, 1));
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPosition = world;
                o.waveNormal = normalize(float3(-slope.x, 1, -slope.y));
                o.screenPosition = ComputeScreenPos(o.pos);
                o.eyeDepth = -UnityObjectToViewPos(v.vertex).z;
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float SceneEyeDepth(float raw)
            {
                // LinearEyeDepth alone is only valid for perspective projection.
                float orthographicRaw = raw;
                #if defined(UNITY_REVERSED_Z)
                    orthographicRaw = 1 - orthographicRaw;
                #endif
                float orthographicDepth = lerp(_ProjectionParams.y, _ProjectionParams.z, orthographicRaw);
                return lerp(LinearEyeDepth(raw), orthographicDepth, unity_OrthoParams.w);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float time = OceanTime();
                float2 p = i.worldPosition.xz;
                float broad = Noise(p * 0.075 + float2(time * 0.012, -time * 0.009));
                float detail = Noise(p * 0.23 - time * 0.018);
                float2 rippleUV = p * _RippleScale;
                float warp = Noise(rippleUV * 0.22 + time * 0.025);
                float ripplePhase = rippleUV.x * 1.65 + sin(rippleUV.y * 1.2 + time * 0.25) * 1.4 + warp * 3;
                float crossingPhase = rippleUV.y * 2.1 + sin(rippleUV.x * 0.8 - time * 0.18) - time * 0.3;
                float2 microSlope = float2(cos(ripplePhase), cos(crossingPhase)) * _RippleStrength * 0.16;
                float3 normal = normalize(i.waveNormal + float3(microSlope.x, 0, microSlope.y));
                float3 viewDirection = normalize(lerp(_WorldSpaceCameraPos.xyz - i.worldPosition,
                    UNITY_MATRIX_V[2].xyz, unity_OrthoParams.w));
                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                float diffuse = saturate(dot(normal, lightDirection));
                float fresnel = pow(1 - saturate(dot(normal, viewDirection)), 3);
                float heightRange = max(0.001, _WaveA.w + _WaveB.w + _WaveC.w);
                float waveHeight = i.worldPosition.y - unity_ObjectToWorld._m13;
                float swell = saturate(0.5 + waveHeight / (heightRange * 2));
                float palette = saturate(0.25 + broad * 0.24 + detail * 0.14 + swell * 0.36 +
                    (normal.x - normal.z) * 0.65);
                float deepWater = DeepWaterBlend(p);
                float3 color = lerp(_ShallowColor.rgb, _DeepColor.rgb, deepWater);
                // Swells only modulate each region's color; they never change its shallow/deep identity.
                color *= 0.86 + palette * 0.28;
                UNITY_LIGHT_ATTENUATION(attenuation, i, i.worldPosition);
                color *= (0.83 + diffuse * 0.17) * lerp(0.72, 1, attenuation);
                color = lerp(color, _ReflectionColor.rgb, fresnel * 0.45);

                // Broken, softly antialiased water ribbons rather than dense glitter/noise.
                float2 swellDirection = normalize(_WaveA.xy);
                float across = dot(p, swellDirection);
                float along = dot(p, float2(-swellDirection.y, swellDirection.x));
                float ribbonPhase = across * 2.7 + sin(along * 0.75 + warp * 3) * 0.65 + warp * 2 - time * 0.5;
                float ribbons = abs(sin(ribbonPhase));
                float lineWidth = max(fwidth(ribbons), 0.035);
                float waterLine = 1 - smoothstep(0.015, 0.015 + lineWidth * 1.3, ribbons);
                waterLine *= smoothstep(0.48, 0.76, Noise(float2(along * 0.9, across * 0.4) + time * 0.04)) * _RippleStrength;
                color = lerp(color, _FoamColor.rgb, waterLine * 0.25);

                float3 halfDirection = normalize(lightDirection + viewDirection);
                float glintMask = smoothstep(0.42, 0.78, Noise(p * 0.35 + time * 0.035));
                float specular = pow(saturate(dot(normal, halfDirection)), 160) * _SunGlint * glintMask * 0.25;
                color += _LightColor0.rgb * specular * attenuation * lerp(1, 0.45, deepWater);

                float crest = smoothstep(0.76, 0.98, swell) * smoothstep(0.45, 0.74, detail);
                color = lerp(color, _FoamColor.rgb, crest * _CrestFoam);
                float rawDepth = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPosition));
                float depthDifference = max(0, SceneEyeDepth(rawDepth) - i.eyeDepth);
                float edge = 1 - smoothstep(0.02, max(0.03, _ContactFoamWidth), depthDifference);
                float contact = edge * (0.65 + 0.35 * sin(ripplePhase + time)) * _ContactFoamStrength;
                color = lerp(color, _FoamColor.rgb, saturate(contact));
                UNITY_APPLY_FOG(i.fogCoord, color);
                return fixed4(color, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
