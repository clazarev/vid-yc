namespace Transcoder.Chunker;
internal static class ResolutionCalculator
{
    private static readonly Dictionary<(int x, int y), (int Width, int Height)> s_ratioToSd = new()
    {
        {(4, 3), (640, 480)},
        {(3, 4),(480, 640)},
        {(16, 9), (640, 360)},
        {(9, 16),(360, 640)},
        {(16, 10), (640, 400)},
        {(10, 16),(400, 640)},
    };

    private static readonly Dictionary<(int x, int y), (int Width, int Height)> s_ratioToHd = new()
    {
        {(4, 3), (1280, 960)},
        {(3, 4),(960, 1280)},
        {(16, 9), (1280, 720)},
        {(9, 16),(720, 1280)},
        {(16, 10), (1280, 800)},
        {(10, 16),(800, 1280)},
    };

    private static readonly Dictionary<(int x, int y), (int Width, int Height)> s_ratioToFullHd = new()
    {
        {(4, 3), (1920, 1440)},
        {(3, 4),(1440, 1920)},
        {(16, 9), (1920, 1080)},
        {(9, 16),(1080, 1920)},
        {(16, 10), (1920, 1200)},
        {(10, 16),(1200, 1920)},
    };

    private static readonly Dictionary<(int x, int y), (int Width, int Height)> s_rationToUhd = new()
    {
        {(4, 3), (3840, 2880)},
        {(3, 4),(2880, 3840)},
        {(16, 9), (3840, 2160)},
        {(9, 16),(2160, 3840)},
        {(16, 10), (3840, 2400)},
        {(10, 16),(2400, 3840)},
    };

    public const int SdResolution = 640 * 360;
    public const int HdResolution = 1280 * 720;
    public const int FullHdResolution = 1920 * 1080;
    public static (int Width, int Height) ChooseBaseSize(int x, int y, bool useSdResolution)
    {
        return useSdResolution ? s_ratioToSd.GetValueOrDefault((x, y)) : s_ratioToHd.GetValueOrDefault((x, y));
    }
    public static List<(int Width, int Height)> CalculateSizes(int x, int y)
    {
        List<(int Width, int Height)> res =
        [
            s_ratioToSd.GetValueOrDefault((x, y)),
            s_ratioToHd.GetValueOrDefault((x, y)),
            s_ratioToFullHd.GetValueOrDefault((x, y)),
            s_rationToUhd.GetValueOrDefault((x, y)),
        ];
        return res;
    }

    private static int Gcd(int a, int b)
    {
        while (b != 0)
        {
            int temp = b;
            b = a % b;
            a = temp;
        }
        return a;
    }

    public static (int x, int y) CalculateAspectRatio(int Width, int Height)
    {
        int gcd = Gcd(Width, Height);
        return (Width / gcd, Height / gcd);
    }
}
