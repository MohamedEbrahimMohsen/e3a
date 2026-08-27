namespace Core.Utilities.Generator;
public interface IGenerator
{
    string Generate(int size = 5, string allowedCharacters = "0123456789abcdefghijklmnopqrstuvwxyz");
    public string Generate(string prefix = "", int size = 5, string separator = "-", string suffix = "", string allowedCharacters = "0123456789abcdefghijklmnopqrstuvwxyz");
    public string GenerateComplex(int size = 12);
}