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
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _DayColor;
            fixed4 _NightColor;
            float _TerminatorSoftness;
            float4 _SunDirection;

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
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 normal = normalize(i.worldNormal);
                float3 sunDir = normalize(_SunDirection.xyz);
                float NdotL = dot(normal, sunDir);

                float dayAmount = smoothstep(0.0, _TerminatorSoftness, NdotL);

                fixed4 day = _DayColor;
                fixed4 night = _NightColor;
                fixed alpha = lerp(night.a, day.a, dayAmount);
                fixed3 color = lerp(night.rgb, day.rgb, dayAmount);

                return fixed4(color, alpha);
            }
            ENDCG
        }
    }
}
