[System.Serializable]
public class ArticleStructure
{
    public string name;
    public string category;
    public string url;
    public Sections[] sections;
}

[System.Serializable]
public class Sections
{
    public string name;
    public string content;
    public Sections[] subsections;
}