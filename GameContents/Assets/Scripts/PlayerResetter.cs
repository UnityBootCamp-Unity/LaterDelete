using UnityEngine;

public class PlayerResetter : MonoBehaviour
{
    public Transform resetPoint;

    CharacterController characterController;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("OutOfBounds"))
        {
            ResetPlayer();
        }
    }

    void ResetPlayer()
    {
        // CharacterController가 있으면 위치 바꾸기 전에 꺼줘야 함
        if (characterController != null)
            characterController.enabled = false;

        transform.position = resetPoint.position;
        transform.rotation = resetPoint.rotation;

        if (characterController != null)
            characterController.enabled = true;
    }
}
