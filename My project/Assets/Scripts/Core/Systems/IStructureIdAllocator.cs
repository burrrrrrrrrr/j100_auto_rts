namespace MultiplayerRts.Core
{
    /// <summary>
    /// Allocates unique <see cref="StructureId"/>s for newly placed structures. Injected
    /// into the placement system so id generation is decoupled from the simulation and can
    /// be replaced with a deterministic stub in property tests. Implementations must return
    /// a distinct id on every call for the lifetime of a match.
    /// </summary>
    public interface IStructureIdAllocator
    {
        /// <summary>Returns a <see cref="StructureId"/> not previously returned for this match.</summary>
        StructureId Next();
    }
}
