using UnityEngine;

public class ParallaxController : MonoBehaviour
{
    [SerializeField] Transform[] layers;
    [SerializeField] float[] coeff;

    private int layersCount;
    void Start()
    {
        layersCount = layers.Length;
    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < layersCount; i++)
        {
            layers[i].position = new Vector2(transform.position.x * coeff[i], layers[i].position.y);
        }
    }
}
