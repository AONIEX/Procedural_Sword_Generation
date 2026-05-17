Shader "Custom/Engraving"
{
    Properties
    {
        _MainTex ("Base (RFloat)", 2D) = "white" {}
        _DoBrush ("Do Brush Pass", Float) = 0
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
            float _DoBrush;
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

            float4 frag(v2f i) : SV_Target
            {
                float baseValue = tex2D(_MainTex, i.uv).r;

                // DISPLAY MODE
                if (_DoBrush < 0.5)
                    return float4(baseValue, baseValue, baseValue, 1);

                // BRUSH MODE
                float2 diff = i.uv - _BrushPos.xy;
                float dist = length(diff);
                float radius = _Size / 1024.0;

                float mask = saturate(1.0 - dist / radius);

                // PURE BLACK STAMP (no greys)
                float brush = (mask > 0.0) ? 0.0 : 1.0;

                float result = min(baseValue, brush);

                return float4(result, result, result, 1);
            }
            ENDCG
        }
    }
}
