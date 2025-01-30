using Unity.Netcode;
using UnityEngine;

public class NetworkedLoadingProgressTracker : NetworkBehaviour
{
    public NetworkVariable<float> Progress { get; } = new NetworkVariable<float>(0,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    void Awake()
    {
        DontDestroyOnLoad(this);
    }
}