namespace Relic.Framework;

public sealed class Engine
{
    private readonly List<ISystem> _systems = new();
    private readonly GameTime _gameTime = new();
    private bool _running;

    public EngineSettings Settings { get; }
    public ILogger Logger { get; }
    public GameTime GameTime => _gameTime;
    public bool IsRunning => _running;

    public Engine(EngineSettings? settings = null, ILogger? logger = null)
    {
        Settings = settings ?? new EngineSettings();
        Logger = logger ?? new ConsoleLogger();
    }

    public void RegisterSystem(ISystem system)
    {
        _systems.Add(system);
        _systems.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    public void Initialize()
    {
        Logger.Info("Engine initializing...");
        
        ServiceLocator.Register(this);
        ServiceLocator.Register(Logger);
        ServiceLocator.Register(Settings);

        foreach (var system in _systems)
        {
            try
            {
                system.Initialize(this);
                Logger.Debug($"Initialized system: {system.GetType().Name}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize system {system.GetType().Name}: {ex.Message}");
                throw;
            }
        }

        _running = true;
        Logger.Info("Engine initialized.");
    }

    public void Update() => Update(1f / 60f);

    public void Update(float deltaTime)
    {
        if (!_running) return;

        _gameTime.Tick(deltaTime);

        foreach (var system in _systems)
        {
            try
            {
                system.Update(_gameTime);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in system {system.GetType().Name}: {ex.Message}");
            }
        }
    }

    public void Shutdown()
    {
        if (!_running) return;

        Logger.Info("Engine shutting down...");

        for (int i = _systems.Count - 1; i >= 0; i--)
        {
            try
            {
                _systems[i].Shutdown();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error shutting down system {_systems[i].GetType().Name}: {ex.Message}");
            }
        }

        ServiceLocator.Clear();
        _running = false;
        Logger.Info("Engine shutdown complete.");
    }
}