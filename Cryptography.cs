using Serilog;
using System;
using System.Security.Cryptography;
using System.Text;

namespace eLogin
{
    public static class Cryptography
    {
        //public static string dbKey = "XcbWy+VLMxAlkJc5fZxFNc510Cuz7JxH5CeYd5Q3mYA=";
        //public static string dbIV = "tNfvCymeZ21BLLQYCLIMEQ==";

        public static string dbKey = "sDR8YVFA7DXA/rQtA/lDYzKax6KBwYReBarGp6Z0rdg="; //PKCS7
        public static string dbIV = "1VCT+BD4jBXQv9V9UlOL+w=="; //PKCS7

        //public static string dbKey = "ZAj9pZygRvRXWjCYXMDmHiepA73VERPHZh84aZfk6sY="; //Padding None
        //public static string dbIV = "eb5z7aNKGpkV86X1DUfPCQ=="; // Padding None

        public static Aes CreateCipher(string Key)
        {
            Log.Information("Cryptography.CreateCipher is called");
            Log.Debug("Creating new Cipher");
            Aes cipher = Aes.Create();  //Defaults - Keysize 256, Mode CBC, Padding PKC27
            //Aes cipher = new AesManaged();
            //Aes cipher = new AesCryptoServiceProvider();

            //cipher.Padding = PaddingMode.ISO10126;
            cipher.Padding = PaddingMode.PKCS7;

            //cipher.Padding = PaddingMode.Zeros;
            cipher.Mode = CipherMode.CBC;

            //Create() makes new key each time, use a consistent key for encrypt/decrypt
            cipher.Key = Convert.FromBase64String(Key);

            Log.Debug("Cipher created with {cipher.Padding} and {cipher.Mode}", cipher.Padding, cipher.Mode);

            return cipher;
        }

        public enum Operation
        {
            Encrypt,
            Decrypt
        }

        public static string AES(in string Key, in string IV, in string Input, Operation Operation)
        {
            if(!string.IsNullOrEmpty(Input))
            {
                Log.Information("Cryptography.AES is called with to {Operation} {Input}", Operation, Input);
                Log.Debug("Converting key, iv and input to byte arrays");
                var KeyBytes = Convert.FromBase64String(Key);
                var IVBytes = Convert.FromBase64String(IV);
                var InputBytes = Convert.FromBase64String(Input);

                Log.Debug("Calling AES for bytes");
                var OutputBytes = AES(in KeyBytes, in IVBytes, in InputBytes, Operation);

                if (Operation == Operation.Encrypt)
                {
                    return Convert.ToBase64String(OutputBytes);
                }
                else
                {
                    return Encoding.UTF8.GetString(OutputBytes);
                }
            }
            else
            {
                Log.Error("Input value is null. Will return Null");
                return (null);
            }
            
                        
        }

        public static byte[] AES(in byte[] Key, in byte[] IV, in byte[] Input, Operation Operation)
        {
            ICryptoTransform CryptoTransform =null ;
            try
            {
                Log.Information("Cryptography.AES (bytes) is called");
                Log.Debug("Creating new Cipher");
                using var Cipher = Aes.Create();

                Cipher.Padding = PaddingMode.PKCS7;

                Log.Debug("Cipher created with {cipher.Padding} and {cipher.Mode}", Cipher.Padding, Cipher.Mode);

                 CryptoTransform = Operation == Operation.Encrypt
                    ? Cipher.CreateEncryptor(Key, IV)
                    : Cipher.CreateDecryptor(Key, IV);


            }
            catch (Exception ex)
            {
                Log.Information("ex "+ ex);
            }
                return CryptoTransform.TransformFinalBlock(Input, 0, Input.Length);


        }

        public static string Hash(string Plaintext)
        {
            Log.Information("Cryptography.Hash is called");
            using var Hasher = SHA512.Create();

            var Hash = Hasher.ComputeHash(Encoding.UTF8.GetBytes(Plaintext));

            return Convert.ToBase64String(Hash);
        }

        public static string GenerateKey()
        {
            Log.Information("Cryptography.GenerateKey is called");
            using var Cipher = Aes.Create();

            Cipher.Padding = PaddingMode.PKCS7;

            Log.Debug("Cipher created with {cipher.Padding} and {cipher.Mode}", Cipher.Padding, Cipher.Mode);

            var key = Convert.ToBase64String(Cipher.Key);
            var iv = Convert.ToBase64String(Cipher.IV);

            return Convert.ToBase64String(Cipher.Key);
        }
    }
}
