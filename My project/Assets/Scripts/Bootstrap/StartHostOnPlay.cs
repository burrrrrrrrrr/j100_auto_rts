using Unity.Netcode;
using UnityEngine;

namespace MultiplayerRts.Bootstrap
{
    /// <summary>
    /// Convenience for solo editor testing: automatically starts the NetworkManager as Host
    /// when Play mode begins, so MatchBootstrap's OnServerStarted hook fires without needing a
    /// UI button. Put this on the same GameObject as the NetworkManager. Remove or disable it
    /// when you add real start-game UI / matchmaking.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public sealed class StartHostOnPlay : MonoBehaviour
    {
        [Tooltip("Seconds to wait after Start before calling StartHost (lets other systems init).")]
        [SerializeField] private float _delaySeconds = 0.25f;

        private void Start()
        {
            Invoke(nameof(StartHostNow), _delaySeconds);
        }

        private void StartHostNow()
        {
            var nm = NetworkManager.Singleton != null ? NetworkManager.Singleton : GetComponent<NetworkManager>();
            if (nm == null)
            {
                Debug.LogError("[StartHostOnPlay] No NetworkManager found.");
                return;
            }

            if (nm.IsServer || nm.IsClient)
            {
                return; // already running
            }

            if (nm.StartHost())
            {
                Debug.Log("[StartHostOnPlay] StartHost() succeeded.");
            }
            else
            {
                Debug.LogError("[StartHostOnPlay] StartHost() failed. Check the Unity Transport is assigned.");
            }
        }
    }
}
