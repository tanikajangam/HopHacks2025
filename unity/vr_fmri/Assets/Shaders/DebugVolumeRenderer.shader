Shader "Custom/DebugVolumeRenderer"
{
    Properties
    {
        _TestColor ("Test Color", Color) = (1,0,0,0.5)
    }

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

            float4 _TestColor;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 volumeUV : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // Convert vertex to 0-1 UV coordinates
                o.volumeUV = v.vertex.xyz + 0.5;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // Simple test: color based on position
                float4 color = _TestColor;
                color.rgb *= i.volumeUV; // R=x, G=y, B=z
                return color;
            }
            ENDCG
        }
    }
}