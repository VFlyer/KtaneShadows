Shader "KT/Custom/Glitch" {
	Properties {
		_Color ("Tint", Color) = (1,1,1,1)
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		Pass
		{
			CGPROGRAM
			#include "UnityCG.cginc"

			#pragma vertex vert
			#pragma fragment frag

			uniform float4 _Color;

			struct vert2frag {
				//float4 pos : SV_POSITION;
				float2 texcoord : TEXCOORD0;
			};
		
			struct frag2screen {
				float4 color : COLOR;
			};

			struct input {
				 float4 pos : POSITION;
				 half2 uv : TEXCOORD0;
			};

			float random (float2 st) {
				//return st.x;
				float OUT = sin(dot(floor(abs(st.xy)),float2(12.9898,78.233)))*43758.5453123;
				return abs(OUT) - floor(abs(OUT));
			}

			vert2frag vert (
                float4 vertex : POSITION, // vertex position input
                float2 uv : TEXCOORD0, // texture coordinate input
                out float4 outpos : SV_POSITION // clip space position output
                )
            {
                vert2frag o;
                o.texcoord = uv;
                outpos = UnityObjectToClipPos(vertex);
                return o;
            }

			fixed4 frag (vert2frag i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
            {
                fixed4 c = random(screenPos / 10.0).xxxx;
				c.a = 1;
                return c;
            }
			ENDCG
		}
	}
}
