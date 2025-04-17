Shader "Instanced/Particle2D"
{
    Properties
    {

    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "UnityCG.cginc"

            StructuredBuffer<float2> Positions2D;
            StructuredBuffer<uint> Selected;
            float scale;
            float4 particleColor;
            float4 particleSecondColor;
            float4 particleThirdColor;
            //Texture2D<float4> ColorMap;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                //float3 color : TEXCOORD1;
            };

            v2f vert (appdata_full v, uint instanceID : SV_INSTANCEID)
            {
                bool selected = Selected[instanceID] > 0;
                bool multipleSelected = Selected[instanceID] > 1;
                float3 centerWorld = float3(Positions2D[instanceID], 0);
                float3 worldVertPos = centerWorld + mul(unity_ObjectToWorld, v.vertex * scale);
                float3 objectVertPos = mul(unity_WorldToObject, float4(worldVertPos, 1));

                v2f o;
                o.uv = float2(0, 0);
                if (selected) {
                    o.uv.x += 1.0;
                }
                if (multipleSelected) {
                    o.uv.x += 1.0;
                }
                o.uv = selected ? float2(1, 1) : float2(0, 0);
                o.pos = UnityObjectToClipPos(objectVertPos);
                //o.color = float3(1, 1, 1);

                return o;
            }

            float4 frag (v2f i) : SV_TARGET
            {
                if (i.uv.x > 1.5){
                    return particleThirdColor;
                } else if (i.uv.x > 0.5){
                    return particleSecondColor;
                } else {
                    return particleColor;
                }
            }

            ENDCG
        }
        

    }
}
