using System.Text;

public class CharMappingPreprocess
{
    private static Dictionary<char, char>? _charMapping;

    public static string Preprocess(string input)
    {
        if (_charMapping == null)
        {
            string charMappingFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Myth.Core/CharMapping.txt");
            _charMapping = LoadCharMapping(charMappingFilePath);
        }

        return PreprocessInput(input);
    }

    private static Dictionary<char, char> LoadCharMapping(string filePath)
    {
        var mapping = new Dictionary<char, char>();
        foreach (var line in File.ReadAllLines(filePath))
        {
            var parts = line.Split('=');
            if (parts.Length == 2 && parts[0].Length == 1 && parts[1].Length == 1)
            {
                mapping[parts[0][0]] = parts[1][0];
            }
        }

        return mapping;
    }


    private static string PreprocessInput(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (_charMapping.TryGetValue(c, out var mappedChar))
            {
                sb.Append(mappedChar);
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
