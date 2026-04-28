using NUnit.Framework;
using Panoptes.Presentation.Common;

namespace Panoptes.Tests.EditMode.UI
{
    public sealed class EventSubscriptionBagTests
    {
        [Test]
        public void Subscribe_ShouldClearPreviousSubscriptionsBeforeAddingNewOnes()
        {
            var bag = new EventSubscriptionBag();
            var first = new EventSource();
            var second = new EventSource();
            var count = 0;

            bag.Subscribe(
                () => first.Changed += OnChanged,
                () => first.Changed -= OnChanged);

            bag.Subscribe(
                () => second.Changed += OnChanged,
                () => second.Changed -= OnChanged);

            first.Raise();
            second.Raise();

            Assert.That(count, Is.EqualTo(1));

            void OnChanged()
            {
                count++;
            }
        }

        [Test]
        public void Clear_ShouldUnsubscribeAllRegisteredHandlers()
        {
            var bag = new EventSubscriptionBag();
            var source = new EventSource();
            var count = 0;

            bag.Subscribe(
                () => source.Changed += OnChanged,
                () => source.Changed -= OnChanged);
            bag.Clear();

            source.Raise();

            Assert.That(count, Is.Zero);

            void OnChanged()
            {
                count++;
            }
        }

        private sealed class EventSource
        {
            public event System.Action Changed;

            public void Raise()
            {
                Changed?.Invoke();
            }
        }
    }
}
