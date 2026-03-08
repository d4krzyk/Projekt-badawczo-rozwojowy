using Newtonsoft.Json;

[System.Serializable]
public class TexturesStructure
{
    [JsonProperty("data")]
    public ImagesStructure images;
}

[System.Serializable]
public class ImagesStructure
{
    [JsonProperty("texture_wall")]
    public string wall;
    [JsonProperty("texture_floor")]
    public string floor;
    [JsonProperty("texture_bookcase")]
    public string bookcase;
}