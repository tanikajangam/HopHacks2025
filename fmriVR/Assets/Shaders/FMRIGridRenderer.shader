Shader "Custom/FMRIGridRenderer"
{
    Properties
    {
        _StepSize ("Step Size", Range(0.02, 0.1)) = 0.08
        _MaxSteps ("Max Steps", Range(10, 100)) = 32
        _IntensityScale ("Intensity Scale", Range(0.1, 3.0)) = 1.5
        _MinColor ("Min Color", Color) = (0, 0, 0, 0)
        _MaxColor ("Max Color", Color) = (1, 1, 1, 1)
        _MinAlpha ("Min Alpha", Range(0, 1)) = 0.1
        _MaxAlpha ("Max Alpha", Range(0, 1)) = 0.8
        _UseTransparency ("Use Transparency", Float) = 1.0
        
        // Grid dimensions
        _GridSizeX ("Grid Size X", Int) = 8
        _GridSizeY ("Grid Size Y", Int) = 8  
        _GridSizeZ ("Grid Size Z", Int) = 8
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
            #pragma target 3.0
            #include "UnityCG.cginc"

            float _StepSize;
            int _MaxSteps;
            float _IntensityScale;
            float4 _MinColor;
            float4 _MaxColor;
            float _MinAlpha;
            float _MaxAlpha;
            float _UseTransparency;
            
            // Grid properties
            int _GridSizeX;
            int _GridSizeY;
            int _GridSizeZ;
            
            // FMRI data array - we'll pass this from C#
            // Maximum array size for Quest 2 compatibility
            #define MAX_GRID_SIZE 512  // 8x8x8 = 512 voxels
            uniform float _FMRIData[MAX_GRID_SIZE];

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
                
                float3 invRayDir = 1.0 / (rayDir + 0.0001);
                float3 t1 = (boxMin - rayOrigin) * invRayDir;
                float3 t2 = (boxMax - rayOrigin) * invRayDir;
                
                float3 tMin = min(t1, t2);
                float3 tMax = max(t1, t2);
                
                float tNear = max(max(tMin.x, tMin.y), tMin.z);
                float tFar = min(min(tMax.x, tMax.y), tMax.z);
                
                return float2(max(tNear, 0.0), tFar);
            }

            float4 GetFMRIVoxelColor(float3 samplePos)
            {
                // Convert sample position to grid coordinates
                int3 gridPos = int3(floor(samplePos * float3(_GridSizeX, _GridSizeY, _GridSizeZ)));
                
                // Clamp to grid bounds
                gridPos = clamp(gridPos, int3(0, 0, 0), int3(_GridSizeX-1, _GridSizeY-1, _GridSizeZ-1));
                
                // Calculate array index (3D to 1D mapping)
                int index = gridPos.x + gridPos.y * _GridSizeX + gridPos.z * _GridSizeX * _GridSizeY;
                
                // Safety check for array bounds
                if (index < 0 || index >= MAX_GRID_SIZE)
                    return float4(0, 0, 0, 0);
                
                // Get normalized fMRI value (should be 0-1)
                float value = _FMRIData[index];
                
                // Skip empty voxels
                if (value < 0.001)
                    return float4(0, 0, 0, 0);
                
                // Apply color mapping
                float4 color = lerp(_MinColor, _MaxColor, value);
                
                // Apply transparency mapping if enabled
                if (_UseTransparency > 0.5)
                {
                    color.a = lerp(_MinAlpha, _MaxAlpha, value);
                }
                else
                {
                    color.a = value;
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
                
                [unroll(32)]
                for (int step = 0; step < steps; step++)
                {
                    if (step >= _MaxSteps) break;
                    
                    float t = tNear + (float(step) / float(steps)) * rayLength;
                    float3 samplePos = rayOrigin + t * rayDir;
                    
                    if (any(samplePos < 0.0) || any(samplePos > 1.0))
                        continue;
                    
                    float4 sampleColor = GetFMRIVoxelColor(samplePos);
                    
                    // Alpha blending
                    float alpha = sampleColor.a * _StepSize * _IntensityScale;
                    alpha = saturate(alpha);
                    
                    accumulatedColor.rgb += sampleColor.rgb * alpha * (1.0 - accumulatedColor.a);
                    accumulatedColor.a += alpha * (1.0 - accumulatedColor.a);
                    
                    if (accumulatedColor.a > 0.9) break;
                }
                
                return accumulatedColor;
            }
            ENDCG
        }
    }
    FallBack "Mobile/Diffuse"
}