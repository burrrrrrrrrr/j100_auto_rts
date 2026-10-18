using System.Collections.Generic;
using MultiplayerRts.Core;

namespace MultiplayerRts.Tests.Fixtures
{
    /// <summary>
    /// Fluent builder for an in-memory <see cref="PlayerState"/> fixture. Defaults model a
    /// freshly started player (Requirement 3.1: 100 resources, full base health, connected);
    /// each <c>With*</c> method overrides a single field so tests state only what matters.
    /// </summary>
    public sealed class PlayerStateBuilder
    {
        private PlayerId _id = new PlayerId(1);
        private int _resourceTotal = SimulationConstants.StartingResources;
        private float _baseHealth = 1000f;
        private float _baseMaxHealth = 1000f;
        private MapVector _baseLocation = new MapVector(0f, 0f);
        private Build_Area _buildArea = Build_Area.Empty;
        private ConnectionState _connection = ConnectionState.Connected;

        public static PlayerStateBuilder APlayer() => new PlayerStateBuilder();

        public PlayerStateBuilder WithId(PlayerId id)
        {
            _id = id;
            return this;
        }

        public PlayerStateBuilder WithResources(int resourceTotal)
        {
            _resourceTotal = resourceTotal;
            return this;
        }

        public PlayerStateBuilder WithBaseHealth(float health)
        {
            _baseHealth = health;
            return this;
        }

        public PlayerStateBuilder WithBaseHealth(float health, float maxHealth)
        {
            _baseHealth = health;
            _baseMaxHealth = maxHealth;
            return this;
        }

        public PlayerStateBuilder WithBaseLocation(MapVector location)
        {
            _baseLocation = location;
            return this;
        }

        public PlayerStateBuilder WithBuildArea(Build_Area buildArea)
        {
            _buildArea = buildArea;
            return this;
        }

        public PlayerStateBuilder WithBuildArea(params Footprint[] regions)
        {
            _buildArea = new Build_Area(new List<Footprint>(regions));
            return this;
        }

        public PlayerStateBuilder WithConnection(ConnectionState connection)
        {
            _connection = connection;
            return this;
        }

        public PlayerState Build()
        {
            return new PlayerState
            {
                Id = _id,
                ResourceTotal = _resourceTotal,
                BaseHealth = _baseHealth,
                BaseMaxHealth = _baseMaxHealth,
                BaseLocation = _baseLocation,
                BuildArea = _buildArea,
                Connection = _connection
            };
        }
    }
}
