using UnityEngine;

public class GetPie : MonoBehaviour
{
    [SerializeField] private Transform PiePoint;
    [SerializeField] private GameObject Pie;

    public void CreatePie()
    {
        GameObject currentPie = Instantiate(Pie, PiePoint.position, Quaternion.identity);
    }
}
