namespace Relic.Framework;

public struct Vector2 : IEquatable<Vector2>
{
    public float X;
    public float Y;

    public static readonly Vector2 Zero = new(0f, 0f);
    public static readonly Vector2 One = new(1f, 1f);
    public static readonly Vector2 UnitX = new(1f, 0f);
    public static readonly Vector2 UnitY = new(0f, 1f);

    public Vector2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public Vector2(float value)
    {
        X = value;
        Y = value;
    }

    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2 operator *(Vector2 a, float s) => new(a.X * s, a.Y * s);
    public static Vector2 operator *(float s, Vector2 a) => new(a.X * s, a.Y * s);
    public static Vector2 operator /(Vector2 a, float s) => new(a.X / s, a.Y / s);

    public float Length() => MathF.Sqrt(X * X + Y * Y);
    public float LengthSquared() => X * X + Y * Y;

    public void Normalize()
    {
        float len = Length();
        if (len > 0f)
        {
            X /= len;
            Y /= len;
        }
    }

    public static Vector2 Normalize(Vector2 v)
    {
        v.Normalize();
        return v;
    }

    public static float Dot(Vector2 a, Vector2 b) => a.X * b.X + a.Y * b.Y;

    public override bool Equals(object? obj) => obj is Vector2 v && Equals(v);
    public bool Equals(Vector2 other) => X == other.X && Y == other.Y;
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"({X}, {Y})";

    public static bool operator ==(Vector2 a, Vector2 b) => a.Equals(b);
    public static bool operator !=(Vector2 a, Vector2 b) => !a.Equals(b);
}

public struct Vector3 : IEquatable<Vector3>
{
    public float X;
    public float Y;
    public float Z;

    public static readonly Vector3 Zero = new(0f, 0f, 0f);
    public static readonly Vector3 One = new(1f, 1f, 1f);
    public static readonly Vector3 UnitX = new(1f, 0f, 0f);
    public static readonly Vector3 UnitY = new(0f, 1f, 0f);
    public static readonly Vector3 UnitZ = new(0f, 0f, 1f);
    // Relic is permanently Z-Up (X forward, Y right, Z up)
    public static readonly Vector3 Up = new(0f, 0f, 1f);
    public static readonly Vector3 Down = new(0f, 0f, -1f);
    public static readonly Vector3 Forward = new(1f, 0f, 0f);
    public static readonly Vector3 Backward = new(-1f, 0f, 0f);
    public static readonly Vector3 Right = new(0f, 1f, 0f);
    public static readonly Vector3 Left = new(0f, -1f, 0f);

    public Vector3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public Vector3(float value)
    {
        X = value;
        Y = value;
        Z = value;
    }

    public Vector3(Vector2 xy, float z)
    {
        X = xy.X;
        Y = xy.Y;
        Z = z;
    }

    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3 operator *(Vector3 a, float s) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vector3 operator *(float s, Vector3 a) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vector3 operator /(Vector3 a, float s) => new(a.X / s, a.Y / s, a.Z / s);
    public static Vector3 operator -(Vector3 a) => new(-a.X, -a.Y, -a.Z);

    public float Length() => MathF.Sqrt(X * X + Y * Y + Z * Z);
    public float LengthSquared() => X * X + Y * Y + Z * Z;

    public void Normalize()
    {
        float len = Length();
        if (len > 0f)
        {
            X /= len;
            Y /= len;
            Z /= len;
        }
    }

    public static Vector3 Normalize(Vector3 v)
    {
        v.Normalize();
        return v;
    }

