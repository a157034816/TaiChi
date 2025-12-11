using System;
using System.Security.Cryptography;

namespace TaiChi.License.Services
{
    /// <summary>
    /// 加密服务：提供 RSA 密钥生成/签名/验签 与 AES-256 加解密。
    /// 注意：不要硬编码密钥，密钥应从安全存储注入或由上层管理。
    /// </summary>
    public static class CryptoService
    {
        // ========================= RSA =========================

        /// <summary>
        /// 生成 RSA 密钥对（默认 2048 位）。
        /// 返回 (PublicKey SPKI, PrivateKey PKCS#8) 二进制表示。
        /// </summary>
        public static (byte[] PublicKeySpki, byte[] PrivateKeyPkcs8) GenerateRsaKeyPair(int keySize = 2048)
        {
            if (keySize < 2048) throw new ArgumentOutOfRangeException(nameof(keySize), "RSA 密钥长度必须 >= 2048");
            using var rsa = RSA.Create(keySize);
            var publicKey = rsa.ExportSubjectPublicKeyInfo();   // X.509 SubjectPublicKeyInfo
            var privateKey = rsa.ExportPkcs8PrivateKey();       // PKCS#8 PrivateKeyInfo
            return (publicKey, privateKey);
        }

        /// <summary>
        /// 使用 RSA-PSS(SHA256) 对数据进行签名。
        /// </summary>
        public static byte[] SignData(byte[] data, byte[] privateKeyPkcs8)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (privateKeyPkcs8 == null || privateKeyPkcs8.Length == 0) throw new ArgumentException("私钥不能为空", nameof(privateKeyPkcs8));

            using var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(privateKeyPkcs8, out _);
            return rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }

        /// <summary>
        /// 使用 RSA-PSS(SHA256) 验证签名。
        /// </summary>
        public static bool VerifyData(byte[] data, byte[] signature, byte[] publicKeySpki)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (signature == null) throw new ArgumentNullException(nameof(signature));
            if (publicKeySpki == null || publicKeySpki.Length == 0) throw new ArgumentException("公钥不能为空", nameof(publicKeySpki));

            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(publicKeySpki, out _);
            return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }

        // ========================= AES-256 =========================

        /// <summary>
        /// 生成 32 字节 AES Key（256 位）。
        /// </summary>
        public static byte[] GenerateAesKey()
        {
            var key = new byte[32];
            RandomNumberGenerator.Fill(key);
            return key;
        }

        /// <summary>
        /// 使用 AES-256-CBC 加密，返回 [IV(16) | CIPHERTEXT] 组合结果。
        /// </summary>
        public static byte[] EncryptAes(byte[] plaintext, byte[] key256)
        {
            if (plaintext == null) throw new ArgumentNullException(nameof(plaintext));
            if (key256 == null) throw new ArgumentNullException(nameof(key256));
            if (key256.Length != 32) throw new ArgumentException("AES-256 密钥长度必须为 32 字节", nameof(key256));

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.BlockSize = 128;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key256;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            var cipher = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);

            var output = new byte[aes.IV.Length + cipher.Length];
            Buffer.BlockCopy(aes.IV, 0, output, 0, aes.IV.Length);
            Buffer.BlockCopy(cipher, 0, output, aes.IV.Length, cipher.Length);
            return output;
        }

        /// <summary>
        /// 使用 AES-256-CBC 解密，输入为 [IV(16) | CIPHERTEXT] 组合结果。
        /// </summary>
        public static byte[] DecryptAes(byte[] combined, byte[] key256)
        {
            if (combined == null) throw new ArgumentNullException(nameof(combined));
            if (key256 == null) throw new ArgumentNullException(nameof(key256));
            if (key256.Length != 32) throw new ArgumentException("AES-256 密钥长度必须为 32 字节", nameof(key256));
            if (combined.Length < 16) throw new ArgumentException("输入数据长度无效", nameof(combined));

            var iv = new byte[16];
            Buffer.BlockCopy(combined, 0, iv, 0, 16);
            var cipherLen = combined.Length - 16;
            var cipher = new byte[cipherLen];
            Buffer.BlockCopy(combined, 16, cipher, 0, cipherLen);

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.BlockSize = 128;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key256;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        }
    }
}

