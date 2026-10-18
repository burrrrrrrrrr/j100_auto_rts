using System.Collections.Generic;
using MultiplayerRts.Core;

namespace MultiplayerRts.Tests.Fixtures
{
    /// <summary>
    /// Fluent builder for an in-memory <see cref="Unit"/> fixture. Defaults model a healthy,
    /// moving unit with a single-waypoint path toward an opponent base, so movement and
    /// combat tests can override only the fields under test (speed, health, path progress,
    /// held state).
    /// </summary>
    public sealed class UnitBuilder
    {
        private UnitId _id = new UnitId(1);
        private PlayerId _owner = new PlayerId(1);
        private MapVector _position = MapVector.Zero;
        private float _health = 100f;
        private float _movementSpeed = 1f;
        private float _damage = 10f;
        private Path _path = new Path(new List<MapVector> { new MapVector(10f, 0f) }, new MapVector(10f, 0f));
        private int _waypointIndex;
        private bool _isHeld;

        public static UnitBuilder AUnit() => new UnitBuilder();

        public UnitBuilder WithId(UnitId id)
        {
            _id = id;
            return this;
        }

        public UnitBuilder WithOwner(PlayerId owner)
        {
            _owner = owner;
            return this;
        }

        public UnitBuilder WithPosition(MapVector position)
        {
            _position = position;
            return this;
        }

        public UnitBuilder WithHealth(float health)
        {
            _health = health;
            return this;
        }

        public UnitBuilder WithMovementSpeed(float speed)
        {
            _movementSpeed = speed;
            return this;
        }

        public UnitBuilder WithDamage(float damage)
        {
            _damage = damage;
            return this;
        }

        public UnitBuilder WithPath(Path path)
        {
            _path = path;
            return this;
        }

        public UnitBuilder WithWaypointIndex(int index)
        {
            _waypointIndex = index;
            return this;
        }

        public UnitBuilder Held(bool held = true)
        {
            _isHeld = held;
            return this;
        }

        public Unit Build()
        {
            return new Unit
            {
                Id = _id,
                Owner = _owner,
                Position = _position,
                Health = _health,
                MovementSpeed = _movementSpeed,
                Damage = _damage,
                AssignedPath = _path,
                WaypointIndex = _waypointIndex,
                IsHeld = _isHeld
            };
        }
    }
}
