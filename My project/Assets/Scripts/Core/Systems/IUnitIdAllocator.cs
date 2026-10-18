namespace MultiplayerRts.Core
{
    /// <summary>
    /// Allocates unique <see cref="UnitId"/>s for newly created units. Injected into the
    /// spawn system so id generation is decoupled from the simulation and can be replaced
    /// with a deterministic stub in property tests. Implementations must return a distinct
    /// id on every call for the lifetime of a match.
    /// </summary>
    public interface IUnitIdAllocator
    {
        /// <summary>Returns a <see cref="UnitId"/> not previously returned for this match.</summary>
        UnitId Next();
    }
}
