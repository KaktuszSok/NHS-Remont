using UnityEngine;

namespace NHSRemont.Gameplay.ItemSystem
{
    /// <summary>
    /// Items must be placed in Resources/Items/ in order to be deserialised correctly!
    /// </summary>
    [CreateAssetMenu(fileName = "New Item", menuName = "NHS/Item")]
    public class ItemType : ScriptableObject
    {
        public GameObject prefab;

        public int maxStackSize = 1;
        public float mass = 1f;

        /// <summary>
        /// Instantiates the item as a preview model
        /// </summary>
        public virtual Transform InstantiateModel(Transform parent)
        {
            GameObject go = Instantiate(prefab, parent);
            foreach (IDestroyOnModels comp in go.GetComponentsInChildren<IDestroyOnModels>())
            {
                Destroy((MonoBehaviour)comp);
            }
            return go.transform;
        }

        public static ItemType FromName(string name)
        {
            return Resources.Load<ItemType>("Items/" + name);
        }
    }
}