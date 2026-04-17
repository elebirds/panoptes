using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.Domestic
{
    public sealed class BuildGroupView : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private bool autoFindReferences = true;
        [SerializeField] private float preferredHeight = 52f;

        private void Awake()
        {
            EnsureReferences();
            EnsureLayoutElement();
        }

        public void Bind(string title)
        {
            EnsureReferences();
            EnsureLayoutElement();
            if (titleText != null)
            {
                titleText.text = string.IsNullOrWhiteSpace(title) ? "未命名分组" : title.Trim();
            }
        }

        private void EnsureReferences()
        {
            if (!autoFindReferences)
            {
                return;
            }

            if (backgroundImage == null)
            {
                var panelNode = transform.Find("Panel");
                if (panelNode != null)
                {
                    backgroundImage = panelNode.GetComponent<Image>();
                }

                if (backgroundImage == null)
                {
                    backgroundImage = GetComponentInChildren<Image>(true);
                }
            }

            if (titleText == null)
            {
                var titleNode = transform.Find("BuildName");
                if (titleNode != null)
                {
                    titleText = titleNode.GetComponent<TMP_Text>();
                }

                if (titleText == null)
                {
                    titleText = GetComponentInChildren<TMP_Text>(true);
                }
            }
        }

        private void EnsureLayoutElement()
        {
            var layout = GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = gameObject.AddComponent<LayoutElement>();
            }

            layout.minHeight = preferredHeight;
            layout.preferredHeight = preferredHeight;
            layout.flexibleHeight = 0f;
        }
    }
}
