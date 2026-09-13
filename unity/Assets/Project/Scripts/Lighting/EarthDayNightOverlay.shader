Shader "Custom/EarthDayNightOverlayTransparent"
{
    Properties
    {
        _DayColor ("Day Color", Color) = (1, 1, 1, 0.1)
        _NightColor ("Night Color", Color) = (0, 0, 0, 0.4)
        _TerminatorSoftness ("Terminator Softness", Range(0.01, 0.5)) = 0.2
        _SunDirection ("Sun Direction", Vector) = (0, 1, 0, 0)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _DayColor;
            half4 _NightColor;
            float _TerminatorSoftness;
            float4 _SunDirection;
            CBUFFER_END

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                o.worldNormal = TransformObjectToWorldNormal(v.normal);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float3 normal = normalize(i.worldNormal);
                float3 sunDir = normalize(_SunDirection.xyz);
                float NdotL = dot(normal, sunDir);

                float dayAmount = smoothstep(0.0, _TerminatorSoftness, NdotL);

                half4 day = _DayColor;
                half4 night = _NightColor;
                half alpha = lerp(night.a, day.a, dayAmount);
                half3 color = lerp(night.rgb, day.rgb, dayAmount);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
