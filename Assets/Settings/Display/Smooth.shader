Shader "Hidden/Smooth"
{
    Properties { 
        _MainTex ("Texture", 2D) = "white" {}
        _Radius ("Radius", Int) = 2
        _Sigma ("Sigma", Float) = 1.0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct v2f { 
                float2 uv : TEXCOORD0; 
                float4 vertex : SV_POSITION; 
            };

            v2f vert (appdata_img v) {
                v2f o; 
                o.vertex = UnityObjectToClipPos(v.vertex); 
                o.uv = v.texcoord;
                return o;
            }

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            int _Radius;
            float _Sigma;

            float gaussian(float x, float y, float sigma2) {
                return exp(-(x*x + y*y) / (2.0 * sigma2));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float sigma2 = _Sigma*_Sigma;

                float4 c = tex2D(_MainTex, i.uv);
                
                float2 dx = float2(_MainTex_TexelSize.x, 0);
                float2 dy = float2(0, _MainTex_TexelSize.y);

                float4 sum = float4(0,0,0,0);
                float totalWeight = 0.0;
                float maxNeighbor = 0.0;
                for(int x = -_Radius; x <= _Radius; x++)
                {
                    for(int y = -_Radius; y <= _Radius; y++)
                    {
                        if (x==0 && y==0) {continue;}

                        float2 offset = float2(x, y) * _MainTex_TexelSize.xy;
                        float weight = gaussian(x, y, sigma2);
                        
                        float4 neighbor = tex2D(_MainTex, i.uv + offset);

                        sum += neighbor * weight;
                        totalWeight += weight;
                        
                        maxNeighbor = max(maxNeighbor, length(neighbor.rgb));
                    }
                }

                if (length(c.rgb) > 0.1 && maxNeighbor < 0.1){
                    return float4(0,0,0,1); 
                }

                float4 weightedAvg = sum / totalWeight;
                return weightedAvg;
            }
            ENDCG
        }
    }
}