using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using UnityEngine;

using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Runtime.Identity
{
    /// <summary>
    ///     Generates and persists an ECDSA keypair (NIST P-256) for player identity.
    ///     The keypair is stored at Application.persistentDataPath/identity.bin as PKCS#8.
    ///     The player UUID is derived as UUIDv5 from the public key.
    ///     Falls back to a persistent random UUID without signing on runtimes that lack
    ///     ECDSA support (e.g. Unity Mono backend).
    /// </summary>
    public sealed class PlayerIdentity : IDisposable
    {
        /// <summary>Fixed UUID used for the local player in singleplayer mode.</summary>
        public const string LocalUuid = "local";

        /// <summary>UUID namespace for Lithforge player IDs (randomly generated, fixed).</summary>
        private static readonly Guid s_lithforgeNamespace =
            new("7b3d5e8a-2f1c-4a9b-b8e6-d4c7f0a1e3b2");

        /// <summary>The ECDSA signing key (NIST P-256). Null when using fallback UUID mode.</summary>
        private ECDsa _signingKey;

        /// <summary>The player's unique identifier derived from the public key.</summary>
        public string Uuid { get; private set; } = "";

        /// <summary>The exported public key bytes (SubjectPublicKeyInfo DER). Empty in fallback mode.</summary>
        public byte[] PublicKeyBytes { get; private set; } = Array.Empty<byte>();

        /// <summary>Whether identity was successfully loaded or generated.</summary>
        public bool IsValid { get; private set; }

        /// <summary>Releases the signing key.</summary>
        public void Dispose()
        {
            _signingKey?.Dispose();
            _signingKey = null;
        }

        /// <summary>
        ///     Loads an existing identity from disk or generates a new one.
        ///     Uses Application.persistentDataPath/identity.bin for ECDSA keys,
        ///     or Application.persistentDataPath/identity_uuid.txt for fallback UUIDs.
        /// </summary>
        public void LoadOrGenerate(ILogger logger)
        {
            // Try ECDSA first (works on .NET CoreCLR, fails on Mono)
            if (TryLoadOrGenerateEcdsa(logger))
            {
                return;
            }

            // Fallback: persistent random UUID without cryptographic signing.
            // The server will skip challenge-response for clients with empty public keys.
            TryLoadOrGenerateFallbackUuid(logger);
        }

        /// <summary>Signs a challenge byte array using the ECDSA private key (SHA-256 digest).</summary>
        public byte[] Sign(byte[] challenge)
        {
            if (_signingKey is null)
            {
                return Array.Empty<byte>();
            }

            return _signingKey.SignData(challenge, HashAlgorithmName.SHA256);
        }

        /// <summary>
        ///     Attempts to load or generate an ECDSA P-256 identity.
        ///     Returns false if the runtime does not support the required crypto operations.
        /// </summary>
        private bool TryLoadOrGenerateEcdsa(ILogger logger)
        {
            string identityPath = Path.Combine(Application.persistentDataPath, "identity.bin");

            try
            {
                if (File.Exists(identityPath))
                {
                    byte[] pkcs8 = File.ReadAllBytes(identityPath);

                    if (pkcs8.Length > 0)
                    {
                        ECDsa loaded = ECDsa.Create();
                        loaded.ImportPkcs8PrivateKey(pkcs8, out int _);
                        _signingKey = loaded;
                        PublicKeyBytes = _signingKey.ExportSubjectPublicKeyInfo();
                        Uuid = DeriveUuid(PublicKeyBytes);
                        IsValid = true;
                        logger?.LogInfo($"[Identity] Loaded ECDSA identity: {Uuid}");
                        return true;
                    }

                    logger?.LogWarning("[Identity] Corrupted identity.bin, regenerating.");
                }

                // Generate new keypair (NIST P-256)
                _signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                PublicKeyBytes = _signingKey.ExportSubjectPublicKeyInfo();
                Uuid = DeriveUuid(PublicKeyBytes);

                // Persist as PKCS#8 private key
                byte[] exported = _signingKey.ExportPkcs8PrivateKey();
                File.WriteAllBytes(identityPath, exported);

                IsValid = true;
                logger?.LogInfo($"[Identity] Generated new ECDSA identity: {Uuid}");
                return true;
            }
            catch (NotImplementedException)
            {
                // Runtime doesn't support ECDSA PKCS#8 (e.g. Unity Mono backend)
                _signingKey?.Dispose();
                _signingKey = null;
                PublicKeyBytes = Array.Empty<byte>();
                return false;
            }
            catch (PlatformNotSupportedException)
            {
                _signingKey?.Dispose();
                _signingKey = null;
                PublicKeyBytes = Array.Empty<byte>();
                return false;
            }
        }

        /// <summary>
        ///     Loads or generates a persistent random UUID without cryptographic signing.
        ///     Used as a fallback when ECDSA is not available on the runtime.
        /// </summary>
        private void TryLoadOrGenerateFallbackUuid(ILogger logger)
        {
            string uuidPath = Path.Combine(Application.persistentDataPath, "identity_uuid.txt");

            try
            {
                if (File.Exists(uuidPath))
                {
                    string existing = File.ReadAllText(uuidPath).Trim();

                    if (existing.Length > 0)
                    {
                        Uuid = existing;
                        PublicKeyBytes = Array.Empty<byte>();
                        IsValid = true;
                        logger?.LogInfo($"[Identity] Loaded fallback UUID: {Uuid}");
                        return;
                    }
                }

                // Generate a random UUIDv4
                Uuid = Guid.NewGuid().ToString();
                File.WriteAllText(uuidPath, Uuid);
                PublicKeyBytes = Array.Empty<byte>();
                IsValid = true;
                logger?.LogWarning(
                    $"[Identity] ECDSA not available, generated fallback UUID: {Uuid}");
            }
            catch (Exception ex)
            {
                logger?.LogError($"[Identity] Failed to load/generate fallback UUID: {ex.Message}");
                IsValid = false;
            }
        }

        /// <summary>
        ///     Derives a UUIDv5 string from the public key bytes using
        ///     SHA-1 of the Lithforge namespace + key (per RFC 4122).
        /// </summary>
        private static string DeriveUuid(byte[] publicKey)
        {
            // UUIDv5: SHA-1 hash of namespace + name, then set version/variant bits
            byte[] namespaceBytes = s_lithforgeNamespace.ToByteArray();

            // Convert to big-endian network byte order (UUID spec)
            SwapGuidByteOrder(namespaceBytes);

            using (SHA1 sha1 = SHA1.Create())
            {
                sha1.TransformBlock(namespaceBytes, 0, namespaceBytes.Length, null, 0);
                sha1.TransformFinalBlock(publicKey, 0, publicKey.Length);
                byte[] hash = sha1.Hash;

                // Set version 5 (bits 4-7 of byte 6)
                hash[6] = (byte)(hash[6] & 0x0F | 0x50);

                // Set variant (bits 6-7 of byte 8)
                hash[8] = (byte)(hash[8] & 0x3F | 0x80);

                // Format as UUID string
                StringBuilder sb = new(36);
                sb.Append(BytesToHex(hash, 0, 4));
                sb.Append('-');
                sb.Append(BytesToHex(hash, 4, 2));
                sb.Append('-');
                sb.Append(BytesToHex(hash, 6, 2));
                sb.Append('-');
                sb.Append(BytesToHex(hash, 8, 2));
                sb.Append('-');
                sb.Append(BytesToHex(hash, 10, 6));
                return sb.ToString();
            }
        }

        /// <summary>Converts bytes to lowercase hex string.</summary>
        private static string BytesToHex(byte[] data, int offset, int count)
        {
            StringBuilder sb = new(count * 2);

            for (int i = 0; i < count; i++)
            {
                sb.Append(data[offset + i].ToString("x2"));
            }

            return sb.ToString();
        }

        /// <summary>Swaps the byte order of a GUID byte array from .NET mixed-endian to big-endian.</summary>
        private static void SwapGuidByteOrder(byte[] guid)
        {
            // .NET GUIDs: first 3 groups are little-endian, last 2 are big-endian
            // Swap first 4 bytes
            (guid[0], guid[3]) = (guid[3], guid[0]);
            (guid[1], guid[2]) = (guid[2], guid[1]);

            // Swap bytes 4-5
            (guid[4], guid[5]) = (guid[5], guid[4]);

            // Swap bytes 6-7
            (guid[6], guid[7]) = (guid[7], guid[6]);
        }
    }
}
