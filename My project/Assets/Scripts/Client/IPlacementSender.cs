using MultiplayerRts.Net;

namespace MultiplayerRts.Client
{
    /// <summary>
    /// The seam between the pure client mirror and the Unity/NGO transport for the one
    /// client→server message the presentation layer produces: a placement intent (Requirement
    /// 4.1). <see cref="GameClientStateMirror.SubmitPlacement"/> builds the pure
    /// <see cref="PlacementIntent"/> payload and hands it to this seam rather than touching
    /// Netcode directly, so the "build the payload and submit it" path is testable out-of-tree
    /// without a Unity runtime — the same split tasks 13.1–13.3 used to keep translation pure.
    ///
    /// <para>
    /// The concrete implementation is the thin NGO behaviour, which converts the intent to the
    /// unmanaged wire struct (<see cref="NetPlacementIntent.From"/>) and calls
    /// <c>MatchRpcBehaviour.SubmitPlacementServerRpc</c>. The server ignores any player id in
    /// the payload and stamps the authenticated sender, so the client never needs to supply a
    /// trustworthy placing-player id here.
    /// </para>
    /// </summary>
    public interface IPlacementSender
    {
        /// <summary>
        /// Sends the built placement <paramref name="intent"/> to the authoritative server. The
        /// implementation is responsible only for transport mechanics; it performs no
        /// validation (the server validates against canonical state, Requirement 2.4).
        /// </summary>
        void Send(PlacementIntent intent);
    }
}
