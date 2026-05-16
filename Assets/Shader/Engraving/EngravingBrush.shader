Shader "Custom/EngravingBrush"
{
    Properties
    {
        _MainTex ("Base (RFloat)", 2D) = "white" {}
        _Depth ("Depth", Float) = 1
        _Size ("Size", Float) = 20
        _BrushPos ("BrushPos", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            ZWrite Off
            Cull Off
            Blend Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            sampler2D _MainTex;
            float _Depth;
            float _Size;
            float4 _BrushPos;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float frag(v2f i) : SV_Target
            {
                // Read existing pixel
                float baseValue = tex2D(_MainTex, i.uv).r;

                // Compute brush mask
                float2 diff = i.uv - _BrushPos.xy;
                float dist = length(diff);
                float radius = _Size / 1024.0;
                float mask = saturate(1.0 - dist / radius);

                // Draw black on white
                float brush = lerp(1.0, 0.0, mask);

                // Combine: take the darker value (black wins)
                return min(baseValue, brush);
            }
            ENDCG
        }
    }
}
