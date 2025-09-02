using Game.Client.GameObjects.Network;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(PoseReport))]
[RequireComponent(typeof(XRGrabInteractable))]
public class PoseReportXRRelay : MonoBehaviour
{
    PoseReport posereport;
    XRGrabInteractable grab;

    void Awake()
    {
        posereport = GetComponent<PoseReport>();
        grab = GetComponent<XRGrabInteractable>();

        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnDrop);
    }

    void OnDestroy()
    {
        grab.selectEntered.RemoveListener(OnGrab);
        grab.selectExited.RemoveListener(OnDrop);
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        // 잡은 사람의 Player(NetworkObjectId) 찾아서 넘기기
        var interactorTransform = args.interactorObject.GetAttachTransform(args.interactableObject);
        var playerNO = interactorTransform ? interactorTransform.GetComponentInParent<NetworkObject>() : null;

        if (playerNO == null)
        {
            Debug.LogWarning("[PickableXRRelay] Player NetworkObject를 찾지 못했습니다.");
            return;
        }
        posereport.BeginLocalGrab(interactorTransform, playerNO.NetworkObjectId);
    }

    void OnDrop(SelectExitEventArgs args)
    {
        posereport.EndLocalGrab();
    }
}
