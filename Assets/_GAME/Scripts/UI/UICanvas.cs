namespace UI
{
    using UnityEngine;

    public class UICanvas : MonoBehaviour
    {
        private void Awake()
        {
            var rectTransform = GetComponent<RectTransform>();
            var         ratio         = (float)Screen.width / Screen.height;
            if (ratio > 2.1f)
            {
                var leftBottom = rectTransform.offsetMin;
                var rightTop   = rectTransform.offsetMax;
                leftBottom.y = 0f;
                rightTop.y   = -100f;

                rectTransform.offsetMin = leftBottom;
                rectTransform.offsetMax = rightTop;
            }
        }

        public virtual void Show()
        {
            gameObject.SetActive(true);
        }

        public virtual void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}