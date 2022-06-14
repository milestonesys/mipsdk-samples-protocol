using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace OauthLoginFlow
{
    public static class Utility
    {
        /// <summary>
        /// Base64url no-padding encodes the given input buffer
        /// </summary>
        /// <param name="buffer">The buffer to generate from</param>
        public static string GenerateBase64UrlEncodeNoPadding(byte[] buffer)
        {
            string base64 = Convert.ToBase64String(buffer);

            // Converts base64 to base64url
            base64 = base64.Replace("+", "-");
            base64 = base64.Replace("/", "_");

            // Strips padding
            base64 = base64.Replace("=", "");

            return base64;
        }

        /// <summary>
        /// Returns URI-safe data (32 bytes)
        /// </summary>
        public static string GenerateRandom32ByteDataBase64Url()
        {
            const uint numBytes = 32;

            byte[] bytes;
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                bytes = new byte[numBytes];
                rng.GetBytes(bytes);
            }
            return GenerateBase64UrlEncodeNoPadding(bytes);
        }

        /// <summary>
        /// Returns the SHA256 hash of the input string, which is assumed to be ASCII
        /// </summary>
        /// /// <param name="text">The text to be hashed</param>
        public static byte[] Sha256Ascii(string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            using (SHA256CryptoServiceProvider sha256 = new SHA256CryptoServiceProvider())
            {
                return sha256.ComputeHash(bytes);
            }
        }

        public static string CreateHttpListenerEndpoint()
        {
            int randomPort = 5656;      //TODO - not very random!
            return $"http://127.0.0.1:{randomPort}/";
        }

        private static readonly string BrowserResponseHtml = $"<html><body>Login process complete, return to your app</body></html>";

        public static void ProvideBrowserResponse(HttpListenerContext context)
        {
            HttpListenerResponse response = context.Response;
            byte[] buffer = Encoding.UTF8.GetBytes(BrowserResponseHtml);
            response.ContentLength64 = buffer.Length;
            Stream responseOutput = response.OutputStream;
            responseOutput.Write(buffer, 0, buffer.Length);
            responseOutput.Close();
        }

    }
}
