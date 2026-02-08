Shader "Custom/BakedPBR"
{
    Properties
    {
        _MainTex ("Base Color", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _MetallicGlossMap ("Metallic", 2D) = "white" {}
        _Metallic ("Metallic", Range(0,1)) = 1.0
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _MetallicGlossMap;
        half _Metallic;
        half _Glossiness;

        struct Input
        {
            float2 uv_MainTex;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
            o.Albedo = c.rgb;
            
            // Normal
            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
            
            // Metallic and smoothness
            fixed4 metallic = tex2D(_MetallicGlossMap, IN.uv_MainTex);
            o.Metallic = metallic.r * _Metallic;
            o.Smoothness = _Glossiness;
            
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}