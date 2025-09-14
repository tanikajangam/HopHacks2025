Shader "Custom/Quest2VolumeRenderer"
{
    Properties
    {
        _VolumeTexture ("Volume Texture", 3D) = "white" {}
        _StepSize ("Step Size", Range(0.02, 0.1)) = 0.08  // Larger steps for Quest 2
        _MaxSteps ("Max Steps", Range(10, 100)) = 32      // Fewer steps for performance
        _IntensityScale ("Intensity Scale", Range(0.1, 3.0)) = 1.5
        _MinValue ("Min Value", Float) = 0.0
        _MaxValue ("Max Value", Float) = 1.0
        _MinColor ("Min Color", Color) = (0, 0, 0, 0)
        _MaxColor ("Max Color", Color) = (1, 1, 1, 1)
        _MinAlpha ("Min Alpha", Range(0, 1)) = 0.1
        _MaxAlpha ("Max Alpha", Range(0, 1)) = 0.8
        _UseTransparency ("Use Transparency", Float) = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0  // Ensure Quest 2 compatibility
            #include "UnityCG.cginc"

            sampler3D _VolumeTexture;
            float _StepSize;
            int _MaxSteps;
            float _IntensityScale;
            float _MinValue;
            float _MaxValue;
            float4 _MinColor;
            float4 _MaxColor;
            float _MinAlpha;
            float _MaxAlpha;
            float _UseTransparency;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 localPos : TEXCOORD0;
                float3 cameraLocalPos : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.localPos = v.vertex.xyz + 0.5;
                float4 cameraObjectPos = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.0));
                o.cameraLocalPos = cameraObjectPos.xyz + 0.5;
                return o;
            }

            float2 RayBoxIntersect(float3 rayOrigin, float3 rayDir)
            {
                float3 boxMin = float3(0, 0, 0);
                float3 boxMax = float3(1, 1, 1);
                
                // Handle division by zero
                float3 invRayDir = 1.0 / (rayDir + 0.0001);
                float3 t1 = (boxMin - rayOrigin) * invRayDir;
                float3 t2 = (boxMax - rayOrigin) * invRayDir;
                
                float3 tMin = min(t1, t2);
                float3 tMax = max(t1, t2);
                
                float tNear = max(max(tMin.x, tMin.y), tMin.z);
                float tFar = min(min(tMax.x, tMax.y), tMax.z);
                
                return float2(max(tNear, 0.0), tFar);
            }

            float4 SampleVolume(float3 uvw)
            {
                // Sample the 3D texture - handle different formats
                float4 texSample = tex3D(_VolumeTexture, uvw);
                float value = texSample.r;  // Use red channel
                
                // Debug: Ensure we're getting valid samples
                if (value < 0.001) return float4(0, 0, 0, 0);
                
                // Normalize the value between min and max
                float normalizedValue = saturate((_MaxValue - _MinValue) > 0.001 ? 
                    (value - _MinValue) / (_MaxValue - _MinValue) : value);
                
                // Apply color scheme (lerp between min and max colors)
                float4 color = lerp(_MinColor, _MaxColor, normalizedValue);
                
                // Apply transparency mapping if enabled
                if (_UseTransparency > 0.5)
                {
                    color.a = lerp(_MinAlpha, _MaxAlpha, normalizedValue);
                }
                else
                {
                    color.a = normalizedValue;
                }
                
                return color;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 rayDir = normalize(i.localPos - i.cameraLocalPos);
                float3 rayOrigin = i.cameraLocalPos;
                
                float2 intersections = RayBoxIntersect(rayOrigin, rayDir);
                float tNear = intersections.x;
                float tFar = intersections.y;
                
                if (tFar <= tNear) return float4(0, 0, 0, 0);
                
                float4 accumulatedColor = float4(0, 0, 0, 0);
                float rayLength = tFar - tNear;
                int steps = min(_MaxSteps, int(rayLength / _StepSize));
                
                // Simplified raymarching for Quest 2 performance
                [unroll(32)]  // Limit unrolling for mobile
                for (int step = 0; step < steps; step++)
                {
                    if (step >= _MaxSteps) break;  // Safety check
                    
                    float t = tNear + (float(step) / float(steps)) * rayLength;
                    float3 samplePos = rayOrigin + t * rayDir;
                    
                    // Bounds check
                    if (any(samplePos < 0.0) || any(samplePos > 1.0))
                        continue;
                    
                    float4 sampleColor = SampleVolume(samplePos);
                    
                    // Simplified alpha blending
                    float alpha = sampleColor.a * _StepSize * _IntensityScale;
                    alpha = saturate(alpha);
                    
                    accumulatedColor.rgb += sampleColor.rgb * alpha * (1.0 - accumulatedColor.a);
                    accumulatedColor.a += alpha * (1.0 - accumulatedColor.a);
                    
                    // Early ray termination for performance
                    if (accumulatedColor.a > 0.9) break;
                }
                
                return accumulatedColor;
            }
            ENDCG
        }
    }
    
    // Fallback for very low-end devices
    FallBack "Mobile/Diffuse"
}