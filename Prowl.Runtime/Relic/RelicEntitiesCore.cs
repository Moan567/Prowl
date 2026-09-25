// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.
//
// RELIC gameplay layer — core actors: player spawns, enemies, items.
// Map entities automatically create these via RelicSceneBuilder.

using System;
using System.Collections.Generic;

using Prowl.Echo;
using Prowl.Vector;

namespace Prowl.Runtime.Relic;

/// <summary>Marks a player spawn point (player_start). Scene builder positions the active camera/player here.</summary>
[AddComponentMenu("Relic/Player/Player Spawn")]
public sealed class RelicPlayerSpawn : MonoBehaviour
{
    public float YawDegrees;
    public string SpawnName = string.Empty;
    public bool IsActiveSpawn = true;
}

/// <summary>First-person / third-person Quake-style player controller (WASD + mouse, CharacterController).</summary>
[AddComponentMenu("Relic/Player/Player Controller")]
[RequireComponent(typeof(CharacterController))]
public sealed class RelicPlayerController : MonoBehaviour
{
    public float WalkSpeed = 6f;
    public float SprintMultiplier = 1.6f;
    public float JumpSpeed = 7f;
    public float MouseSensitivity = 0.15f;
    public float MaxHealth = 100f;
    public float Health = 100f;

    [SerializeIgnore] private float _yaw;
    [SerializeIgnore] private float _pitch;
    [SerializeIgnore] private bool _initialized;

    public override void Start()
    {
        var spawn = GameObject.GetComponent<RelicPlayerSpawn>();
        _yaw = spawn != null ? spawn.YawDegrees : Transform.EulerAngles.Y;
        _initialized = true;
    }

    public override void Update()
    {
        var cc = GetComponent<CharacterController>();
        if (cc == null) return;

        float dt = (float)Time.DeltaTime;
        var move = Float3.Zero;
        if (Input.GetKey(KeyCode.W)) move += Transform.Forward;
        if (Input.GetKey(KeyCode.S)) move -= Transform.Forward;
        if (Input.GetKey(KeyCode.A)) move -= Transform.Right;
        if (Input.GetKey(KeyCode.D)) move += Transform.Right;
        bool sprint = Input.GetKey(KeyCode.ShiftLeft) || Input.GetKey(KeyCode.ShiftRight);
        float speed = WalkSpeed * (sprint ? SprintMultiplier : 1f);
        move.Y = 0;
        if (Float3.LengthSquared(move) > 0.001f) move = Float3.Normalize(move) * speed;

        float vy = cc.Velocity.Y;
        if (cc.IsGrounded)
        {
            vy = -1f;
            if (Input.GetKeyDown(KeyCode.Space)) vy = JumpSpeed;
        }
        else vy -= 22f * dt;

        cc.Move(new Float3(move.X, vy, move.Z) * dt);

        // Mouse look when cursor locked (game view).
        if (Input.CursorLockState == CursorLockMode.Locked)
        {
            var delta = Input.MouseDelta;
            _yaw -= delta.X * MouseSensitivity;
            _pitch = Math.Clamp(_pitch - delta.Y * MouseSensitivity, -89f, 89f);
            Transform.EulerAngles = new Float3(_pitch, _yaw, 0);
        }
        else if (!_initialized)
        {
            _yaw = Transform.EulerAngles.Y;
            _pitch = Transform.EulerAngles.X;
            _initialized = true;
        }

        if (Health <= 0)
        {
            Health = MaxHealth; // respawn at spawn point
            var scene = GameObject.Scene;
            if (scene.IsValid())
            {
                foreach (var s in scene.FindObjectsOfType<RelicPlayerSpawn>())
                {
                    if (s.IsValid() && s.EnabledInHierarchy) { Transform.Position = s.Transform.Position; break; }
                }
            }
        }
    }

    public void Damage(float amount) => Health = Math.Max(0, Health - amount);
    public void Heal(float amount) => Health = Math.Min(MaxHealth, Health + amount);
}

public enum RelicEnemyKind { Soldier, Dog, Demon }

/// <summary>Enemy actor (enemy_soldier / enemy_dog / enemy_demon). Simple chase + melee/ranged stub AI.</summary>
[AddComponentMenu("Relic/AI/Enemy")]
public sealed class RelicEnemy : MonoBehaviour
{
    public RelicEnemyKind Kind = RelicEnemyKind.Soldier;
    public float MaxHealth = 100f;
    public float Health = 100f;
    public float MoveSpeed = 3.5f;
    public float AttackRange = 2.2f;
    public float AttackDamage = 10f;
    public float AttackCooldown = 1.2f;
    public string TargetName = string.Empty;
    public string PatrolPath = string.Empty;

    [SerializeIgnore] private float _cooldown;
    [SerializeIgnore] private int _patrolIndex;

    public override void Update()
    {
        if (Health <= 0) return;
        _cooldown -= (float)Time.DeltaTime;
        var player = FindPlayer();
        if (player == null) { Patrol((float)Time.DeltaTime); return; }

        var toPlayer = player.Transform.Position - Transform.Position;
        toPlayer.Y = 0;
        float dist = Float3.Length(toPlayer);
        if (dist > 0.01f)
            Transform.EulerAngles = new Float3(0, MathF.Atan2(-toPlayer.Z, toPlayer.X) * (180f / MathF.PI) - 90f, 0);

        if (dist > AttackRange)
        {
            var step = Float3.NormalizeSafe(toPlayer, Float3.Zero) * MoveSpeed * (float)Time.DeltaTime;
            Transform.Position += step;
        }
        else if (_cooldown <= 0f)
        {
            _cooldown = AttackCooldown;
            var pc = player.GameObject.GetComponent<RelicPlayerController>();
            if (pc.IsValid()) pc.Damage(AttackDamage);
        }
    }

