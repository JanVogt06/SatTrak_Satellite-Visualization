Shader "SatTrak/TerrainTile"
{
    Properties
    {
        _MainTex ("Imagery", 2D) = "white" {}
        _Ambient ("Ambient", Range(0, 1)) = 0.18
        _Exposure ("Exposure", Range(0.5, 2)) = 1.15
        _Sink ("Sink", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half _Ambient;
            half _Exposure;
            float _Sink;
            CBUFFER_END

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex.xyz - v.normal * _Sink);
                o.uv = v.uv;
                o.normalWS = TransformObjectToWorldNormal(v.normal);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb * _Exposure;
                Light sun = GetMainLight();
                half diffuse = saturate(dot(normalize(i.normalWS), sun.direction));
                return half4(albedo * (_Ambient + diffuse * sun.color), 1);
            }
            ENDHLSL
        }
    }
}
