using System;
using System.Security.Cryptography;
using System.Text;

namespace TaiChi.Security
{
    /// <summary>
    /// RSA非对称加密
    /// </summary>
    public class RSAEncryption
    {
        public static void Test()
        {
            try
            {
                // 生成RSA密钥对
                using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(2048)) // 建议密钥长度至少为2048位
                {
                    string publicKeyXml = rsa.ToXmlString(false); // 公钥，用于加密
                    string privateKeyXml = rsa.ToXmlString(true); // 私钥，用于解密

                    Console.WriteLine("公钥:\n" + publicKeyXml);
                    Console.WriteLine("\n私钥:\n" + privateKeyXml);

                    string originalData = "这是一段需要加密的文本";
                    Console.WriteLine("\n原始数据: " + originalData);


                    // 使用公钥加密
                    byte[] encryptedData = EncryptData(originalData, publicKeyXml);
                    Console.WriteLine("\n加密后数据: " + Convert.ToBase64String(encryptedData));


                    // 使用私钥解密
                    byte[] decryptedData = DecryptData(encryptedData, privateKeyXml);
                    Console.WriteLine("\n解密后数据: " + decryptedData);


                    // 使用私钥签名, 公钥验证签名  --  数字签名示例
                    byte[] signature = SignData(originalData, privateKeyXml);
                    Console.WriteLine("\n签名: " + Convert.ToBase64String(signature));

                    bool isVerified = VerifySignature(originalData, signature, publicKeyXml);
                    Console.WriteLine("签名验证结果: " + isVerified);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("发生错误: " + ex.Message);
            }

            Console.ReadKey(); // 暂停控制台
        }

        // 使用公钥加密数据
        public static byte[] EncryptData(string data, string publicKeyXml)
        {
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(publicKeyXml);
                byte[] dataToEncrypt = Encoding.UTF8.GetBytes(data);
                return rsa.Encrypt(dataToEncrypt, false); //OAEP填充
            }
        }

        /// <summary>
        /// 使用私钥解密数据
        /// </summary>
        /// <param name="data"></param>
        /// <param name="privateKeyXml"></param>
        /// <returns></returns>
        public static byte[] DecryptData(byte[] data, string privateKeyXml)
        {
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(privateKeyXml);
                byte[] decryptedData = rsa.Decrypt(data, false); //OAEP填充
                return decryptedData;
            }
        }

        // 使用私钥签名数据
        public static byte[] SignData(string data, string privateKeyXml)
        {
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(privateKeyXml);

                byte[] dataToSign = Encoding.UTF8.GetBytes(data);

                // SHA256 哈希算法
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hash = sha256.ComputeHash(dataToSign);
                    return rsa.SignHash(hash, CryptoConfig.MapNameToOID("SHA256"));
                }
            }
        }

        // 使用公钥验证签名
        public static bool VerifySignature(string data, byte[] signature, string publicKeyXml)
        {
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(publicKeyXml);

                byte[] dataToVerify = Encoding.UTF8.GetBytes(data);

                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hash = sha256.ComputeHash(dataToVerify);
                    return rsa.VerifyHash(hash, CryptoConfig.MapNameToOID("SHA256"), signature);
                }
            }
        }
    }
}