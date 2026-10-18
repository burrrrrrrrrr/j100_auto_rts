using MultiplayerRts.Core;

namespace MultiplayerRts.Tests.Fixtures
{
    /// <summary>
    /// Fluent builder for an in-memory <see cref="Structure"/> fixture. A <see cref="Structure"/>
    /// carries the union of fields for all three <see cref="StructureType"/>s; this builder
    /// exposes the common fields plus typed presets (<see cref="AsSpawner"/>,
    /// <see cref="AsDefensive"/>, <see cref="AsEconomy"/>) that set the group relevant to each
    /// type, so placement, spawn, and combat tests can describe just the structure they need.
    /// </summary>
    public sealed class StructureBuilder
    {
        private StructureId _id = new StructureId(1);
        private PlayerId _owner = new PlayerId(1);
        private StructureType _type = StructureType.Economy;
        private Footprint _footprint = new Footprint(MapVector.Zero, new MapVector(1f, 1f));
        private bool _isActive = true;
        private int _cost = 50;

        // Spawner
        private float _spawnIntervalSeconds = 5f;
        private MapVector _spawnPoint = MapVector.Zero;
        private float _spawnTimer;

        // Defensive
        private float _range = 5f;
        private float _attackInterval = 1f;
        private float _attackDamage = 10f;
        private float _attackTimer;
        private UnitId? _currentTarget;

        // Economy
        private int _generationRate = 10;
        private float _generationTimer;

        public static StructureBuilder AStructure() => new StructureBuilder();

        public StructureBuilder WithId(StructureId id)
        {
            _id = id;
            return this;
        }

        public StructureBuilder WithOwner(PlayerId owner)
        {
            _owner = owner;
            return this;
        }

        public StructureBuilder WithType(StructureType type)
        {
            _type = type;
            return this;
        }

        public StructureBuilder WithFootprint(Footprint footprint)
        {
            _footprint = footprint;
            return this;
        }

        public StructureBuilder Active(bool active = true)
        {
            _isActive = active;
            return this;
        }

        public StructureBuilder WithCost(int cost)
        {
            _cost = cost;
            return this;
        }

        public StructureBuilder AsSpawner(float spawnIntervalSeconds, MapVector spawnPoint, float spawnTimer = 0f)
        {
            _type = StructureType.Spawner;
            _spawnIntervalSeconds = spawnIntervalSeconds;
            _spawnPoint = spawnPoint;
            _spawnTimer = spawnTimer;
            return this;
        }

        public StructureBuilder AsDefensive(float range, float attackInterval, float attackDamage, float attackTimer = 0f)
        {
            _type = StructureType.Defensive;
            _range = range;
            _attackInterval = attackInterval;
            _attackDamage = attackDamage;
            _attackTimer = attackTimer;
            return this;
        }

        public StructureBuilder AsEconomy(int generationRate, float generationTimer = 0f)
        {
            _type = StructureType.Economy;
            _generationRate = generationRate;
            _generationTimer = generationTimer;
            return this;
        }

        public StructureBuilder WithCurrentTarget(UnitId? target)
        {
            _currentTarget = target;
            return this;
        }

        public Structure Build()
        {
            return new Structure
            {
                Id = _id,
                Owner = _owner,
                Type = _type,
                Footprint = _footprint,
                IsActive = _isActive,
                Cost = _cost,
                SpawnIntervalSeconds = _spawnIntervalSeconds,
                SpawnPoint = _spawnPoint,
                SpawnTimer = _spawnTimer,
                Range = _range,
                AttackInterval = _attackInterval,
                AttackDamage = _attackDamage,
                AttackTimer = _attackTimer,
                CurrentTarget = _currentTarget,
                GenerationRate = _generationRate,
                GenerationTimer = _generationTimer
            };
        }
    }
}
