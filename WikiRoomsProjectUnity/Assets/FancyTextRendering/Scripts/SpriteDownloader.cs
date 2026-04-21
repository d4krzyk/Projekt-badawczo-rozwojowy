using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;


public class SpriteDownloader
{
    [SerializeField] TMP_Text textComponent;
    public static async Task CreateSprite(string link, string spriteName)
    {
        UnityWebRequest www = UnityWebRequestTexture.GetTexture(link);
        var asyncOp = www.SendWebRequest();
        while (!asyncOp.isDone)
        {
            await Task.Yield();
        }

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(www.error);
        }
        else
        {
            Texture2D texture = DownloadHandlerTexture.GetContent(www);
            Sprite newSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
            AddSpriteToTMP(newSprite, spriteName);
            
        }
    }

   private static void AddSpriteToTMP(Sprite sprite, string spriteName)
    {
        // Ensure the text has a sprite asset
        TMP_SpriteAsset spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
        spriteAsset.name = "RuntimeSpriteAsset";

        // Create glyph and character for TMP
        int id = spriteAsset.spriteCharacterTable.Count;

        TMP_SpriteGlyph glyph = new TMP_SpriteGlyph
        {
            index = (uint)id,
            glyphRect = new UnityEngine.TextCore.GlyphRect(0, 0, (int)sprite.rect.width, (int)sprite.rect.height),
            metrics = new UnityEngine.TextCore.GlyphMetrics(sprite.rect.width, sprite.rect.height, 0, 0, sprite.rect.width),
            scale = 1.0f,
            sprite = sprite
        };

        TMP_SpriteCharacter character = new TMP_SpriteCharacter((uint)id, glyph)
        {
            name = spriteName
        };

        // Add into asset
        spriteAsset.spriteGlyphTable.Add(glyph);
        spriteAsset.spriteCharacterTable.Add(character);

        // Refresh lookups
        spriteAsset.UpdateLookupTables();
    }
}
