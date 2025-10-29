using UnityEngine;
using UnityEngine.UIElements;

public class DataCollectionUtil : MonoBehaviour
{

    const float SQRT3 = 1.73205080757f;
    public const float HEX_SIZE = 1.0f;
    public const float HEX_WIDTH = SQRT3 * HEX_SIZE;
    public const float HEX_HEIGHT = 2.0f * HEX_SIZE;

    Vector2Int Vector2IntRound(Vector2 pos)
    {
        float xgrid = Mathf.Round(pos.x);
        float ygrid = Mathf.Round(pos.y);

        float xrem = pos.x - xgrid;
        float yrem = pos.y - ygrid;

        if (Mathf.Abs(xrem) >= Mathf.Abs(yrem))
        {
            return new Vector2Int(Mathf.FloorToInt(xgrid + Mathf.Round(xrem + 0.5f * yrem)), Mathf.FloorToInt(ygrid));
        }
        else
        {
            return new Vector2Int(Mathf.FloorToInt(xgrid), Mathf.FloorToInt(ygrid + Mathf.Round(yrem + 0.5f * xrem)));
        }
    }

    public static Vector2Int PixelToHexPos(Vector2 pixelPos) 
    {
        float x = (pixelPos.x - HEX_WIDTH / 2.0f) / HEX_SIZE;
        float y = (pixelPos.y - HEX_HEIGHT / 2.0f) / HEX_SIZE;

        float xx = SQRT3 / 3.0f * x - 1.0f / 3.0f * y;
        float yy = 2.0f / 3.0f * y;

        return new Vector2Int(Mathf.FloorToInt(xx), Mathf.FloorToInt(yy));
    }

}
