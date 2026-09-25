namespace Relic.Framework;

public interface ISystem
{
    int Priority { get; }
    void Initialize(Engine engine);
    void Update(GameTime gameTime);
    void Shutdown();
}