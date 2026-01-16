using System.Numerics;

namespace PZShrinker.Lib;

public class VertexHashKey(Vector3 pos, Vector3 norm, Vector2 uv)
{
    public readonly Vector3 Position = pos;
    public readonly Vector3 Normal = norm;
    public readonly Vector2 TexCoord = uv;

    public static bool ApproximatelyEqual(Vector2 a, Vector2 b, float epsilon = 1e-5f)
    => Math.Abs(a.X - b.X) < epsilon && Math.Abs(a.Y - b.Y) < epsilon;
    public static bool ApproximatelyEqual(Vector3 a, Vector3 b, float epsilon = 1e-5f)
    => Math.Abs(a.X - b.X) < epsilon && Math.Abs(a.Y - b.Y) < epsilon && Math.Abs(a.Z - b.Z) < epsilon;

    public bool Equals(VertexHashKey other)
    {
        return ApproximatelyEqual(Position, other.Position) &&
                ApproximatelyEqual(Normal, other.Normal) &&
                ApproximatelyEqual(TexCoord, other.TexCoord);
    }

    public override bool Equals(object? obj) => obj is VertexHashKey other && Equals(other);

    public override int GetHashCode()
    {
        return HashCode.Combine(Position, Normal, TexCoord);
    }
}
