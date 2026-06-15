using UnityEngine;
using Unity.Cinemachine; // O using Cinemachine;

public class CameraFinder : MonoBehaviour
{
    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        var vcam = GetComponent<CinemachineCamera>(); // O CinemachineVirtualCamera en v2

        if (player != null && vcam != null)
        {
            vcam.Follow = player.transform;
            // vcam.LookAt = player.transform; // Si usas LookAt
        }
    }
}