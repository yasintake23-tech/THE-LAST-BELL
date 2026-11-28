Shader "TheLastBell/CRTScreening"
{
    Properties { _MainTex ("Signal", 2D) = "black" {} _ScreenRatio ("CRT ratio", Float) = 1.31 }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragment
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _ScreenRatio;
            fixed4 fragment(v2f_img input) : SV_Target
            {
                float2 uv = input.uv;
                uv.y = (uv.y - 0.5) * (1.777778 / _ScreenRatio) + 0.5;
                half visible = step(0.0, uv.y) * step(uv.y, 1.0);
                half scan = 0.96 + 0.025 * cos(uv.y * 907.0);
                half flicker = 0.98 + 0.02 * sin(_Time.y * 8.0);
                half3 picture = tex2D(_MainTex, saturate(uv)).rgb * scan * flicker;
                return fixed4(lerp(half3(0.007, 0.009, 0.011), picture, visible), 1);
            }
            ENDCG
        }
    }
}
