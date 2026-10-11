using System;
using System.Security.Cryptography;
using System.Text;

namespace WindowsHost.Protocol
{
    /// <summary>Protocol-v2 helpers for a locally approved Android installation key.</summary>
    public static class PairingAuthenticator
    {
        public const int ChallengeBytes = 32;

        public static string CreateChallenge()
            => Convert.ToBase64String(RandomNumberGenerator.GetBytes(ChallengeBytes));

        public static bool IsValidInstallationId(string? value)
            => Guid.TryParse(value, out _);

        public static string CreatePairingCode(string publicKey)
        {
            var digest = SHA256.HashData(Convert.FromBase64String(publicKey));
            var number = BitConverter.ToUInt32(digest, 0) % 1_000_000;
            return number.ToString("D6");
        }

        public static bool Verify(string publicKey, string challenge, string installationId, string signature)
        {
            try
            {
                var data = Encoding.UTF8.GetBytes($"{challenge}|{installationId}");
                var signatureBytes = Convert.FromBase64String(signature);
                using var key = ECDsa.Create();
                key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
                // Android's java.security.Signature emits ASN.1 DER (RFC 3279), whereas
                // .NET's default ECDsa overload uses IEEE P1363. Accept the Android wire
                // format first and retain P1363 for existing .NET companions/tests.
                return key.VerifyData(data, signatureBytes, HashAlgorithmName.SHA256,
                           DSASignatureFormat.Rfc3279DerSequence)
                    || key.VerifyData(data, signatureBytes, HashAlgorithmName.SHA256,
                           DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            }
            catch (CryptographicException) { return false; }
            catch (FormatException) { return false; }
        }
    }
}
