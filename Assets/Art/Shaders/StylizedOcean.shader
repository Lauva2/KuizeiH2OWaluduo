Shader "RaftGame/Stylized Ocean"
{
    Properties
    {
        _DeepColor ("Deep sea blue", Color) = (0.02, 0.22, 0.43, 1)
        _ShallowColor ("Shallow sea turquoise", Color) = (0.16, 0.68, 0.63, 1)
        _ShallowArea ("Shallow center X/Z, radii X/Z", Vector) = (-28, 12, 29, 40)
        _DepthTransition ("Transition width, boundary variation", Vector) = (24, 4, 0, 0)
        _FoamColor ("Foam", Color) = (0.82, 0.96, 0.91, 1)
        _ReflectionColor ("Sky reflection", Color) = (0.29, 0.67, 0.80, 1)
        _WaveA ("Wave A: direction X/Z, length, amplitude", Vector) = (1, 0.35, 12, 0.14)
        _WaveB ("Wave B", Vector) = (-0.5, 1, 6.5, 0.075)
        _WaveC ("Wave C", Vector) = (0.2, 1, 3.1, 0.03)
        _WaveSpeed ("Wave speed", Float) = 0.45
        _RippleStrength ("Ripple strength", Range(0, 1)) = 0.45
        _RippleScale ("Ripple scale", Float) = 1.15
        _CrestFoam ("Sparse crest foam", Range(0, 1)) = 0.12
        _ContactFoamWidth ("Contact foam width", Float) = 0.4
        _ContactFoamStrength ("Contact foam opacity", Range(0, 1)) = 0.75
        _SunGlint ("Sun glint", Range(0, 1)) = 0.6
        _ReflectionStrength ("Soft sky reflection", Range(0, 1)) = 0.38
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
            Stencil { Ref 1 Comp Always Pass Replace }

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
            float _ContactFoamWidth, _ContactFoamStrength, _SunGlint, _OceanTime, _ReflectionStrength;

            struct appdata { float4 vertex : POSITION; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPosition : TEXCOORD0;
                float4 screenPosition : TEXCOORD1;
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
                // Analytic fragment normals avoid faceted highlights on the large, coarse ocean mesh.
                // The displaced geometry and CPU buoyancy retain the same three wave equations.
                float height = 0;
                float2 slope = 0;
                EvaluateWave(_WaveA, p, time, height, slope);
                EvaluateWave(_WaveB, p, time, height, slope);
                EvaluateWave(_WaveC, p, time, height, slope);

                float2 direction = normalize(_WaveA.xy);
                float2 tangent = float2(-direction.y, direction.x);
                float2 uv = float2(dot(p, direction), dot(p, tangent)) * _RippleScale;
                float broad = Noise(p * 0.09 + float2(time * 0.016, -time * 0.012));
                float breakup = Noise(uv * float2(0.38, 0.52) + float2(-time * 0.045, time * 0.025));
                float warp = Noise(uv * 0.27 + float2(time * 0.025, -time * 0.018));
                float phaseA = uv.x * 1.9 + sin(uv.y * 0.85 + time * 0.22) * 0.8 + warp * 3.2 - time * 0.7;
                float phaseB = (uv.x * 0.65 + uv.y * 0.76) * 3.3 + warp * 2.4 + time * 0.55;
                // Filter out subpixel ripples when zoomed out, including their specular response.
                float rippleVisibility = 1 - smoothstep(0.35, 1.2, max(fwidth(phaseA), fwidth(phaseB)));
                float2 microSlope = (direction * cos(phaseA) +
                    (direction * 0.65 + tangent * 0.76) * cos(phaseB) * 0.45) *
                    (_RippleStrength * 0.08 * rippleVisibility * lerp(0.18, 1, smoothstep(0.28, 0.72, breakup)));
                float3 normal = normalize(float3(-slope.x - microSlope.x, 1, -slope.y - microSlope.y));
                float3 viewDirection = normalize(lerp(_WorldSpaceCameraPos.xyz - i.worldPosition,
                    UNITY_MATRIX_V[2].xyz, unity_OrthoParams.w));
                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                float diffuse = saturate(dot(normal, lightDirection));
                float facing = saturate(dot(normal, viewDirection));
                float fresnel = 0.035 + 0.965 * pow(1 - facing, 5);
                float heightRange = max(0.001, abs(_WaveA.w) + abs(_WaveB.w) + abs(_WaveC.w));
                float swell = saturate(0.5 + height / (heightRange * 2));
                float deepWater = DeepWaterBlend(p);
                float3 color = lerp(_ShallowColor.rgb, _DeepColor.rgb, deepWater);
                // Gentle color variation keeps the water readable behind crew and floating supplies.
                color *= 0.93 + broad * 0.08 + swell * 0.08;
                UNITY_LIGHT_ATTENUATION(attenuation, i, i.worldPosition);
                color *= (0.86 + diffuse * 0.14) * lerp(0.78, 1, attenuation);
                float3 reflected = reflect(-viewDirection, normal);
                float skyBand = smoothstep(0.15, 0.85, reflected.y);
                float reflection = _ReflectionStrength * (0.10 + skyBand * 0.20 + fresnel * 0.65);
                color = lerp(color, _ReflectionColor.rgb, reflection);

                // Broad translucent ripple shoulders with sparse broken tips, rather than white hatching.
                float ripple = pow(saturate(sin(phaseA) * 0.5 + 0.5), 10);
                float rippleMask = smoothstep(0.48, 0.78, breakup);
                float waterLine = ripple * rippleMask * _RippleStrength * rippleVisibility;
                color = lerp(color, _ReflectionColor.rgb, waterLine * 0.16);
                float glimmer = ripple * ripple * smoothstep(0.62, 0.84, breakup);
                glimmer += pow(saturate(sin(phaseB) * 0.5 + 0.5), 28) * smoothstep(0.72, 0.9, breakup) * 0.35;
                color = lerp(color, _FoamColor.rgb, glimmer * _RippleStrength * 0.10 * rippleVisibility);

                float3 halfDirection = normalize(lightDirection + viewDirection);
                float highlight = saturate(dot(normal, halfDirection));
                float specular = (pow(highlight, 64) * 0.004 + pow(highlight, 220) * 0.06 * glimmer) * _SunGlint;
                color += _LightColor0.rgb * specular * attenuation;

                float crest = smoothstep(0.82, 0.98, swell) * ripple * smoothstep(0.62, 0.86, breakup);
                color = lerp(color, _FoamColor.rgb, crest * _CrestFoam * rippleVisibility);
                float rawDepth = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPosition));
                float depthDifference = max(0, SceneEyeDepth(rawDepth) - i.eyeDepth);
                float edge = 1 - smoothstep(0.02, max(0.03, _ContactFoamWidth), depthDifference);
                float contact = edge * (0.68 + breakup * 0.32) * _ContactFoamStrength;
                // A second soft band breathes around hulls, while the waterline stays thin and readable.
                float foamBand = exp(-abs(depthDifference - _ContactFoamWidth * 0.50) * 10 / max(0.05, _ContactFoamWidth));
                contact += foamBand * rippleMask * _ContactFoamStrength * 0.18;
                color = lerp(color, _FoamColor.rgb, saturate(contact));
                UNITY_APPLY_FOG(i.fogCoord, color);
                return fixed4(color, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
