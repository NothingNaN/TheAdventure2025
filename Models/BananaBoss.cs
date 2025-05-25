using Silk.NET.SDL;

namespace TheAdventure.Models;

public class BananaBoss : RenderableGameObject
{
    public enum BossState
    {
        Idle,
        Move,
        TakingDamage,
        Death
    }

    private BossState _currentState;
    private int _hitPoints;
    private readonly int _maxHitPoints;
    private DateTimeOffset _lastDamageTime = DateTimeOffset.MinValue;
    private readonly TimeSpan _damageCooldown = TimeSpan.FromMilliseconds(500); // 0.5 second damage immunity
    private DateTimeOffset _lastMoveTime = DateTimeOffset.MinValue;
    private readonly TimeSpan _moveInterval = TimeSpan.FromSeconds(1); // Move every 1 second
    private readonly Random _random = new();
    private bool _isDead = false;

    public bool IsDead => _isDead;
    public bool IsDeadAndOnGround => _isDead && _currentState == BossState.Death;
    public int HitPoints => _hitPoints;
    public int MaxHitPoints => _maxHitPoints;

    public BananaBoss(SpriteSheet spriteSheet, int x, int y, int hitPoints = 100)
        : base(spriteSheet, (x, y))
    {
        _hitPoints = hitPoints;
        _maxHitPoints = hitPoints;
        _currentState = BossState.Idle;
        SetState(BossState.Idle);
    }

    private void SetState(BossState newState)
    {
        if (_currentState == newState) return;

        _currentState = newState;

        switch (newState)
        {
            case BossState.Idle:
                SpriteSheet.ActivateAnimation("Idle");
                break;
            case BossState.Move:
                SpriteSheet.ActivateAnimation("Move");
                break;
            case BossState.TakingDamage:
                SpriteSheet.ActivateAnimation("TakingDamage");
                break;
            case BossState.Death:
                SpriteSheet.ActivateAnimation("Death");
                _isDead = true;
                break;
        }
    }

    public void Update(int playerX, int playerY)
    {
        if (_isDead) return;

        var currentTime = DateTimeOffset.Now;

        // Handle damage state timeout
        if (_currentState == BossState.TakingDamage &&
            (currentTime - _lastDamageTime).TotalMilliseconds > 1000) // 1 second damage animation
        {
            SetState(BossState.Idle);
        }

        // Handle movement (only when not taking damage)
        if (_currentState != BossState.TakingDamage &&
            (currentTime - _lastMoveTime) >= _moveInterval)
        {
            MoveRandomly();
            _lastMoveTime = currentTime;
        }
    }

    private void MoveRandomly()
    {
        SetState(BossState.Move);

        // Move in a random direction
        var direction = _random.Next(4);
        var moveDistance = 32; // Move 32 pixels (2 units)

        switch (direction)
        {
            case 0: // Up
                Position = (Position.X, Math.Max(0, Position.Y - moveDistance));
                break;
            case 1: // Down
                Position = (Position.X, Position.Y + moveDistance);
                break;
            case 2: // Left
                Position = (Math.Max(0, Position.X - moveDistance), Position.Y);
                break;
            case 3: // Right
                Position = (Position.X + moveDistance, Position.Y);
                break;
        }

        // Return to idle after a short time
        Task.Delay(300).ContinueWith(_ =>
        {
            if (_currentState == BossState.Move && !_isDead)
            {
                SetState(BossState.Idle);
            }
        });
    }

    public bool TakeDamage(int damage, int playerX, int playerY)
    {
        if (_isDead || _currentState == BossState.TakingDamage) return false;

        var currentTime = DateTimeOffset.Now;
        if ((currentTime - _lastDamageTime) < _damageCooldown) return false;

        // Calculate fart damage based on inverse square law
        var distance = CalculateDistance(playerX, playerY);
        var maxDistance = 20; // 20 units (320 pixels)

        if (distance > maxDistance) return false; // Out of range

        // Inverse square law: damage decreases with square of distance
        // At distance 0: full damage, at max distance: 1% damage
        var damageMultiplier = Math.Max(0.01, 1.0 / (1.0 + (distance * distance) / (maxDistance * maxDistance)));
        var actualDamage = (int)(damage * damageMultiplier);

        _hitPoints = Math.Max(0, _hitPoints - actualDamage);
        _lastDamageTime = currentTime;

        if (_hitPoints <= 0)
        {
            SetState(BossState.Death);
        }
        else
        {
            SetState(BossState.TakingDamage);
        }

        return true;
    }

    private double CalculateDistance(int playerX, int playerY)
    {
        var deltaX = Math.Abs(Position.X - playerX);
        var deltaY = Math.Abs(Position.Y - playerY);
        var pixelDistance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        return pixelDistance / 16.0; // Convert pixels to units (1 unit = 16 pixels)
    }

    public bool CheckPlayerCollision(int playerX, int playerY)
    {
        if (!IsDeadAndOnGround) return false;

        var deltaX = Math.Abs(Position.X - playerX);
        var deltaY = Math.Abs(Position.Y - playerY);

        // Check if player is within collision range (32x32 pixels)
        return deltaX < 32 && deltaY < 32;
    }
}