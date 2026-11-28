Shader "TheLastBell/VergeFoliage"
{
    Properties { _MainTex ("Leaf and vein", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct Output { float4 position : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; UNITY_FOG_COORDS(1) };
            Output vert(Input input)
            {
                Output output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color;
                UNITY_TRANSFER_FOG(output, output.position);
                return output;
            }
            fixed4 frag(Output input) : SV_Target
            {
                fixed4 leaf = tex2D(_MainTex, input.uv);
                clip(leaf.a - 0.48);
                leaf.rgb *= input.color.rgb * 0.61;
                UNITY_APPLY_FOG(input.fogCoord, leaf);
                return leaf;
            }
            ENDCG
        }
    }
}
