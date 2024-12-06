namespace Transcoder.Common.MessageModels;

public record Resolution(int Width, int Height)
{
    public override string ToString() { return $"{Width}:{Height}"; }
}
