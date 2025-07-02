[System.Serializable]
public class ArticleStructure
{
    public string name;
    public string url;
    public string category;
    public Sections[] content;
}

[System.Serializable]
public class Sections
{
    public string name;
    public string content;
    public Sections[] subsections;
}