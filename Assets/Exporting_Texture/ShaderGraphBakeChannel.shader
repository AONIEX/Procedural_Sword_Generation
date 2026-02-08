Shader "Hidden/ShaderGraphBakeChannel"
{
    SubShader
    {
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            sampler2D _MainTex;
            float4 _Color;

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
                float4 tex = tex2D(_MainTex, i.uv);
                return tex * _Color;
            }
            ENDHLSL
        }
    }
}
