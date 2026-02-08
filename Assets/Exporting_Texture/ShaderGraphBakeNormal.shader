
Shader "Hidden/ShaderGraphBakeNormal"
{
    SubShader
    {
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            sampler2D _NormalMap;
            float _NormalScale;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(uint id : SV_VertexID)
            {
                v2f o;
                float2 uv = float2((id << 1) & 2, id & 2);
                o.pos = float4(uv * 2 - 1, 0, 1);
                o.uv = uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 n = UnpackNormal(tex2D(_NormalMap, i.uv));
                n.xy *= _NormalScale;
                n = normalize(n);
                return float4(n * 0.5 + 0.5, 1);
            }
            ENDHLSL
        }
    }
}