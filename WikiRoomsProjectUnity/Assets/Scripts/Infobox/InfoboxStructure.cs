using System;
using System.Collections.Generic;
using Newtonsoft.Json;

[Serializable]
public class WikiPageRaw {
    public string page_name;
    public List<List<InfoboxItemRaw>> infobox;
}

[Serializable]
public class InfoboxItemRaw {
    public string @class;
    public LabelRaw label;
    public List<ValueRaw> value;
}

[Serializable]
public class LabelRaw {
    public string @class;
    public string value;
}

[Serializable]
public class ValueRaw {
    public string @class;

    [JsonConverter(typeof(ValueFlexibleConverter))]
    public object value;

    public string text;
    public string href;
    public List<object> caption;
}