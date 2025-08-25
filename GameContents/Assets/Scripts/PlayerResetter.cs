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
        // CharacterController�� ������ ��ġ �ٲٱ� ���� ����� ��
        if (characterController != null)
            characterController.enabled = false;

        transform.position = resetPoint.position;
        transform.rotation = resetPoint.rotation;

        if (characterController != null)
            characterController.enabled = true;
    }
}
