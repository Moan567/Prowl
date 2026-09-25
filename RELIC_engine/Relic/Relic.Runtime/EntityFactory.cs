namespace Relic.Runtime;

using Relic.Content;

public static class EntityFactory
{
    private static readonly Dictionary<string, Func<World, MapEntity, Entity>> _entityCreators = new();

    static EntityFactory()
    {
        Register("player_start", (world, mapEntity) => new PlayerSpawnEntity(world, mapEntity));
        Register("enemy_soldier", (world, mapEntity) => new EnemySoldierEntity(world, mapEntity));
        Register("weapon_pickup", (world, mapEntity) => new WeaponPickupEntity(world, mapEntity));
        Register("light", (world, mapEntity) => new LightEntity(world, mapEntity));
        Register("info_player_start", (world, mapEntity) => new PlayerSpawnEntity(world, mapEntity));
    }

    public static void Register(string className, Func<World, MapEntity, Entity> creator)
    {
        _entityCreators[className.ToLowerInvariant()] = creator;
    }

    public static void SpawnMap(World world, MapData mapData)
    {
        foreach (var mapEntity in mapData.Entities)
        {
            SpawnEntity(world, mapEntity);
        }
    }

    public static Entity? SpawnEntity(World world, MapEntity mapEntity)
    {
        var className = mapEntity.ClassName.ToLowerInvariant();
        
        if (_entityCreators.TryGetValue(className, out var creator))
        {
            var entity = creator(world, mapEntity);
            entity.Transform.Position = mapEntity.Position;
            world.AddEntity(entity);
            
            // Apply properties
            foreach (var prop in mapEntity.Properties)
            {
                ApplyProperty(entity, prop.Key, prop.Value);
            }
            
            return entity;
        }

        // Create generic entity for unknown types
        var genericEntity = world.CreateEntity(mapEntity.ClassName);
        genericEntity.Transform.Position = mapEntity.Position;
        foreach (var prop in mapEntity.Properties)
            ApplyProperty(genericEntity, prop.Key, prop.Value);
        return genericEntity;
    }

    private static void ApplyProperty(Entity entity, string key, string value)
    {
        // Handle common properties
        switch (key.ToLowerInvariant())
        {
            case "angle":
                if (float.TryParse(value, out float angle))
                {
                    var rot = entity.Transform.Rotation;
                    rot.Y = MathF.PI * angle / 180f;
                    entity.Transform.Rotation = rot;
                }
                break;
            case "health":
                // Could be used by health component
                break;
            case "weapon":
                // Could be used by weapon component
                break;
        }
    }
}

// Built-in entity types
public class PlayerSpawnEntity : Entity
{
    public PlayerSpawnEntity(World world, MapEntity mapEntity) : base("PlayerSpawn")
    {
        // Could add PlayerSpawnComponent here
    }
}

public class EnemySoldierEntity : Entity
{
    public EnemySoldierEntity(World world, MapEntity mapEntity) : base("EnemySoldier")
    {
        // Could add AIComponent, HealthComponent, etc.
    }
}

public class WeaponPickupEntity : Entity
{
    public WeaponPickupEntity(World world, MapEntity mapEntity) : base("WeaponPickup")
    {
        // Could add PickupComponent here
    }
}

public class LightEntity : Entity
{
    public LightEntity(World world, MapEntity mapEntity) : base("Light")
    {
        // Could add LightComponent here
    }
}