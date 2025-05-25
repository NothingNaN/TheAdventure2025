using System.Reflection;
using System.Text.Json;
using Silk.NET.Maths;
using TheAdventure.Models;
using TheAdventure.Models.Data;
using TheAdventure.Scripting;

namespace TheAdventure;

public class Engine
{
    private readonly GameRenderer _renderer;
    private readonly Input _input;
    private readonly ScriptEngine _scriptEngine = new();
    private readonly AudioManager _audioManager;

    private readonly Dictionary<int, GameObject> _gameObjects = new();
    private readonly Dictionary<string, TileSet> _loadedTileSets = new();
    private readonly Dictionary<int, Tile> _tileIdMap = new();

    private Level _currentLevel = new();
    private PlayerObject? _player;
    private BananaBoss? _bananaBoss;

    private DateTimeOffset _lastUpdate = DateTimeOffset.Now;
    private DateTimeOffset _lastMegaFart = DateTimeOffset.MinValue;
    private readonly TimeSpan _megaFartCooldown = TimeSpan.FromSeconds(5); // 5 second cooldown

    public Engine(GameRenderer renderer, Input input, AudioManager audioManager)
    {
        _renderer = renderer;
        _input = input;
        _audioManager = audioManager;

        _input.OnMouseClick += (_, coords) => AddBomb(coords.x, coords.y);
        _input.OnFartPressed += (_, _) => HandleFartPress();
        _input.OnMegaFartPressed += (_, _) => HandleMegaFartPress();
    }

    private void HandleFartPress()
    {
        _audioManager.PlayRandomFart();

        Task.Delay(500).ContinueWith(_ =>
        {
            // Apply fart damage to banana boss after 0.5 second delay
            if (_bananaBoss != null && !_bananaBoss.IsDead && _player != null)
            {
                var playerX = _player.Position.X;
                var playerY = _player.Position.Y;

                if (_bananaBoss != null && !_bananaBoss.IsDead)
                {
                    var damageDealt = _bananaBoss.TakeDamage(25, playerX, playerY);
                    if (damageDealt)
                    {
                        Console.WriteLine($"Boss took fart damage! HP: {_bananaBoss.HitPoints}/{_bananaBoss.MaxHitPoints}");
                        if (_bananaBoss.IsDead)
                        {
                            Console.WriteLine("BANANA BOSS DEFEATED! Watch out for the peel!");
                        }
                    }
                }
            }
        });
    }

    private void HandleMegaFartPress()
    {
        var timeSinceLastMegaFart = DateTimeOffset.Now - _lastMegaFart;
        if (timeSinceLastMegaFart >= _megaFartCooldown)
        {
            _audioManager.PlayMegaFart();
            _lastMegaFart = DateTimeOffset.Now;
            Console.WriteLine("MEGA FART!");

            Task.Delay(1000).ContinueWith(_ =>
            {
                // Apply mega fart damage to banana boss after 1 second delay
                if (_bananaBoss != null && !_bananaBoss.IsDead && _player != null)
                {
                    var playerX = _player.Position.X;
                    var playerY = _player.Position.Y;

                    if (_bananaBoss != null && !_bananaBoss.IsDead)
                    {
                        var damageDealt = _bananaBoss.TakeDamage(75, playerX, playerY);
                        if (damageDealt)
                        {
                            Console.WriteLine($"Boss took MEGA fart damage! HP: {_bananaBoss.HitPoints}/{_bananaBoss.MaxHitPoints}");
                            if (_bananaBoss.IsDead)
                            {
                                Console.WriteLine("BANANA BOSS DEFEATED BY MEGA FART! Watch out for the peel!");
                            }
                        }
                    }
                }
            });
        }
        else
        {
            var remainingCooldown = _megaFartCooldown - timeSinceLastMegaFart;
            Console.WriteLine($"Mega fart on cooldown! {remainingCooldown.TotalSeconds:F1} seconds remaining");
        }
    }

    public void SetupWorld()
    {
        // Load audio files
        _audioManager.LoadAudio(Path.Combine("Assets", "fart1.wav"), "fart1");
        _audioManager.LoadAudio(Path.Combine("Assets", "fart2.wav"), "fart2");
        _audioManager.LoadAudio(Path.Combine("Assets", "fart3.wav"), "fart3");
        _audioManager.LoadAudio(Path.Combine("Assets", "megafart.wav"), "megafart");
        _audioManager.LoadAudio(Path.Combine("Assets", "oof.wav"), "oof");

        _player = new(SpriteSheet.Load(_renderer, "Player.json", "Assets"), 100, 100);
        _player.OnPlayerDeath += (_, _) => _audioManager.PlayOof();

        // Create banana boss
        var bossSprite = SpriteSheet.Load(_renderer, "BananaBoss.json", "Assets");
        _bananaBoss = new BananaBoss(bossSprite, 400, 300, 150); // Position at (400, 300) with 150 HP

        var levelContent = File.ReadAllText(Path.Combine("Assets", "terrain.tmj"));
        var level = JsonSerializer.Deserialize<Level>(levelContent);
        if (level == null)
        {
            throw new Exception("Failed to load level");
        }

        foreach (var tileSetRef in level.TileSets)
        {
            var tileSetContent = File.ReadAllText(Path.Combine("Assets", tileSetRef.Source));
            var tileSet = JsonSerializer.Deserialize<TileSet>(tileSetContent);
            if (tileSet == null)
            {
                throw new Exception("Failed to load tile set");
            }

            foreach (var tile in tileSet.Tiles)
            {
                tile.TextureId = _renderer.LoadTexture(Path.Combine("Assets", tile.Image), out _);
                _tileIdMap.Add(tile.Id!.Value, tile);
            }

            _loadedTileSets.Add(tileSet.Name, tileSet);
        }

        if (level.Width == null || level.Height == null)
        {
            throw new Exception("Invalid level dimensions");
        }

        if (level.TileWidth == null || level.TileHeight == null)
        {
            throw new Exception("Invalid tile dimensions");
        }

        _renderer.SetWorldBounds(new Rectangle<int>(0, 0, level.Width.Value * level.TileWidth.Value,
            level.Height.Value * level.TileHeight.Value));

        _currentLevel = level;

        _scriptEngine.LoadAll(Path.Combine("Assets", "Scripts"));
    }

