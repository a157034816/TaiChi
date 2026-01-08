// MonoGame Rounded Rectangle Effect
// - 在 SpriteBatch 中使用，按目标矩形像素尺寸与圆角半径计算 Alpha
// - 传入参数：
//   MatrixTransform  : 由 SpriteBatch.Begin 自动设置
//   RectSize         : 目标绘制矩形的像素尺寸（Width, Height）
//   CornerRadius     : 圆角半径（像素）
//   Softness         : 边缘柔化宽度（像素，建议 1.5~2.5）
//   FillColor        : 填充颜色（RGBA，Alpha 会与形状 Alpha 相乘）

float4x4 MatrixTransform;

// 为了与 SpriteBatch 兼容，提供 Texture 与采样器（即使我们不依赖其颜色）
texture Texture;
sampler TextureSampler = sampler_state
{
    Texture = <Texture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

float2 RectSize = float2(100.0, 100.0);
float  CornerRadius = 8.0;
float  Softness = 2.0;
float4 FillColor = float4(1, 1, 1, 1);
float  Bypass = 0.0; // >0.5 时绕过 SDF（调试）

struct VS_INPUT
{
    float4 Position : POSITION0;
    float4 Color    : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

struct VS_OUTPUT
{
    float4 Position : SV_Position; // 与 MonoGame SpriteEffect 保持一致
    float4 Color    : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

VS_OUTPUT VSMain(VS_INPUT input)
{
    VS_OUTPUT output;
    output.Position = mul(input.Position, MatrixTransform);
    output.Color = input.Color;
    output.TexCoord = input.TexCoord;
    return output;
}

// 有符号距离：圆角矩形（基于 IQ 的 SDF 公式）
float sdRoundRect(float2 p, float2 halfSize, float r)
{
    // 将点映射到第一象限并减去圆角半径后的半尺寸
    float2 q = abs(p) - (halfSize - r);

    // 外部距离（到矩形外 + 圆角圆弧）
    float outside = length(max(q, 0.0));
    // 内部距离（在矩形内时，取最大分量）
    float inside = min(max(q.x, q.y), 0.0);
    return outside + inside - r;
}

float4 PSMain(VS_OUTPUT input) : COLOR0
{
    // 将 0..1 的纹理坐标转换为以中心为原点的像素坐标
    float2 size = max(RectSize, float2(1.0, 1.0));
    float2 halfSize = size * 0.5;
    float2 uv = input.TexCoord;
    float2 p = (uv - 0.5) * size; // 像素空间下的点

    // 限制圆角半径不超过一半最小边
    float r = min(CornerRadius, min(halfSize.x, halfSize.y));

    // 以像素为单位的边缘柔化宽度（防走样）
    float edge = max(Softness, 0.5);

    // 计算 SDF 并做平滑阈值，得到形状 Alpha
    float dist = sdRoundRect(p, halfSize, r);
    float shapeAlpha = 1.0 - smoothstep(0.0, edge, dist);

    // 组合颜色：纹理(通常为白像素) * FillColor * 顶点颜色
    float4 baseCol = tex2D(TextureSampler, input.TexCoord);
    float4 col = baseCol * FillColor * input.Color;
    float a = (Bypass > 0.5) ? 1.0 : shapeAlpha;
    col *= a; // 非预乘：rgb/alpha 同步按形状透明度缩放
    return col;
}

technique SpriteTechnique
{
    pass P0
    {
        VertexShader = compile vs_2_0 VSMain();
        PixelShader  = compile ps_2_0 PSMain();
    }
}
