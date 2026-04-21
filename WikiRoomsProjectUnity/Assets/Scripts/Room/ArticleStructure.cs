using UnityEngine;

[System.Serializable]
public class ArticleStructure
{
    public string name;
    public string url;
    public string category;
    public Section[] content;
}

[System.Serializable]
public class Section
{
    public string name;
    public string content;
    public Section[] subsections;
}