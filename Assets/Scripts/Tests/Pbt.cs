namespace MultiplayerRts.Tests
{
    /// <summary>
    /// Shared configuration for the project's property-based tests. Property tests use the
    /// established CsCheck library (design.md, Testing Strategy) and MUST run a minimum of
    /// 100 generated iterations per property. Centralizing the iteration count here means a
    /// single edit raises the floor across every property test, and makes the minimum
    /// explicit at each call site: <c>gen.Sample(..., iter: Pbt.Iterations)</c>.
    /// </summary>
    public static class Pbt
    {
        /// <summary>
        /// Minimum number of generated iterations every property test must run
        /// (design.md: "each property test runs a minimum of 100 generated iterations").
        /// CsCheck's own default is also 100; passing this explicitly documents the
        /// requirement and guards against it being lowered.
        /// </summary>
        public const int Iterations = 100;
    }
}
