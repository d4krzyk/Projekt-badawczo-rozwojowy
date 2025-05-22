[System.Serializable]
public class ArticleStructure
{
    public string name;
    public string category;
    public Paragraph[] paragraphs;
}

[System.Serializable]
public class Paragraph
{
    public string name;
    public string content;
}