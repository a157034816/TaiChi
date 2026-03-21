using System;
using System.Security.Cryptography;
using TaiChi.License.Services;
using Xunit;

namespace TaiChi.License.Tests.Services;

/// <summary>
/// <see cref="CryptoService"/> 的单元测试：覆盖 RSA 密钥生成/签名验签，以及 AES 加解密的边界条件。
/// </summary>
public class CryptoServiceTests
{
    /// <summary>
    /// 验证生成 RSA 密钥对：公钥/私钥字节数组应非空且可被 .NET RSA 正确导入。
    /// </summary>
    [Fact]
    public void GenerateRsaKeyPair_ShouldReturnValidKeys()
    {
        var (pub, prv) = CryptoService.GenerateRsaKeyPair(2048);
        Assert.NotNull(pub);
        Assert.NotNull(prv);
        Assert.True(pub.Length > 0);
        Assert.True(prv.Length > 0);

        using var rsaPub = RSA.Create();
        rsaPub.ImportSubjectPublicKeyInfo(pub, out _);

        using var rsaPrv = RSA.Create();
        rsaPrv.ImportPkcs8PrivateKey(prv, out _);
    }

    /// <summary>
    /// 验证签名与验签：同一数据签名后应能通过公钥验签。
    /// </summary>
    [Fact]
    public void Sign_Then_Verify_ShouldSucceed()
    {
        var (pub, prv) = CryptoService.GenerateRsaKeyPair();
        var data = RandomBytes(256);
        var sig = CryptoService.SignData(data, prv);
        Assert.True(CryptoService.VerifyData(data, sig, pub));
    }

    /// <summary>
    /// 验证篡改签名：签名被修改后验签应失败。
    /// </summary>
    [Fact]
    public void Verify_ShouldFail_WhenSignatureTampered()
    {
        var (pub, prv) = CryptoService.GenerateRsaKeyPair();
        var data = RandomBytes(128);
        var sig = CryptoService.SignData(data, prv);
        // 篡改签名末尾一个字节
        sig[^1] ^= 0xFF;
        Assert.False(CryptoService.VerifyData(data, sig, pub));
    }

    /// <summary>
    /// 验证 AES 往返：明文加密后再解密应还原原始字节数组。
    /// </summary>
    [Fact]
    public void Aes_EncryptDecrypt_Roundtrip()
    {
        var key = CryptoService.GenerateAesKey();
        var plaintext = RandomBytes(1024);
        var cipher = CryptoService.EncryptAes(plaintext, key);
        Assert.NotEqual(plaintext, cipher);
        var restored = CryptoService.DecryptAes(cipher, key);
        Assert.Equal(plaintext, restored);
    }

    /// <summary>
    /// 验证 AES 密钥长度：当密钥长度非法时应抛出 <see cref="ArgumentException"/>。
    /// </summary>
    [Fact]
    public void Aes_ShouldThrow_OnInvalidKeyLength()
    {
        var badKey = new byte[16]; // 非 32 字节
        var data = RandomBytes(32);
        Assert.Throws<ArgumentException>(() => CryptoService.EncryptAes(data, badKey));
        Assert.Throws<ArgumentException>(() => CryptoService.DecryptAes(new byte[32], badKey));
    }

    /// <summary>
    /// 验证 AES 解密输入长度：当输入过短（缺少 IV 等必要信息）时应抛出 <see cref="ArgumentException"/>。
    /// </summary>
    [Fact]
    public void Aes_Decrypt_ShouldThrow_OnTooShortInput()
    {
        var key = CryptoService.GenerateAesKey();
        Assert.Throws<ArgumentException>(() => CryptoService.DecryptAes(new byte[8], key));
    }

    /// <summary>
    /// 生成指定长度的随机字节数组（用于测试数据）。
    /// </summary>
    /// <param name="count">字节数。</param>
    /// <returns>随机字节数组。</returns>
    private static byte[] RandomBytes(int count)
    {
        var buf = new byte[count];
        RandomNumberGenerator.Fill(buf);
        return buf;
    }
}

