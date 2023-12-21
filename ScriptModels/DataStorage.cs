using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

public static class DataStorage
{
    private const string _file = "user://data.json";
    private static FileInfo SaveFile => new FileInfo(_file);

    public static Task Write(Dictionary<string, string> data)
    {
        return File.WriteAllTextAsync(SaveFile.FullName, JsonSerializer.Serialize(data));
    }

    public static ValueTask<Dictionary<string, string>> Read()
    {
        using var stream = SaveFile.OpenRead();
        return JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream);
    }
}