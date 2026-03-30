using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using static GkmStatus.src.AppConstants;

namespace GkmStatus.src
{
    public sealed class ProduceCharacter
    {
        public string Id { get; set; } = "";
        public string Display { get; set; } = "";
        public string NameEn { get; set; } = "";
    }

    internal static class Characters
    {
        private sealed class ProduceCharactersData
        {
            public List<ProduceCharacter> Characters { get; set; } = [];
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true
        };

        public static IReadOnlyList<ProduceCharacter> ProduceCharacters { get; } = LoadProduceCharacters();

        public static int FindProduceCharacterIndex(string? id)
        {
            if (string.IsNullOrEmpty(id)) return -1;

            for (int i = 0; i < ProduceCharacters.Count; i++)
            {
                if (string.Equals(ProduceCharacters[i].Id, id, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private static List<ProduceCharacter> LoadProduceCharacters()
        {

            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                using Stream? stream = asm.GetManifestResourceStream(ProduceCharacter_Data_Path);

                if (stream is null)
                {
                    Debug.WriteLine($"Character JSON resource not found: {ProduceCharacter_Data_Path}");
                    return [];
                }

                var root = JsonSerializer.Deserialize<ProduceCharactersData>(stream, JsonOptions);
                if (root?.Characters is { Count: > 0 })
                {
                    return root.Characters;
                }

                Debug.WriteLine("Character JSON is empty or invalid.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Character JSON load error: " + ex.Message);
            }

            return [];
        }
    }
}