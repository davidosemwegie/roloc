using System;

namespace Roloc.Core
{
    /// <summary>Protocol v1 PRNG. Its modulo behavior is mirrored by the server.</summary>
    public sealed class DailyRandom : Random
    {
        uint state;
        public uint State => state;
        public DailyRandom(uint seed) { Reset(seed); }
        public void Reset(uint seed) { state = seed == 0 ? 0x6D2B79F5u : seed; }
        public uint NextUInt()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }
        public override int Next(int maxValue)
        {
            if (maxValue <= 0) throw new ArgumentOutOfRangeException(nameof(maxValue));
            return (int)(NextUInt() % (uint)maxValue);
        }
        public override int Next(int minValue, int maxValue)
        {
            if (maxValue <= minValue) throw new ArgumentOutOfRangeException(nameof(maxValue));
            return minValue + Next(maxValue - minValue);
        }
    }

    [Serializable]
    public struct BoardPoint
    {
        public int X, Y;
        public BoardPoint(int x, int y) { X = x; Y = y; }
    }

    /// <summary>
    /// Daily v1 uses integer milliseconds and 1,000 coordinate units per UI point.
    /// Fixed hit geometry is independent of cosmetic scale, frame rate and device pixels.
    /// </summary>
    public static class DailyRules
    {
        public const int Version = 1;
        public const int CoordinateScale = 1000;
        public const int RingRadius = 63500;
        public const int PerfectRadius = 22225;
        public const int MaximumCoordinate = 1000000;
        static readonly BoardPoint[] RingSlots = {
            new BoardPoint(-103000, 152000), new BoardPoint(103000, 152000),
            new BoardPoint(-103000, -152000), new BoardPoint(103000, -152000)
        };
        static readonly BoardPoint[] PuckSlots = {
            new BoardPoint(-47000, 53000), new BoardPoint(47000, 53000),
            new BoardPoint(-47000, -53000), new BoardPoint(47000, -53000)
        };

        public static BoardPoint RingCenter(int slot, int color, int round, uint seed,
            FlowMode mode, BoardStyle style, int elapsedMs)
        {
            if (slot < 0 || slot > 3) throw new ArgumentOutOfRangeException(nameof(slot));
            var center = RingSlots[slot];
            if (style != BoardStyle.Lively || mode != FlowMode.Drifting) return center;
            return Offset(center, seed, round, color, elapsedMs, 8000, 5200);
        }

        public static BoardPoint PuckHome(int slot, int color, int activeColor, int round,
            uint seed, FlowMode mode, BoardStyle style, int elapsedMs)
        {
            if (slot < 0 || slot > 3) throw new ArgumentOutOfRangeException(nameof(slot));
            var center = PuckSlots[slot];
            if (style != BoardStyle.Lively || mode != FlowMode.Floating || color == activeColor) return center;
            return Offset(center, seed, round, color, elapsedMs, 3000, 2000);
        }

        public static bool Within(BoardPoint point, BoardPoint center, int radius)
        {
            if (point.X < -MaximumCoordinate || point.X > MaximumCoordinate
                || point.Y < -MaximumCoordinate || point.Y > MaximumCoordinate) return false;
            long dx = (long)point.X - center.X, dy = (long)point.Y - center.Y;
            return dx * dx + dy * dy <= (long)radius * radius;
        }

        static BoardPoint Offset(BoardPoint center, uint seed, int round, int color,
            int elapsedMs, int amplitudeX, int amplitudeY)
        {
            long time = Math.Max(0, elapsedMs);
            long phase = (seed % 8000u + (long)round * 977 + (long)color * 1999 + time) % 8000;
            long blend = Math.Min(time, 500);
            center.X += (int)(Wave(phase) * amplitudeX * blend / 500000);
            center.Y += (int)(Wave((phase + 2000) % 8000) * amplitudeY * blend / 500000);
            return center;
        }

        static long Wave(long phase)
        {
            long u = phase <= 4000 ? phase : 8000 - phase;
            long eased = u * u * (12000 - 2 * u) * 1000 / 64000000000L;
            return 2 * eased - 1000;
        }
    }
}
