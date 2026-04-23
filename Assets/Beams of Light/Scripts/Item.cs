using UnityEngine;
public class Item : MonoBehaviour
{
    public string objectName;

    void OnTriggerEnter(Collider healthcare)
    {
        if (healthcare.CompareTag("Player"))
        {
                Manager.instance.ItemCollected();
                Destroy(gameObject);
        }
    }
}
