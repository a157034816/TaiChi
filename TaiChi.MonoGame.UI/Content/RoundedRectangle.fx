sampler2D textureSampler : register(s0);

float4x4 WorldViewProjection;
float4 Color;
float2 RectangleSize;
float CornerRadius;
float BorderWidth;
float4 BorderColor;

struct VSInput
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

struct VSOutput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

VSOutput mainVS(VSInput input)
{
    VSOutput o;
    o.Position = mul(input.Position, WorldViewProjection);
    o.TexCoord = input.TexCoord;
    return o;
}

float4 mainPS(VSOutput input) : SV_TARGET
{
    // 归一化坐标到[0,1]范围
    float2 uv = input.TexCoord;

    // 计算到各边缘的距离
    float2 distToEdge = min(uv, 1.0 - uv);

    // 计算最小距离（到最近的边缘）
    float minDist = min(distToEdge.x, distToEdge.y);

    // 计算到角落的距离
    float2 cornerDist = min(uv, 1.0 - uv) * RectangleSize;
    float distToCorner = length(cornerDist - CornerRadius);

    // 判断是否在圆角区域
    bool inCornerArea = (cornerDist.x < CornerRadius && cornerDist.y < CornerRadius);
    bool insideCorner = distToCorner <= CornerRadius;

    // 判断是否在内部
    bool insideRect = minDist >= 0.0;

    // 边框计算
    bool isBorder = false;
    if (BorderWidth > 0)
    {
        float2 innerDist = min(uv, 1.0 - uv) * RectangleSize;
        float innerCornerDist = length(innerDist - (CornerRadius - BorderWidth));
        bool inInnerCornerArea = (innerDist.x < CornerRadius - BorderWidth && innerDist.y < CornerRadius - BorderWidth);
        bool insideInnerCorner = (CornerRadius - BorderWidth) <= 0 ? true : innerCornerDist <= (CornerRadius - BorderWidth);

        float innerMinDist = min(minDist * RectangleSize.x, minDist * RectangleSize.y);
        bool insideInnerRect = innerMinDist >= BorderWidth;

        isBorder = ((inCornerArea && insideCorner && (!inInnerCornerArea || !insideInnerCorner)) ||
                   (!inCornerArea && insideRect && !insideInnerRect));
    }

    // 最终alpha计算
    float alpha = 1.0;
    if (inCornerArea)
    {
        alpha = insideCorner ? 1.0 : 0.0;
    }
    else
    {
        alpha = insideRect ? 1.0 : 0.0;
    }

    // 边框颜色
    float4 finalColor = isBorder ? BorderColor : Color;
    finalColor.a *= alpha;

    // 抗锯齿处理
    float aa = 1.0;
    if (inCornerArea)
    {
        aa = smoothstep(CornerRadius - 1.0, CornerRadius + 1.0, distToCorner);
    }
    finalColor.a *= aa;

    return finalColor;
}

technique RoundedRect
{
    pass Pass0
    {
        VertexShader = compile vs_3_0 mainVS();
        PixelShader = compile ps_3_0 mainPS();
    }
}
