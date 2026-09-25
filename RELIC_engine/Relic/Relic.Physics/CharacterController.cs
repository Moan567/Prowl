namespace Relic.Physics;

using Relic.Framework;

/// <summary>
/// Original Relic character controller — arena-shooter inspired, not a Quake port.
/// Uses Z-up, swept AABB against brush planes, with friction, air control, and step handling.
/// Study SharpQuake movement **concepts** (acceleration-based, ground vs air) then re-implemented distinct.
/// </summary>
public sealed class CharacterController
{
    private readonly PhysicsWorld _world;

    public Vector3 Position { get; private set; }
    public Vector3 Velocity { get; private set; }
    public bool IsOnGround { get; private set; }
    public Vector3 GroundNormal { get; private set; } = new(0, 0, 1);
    public float Yaw { get; set; } // radians, around Z

    // Tuning — Relic original (arena feel, not Quake values)
    public float WalkSpeed { get; set; } = 160f;
    public float RunSpeed { get; set; } = 320f;
    public float SprintMultiplier { get; set; } = 1.5f;
    public float JumpSpeed { get; set; } = 280f;
    public float Gravity { get; set; } = 900f;
    public float GroundFriction { get; set; } = 8f;
    public float GroundAcceleration { get; set; } = 1200f;
    public float AirAcceleration { get; set; } = 900f;
    public float AirFriction { get; set; } = 0.2f;
    public float MaxStepHeight { get; set; } = 18f;
    public float EyeHeight { get; set; } = 64f * 0.9f;
    public float SlopeLimit { get; set; } = 0.7f; // min Z of walkable normal

    // Hull — standing AABB (Z-up, half extents)
    public Vector3 HalfExtents { get; set; } = new(16f, 16f, 36f); // 32x32x72
    public float StepCheckEpsilon { get; set; } = 0.1f;

    public CharacterController(PhysicsWorld world, Vector3 startPosition)
    {
        _world = world;
        Position = startPosition;
        Velocity = Vector3.Zero;
    }

    public void Teleport(Vector3 pos)
    {
        Position = pos;
        Velocity = Vector3.Zero;
    }

    public Vector3 EyePosition => Position + new Vector3(0, 0, EyeHeight);

    public void SetYaw(float yaw) => Yaw = yaw;

    public Vector3 GetForward() => new(MathF.Cos(Yaw), MathF.Sin(Yaw), 0);
    public Vector3 GetRight() => new(-MathF.Sin(Yaw), MathF.Cos(Yaw), 0);

    /// <summary>
    /// WishDir: horizontal unit vector from input (already includes yaw). Length may be 0-1 for analog.
    /// sprint: if true use RunSpeed * Sprint
    /// jumpPressed: edge-triggered jump
    /// dt: delta time
    /// </summary>
    public void Move(Vector3 wishDir, bool sprint, bool jumpPressed, float dt)
    {
        wishDir.Z = 0;
        float wishLen = wishDir.Length();
        if (wishLen > 1f) { wishDir /= wishLen; wishLen = 1f; }

        float targetSpeed = (sprint ? RunSpeed * SprintMultiplier : (wishLen > 0.1f && sprint ? RunSpeed : WalkSpeed)) * wishLen;
        // Simpler: if sprint held use run*sprint else walk; if moving
        if (wishLen < 0.01f) targetSpeed = 0;

        // Ground check at start
        UpdateGroundState();

        // Jump — only if on ground
        if (jumpPressed && IsOnGround)
        {
            Velocity = new Vector3(Velocity.X, Velocity.Y, JumpSpeed);
            IsOnGround = false;
            GroundNormal = new Vector3(0, 0, 1);
        }

        // Apply gravity if not grounded
        if (!IsOnGround)
        {
            Velocity -= new Vector3(0, 0, Gravity * dt);
        }

        // Horizontal movement: accelerate
        Vector3 horizontalVel = new(Velocity.X, Velocity.Y, 0);
        float horizSpeed = horizontalVel.Length();

        if (IsOnGround)
        {
            // Friction
            if (horizSpeed > 0.01f && targetSpeed == 0)
            {
                float drop = horizSpeed * GroundFriction * dt;
                float newSpeed = MathF.Max(0, horizSpeed - drop);
                horizontalVel *= (newSpeed / horizSpeed);
            }

            // Ground accelerate toward wishDir
            if (wishLen > 0)
            {
                float currSpeedInWish = Vector3.Dot(horizontalVel, wishDir);
                float addSpeed = targetSpeed - currSpeedInWish;
                if (addSpeed > 0)
                {
                    float accel = GroundAcceleration * dt;
                    if (accel > addSpeed) accel = addSpeed;
                    horizontalVel += wishDir * accel;
                }
            }
        }
        else
        {
            // Air: small friction, air accel limited
            if (horizSpeed > 0)
            {
                // light air friction
                float drop = horizSpeed * AirFriction * dt * 0.1f;
                horizontalVel *= MathF.Max(0, (horizSpeed - drop) / horizSpeed);
            }
            if (wishLen > 0)
            {
                float curr = Vector3.Dot(horizontalVel, wishDir);
                float add = targetSpeed - curr;
                if (add > 0)
                {
                    float accel = AirAcceleration * dt;
                    if (accel > add) accel = add;
                    horizontalVel += wishDir * accel;
                }
            }
        }

        Velocity = new Vector3(horizontalVel.X, horizontalVel.Y, Velocity.Z);

        // Integrate horizontal + vertical with collision
        Vector3 desired = Velocity * dt;
        MoveWithCollision(desired);

        // Re-evaluate ground after move
        UpdateGroundState();

        // If landed on slope that is too steep, slide
        if (IsOnGround && GroundNormal.Z < SlopeLimit)
        {
            // Treat as not grounded, slide down slope
            IsOnGround = false;
        }
    }

