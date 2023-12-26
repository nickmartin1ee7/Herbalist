using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

using Godot;

public static class DataStorage
{
    // Avoid using directly
    private static readonly string _filePath = Path.Combine(OS.GetUserDataDir(), "data.json");

    private static FileInfo DataFile => new(_filePath);

    public static bool HasPreviousSession
        => DataFile.Exists && DataFile.Length > 0;

    public static void Delete() => DataFile.Delete();

    public static void Write(Dictionary<string, string> data)
    {
        var dataStr = JsonSerializer.Serialize(data);
        File.WriteAllText(DataFile.FullName, dataStr);
    }

    public static Dictionary<string, string> Read()
    {
        try
        {
            if (!DataFile.Exists)
            {
                return null;
            }

            var content = File.ReadAllText(DataFile.FullName);

            if (string.IsNullOrEmpty(content))
            {
                GD.Print($"{nameof(DataStorage)} {nameof(Read)} no content.");
                return null;
            }

            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(content);
            return data;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{nameof(DataStorage)} {nameof(Read)} failed.", ex);
            return null;
        }
    }
}