    public static float Dot(Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    public static Vector3 Cross(Vector3 a, Vector3 b) => new(
        a.Y * b.Z - a.Z * b.Y,
        a.Z * b.X - a.X * b.Z,
        a.X * b.Y - a.Y * b.X
    );

    public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + (b - a) * t;

    public static Vector3 Transform(Vector3 v, Matrix4x4 m) => new(
        v.X * m.M11 + v.Y * m.M21 + v.Z * m.M31 + m.M41,
        v.X * m.M12 + v.Y * m.M22 + v.Z * m.M32 + m.M42,
        v.X * m.M13 + v.Y * m.M23 + v.Z * m.M33 + m.M43
    );

    public static Vector3 TransformNormal(Vector3 v, Matrix4x4 m) => new(
        v.X * m.M11 + v.Y * m.M21 + v.Z * m.M31,
        v.X * m.M12 + v.Y * m.M22 + v.Z * m.M32,
        v.X * m.M13 + v.Y * m.M23 + v.Z * m.M33
    );

    public override bool Equals(object? obj) => obj is Vector3 v && Equals(v);
    public bool Equals(Vector3 other) => X == other.X && Y == other.Y && Z == other.Z;
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    public override string ToString() => $"({X}, {Y}, {Z})";

    public static bool operator ==(Vector3 a, Vector3 b) => a.Equals(b);
    public static bool operator !=(Vector3 a, Vector3 b) => !a.Equals(b);
}

public struct Matrix4x4 : IEquatable<Matrix4x4>
{
    public float M11, M12, M13, M14;
    public float M21, M22, M23, M24;
    public float M31, M32, M33, M34;
    public float M41, M42, M43, M44;

    public static readonly Matrix4x4 Identity = new(
        1f, 0f, 0f, 0f,
        0f, 1f, 0f, 0f,
        0f, 0f, 1f, 0f,
        0f, 0f, 0f, 1f
    );

    public Matrix4x4(
        float m11, float m12, float m13, float m14,
        float m21, float m22, float m23, float m24,
        float m31, float m32, float m33, float m34,
        float m41, float m42, float m43, float m44)
    {
        M11 = m11; M12 = m12; M13 = m13; M14 = m14;
        M21 = m21; M22 = m22; M23 = m23; M24 = m24;
        M31 = m31; M32 = m32; M33 = m33; M34 = m34;
        M41 = m41; M42 = m42; M43 = m43; M44 = m44;
    }

    public static Matrix4x4 CreateTranslation(Vector3 position) => new(
        1f, 0f, 0f, position.X,
        0f, 1f, 0f, position.Y,
        0f, 0f, 1f, position.Z,
        0f, 0f, 0f, 1f
    );

    public static Matrix4x4 CreateScale(Vector3 scale) => new(
        scale.X, 0f,     0f,     0f,
        0f,     scale.Y, 0f,     0f,
        0f,     0f,     scale.Z, 0f,
        0f,     0f,     0f,     1f
    );

    public static Matrix4x4 CreateRotationX(float radians)
    {
        float c = MathF.Cos(radians);
        float s = MathF.Sin(radians);
        return new(
            1f, 0f, 0f, 0f,
            0f, c,  -s, 0f,
            0f, s,  c,  0f,
            0f, 0f, 0f, 1f
        );
    }

    public static Matrix4x4 CreateRotationY(float radians)
    {
        float c = MathF.Cos(radians);
        float s = MathF.Sin(radians);
        return new(
            c,  0f, s,  0f,
            0f, 1f, 0f, 0f,
            -s, 0f, c,  0f,
            0f, 0f, 0f, 1f
        );
    }

    public static Matrix4x4 CreateRotationZ(float radians)
    {
        float c = MathF.Cos(radians);
        float s = MathF.Sin(radians);
        return new(
            c,  -s, 0f, 0f,
            s,  c,  0f, 0f,
            0f, 0f, 1f, 0f,
            0f, 0f, 0f, 1f
        );
    }

    // Z-Up: yaw around Z, pitch around Y, roll around X
    public static Matrix4x4 CreateFromYawPitchRoll(float yaw, float pitch, float roll)
    {
        return CreateRotationZ(yaw) * CreateRotationY(pitch) * CreateRotationX(roll);
    }

    public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b) => new(
        a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31 + a.M14 * b.M41,
        a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32 + a.M14 * b.M42,
        a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33 + a.M14 * b.M43,
        a.M11 * b.M14 + a.M12 * b.M24 + a.M13 * b.M34 + a.M14 * b.M44,

        a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31 + a.M24 * b.M41,
        a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32 + a.M24 * b.M42,
        a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33 + a.M24 * b.M43,
        a.M21 * b.M14 + a.M22 * b.M24 + a.M23 * b.M34 + a.M24 * b.M44,

        a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31 + a.M34 * b.M41,
        a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32 + a.M34 * b.M42,
        a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33 + a.M34 * b.M43,
        a.M31 * b.M14 + a.M32 * b.M24 + a.M33 * b.M34 + a.M34 * b.M44,

        a.M41 * b.M11 + a.M42 * b.M21 + a.M43 * b.M31 + a.M44 * b.M41,
        a.M41 * b.M12 + a.M42 * b.M22 + a.M43 * b.M32 + a.M44 * b.M42,
        a.M41 * b.M13 + a.M42 * b.M23 + a.M43 * b.M33 + a.M44 * b.M43,
        a.M41 * b.M14 + a.M42 * b.M24 + a.M43 * b.M34 + a.M44 * b.M44
    );