    private void UpdateGroundState()
    {
        // Check a little below feet
        float checkDist = 2f;
        var res = _world.SweepAABB(Position, Position - new Vector3(0, 0, checkDist), HalfExtents);
        if (res.Hit && res.HitNormal.Z > SlopeLimit)
        {
            IsOnGround = true;
            GroundNormal = res.HitNormal;
        }
        else
        {
            // Also consider if velocity is upward, not grounded
            if (Velocity.Z > 0.1f) IsOnGround = false;
            else
            {
                // Secondary check: point just below
                var probe = _world.Raycast(Position, new Vector3(0, 0, -1), HalfExtents.Z + checkDist);
                if (probe.Hit && probe.Normal.Z > SlopeLimit) { IsOnGround = true; GroundNormal = probe.Normal; }
                else IsOnGround = false;
            }
        }
    }

    private void MoveWithCollision(Vector3 delta)
    {
        if (delta.LengthSquared() < 1e-8f) return;

        // Try direct sweep
        var res = _world.SweepAABB(Position, Position + delta, HalfExtents);

        if (!res.Hit || res.Fraction >= 1f - 1e-4f)
        {
            Position += delta;
            if (res.Hit) ClipVelocity(res.HitNormal);
            return;
        }

        // Hit before end — move up to hit with small epsilon backoff
        float fraction = MathF.Max(0, res.Fraction - 0.001f);
        Position += delta * fraction;

        // Step handling: if on ground and hit is wall (not floor), try stepping over
        if (IsOnGround && res.HitNormal.Z < 0.3f) // wall
        {
            if (TryStepMove(delta, res.HitNormal)) return;
        }

        // Slide: clip velocity along plane and try residual move
        Vector3 remaining = delta * (1f - res.Fraction);
        ClipVelocity(res.HitNormal);

        // Project remaining onto plane (remove component into wall)
        float into = Vector3.Dot(remaining, res.HitNormal);
        if (into < 0) remaining -= res.HitNormal * into;

        // Second sweep with remaining
        if (remaining.LengthSquared() > 1e-6f)
        {
            var res2 = _world.SweepAABB(Position, Position + remaining, HalfExtents);
            if (!res2.Hit || res2.Fraction >= 1f - 1e-4f)
            {
                Position += remaining;
                if (res2.Hit) ClipVelocity(res2.HitNormal);
            }
            else
            {
                float f2 = MathF.Max(0, res2.Fraction - 0.001f);
                Position += remaining * f2;
                ClipVelocity(res2.HitNormal);
                // Could iterate once more but 2 slides sufficient for arena shooter
            }
        }
    }

    private bool TryStepMove(Vector3 delta, Vector3 hitNormal)
    {
        // Try moving up by step height, forward, then down
        // 1) up
        Vector3 up = new(0, 0, MaxStepHeight);
        var upRes = _world.SweepAABB(Position, Position + up, HalfExtents);
        float upFrac = upRes.Hit ? upRes.Fraction - 0.001f : 1f;
        if (upFrac <= 0.01f) return false; // blocked upward
        Vector3 steppedPos = Position + up * upFrac;

        // 2) forward from stepped height
        // Clip delta to not go into wall again? Use same delta but from higher
        Vector3 forwardDelta = delta;
        // Remove component into wall for forward attempt? Keep original wish
        var fwdRes = _world.SweepAABB(steppedPos, steppedPos + forwardDelta, HalfExtents);
        float fwdFrac = fwdRes.Hit ? MathF.Max(0, fwdRes.Fraction - 0.001f) : 1f;
        if (fwdFrac < 0.01f)
        {
            // Still blocked after stepping, try sliding forward a bit with step? fail
            return false;
        }
        steppedPos += forwardDelta * fwdFrac;

        // 3) down to ground from stepped height
        var downRes = _world.SweepAABB(steppedPos, steppedPos - up, HalfExtents);
        float downFrac = downRes.Hit ? downRes.Fraction : 1f;
        // Only accept step if we land on walkable floor
        if (downRes.Hit && downRes.HitNormal.Z > SlopeLimit)
        {
            steppedPos -= up * downFrac;
            // Commit step
            Position = steppedPos;
            // Keep horizontal velocity, zero vertical step impact
            return true;
        }

        // If no ground below stepped position (step leads off cliff), reject step and do normal slide
        return false;
    }

    private void ClipVelocity(Vector3 normal)
    {
        float into = Vector3.Dot(Velocity, normal);
        if (into < 0)
            Velocity -= normal * into;
        // Small epsilon to prevent sticking due to floating error
        if (Velocity.LengthSquared() < 0.01f) Velocity = Vector3.Zero;
    }
}
