using UnityEngine;

namespace LittleBeakCluck.UI
{
    /// <summary>
    /// Simple controller for toggling the defeat menu visibility.
    /// Optionally deactivates the menu on startup so it only appears when shown.
    /// </summary>
    public class DefeatMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private bool deactivateOnAwake = true;

        public GameObject MenuRoot => menuRoot != null ? menuRoot : gameObject;

        private void Awake()
        {
            if (menuRoot == null)
            {
                menuRoot = gameObject;
            }

            if (deactivateOnAwake && menuRoot.activeSelf)
            {
                menuRoot.SetActive(false);
            }
        }

        public void Hide()
        {
            if (menuRoot != null && menuRoot.activeSelf)
            {
                menuRoot.SetActive(false);
            }
        }
    }
}
