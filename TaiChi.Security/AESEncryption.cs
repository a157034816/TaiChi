using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TaiChi.Security
{
    /// <summary>
    /// AES对称加密
    /// </summary>
    public class AESEncryption
    {
        private static readonly byte[] _salt = Encoding.UTF8.GetBytes("这是颜值"); //  盐值，建议随机生成并妥善保管

        public static string Encrypt(string plainText, string password)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.KeySize = 256; // 设置密钥大小，可以是128, 192 或 256 位
                aesAlg.BlockSize = 128; // 块大小固定为 128 位
                aesAlg.Mode = CipherMode.CBC; //  使用 CBC 模式，需要 IV
                aesAlg.Padding = PaddingMode.PKCS7; // 使用 PKCS7 填充

                // 使用密码派生密钥和 IV
                using (Rfc2898DeriveBytes keyDerivation = new Rfc2898DeriveBytes(password, _salt, 10000)) // 迭代次数建议至少 10000
                {
                    aesAlg.Key = keyDerivation.GetBytes(32); // 256 位密钥
                    aesAlg.IV = keyDerivation.GetBytes(16); // 128 位 IV
                }


                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(plainText);
                        }
                    }

                    byte[] encryptedBytes = msEncrypt.ToArray();

                    // 将加密后的字节数组转换为 Base64 字符串
                    return Convert.ToBase64String(encryptedBytes);
                }
            }
        }

        public static string Decrypt(string cipherText, string password)
        {
            byte[] encryptedBytes = Convert.FromBase64String(cipherText);

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.KeySize = 256;
                aesAlg.BlockSize = 128;
                aesAlg.Mode = CipherMode.CBC;
                aesAlg.Padding = PaddingMode.PKCS7;

                using (Rfc2898DeriveBytes keyDerivation = new Rfc2898DeriveBytes(password, _salt, 10000))
                {
                    aesAlg.Key = keyDerivation.GetBytes(32);
                    aesAlg.IV = keyDerivation.GetBytes(16);
                }

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msDecrypt = new MemoryStream(encryptedBytes))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            return srDecrypt.ReadToEnd();
                        }
                    }
                }
            }
        }
        
        public static void Test()
        {
            string originalText = "This is some text to encrypt.";
            string password = "MySecurePassword"; //  请使用强密码!

            string encryptedText = AESEncryption.Encrypt(originalText, password);
            Console.WriteLine($"Encrypted: {encryptedText}");

            string decryptedText = AESEncryption.Decrypt(encryptedText, password);
            Console.WriteLine($"Decrypted: {decryptedText}");
        }
    }
}