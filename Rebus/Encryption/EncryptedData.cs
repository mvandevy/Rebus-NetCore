using System;

namespace Rebus.Encryption
{
    /// <summary>
    /// Represents a chunk of encrypted data along with the salt (a.k.a. "Initialization Vector"/"IV") that was used to encrypt it.
    /// </summary>
    public class EncryptedData
    {
        /// <summary>
        /// Constructs an instance from the given bytes and iv.
        /// </summary>
        public EncryptedData(byte[] bytes, byte[] iv)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (iv == null) throw new ArgumentNullException(nameof(iv));
            Bytes = bytes;
            Iv = iv;
        }

        /// <summary>
        /// Gets the salt (a.k.a. "Initialization Vector"/"IV") from this encrypted data instance
        /// </summary>
        public byte[] Iv { get; }

        /// <summary>
        /// Gets the raw data from this encrypted data instance
        /// </summary>
        public byte[] Bytes { get; }
    }
}