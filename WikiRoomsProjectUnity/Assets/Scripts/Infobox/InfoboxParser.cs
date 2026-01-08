using Newtonsoft.Json;

public class InfoboxParser
{
    public static WikiPageRaw Parse(string json)
    {
        return JsonConvert.DeserializeObject<WikiPageRaw>(json);
    }
}
