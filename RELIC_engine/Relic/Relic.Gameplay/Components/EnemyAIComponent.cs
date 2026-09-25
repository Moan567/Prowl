namespace Relic.Gameplay.Components;

using Relic.Framework;
using Relic.Physics;
using Relic.Gameplay.Animation;

public enum AIState { Idle, Patrol, Chase, Attack, Dead }

public sealed class EnemyAIComponent : Component
{
    public AIState State { get; private set; } = AIState.Idle;
    public float SightRange { get; set; } = 800f;
    public float AttackRange { get; set; } = 512f;
    public float FovDegrees { get; set; } = 120f;
    public float MoveSpeed { get; set; } = 120f;
    public float Damage { get; set; } = 25f;
    public float AttackCooldown { get; set; } = 1.0f;
    public float AttackDuration { get; set; } = 0.6f;
    public float PainDuration { get; set; } = 0.4f;

    private float _attackTimer;
    private float _stateTime;
    private bool _hasSeenPlayer;
    private Vector3 _patrolTarget;
    private Entity? _player;
    private PhysicsWorld? _world;
    private EntityManager? _manager;
    private AudioSystem? _audio;
    private HealthComponent? _health;
    private AnimatedModelComponent? _anim;

    // Enemy type configuration
    public void ConfigureFor(string className)
    {
        switch (className.ToLowerInvariant())
        {
            case "enemy_soldier":
                SightRange = 800f; AttackRange = 512f; MoveSpeed = 120f; Damage = 25f; AttackCooldown = 1.2f;
                break;
            case "enemy_dog":
                SightRange = 600f; AttackRange = 64f; MoveSpeed = 220f; Damage = 15f; AttackCooldown = 0.8f;
                break;
            case "enemy_demon":
                SightRange = 700f; AttackRange = 96f; MoveSpeed = 100f; Damage = 40f; AttackCooldown = 1.5f;
                break;
        }
    }

    public void Initialize(PhysicsWorld world, EntityManager manager, AudioSystem? audio = null)
    {
        _world = world;
        _manager = manager;
        _audio = audio;
        _health = Entity?.GetComponent<HealthComponent>();
        _anim = Entity?.GetComponent<AnimatedModelComponent>();
        if (_health != null)
        {
            _health.OnDamaged += OnDamaged;
            _health.OnDeath += OnDeath;
        }
        // Set initial animation
        _anim?.SetAnimation(AnimationState.Idle);
        // Find player
        _player = manager.FindFirstByClassName("player_start") ?? manager.FindFirstByClassName("info_player_start");
        if (_player == null) _player = manager.Entities.FirstOrDefault(e => e.GetComponent<PlayerComponent>() != null);
        _patrolTarget = Entity?.Transform.Position ?? Vector3.Zero;
    }

    public override void Initialize()
    {
        // Called when component added; manager/world may not be set yet - will be set via MapSpawner or Gameplay init
        _health = Entity?.GetComponent<HealthComponent>();
        _anim = Entity?.GetComponent<AnimatedModelComponent>();
        if (_health != null)
        {
            _health.OnDamaged += OnDamaged;
            _health.OnDeath += OnDeath;
        }
    }

    private void OnDamaged(float amount)
    {
        if (State == AIState.Dead) return;
        // Switch to Pain
        SetState(AIState.Attack); // briefly show pain? Actually set to Pain then back
        _anim?.SetAnimation(AnimationState.Pain);
        _stateTime = 0;
        _audio?.PlaySound3D("player/pain1.wav", Entity!.Transform.Position, 0.9f);
        Console.WriteLine($"[Enemy] Pain {Entity?.Id} health {Entity?.GetComponent<HealthComponent>()?.Health}");
    }

    private void OnDeath()
    {
        if (State == AIState.Dead) return;
        SetState(AIState.Dead);
        _anim?.SetAnimation(AnimationState.Death);
        _audio?.PlaySound3D("player/death1.wav", Entity!.Transform.Position, 1f);
        Console.WriteLine($"[Enemy] Dead {Entity?.Id}");
    }

    private void SetState(AIState newState)
    {
        if (State == newState) return;
        State = newState;
        _stateTime = 0;
        // Animation mapping
        switch (newState)
        {
            case AIState.Idle: _anim?.SetAnimation(AnimationState.Idle); break;
            case AIState.Patrol:
            case AIState.Chase: _anim?.SetAnimation(AnimationState.Walk); break;
            case AIState.Attack: _anim?.SetAnimation(AnimationState.Attack); break;
            case AIState.Dead: _anim?.SetAnimation(AnimationState.Death); break;
        }
        Console.WriteLine($"[AI] {Entity?.Id} {State}");
    }

