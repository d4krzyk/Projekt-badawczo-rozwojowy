using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

public class ValueFlexibleConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(object);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        JToken token = JToken.Load(reader);

        // STRING
        if (token.Type == JTokenType.String)
            return token.ToString();

        // ARRAY
        if (token.Type == JTokenType.Array)
        {
            var list = new List<object>();

            foreach (var item in token)
            {
                // plain text
                if (item.Type == JTokenType.String)
                {
                    list.Add(item.ToString());
                }
                else
                {
                    // full ValueRaw object
                    list.Add(item.ToObject<ValueRaw>(serializer));
                }
            }

            return list;
        }

        return null;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, value);
    }
}
