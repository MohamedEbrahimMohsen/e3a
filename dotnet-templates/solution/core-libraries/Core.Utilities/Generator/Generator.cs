using NanoidDotNet;

namespace Core.Utilities.Generator;

public class Generator : IGenerator
{
    const string UPPER_LETTERS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    const string LOWER_LETTERS = "abcdefghijklmnopqrstuvwxyz";
    const string DIGITS = "0123456789";
    const string SPECIAL_CHARACTERS = "!@#$%*-_+?";
    const string ALL = $"{UPPER_LETTERS}{LOWER_LETTERS}{DIGITS}{SPECIAL_CHARACTERS}";
    
    public string Generate(string prefix = "", int size = 5, string separator = "-", string suffix = "", string allowedCharacters = "0123456789abcdefghijklmnopqrstuvwxyz")
    {
        return $"{prefix}{separator}{Nanoid.Generate(allowedCharacters, size)}{separator}{suffix}";
    }

    public string Generate(int size = 5, string allowedCharacters = "0123456789abcdefghijklmnopqrstuvwxyz")
    {
        return $"{Nanoid.Generate(allowedCharacters, size)}";
    }

    public string GenerateComplex(int size = 12)
    {
        string complex = Nanoid.Generate(UPPER_LETTERS, 1) + Nanoid.Generate(LOWER_LETTERS, 1) + Nanoid.Generate(DIGITS, 1) + Nanoid.Generate(SPECIAL_CHARACTERS, 1) + Nanoid.Generate(ALL, size - 4);
        return new string(complex.Shuffle().ToArray());
    }
}