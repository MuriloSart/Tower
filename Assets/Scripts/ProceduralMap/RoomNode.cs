using UnityEngine;

public class RoomNode : MonoBehaviour
{
    public int Id;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(transform.position, 0.5f);
    }
}
