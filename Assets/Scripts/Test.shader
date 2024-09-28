Shader "Custom/InstancedWhiteQuad"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            StructuredBuffer<float4x4> _InstanceMatrices;

            sampler2D _MainTex;

            v2f vert(appdata v, uint id : SV_InstanceID)
            {
                v2f o;
                float4x4 instanceMatrix = _InstanceMatrices[id];
                o.pos = UnityObjectToClipPos(mul(instanceMatrix, v.vertex));
                
                o.uv = v.uv;
                return o;
            }
            
            CBUFFER_START( UnityPerMaterial )
                half4 _Color;
            CBUFFER_END

            fixed4 frag(v2f i) : SV_Target
            {
                // Sample the texture using the UV coordinates
                fixed4 texColor = tex2D(_MainTex, i.uv);
                // Multiply the texture color by the instance color
                // return texColor * _Color;
                // texColor *= white
                return texColor;
            }
            ENDCG
        }
    }
}