    public void ProcessFrame()
    {
        var currentTime = DateTimeOffset.Now;
        var msSinceLastFrame = (currentTime - _lastUpdate).TotalMilliseconds;
        _lastUpdate = currentTime;

        if (_player == null)
        {
            return;
        }

        double up = _input.IsUpPressed() ? 1.0 : 0.0;
        double down = _input.IsDownPressed() ? 1.0 : 0.0;
        double left = _input.IsLeftPressed() ? 1.0 : 0.0;
        double right = _input.IsRightPressed() ? 1.0 : 0.0;
        bool isAttacking = _input.IsKeyAPressed() && (up + down + left + right <= 1);
        bool addBomb = _input.IsKeyBPressed();

        _player.UpdatePosition(up, down, left, right, 48, 48, msSinceLastFrame);
        if (isAttacking)
        {
            _player.Attack();
        }

        // Update banana boss
        if (_bananaBoss != null)
        {
            _bananaBoss.Update(_player.Position.X, _player.Position.Y);

            // Check if player stepped on dead banana boss (banana peel)
            if (_bananaBoss.CheckPlayerCollision(_player.Position.X, _player.Position.Y))
            {
                Console.WriteLine("Player slipped on banana peel!");
                _player.GameOver();
            }
        }

        _scriptEngine.ExecuteAll(this);

        if (addBomb)
        {
            AddBomb(_player.Position.X, _player.Position.Y, false);
        }
    }

    public void RenderFrame()
    {
        _renderer.SetDrawColor(0, 0, 0, 255);
        _renderer.ClearScreen();

        var playerPosition = _player!.Position;
        _renderer.CameraLookAt(playerPosition.X, playerPosition.Y);

        RenderTerrain();
        RenderAllObjects();

        // Render banana boss
        if (_bananaBoss != null)
        {
            _bananaBoss.Render(_renderer);
        }

        _renderer.PresentFrame();
    }

    public void RenderAllObjects()
    {
        var toRemove = new List<int>();
        foreach (var gameObject in GetRenderables())
        {
            gameObject.Render(_renderer);
            if (gameObject is TemporaryGameObject { IsExpired: true } tempGameObject)
            {
                toRemove.Add(tempGameObject.Id);
            }
        }

        foreach (var id in toRemove)
        {
            _gameObjects.Remove(id, out var gameObject);

            if (_player == null)
            {
                continue;
            }

            var tempGameObject = (TemporaryGameObject)gameObject!;
            var deltaX = Math.Abs(_player.Position.X - tempGameObject.Position.X);
            var deltaY = Math.Abs(_player.Position.Y - tempGameObject.Position.Y);
            if (deltaX < 32 && deltaY < 32)
            {
                _player.GameOver();
            }
        }

        _player?.Render(_renderer);
    }

    public void RenderTerrain()
    {
        foreach (var currentLayer in _currentLevel.Layers)
        {
            for (int i = 0; i < _currentLevel.Width; ++i)
            {
                for (int j = 0; j < _currentLevel.Height; ++j)
                {
                    int? dataIndex = j * currentLayer.Width + i;
                    if (dataIndex == null)
                    {
                        continue;
                    }

                    var currentTileId = currentLayer.Data[dataIndex.Value] - 1;
                    if (currentTileId == null)
                    {
                        continue;
                    }

                    var currentTile = _tileIdMap[currentTileId.Value];

                    var tileWidth = currentTile.ImageWidth ?? 0;
                    var tileHeight = currentTile.ImageHeight ?? 0;

                    var sourceRect = new Rectangle<int>(0, 0, tileWidth, tileHeight);
                    var destRect = new Rectangle<int>(i * tileWidth, j * tileHeight, tileWidth, tileHeight);
                    _renderer.RenderTexture(currentTile.TextureId, sourceRect, destRect);
                }
            }
        }
    }

    public IEnumerable<RenderableGameObject> GetRenderables()
    {
        foreach (var gameObject in _gameObjects.Values)
        {
            if (gameObject is RenderableGameObject renderableGameObject)
            {
                yield return renderableGameObject;
            }
        }
    }

    public (int X, int Y) GetPlayerPosition()
    {
        return _player!.Position;
    }

    public void AddBomb(int X, int Y, bool translateCoordinates = true)
    {
        var worldCoords = translateCoordinates ? _renderer.ToWorldCoordinates(X, Y) : new Vector2D<int>(X, Y);

        SpriteSheet spriteSheet = SpriteSheet.Load(_renderer, "BombExploding.json", "Assets");
        spriteSheet.ActivateAnimation("Explode");

        TemporaryGameObject bomb = new(spriteSheet, 2.1, (worldCoords.X, worldCoords.Y));
        _gameObjects.Add(bomb.Id, bomb);
    }
}