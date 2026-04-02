using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Lokalny odpowiednik backendowego wikipedia_cache/cache.py
/// oraz runtime'owego użycia TextureAPI/cache.
/// 
/// Zamiast zewnętrznego serwera gra losuje deterministycznie
/// pre-renderowany zestaw tekstur z lokalnego JSON-a po kategorii.
/// </summary>
public static class LocalTextureCacheService
{
    static readonly Dictionary<string, List<TextureEntry>> CategoryTextures =
        new Dictionary<string, List<TextureEntry>>(StringComparer.OrdinalIgnoreCase);

    static bool isInitialized;
    static Task initializationTask;

    public static async Task<TexturesStructure> GetTextureSetAsync(string articleName, string category)
    {
        await EnsureInitializedAsync();

        if (CategoryTextures.Count == 0)
            return null;

        string normalizedCategory = NormalizeKey(category);
        if (!CategoryTextures.TryGetValue(normalizedCategory, out List<TextureEntry> entries) || entries.Count == 0)
        {
            normalizedCategory = NormalizeKey(WikipediaRuntimeUtility.DefaultTopCategory);
            if (!CategoryTextures.TryGetValue(normalizedCategory, out entries) || entries.Count == 0)
                return null;
        }

        int index = GetDeterministicIndex($"{articleName}|{category}", entries.Count);
        TextureEntry selected = entries[index];

        return new TexturesStructure
        {
            images = new ImagesStructure
            {
                wall = selected.texture_wall,
                floor = selected.texture_floor,
                bookcase = selected.texture_bookcase,
            },
        };
    }

    static async Task EnsureInitializedAsync()
    {
        if (isInitialized)
            return;

        if (initializationTask == null)
            initializationTask = LoadCacheAsync();

        await initializationTask;
    }

    static async Task LoadCacheAsync()
    {
        string json = await WikipediaRuntimeUtility.LoadStreamingAssetTextAsync("cached_textures.json");
        if (string.IsNullOrWhiteSpace(json))
        {
            isInitialized = true;
            return;
        }

        try
        {
            JObject root = JObject.Parse(json);
            foreach (KeyValuePair<string, JToken> property in root)
            {
                if (!(property.Value is JArray entriesArray))
                    continue;

                string normalizedKey = NormalizeKey(property.Key);
                var entries = new List<TextureEntry>();
                foreach (JToken token in entriesArray)
                {
                    TextureEntry entry = token.ToObject<TextureEntry>();
                    if (entry == null ||
                        string.IsNullOrWhiteSpace(entry.texture_wall) ||
                        string.IsNullOrWhiteSpace(entry.texture_floor) ||
                        string.IsNullOrWhiteSpace(entry.texture_bookcase))
                    {
                        continue;
                    }

                    entries.Add(entry);
                }

                if (entries.Count > 0)
                    CategoryTextures[normalizedKey] = entries;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[LocalTextureCacheService] Failed to parse cached_textures.json: {exception.Message}");
        }

        isInitialized = true;
    }

    static string NormalizeKey(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? WikipediaRuntimeUtility.DefaultTopCategory
            : value.Trim().Replace('_', ' ');
    }

    static int GetDeterministicIndex(string seed, int count)
    {
        if (count <= 0)
            return 0;

        unchecked
        {
            const int fnvPrime = 16777619;
            int hash = (int)2166136261;
            foreach (char character in seed ?? string.Empty)
            {
                hash ^= character;
                hash *= fnvPrime;
            }

            if (hash == int.MinValue)
                hash = int.MaxValue;

            return Mathf.Abs(hash) % count;
        }
    }

    [Serializable]
    sealed class TextureEntry
    {
        public string texture_id;
        public string texture_wall;
        public string texture_floor;
        public string texture_bookcase;
    }
}