    public void Damage(float amount)
    {
        Health -= amount;
        if (Health <= 0) { Health = 0; GameObject.Enabled = false; }
    }

    private RelicPlayerController? FindPlayer()
    {
        var scene = GameObject.Scene;
        if (!scene.IsValid()) return null;
        foreach (var f in scene.FindObjectsOfType<RelicPlayerController>())
            if (f.IsValid() && f.EnabledInHierarchy) return f;
        return null;
    }

    private void Patrol(float dt)
    {
        if (string.IsNullOrEmpty(PatrolPath)) return;
        var scene = GameObject.Scene;
        if (!scene.IsValid()) return;
        var corners = new List<RelicPathCorner>();
        foreach (var c in scene.FindObjectsOfType<RelicPathCorner>())
        {
            if (!c.IsValid()) continue;
            if (c.PathName.Equals(PatrolPath, StringComparison.OrdinalIgnoreCase)) corners.Add(c);
        }
        if (corners.Count == 0) return;
        var target = corners[_patrolIndex % corners.Count].Transform.Position;
        var to = target - Transform.Position;
        if (Float3.Length(to) < 0.5f) { _patrolIndex++; return; }
        Transform.Position += Float3.NormalizeSafe(to, Float3.Zero) * MoveSpeed * 0.5f * dt;
    }
}

/// <summary>Delayed / repeating enemy spawner (info_enemy_spawn).</summary>
[AddComponentMenu("Relic/AI/Enemy Spawn")]
public sealed class RelicEnemySpawn : MonoBehaviour
{
    public RelicEnemyKind Kind = RelicEnemyKind.Soldier;
    public int Count = 1;
    public float Delay = 1f;
    public string SpawnName = string.Empty;
    [SerializeIgnore] private float _timer;
    [SerializeIgnore] private int _spawned;

    public override void Update()
    {
        if (_spawned >= Count) return;
        _timer += (float)Time.DeltaTime;
        if (_timer < Delay) return;
        _timer = 0;
        _spawned++;
        var go = new GameObject($"{Kind}_{_spawned}");
        go.Transform.Position = Transform.Position;
        var enemy = go.AddComponent<RelicEnemy>();
        enemy.Kind = Kind;
        enemy.MaxHealth = enemy.Health = Kind switch
        {
            RelicEnemyKind.Dog => 50f,
            RelicEnemyKind.Demon => 300f,
            _ => 100f
        };
        var scene = GameObject.Scene;
        if (scene.IsValid()) scene.Add(go);
    }
}

/// <summary>Base pickup (weapon/ammo/health/armor/keycard). Grants on trigger touch.</summary>
[AddComponentMenu("Relic/Items/Pickup")]
public sealed class RelicPickup : MonoBehaviour
{
    public enum PickupKind { Weapon, Ammo, Health, Armor, Keycard }
    public PickupKind Kind = PickupKind.Health;
    public string ItemName = "Rifle";
    public float Amount = 25f;
    public float RespawnSeconds = 10f;
    public string PickupName = string.Empty;

    [SerializeIgnore] private float _hiddenTimer;

    public override void Update()
    {
        if (_hiddenTimer > 0)
        {
            _hiddenTimer -= (float)Time.DeltaTime;
            if (_hiddenTimer <= 0) GameObject.Enabled = true;
        }
    }

    public override void OnTriggerEnter(Rigidbody3D other)
    {
        var player = other.GameObject.GetComponent<RelicPlayerController>();
        if (player == null) player = other.GameObject.GetComponentInParent<RelicPlayerController>();
        if (player == null) return;
        Apply(player);
        if (RespawnSeconds > 0) { GameObject.Enabled = false; _hiddenTimer = RespawnSeconds; }
        else GameObject.Destroy();
    }

    private void Apply(RelicPlayerController player)
    {
        switch (Kind)
        {
            case PickupKind.Health: player.Heal(Amount); break;
            case PickupKind.Armor:
            case PickupKind.Ammo:
            case PickupKind.Weapon:
            case PickupKind.Keycard:
                var inv = player.GameObject.GetComponent<RelicInventory>();
                if (inv == null) inv = player.GameObject.AddComponent<RelicInventory>();
                inv.Add(ItemName, (int)Amount);
                break;
        }
    }
}

/// <summary>Simple inventory (weapons, keys, ammo counts).</summary>
[AddComponentMenu("Relic/Items/Inventory")]
public sealed class RelicInventory : MonoBehaviour
{
    public List<string> Weapons = new();
    public List<string> AmmoNames = new();
    public List<int> AmmoCounts = new();
    public List<string> Keys = new();

    public void Add(string name, int amount)
    {
        if (name.EndsWith("_key", StringComparison.OrdinalIgnoreCase) || name is "red" or "blue" or "gold")
        {
            if (!Keys.Contains(name)) Keys.Add(name);
            return;
        }
        if (!Weapons.Contains(name)) Weapons.Add(name);
        int idx = AmmoNames.IndexOf(name);
        if (idx >= 0) AmmoCounts[idx] += amount;
        else { AmmoNames.Add(name); AmmoCounts.Add(amount); }
    }

    public int GetAmmo(string name)
    {
        int idx = AmmoNames.IndexOf(name);
        return idx >= 0 ? AmmoCounts[idx] : 0;
    }
}

/// <summary>Waypoint for patrol AI (path_corner / path_patrol).</summary>
[AddComponentMenu("Relic/AI/Path Corner")]
public sealed class RelicPathCorner : MonoBehaviour
{
    public string PathName = string.Empty;
    public string NextTarget = string.Empty;
    public float WaitSeconds;
}
