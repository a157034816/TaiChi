using System;
using System.Security.Cryptography;
using TaiChi.License.Services;
using Xunit;

namespace TaiChi.License.Tests.Services;

public class CryptoServiceTests
{
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

    [Fact]
    public void Sign_Then_Verify_ShouldSucceed()
    {
        var (pub, prv) = CryptoService.GenerateRsaKeyPair();
        var data = RandomBytes(256);
        var sig = CryptoService.SignData(data, prv);
        Assert.True(CryptoService.VerifyData(data, sig, pub));
    }

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

    [Fact]
    public void Aes_ShouldThrow_OnInvalidKeyLength()
    {
        var badKey = new byte[16]; // 非 32 字节
        var data = RandomBytes(32);
        Assert.Throws<ArgumentException>(() => CryptoService.EncryptAes(data, badKey));
        Assert.Throws<ArgumentException>(() => CryptoService.DecryptAes(new byte[32], badKey));
    }

    [Fact]
    public void Aes_Decrypt_ShouldThrow_OnTooShortInput()
    {
        var key = CryptoService.GenerateAesKey();
        Assert.Throws<ArgumentException>(() => CryptoService.DecryptAes(new byte[8], key));
    }

    private static byte[] RandomBytes(int count)
    {
        var buf = new byte[count];
        RandomNumberGenerator.Fill(buf);
        return buf;
    }
}

