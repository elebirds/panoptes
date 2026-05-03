using NUnit.Framework;
using Panoptes.Presentation.Binders.Ugui;
using Panoptes.Presentation.ViewModels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Tests.EditMode.Presentation
{
    public sealed class ResourceHudUguiBinderTests
    {
        private sealed class CoroutineHost : MonoBehaviour
        {
        }

        private GameObject _root;
        private ResourceHudUguiBinder _binder;

        [TearDown]
        public void TearDown()
        {
            _binder?.Dispose();
            _binder = null;

            if (_root != null)
            {
                Object.DestroyImmediate(_root);
                _root = null;
            }
        }

        [Test]
        public void Render_ShouldCloneTemplateAndApplyRows()
        {
            var listRoot = CreateResourceListRoot();
            _binder = CreateBinder(listRoot);

            _binder.Render(new ResourceHudState(new[]
            {
                new ResourceHudRowState("ore", 3),
                new ResourceHudRowState("coal", 12)
            }));

            Assert.That(listRoot.childCount, Is.EqualTo(2));
            Assert.That(FindText(listRoot.GetChild(0), "BaseNum").text, Is.EqualTo("3"));
            Assert.That(FindText(listRoot.GetChild(1), "BaseNum").text, Is.EqualTo("12"));
            Assert.That(FindText(listRoot.GetChild(0), "ChangeNum").gameObject.activeSelf, Is.False);
            Assert.That(FindText(listRoot.GetChild(1), "ChangeNum").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void Render_ShouldShowDeltaAfterFirstSnapshotAndHideUnusedRows()
        {
            var listRoot = CreateResourceListRoot();
            _binder = CreateBinder(listRoot);
            _binder.Render(new ResourceHudState(new[]
            {
                new ResourceHudRowState("ore", 3),
                new ResourceHudRowState("coal", 12)
            }));

            _binder.Render(new ResourceHudState(new[]
            {
                new ResourceHudRowState("ore", 7)
            }));

            var changeText = FindText(listRoot.GetChild(0), "ChangeNum");
            Assert.That(changeText.gameObject.activeSelf, Is.True);
            Assert.That(changeText.text, Is.EqualTo("+4"));
            Assert.That(listRoot.GetChild(1).gameObject.activeSelf, Is.False);

            _binder.StopAllHideCoroutines();

            Assert.That(changeText.gameObject.activeSelf, Is.False);
        }

        private RectTransform CreateResourceListRoot()
        {
            _root = new GameObject("ResourceHudBinderTestRoot", typeof(RectTransform), typeof(CoroutineHost));
            var listObject = new GameObject("ResourceList", typeof(RectTransform));
            listObject.transform.SetParent(_root.transform, false);
            CreateResourceItem(listObject.transform);
            return listObject.GetComponent<RectTransform>();
        }

        private ResourceHudUguiBinder CreateBinder(RectTransform listRoot)
        {
            return new ResourceHudUguiBinder(
                _root.GetComponent<CoroutineHost>(),
                listRoot,
                new[] { "Icons/Resources" },
                3f,
                Color.green,
                Color.red);
        }

        private static void CreateResourceItem(Transform parent)
        {
            var item = new GameObject("ResourceItem", typeof(RectTransform));
            item.transform.SetParent(parent, false);

            var icon = new GameObject("Image", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(item.transform, false);

            var baseNum = new GameObject("BaseNum", typeof(RectTransform), typeof(TextMeshProUGUI));
            baseNum.transform.SetParent(item.transform, false);

            var changeNum = new GameObject("ChangeNum", typeof(RectTransform), typeof(TextMeshProUGUI));
            changeNum.transform.SetParent(item.transform, false);
        }

        private static TMP_Text FindText(Transform root, string childName)
        {
            return root.Find(childName).GetComponent<TMP_Text>();
        }
    }
}