    public override void Update(float deltaTime)
    {
        if (Entity == null) return;
        _stateTime += deltaTime;
        _attackTimer -= deltaTime;

        _health ??= Entity.GetComponent<HealthComponent>();
        _anim ??= Entity.GetComponent<AnimatedModelComponent>();

        if (_health != null && !_health.IsAlive)
        {
            if (State != AIState.Dead) OnDeath();
            return;
        }

        if (_player == null && _manager != null)
        {
            _player = _manager.FindFirstByClassName("player_start") ?? _manager.Entities.FirstOrDefault(e => e.GetComponent<PlayerComponent>() != null);
        }
        if (_player == null) { SetState(AIState.Idle); return; }

        Vector3 myPos = Entity.Transform.Position;
        Vector3 playerPos = _player.Transform.Position;
        Vector3 toPlayer = playerPos - myPos;
        float dist = toPlayer.Length();
        bool hasLOS = HasLineOfSight(myPos, playerPos);
        bool inSight = dist <= SightRange && hasLOS && IsInFov(myPos, toPlayer);
        bool inAttackRange = dist <= AttackRange && hasLOS;

        // FSM
        switch (State)
        {
            case AIState.Idle:
                if (inSight)
                {
                    _hasSeenPlayer = true;
                    _audio?.PlaySound3D("soldier/sight1.wav", myPos, 0.9f);
                    Console.WriteLine($"[Enemy] Sight {Entity.Id}");
                    SetState(AIState.Chase);
                }
                else if (_stateTime > 2f)
                {
                    // simple patrol wander
                    SetState(AIState.Patrol);
                    _patrolTarget = myPos + new Vector3((Random.Shared.NextSingle() - 0.5f) * 200, (Random.Shared.NextSingle() - 0.5f) * 200, 0);
                }
                break;
            case AIState.Patrol:
                if (inSight) { SetState(AIState.Chase); break; }
                MoveTowards(_patrolTarget, deltaTime);
                if ((myPos - _patrolTarget).LengthSquared() < 400f || _stateTime > 5f) SetState(AIState.Idle);
                break;
            case AIState.Chase:
                if (!inSight && dist > SightRange * 1.2f) { SetState(AIState.Idle); break; }
                if (inAttackRange) { SetState(AIState.Attack); break; }
                MoveTowards(playerPos, deltaTime);
                break;
            case AIState.Attack:
                // Face player
                if (dist > 0.01f)
                {
                    float yaw = MathF.Atan2(toPlayer.Y, toPlayer.X);
                    var rot = Entity.Transform.Rotation;
                    rot.Y = yaw;
                    Entity.Transform.Rotation = rot;
                }
                if (!inAttackRange || !hasLOS)
                {
                    if (_stateTime > AttackDuration) SetState(AIState.Chase);
                    break;
                }
                if (_attackTimer <= 0f && _stateTime > 0.2f)
                {
                    DoAttack(playerPos);
                    _attackTimer = AttackCooldown;
                    _stateTime = 0;
                }
                if (_stateTime > AttackDuration && !inAttackRange) SetState(AIState.Chase);
                break;
            case AIState.Dead:
                // stay dead
                break;
        }
    }

    private bool IsInFov(Vector3 myPos, Vector3 toPlayer)
    {
        if (toPlayer.LengthSquared() < 0.001f) return true;
        var dir = Vector3.Normalize(toPlayer);
        // Enemy forward from yaw
        float yaw = Entity!.Transform.Rotation.Y;
        Vector3 forward = new(MathF.Cos(yaw), MathF.Sin(yaw), 0);
        float dot = Vector3.Dot(forward, new Vector3(dir.X, dir.Y, 0));
        float cosFov = MathF.Cos(FovDegrees * 0.5f * MathF.PI / 180f);
        return dot >= cosFov;
    }

    private bool HasLineOfSight(Vector3 from, Vector3 to)
    {
        if (_world == null) return true;
        Vector3 dir = to - from;
        float len = dir.Length();
        if (len < 1e-6f) return true;
        dir /= len;
        // Slight eye height
        Vector3 eye = from + new Vector3(0, 0, 24);
        var hit = _world.Raycast(eye, dir, len);
        return !hit.Hit; // if wall hit before player, blocked
    }

    private void MoveTowards(Vector3 target, float dt)
    {
        Vector3 pos = Entity!.Transform.Position;
        Vector3 delta = target - pos;
        delta.Z = 0;
        float len = delta.Length();
        if (len < 1f) return;
        Vector3 dir = delta / len;
        // Update yaw
        float yaw = MathF.Atan2(dir.Y, dir.X);
        var rot = Entity.Transform.Rotation;
        rot.Y = yaw;
        Entity.Transform.Rotation = rot;

        Vector3 next = pos + dir * MoveSpeed * dt;
        // Simple sweep to avoid walls
        if (_world != null)
        {
            var half = new Vector3(16, 16, 24);
            var sweep = _world.SweepAABB(pos, next, half);
            if (!sweep.Hit) Entity.Transform.Position = next;
            else
            {
                // slide along wall
                Vector3 rem = (next - pos) * (1f - sweep.Fraction);
                float into = Vector3.Dot(rem, sweep.HitNormal);
                if (into < 0) rem -= sweep.HitNormal * into;
                var sweep2 = _world.SweepAABB(pos + (next - pos) * sweep.Fraction, pos + (next - pos) * sweep.Fraction + rem, half);
                if (!sweep2.Hit) Entity.Transform.Position = pos + (next - pos) * sweep.Fraction + rem;
            }
        }
        else
        {
            Entity.Transform.Position = next;
        }
    }

    private void DoAttack(Vector3 playerPos)
    {
        if (_player == null) return;
        var playerHealth = _player.GetComponent<HealthComponent>();
        if (playerHealth == null || !playerHealth.IsAlive) return;
        playerHealth.Damage(Damage);
        _audio?.PlaySound3D("weapons/shotgn2.wav", Entity!.Transform.Position, 1f);
        Console.WriteLine($"[Enemy] Attack {Entity!.Id} damage {Damage} player health {playerHealth.Health}");
        _audio?.PlaySound3D("player/pain1.wav", playerPos, 0.8f);
    }
}