    public static Vector3 Transform(Vector3 v, Matrix4x4 m) => new(
        v.X * m.M11 + v.Y * m.M21 + v.Z * m.M31 + m.M41,
        v.X * m.M12 + v.Y * m.M22 + v.Z * m.M32 + m.M42,
        v.X * m.M13 + v.Y * m.M23 + v.Z * m.M33 + m.M43
    );

    public static Matrix4x4 CreatePerspectiveFieldOfView(float fovYRadians, float aspect, float near, float far)
    {
        float f = 1f / MathF.Tan(fovYRadians * 0.5f);
        float nf = 1f / (near - far);
        // OpenGL-style column-major perspective (right-handed, depth -1..1)
        return new Matrix4x4(
            f / aspect, 0, 0, 0,
            0, f, 0, 0,
            0, 0, (far + near) * nf, -1,
            0, 0, 2 * far * near * nf, 0
        );
    }

    public static Matrix4x4 CreateLookAt(Vector3 eye, Vector3 target, Vector3 up)
    {
        var f = Vector3.Normalize(target - eye);
        var s = Vector3.Normalize(Vector3.Cross(f, up));
        var u = Vector3.Cross(s, f);

        return new Matrix4x4(
            s.X, u.X, -f.X, 0,
            s.Y, u.Y, -f.Y, 0,
            s.Z, u.Z, -f.Z, 0,
            -Vector3.Dot(s, eye), -Vector3.Dot(u, eye), Vector3.Dot(f, eye), 1
        );
    }

    public static Matrix4x4 Transpose(Matrix4x4 m) => new(
        m.M11, m.M21, m.M31, m.M41,
        m.M12, m.M22, m.M32, m.M42,
        m.M13, m.M23, m.M33, m.M43,
        m.M14, m.M24, m.M34, m.M44
    );

    public float[] ToArray() => new[] { M11, M12, M13, M14, M21, M22, M23, M24, M31, M32, M33, M34, M41, M42, M43, M44 };

    public float[] ToColumnMajorArray() => new[]
    {
        M11, M21, M31, M41,
        M12, M22, M32, M42,
        M13, M23, M33, M43,
        M14, M24, M34, M44
    };

    public override bool Equals(object? obj) => obj is Matrix4x4 m && Equals(m);
    public bool Equals(Matrix4x4 other) =>
        M11 == other.M11 && M12 == other.M12 && M13 == other.M13 && M14 == other.M14 &&
        M21 == other.M21 && M22 == other.M22 && M23 == other.M23 && M24 == other.M24 &&
        M31 == other.M31 && M32 == other.M32 && M33 == other.M33 && M34 == other.M34 &&
        M41 == other.M41 && M42 == other.M42 && M43 == other.M43 && M44 == other.M44;

    public override int GetHashCode()
    {
        var h = new HashCode();
        h.Add(M11); h.Add(M12); h.Add(M13); h.Add(M14);
        h.Add(M21); h.Add(M22); h.Add(M23); h.Add(M24);
        h.Add(M31); h.Add(M32); h.Add(M33); h.Add(M34);
        h.Add(M41); h.Add(M42); h.Add(M43); h.Add(M44);
        return h.ToHashCode();
    }
    public override string ToString() => $"[{M11} {M12} {M13} {M14}]\n[{M21} {M22} {M23} {M24}]\n[{M31} {M32} {M33} {M34}]\n[{M41} {M42} {M43} {M44}]";

    public static bool operator ==(Matrix4x4 a, Matrix4x4 b) => a.Equals(b);
    public static bool operator !=(Matrix4x4 a, Matrix4x4 b) => !a.Equals(b);
}