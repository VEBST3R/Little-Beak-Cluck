using UnityEngine;
using UnityEngine.UI;

namespace LittleBeakCluck.UI
{
    /// <summary>
    /// Simple off-screen arrow indicator controlled by <see cref="EnemyHudController"/>.
    /// </summary>
    public class EnemyHudArrow : MonoBehaviour
    {
        [SerializeField] private RectTransform _root;
        [SerializeField] private Image _icon;
        [SerializeField] private CanvasGroup _canvasGroup;

        private RectTransform _rectTransform;

        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = transform as RectTransform);

        private void Awake()
        {
            if (_root == null)
                _root = transform as RectTransform;
        }

        public void SetActive(bool value)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = value ? 1f : 0f;
                _canvasGroup.interactable = value;
                _canvasGroup.blocksRaycasts = value;
            }
            else
            {
                gameObject.SetActive(value);
            }
        }

        public void SetColor(Color color)
        {
            if (_icon != null)
                _icon.color = color;
        }

        public void SetRotation(float degrees)
        {
            RectTransform.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }
    }
}
