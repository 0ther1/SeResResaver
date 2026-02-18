using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace SeResResaver.Core
{
    /// <summary>
    /// RSA PSS signer
    /// </summary>
    public class RsaPssSigner
    {
        /// <summary>
        /// Hash method
        /// </summary>
        public enum HashMethod
        {
            SHA1 = 4,
            SHA256 = 6,
        }

        private const int SALT_LEN = 0xB;
        private PssSigner signer;

        /// <summary>
        /// Create a new signer.
        /// </summary>
        /// <param name="hashMethod">Hash method.</param>
        /// <param name="key">DER-encoded RSA private key.</param>
        /// <exception cref="ArgumentException"></exception>
        public RsaPssSigner(HashMethod hashMethod, byte[] key)
        {
            IDigest digest;

            switch (hashMethod)
            {
                case HashMethod.SHA1:
                    digest = new Sha1Digest();
                    break;
                case HashMethod.SHA256:
                    digest = new Sha256Digest();
                    break;
                default:
                    throw new ArgumentException($"Invalid hash method {hashMethod}", nameof(hashMethod));
            }

            var keyObj = Asn1Object.FromByteArray(key);
            var privateKey = new RsaPrivateCrtKeyParameters(RsaPrivateKeyStructure.GetInstance(keyObj));

            signer = new PssSigner(new RsaEngine(), digest, digest, SALT_LEN, 0xBC);
            signer.Init(true, privateKey);
        }

        /// <summary>
        /// Update the signer with a span of bytes.
        /// </summary>
        /// <param name="data">Data.</param>
        public void Update(ReadOnlySpan<byte> data)
        {
            signer.BlockUpdate(data);
        }

        /// <summary>
        /// Update the signer with bytes.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <param name="offset">Data offset.</param>
        /// <param name="count">Byte count.</param>
        public void Update(byte[] data, int offset, int count)
        {
            signer.BlockUpdate(data, offset, count);
        }

        /// <summary>
        /// Generate a signature.
        /// </summary>
        /// <returns>A byte array containing the signature.</returns>
        public byte[] Sign()
        {
            byte[] signature = signer.GenerateSignature();
            signer.Reset();
            return signature;
        }

        /// <summary>
        /// Update the signer with a span of bytes and generate a signature.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <returns>A byte array containing the signature.</returns>
        public byte[] Sign(ReadOnlySpan<byte> data)
        {
            Update(data);
            return Sign();
        }

        /// <summary>
        /// Update the signer with bytes and generate a signature.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <param name="offset">Data offset.</param>
        /// <param name="count">Byte count.</param>
        /// <returns>A byte array containing the signature.</returns>
        public byte[] Sign(byte[] data, int offset, int count)
        {
            Update(data, offset, count);
            return Sign();
        }
    }
}
