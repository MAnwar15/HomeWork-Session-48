using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class AllowSocketGrab : MonoBehaviour
{
    void Awake()
    {
        var socket = GetComponent<XRSocketInteractor>();
        if (socket != null)
            socket.allowSelect = true; // Use the correct property to allow selection
    }
}
