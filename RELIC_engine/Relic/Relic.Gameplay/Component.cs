namespace Relic.Gameplay;

public abstract class Component
{
    public Entity? Entity { get; internal set; }
    public bool Enabled { get; set; } = true;
    public virtual void Initialize() { }
    public virtual void Update(float deltaTime) { }
    public virtual void Shutdown() { }
}
