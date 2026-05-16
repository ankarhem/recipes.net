using System.Security.Cryptography;
using App.Identity;
using App.Identity.Ports;

namespace Infrastructure.Identity;

public sealed class RecoveryCodeGenerator : IRecoveryCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int CodeCount = 10;
    private const int CodeCharacterCount = 12;

    public IReadOnlyList<string> Generate()
    {
        var codes = new string[CodeCount];
        for (var i = 0; i < codes.Length; i++)
        {
            codes[i] = GenerateCode();
        }

        return codes;
    }

    private static string GenerateCode()
    {
        var characters = new char[CodeCharacterCount];
        for (var i = 0; i < characters.Length; i++)
        {
            characters[i] = Alphabet[RandomNumberGenerator.GetInt32(0, Alphabet.Length)];
        }

        return $"{new string(characters, 0, 4)}-{new string(characters, 4, 4)}-{new string(characters, 8, 4)}";
    }
}
