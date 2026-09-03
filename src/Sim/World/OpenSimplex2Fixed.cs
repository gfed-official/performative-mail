namespace PerformativeMail.Sim.World;

internal static class OpenSimplex2Fixed
{
    public const int One = 65536;

    private const long PrimeX = 0x5205402B9270C86FL;
    private const long PrimeY = 0x598CD327003817B5L;
    private const long HashMultiplier = 0x53A3F72DEEC546F5L;
    private const int NGrads2dExponent = 7;
    private const int NGrads2d = 1 << NGrads2dExponent;

    private const int Skew2d = 23988;
    private const int Unskew2d = -13849;
    private const int RSquared2d = 32768;
    private const int A1T = -206746;
    private const int A1C = -43691;
    private const int OnePlus2Unskew = 37837;
    private const int UnskewPlus1 = 51687;

    private static readonly int[] Gradients2d;

    static OpenSimplex2Fixed()
    {
        int[] unit = {
            2503863, 6044859, 6044859, 2503863, 6044859, -2503863, 2503863, -6044859,
            -2503863, -6044859, -6044859, -2503863, -6044859, 2503863, -2503863, 6044859,
            854021, 6486933, 3983070, 5190838, 5190838, 3983070, 6486933, 854021,
            6486933, -854021, 5190838, -3983070, 3983070, -5190838, 854021, -6486933,
            -854021, -6486933, -3983070, -5190838, -5190838, -3983070, -6486933, -854021,
            -6486933, 854021, -5190838, 3983070, -3983070, 5190838, -854021, 6486933,
        };

        Gradients2d = new int[NGrads2d * 2];
        for (int i = 0, j = 0; i < Gradients2d.Length; i++, j++)
        {
            if (j == unit.Length) j = 0;
            Gradients2d[i] = unit[j];
        }
    }

    public static int Noise2(long seed, int x, int y)
    {
        int s = Mul(Skew2d, x + y);
        return Noise2UnskewedBase(seed, x + s, y + s);
    }

    private static int Noise2UnskewedBase(long seed, int xs, int ys)
    {
        int xsb = xs >> 16;
        int ysb = ys >> 16;
        int xi = xs - (xsb << 16);
        int yi = ys - (ysb << 16);

        long xsbp = xsb * PrimeX;
        long ysbp = ysb * PrimeY;

        int t = Mul(xi + yi, Unskew2d);
        int dx0 = xi + t;
        int dy0 = yi + t;

        int value = 0;
        int a0 = RSquared2d - Mul(dx0, dx0) - Mul(dy0, dy0);
        if (a0 > 0)
            value = Kernel(a0, Grad(seed, xsbp, ysbp, dx0, dy0));

        int a1 = Mul(A1T, t) + A1C + a0;
        if (a1 > 0)
        {
            int dx1 = dx0 - OnePlus2Unskew;
            int dy1 = dy0 - OnePlus2Unskew;
            value += Kernel(a1, Grad(seed, xsbp + PrimeX, ysbp + PrimeY, dx1, dy1));
        }

        if (dy0 > dx0)
        {
            int dx2 = dx0 - Unskew2d;
            int dy2 = dy0 - UnskewPlus1;
            int a2 = RSquared2d - Mul(dx2, dx2) - Mul(dy2, dy2);
            if (a2 > 0)
                value += Kernel(a2, Grad(seed, xsbp, ysbp + PrimeY, dx2, dy2));
        }
        else
        {
            int dx2 = dx0 - UnskewPlus1;
            int dy2 = dy0 - Unskew2d;
            int a2 = RSquared2d - Mul(dx2, dx2) - Mul(dy2, dy2);
            if (a2 > 0)
                value += Kernel(a2, Grad(seed, xsbp + PrimeX, ysbp, dx2, dy2));
        }

        return value;
    }

    private static int Kernel(int a, int grad) => Mul(Mul(Mul(a, a), Mul(a, a)), grad);

    private static int Grad(long seed, long xsvp, long ysvp, int dx, int dy)
    {
        long hash = seed ^ xsvp ^ ysvp;
        hash *= HashMultiplier;
        hash ^= hash >> (64 - NGrads2dExponent + 1);
        int gi = (int)hash & ((NGrads2d - 1) << 1);
        return Mul(Gradients2d[gi], dx) + Mul(Gradients2d[gi | 1], dy);
    }

    internal static int Mul(int a, int b) => (int)(((long)a * b) >> 16);
}
