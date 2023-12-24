using System;
using System.Collections.Generic;
using System.Text.Json;

using Godot;

public static class DataStorage
{
    private const string _filePath = "user://data.json";

    public static void Write(Dictionary<string, string> data)
    {
        var fa = FileAccess.Open(_filePath, FileAccess.ModeFlags.Write);

        if (fa is null)
        {
            GD.PrintErr($"{nameof(DataStorage)} {nameof(Write)} operation failed!");
            return;
        }

        fa.StoreString(JsonSerializer.Serialize(data));
    }

    public static Dictionary<string, string> Read()
    {
        try
        {
            var fa = FileAccess.Open(_filePath, FileAccess.ModeFlags.Read);

            if (fa is null)
            {
                GD.PrintErr($"{nameof(DataStorage)} {nameof(Read)} operation failed!");
                return null;
            }

            var content = fa.GetAsText();

            if (string.IsNullOrEmpty(content))
            {
                GD.Print($"{nameof(DataStorage)} {nameof(Read)} no content.");
                return null;
            }

            return JsonSerializer.Deserialize<Dictionary<string, string>>(content);
        }
        catch (Exception)
        {
            return null;
        }
    }
}