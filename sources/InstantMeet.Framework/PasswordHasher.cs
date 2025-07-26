using System.Security.Cryptography;

namespace InstantMeet.Framework;

public static class PasswordHasher
{
  private const int SaltSize = 16; // 128 bit
  private const int HashSize = 20; // 160 bit
  private const int Iterations = 10000;

  /// <summary>
  /// Creates a salted PBKDF2 hash of a password.
  /// </summary>
  /// <param name="password">The password to hash.</param>
  /// <returns>The hash of the password, including the salt, in a string format.</returns>
  public static string Hash(string password)
  {
    // 1. Create a salt
    byte[] salt;
    using (var rng = RandomNumberGenerator.Create())
    {
      salt = new byte[SaltSize];
      rng.GetBytes(salt);
    }

    // 2. Create the hash
    var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
    byte[] hash = pbkdf2.GetBytes(HashSize);

    // 3. Combine salt and hash
    byte[] hashBytes = new byte[SaltSize + HashSize];
    Array.Copy(salt, 0, hashBytes, 0, SaltSize);
    Array.Copy(hash, 0, hashBytes, SaltSize, HashSize);

    // 4. Convert to base64 for storage
    return Convert.ToBase64String(hashBytes);
  }


  /// <summary>
  /// Verifies a password against a stored, salted hash.
  /// </summary>
  /// <param name="password">The password to check.</param>
  /// <param name="storedHash">The stored hash from the file.</param>
  /// <returns>True if the password is correct, false otherwise.</returns>
  public static bool Verify(string password, string storedHash)
  {
    // 1. Get hash bytes from storage
    byte[] hashBytes = Convert.FromBase64String(storedHash);

    // 2. Extract the salt
    byte[] salt = new byte[SaltSize];
    Array.Copy(hashBytes, 0, salt, 0, SaltSize);

    // 3. Re-hash the provided password with the original salt
    var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
    byte[] hash = pbkdf2.GetBytes(HashSize);

    // 4. Compare the results
    for (int i = 0; i < HashSize; i++)
    {
      if (hashBytes[i + SaltSize] != hash[i])
      {
        return false; // Mismatch
      }
    }
    return true; // Match
  }
}