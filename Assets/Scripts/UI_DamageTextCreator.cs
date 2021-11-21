using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_DamageTextCreator : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private Transform prefab_root;

    public void DamageTextCreate(float damage)
    {
        GameObject currentDamage = Instantiate(prefab, new Vector3(0, 0, 0), Quaternion.identity);
        currentDamage.transform.SetParent(prefab_root, false);
        currentDamage.transform.position = new Vector3(0, 0, 0);
        currentDamage.GetComponent<Text>().text = $"-{damage.ToString()}";
    }
}
