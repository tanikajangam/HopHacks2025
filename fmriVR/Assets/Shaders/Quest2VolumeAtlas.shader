Shader "Custom/Quest2VolumeAtlas"
{
    Properties
    {
        _VolumeAtlas ("Volume Atlas", 2D) = "black" {}
        _AtlasSize ("Atlas Size", Vector) = (8, 8, 8, 0)  // (width, height, depth, unused)
        _StepSize ("Step Size", Range(0.05, 0.2)) = 0.1
        _MaxSteps ("Max Steps", Range(8, 64)) = 24
        _IntensityScale ("Intensity Scale", Range(0.5, 4.0)) = 2.0
        _MinColor ("Min Color", Color) = (0, 0, 0, 0)
        _MaxColor ("Max Color", Color) = (1, 1, 1, 1)
        _MinAlpha ("Min Alpha", Range(0, 1)) = 0.1
        _MaxAlpha ("Max Alpha", Range(0, 1)) = 0.8
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

            sampler2D _VolumeAtlas;
            float4 _VolumeAtlas_ST;
            float4 _AtlasSize;
            float _StepSize;
            int _MaxSteps;
            float _IntensityScale;
            float4 _MinColor;
            float4 _MaxColor;
            float _MinAlpha;
            float _MaxAlpha;

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

            // Sample 3D volume from 2D texture atlas
            float4 SampleVolumeAtlas(float3 samplePos)
            {
                // Convert 3D position to grid coordinates
                int3 gridPos = int3(floor(samplePos * _AtlasSize.xyz));
                gridPos = clamp(gridPos, int3(0, 0, 0), int3(_AtlasSize.xyz - 1));
                
                // Calculate which "slice" this Z position represents
                int sliceIndex = gridPos.z;
                
                // Calculate 2D position within the slice
                float2 posInSlice = float2(gridPos.x, gridPos.y) / _AtlasSize.xy;
                
                // Calculate atlas UV coordinates
                // Layout: slices arranged horizontally in atlas
                float sliceWidth = 1.0 / _AtlasSize.z;  // Each slice takes up this fraction of atlas width
                float2 atlasUV = float2(
                    (sliceIndex * sliceWidth) + (posInSlice.x * sliceWidth),
                    posInSlice.y
                );
                
                // Sample the 2D atlas texture
                float4 texSample = tex2D(_VolumeAtlas, atlasUV);
                float value = texSample.r;
                
                // Skip empty voxels
                if (value < 0.01) return float4(0, 0, 0, 0);
                
                // Apply color mapping
                float4 color = lerp(_MinColor, _MaxColor, value);
                color.a = lerp(_MinAlpha, _MaxAlpha, value);
                
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
                
                // Fixed loop with compile-time bounds for Quest 2 compatibility
                [unroll(64)]
                for (int step = 0; step < 64; step++)
                {
                    // Dynamic exit condition based on _MaxSteps
                    if (step >= _MaxSteps) break;
                    
                    float t = tNear + (float(step) / float(_MaxSteps)) * rayLength;
                    float3 samplePos = rayOrigin + t * rayDir;
                    
                    if (any(samplePos < 0.0) || any(samplePos > 1.0))
                        continue;
                    
                    float4 sampleColor = SampleVolumeAtlas(samplePos);
                    
                    // Alpha blending
                    float alpha = sampleColor.a * _StepSize * _IntensityScale;
                    alpha = saturate(alpha);
                    
                    accumulatedColor.rgb += sampleColor.rgb * alpha * (1.0 - accumulatedColor.a);
                    accumulatedColor.a += alpha * (1.0 - accumulatedColor.a);
                    
                    if (accumulatedColor.a > 0.85) break;
                }
                
                return accumulatedColor;
            }
            ENDCG
        }
    }
    FallBack "Mobile/Diffuse"
}