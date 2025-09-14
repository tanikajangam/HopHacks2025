Shader "Custom/FixedVolumeRenderer"
{
    Properties
    {
        _StepSize ("Step Size", Range(0.01, 0.2)) = 0.05
        _MaxSteps ("Max Steps", Range(10, 200)) = 50
        _IntensityScale ("Intensity Scale", Range(0.1, 5.0)) = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha  // Standard alpha blending
        ZWrite Off
        Cull Off  // Render both front AND back faces

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _StepSize;
            int _MaxSteps;
            float _IntensityScale;

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
                
                // Convert positions to local object space
                o.localPos = v.vertex.xyz + 0.5;  // Convert cube (-0.5 to 0.5) to (0 to 1)
                
                // Camera position in local object space
                float4 cameraObjectPos = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.0));
                o.cameraLocalPos = cameraObjectPos.xyz + 0.5;  // Also convert to 0-1 range
                
                return o;
            }

            // Ray-Box intersection to find entry and exit points
            float2 RayBoxIntersect(float3 rayOrigin, float3 rayDir)
            {
                float3 boxMin = float3(0, 0, 0);
                float3 boxMax = float3(1, 1, 1);
                
                float3 invRayDir = 1.0 / rayDir;
                float3 t1 = (boxMin - rayOrigin) * invRayDir;
                float3 t2 = (boxMax - rayOrigin) * invRayDir;
                
                float3 tMin = min(t1, t2);
                float3 tMax = max(t1, t2);
                
                float tNear = max(max(tMin.x, tMin.y), tMin.z);
                float tFar = min(min(tMax.x, tMax.y), tMax.z);
                
                return float2(max(tNear, 0.0), tFar);
            }

            float4 GetGridCellColor(int3 gridPos)
            {
                gridPos = clamp(gridPos, 0, 2);
                int cellIndex = gridPos.x + gridPos.y * 3 + gridPos.z * 9;
                
                float4 colors[27];
                
                // Bottom layer (z=0)
                colors[0]  = float4(1.0, 0.0, 0.0, 0.5);  // Red
                colors[1]  = float4(0.0, 1.0, 0.0, 0.5);  // Green  
                colors[2]  = float4(0.0, 0.0, 1.0, 0.5);  // Blue
                colors[3]  = float4(1.0, 1.0, 0.0, 0.5);  // Yellow
                colors[4]  = float4(1.0, 0.0, 1.0, 0.5);  // Magenta
                colors[5]  = float4(0.0, 1.0, 1.0, 0.5);  // Cyan
                colors[6]  = float4(0.5, 0.5, 0.0, 0.5);  // Olive
                colors[7]  = float4(0.5, 0.0, 0.5, 0.5);  // Purple
                colors[8]  = float4(0.0, 0.5, 0.5, 0.5);  // Teal
                
                // Middle layer (z=1) - BRIGHT CENTER
                colors[9]  = float4(1.0, 0.5, 0.0, 0.6);  // Orange
                colors[10] = float4(0.5, 1.0, 0.0, 0.6);  // Lime
                colors[11] = float4(0.0, 0.5, 1.0, 0.6);  // Sky Blue
                colors[12] = float4(1.0, 0.0, 0.5, 0.6);  // Pink
                colors[13] = float4(1.0, 1.0, 0.0, 0.9);  // BRIGHT YELLOW CENTER!
                colors[14] = float4(0.2, 0.8, 0.2, 0.6);  // Light Green
                colors[15] = float4(0.8, 0.2, 0.2, 0.6);  // Light Red
                colors[16] = float4(0.2, 0.2, 0.8, 0.6);  // Light Blue
                colors[17] = float4(0.6, 0.3, 0.9, 0.6);  // Violet
                
                // Top layer (z=2)
                colors[18] = float4(0.9, 0.9, 0.1, 0.4);  // Bright Yellow
                colors[19] = float4(0.1, 0.9, 0.9, 0.4);  // Bright Cyan
                colors[20] = float4(0.9, 0.1, 0.9, 0.4);  // Bright Magenta
                colors[21] = float4(0.3, 0.7, 0.1, 0.4);  // Forest Green
                colors[22] = float4(0.7, 0.3, 0.1, 0.4);  // Brown
                colors[23] = float4(0.1, 0.3, 0.7, 0.4);  // Navy Blue
                colors[24] = float4(0.5, 0.5, 0.5, 0.4);  // Gray
                colors[25] = float4(0.8, 0.8, 0.8, 0.4);  // Light Gray
                colors[26] = float4(0.2, 0.2, 0.2, 0.4);  // Dark Gray
                
                return colors[cellIndex];
            }

            float4 frag (v2f i) : SV_Target
            {
                // === PROPER RAY SETUP ===
                float3 rayDir = normalize(i.localPos - i.cameraLocalPos);
                float3 rayOrigin = i.cameraLocalPos;
                
                // Find where ray enters and exits the volume box
                float2 intersections = RayBoxIntersect(rayOrigin, rayDir);
                float tNear = intersections.x;
                float tFar = intersections.y;
                
                // If no intersection, return transparent
                if (tFar <= tNear) return float4(0, 0, 0, 0);
                
                // === RAYMARCHING ===
                float4 accumulatedColor = float4(0, 0, 0, 0);
                float rayLength = tFar - tNear;
                int steps = min(_MaxSteps, int(rayLength / _StepSize));
                
                for (int step = 0; step < steps; step++)
                {
                    float t = tNear + (float(step) / float(steps)) * rayLength;
                    float3 samplePos = rayOrigin + t * rayDir;
                    
                    // Make sure we're in bounds
                    if (any(samplePos < 0.0) || any(samplePos > 1.0))
                        continue;
                    
                    // Sample grid
                    int3 gridPos = int3(floor(samplePos * 3.0));
                    float4 sampleColor = GetGridCellColor(gridPos);
                    
                    // Alpha blend (front to back)
                    float alpha = sampleColor.a * _StepSize * _IntensityScale;
                    alpha = saturate(alpha);
                    
                    accumulatedColor.rgb += sampleColor.rgb * alpha * (1.0 - accumulatedColor.a);
                    accumulatedColor.a += alpha * (1.0 - accumulatedColor.a);
                    
                    // Early ray termination
                    if (accumulatedColor.a > 0.95) break;
                }
                
                return accumulatedColor;
            }
            ENDCG
        }
    }
}