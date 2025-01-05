namespace Myth;

public class MythToken
{
    public MythTokenType Type;
    public string Text; // 例如 "AllRoomLevel" 或 "==" 或 "2" 等

    public MythToken(MythTokenType type, string text)
    {
        this.Type = type;
        this.Text = text;
    }

    public override string ToString()
    {
        return $"{Type}({Text})";
    }
}
