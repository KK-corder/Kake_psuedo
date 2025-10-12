Shader "Custom/HandTransition"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _StartPoint("Start Point", Vector) = (0,0,0,0) //startが手首
        _EndPoint("End Point", Vector) = (1,0,0,0) //endが指先
        _Progress("Progress", Range(0,1)) = 0.5
        _Color1("Color 1", Color) = (1,1,1,1)
        _Color2("Color 2", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        
        CGPROGRAM
        // Surface shader using the Standard lighting model
        #pragma surface surf Standard fullforwardshadows

        sampler2D _MainTex;
        float4 _StartPoint;
        float4 _EndPoint;
        float _Progress;
        float4 _Color1;
        float4 _Color2;
        
        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // 始点から終点への方向ベクトルを算出
            float3 direction = normalize(_EndPoint.xyz - _StartPoint.xyz);
            float totalDistance = distance(_StartPoint.xyz, _EndPoint.xyz);

            // 現在のピクセルの world position を _StartPoint からの方向に射影
            float projection = dot(IN.worldPos - _StartPoint.xyz, direction);
            // 0～1 に正規化
            float t = saturate(projection / totalDistance);

            // _Progress を境界とし、smoothstep で境界周辺を補間
            float gradientFactor = smoothstep(_Progress - 0, _Progress + 0, t);
            float4 gradColor = lerp(_Color1, _Color2, gradientFactor);

            // テクスチャと掛け合わせた最終色を設定
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
            o.Albedo = gradColor.rgb * tex.rgb;
            o.Alpha = gradColor.a * tex.a;
        }
        ENDCG
    }
    FallBack "Standard"
}
