// Nightflow - Notification Controller
// Manages notification queue, display, and lifecycle

using UnityEngine.UIElements;
using System.Collections.Generic;

namespace Nightflow.UI
{
    /// <summary>
    /// Manages the notification toast queue: enqueue, display, and fade-out lifecycle.
    /// </summary>
    public class NotificationController
    {
        private struct NotificationData
        {
            public string Text;
            public string Value;
            public string StyleClass;
            public float Duration;
        }

        private float notificationDuration;
        private VisualElement notificationContainer;
        private Queue<NotificationData> pendingNotifications = new Queue<NotificationData>();
        private List<VisualElement> activeNotifications = new List<VisualElement>();

        public void Initialize(VisualElement root, float duration)
        {
            notificationDuration = duration;
            notificationContainer = root.Q<VisualElement>("notification-container");
        }

        public void Update()
        {
            // Remove expired notifications
            for (int i = activeNotifications.Count - 1; i >= 0; i--)
            {
                var notification = activeNotifications[i];
                if (notification.resolvedStyle.opacity < 0.1f)
                {
                    notification.RemoveFromHierarchy();
                    activeNotifications.RemoveAt(i);
                }
            }

            // Show pending notifications (max 3 visible at once)
            while (pendingNotifications.Count > 0 && activeNotifications.Count < 3)
            {
                var data = pendingNotifications.Dequeue();
                CreateNotificationElement(data);
            }
        }

        public void ShowNotification(string text, string value = null, string styleClass = null)
        {
            pendingNotifications.Enqueue(new NotificationData
            {
                Text = text,
                Value = value,
                StyleClass = styleClass,
                Duration = notificationDuration
            });
        }

        private void CreateNotificationElement(NotificationData data)
        {
            if (notificationContainer == null) return;

            var notification = new VisualElement();
            notification.AddToClassList("notification");

            if (!string.IsNullOrEmpty(data.StyleClass))
            {
                notification.AddToClassList(data.StyleClass);
            }

            var textLabel = new Label(data.Text);
            textLabel.AddToClassList("notification-text");
            notification.Add(textLabel);

            if (!string.IsNullOrEmpty(data.Value))
            {
                var valueLabel = new Label(data.Value);
                valueLabel.AddToClassList("notification-value");
                notification.Add(valueLabel);
            }

            notificationContainer.Add(notification);
            activeNotifications.Add(notification);

            // Schedule fade out
            notification.schedule.Execute(() =>
            {
                notification.style.opacity = 0f;
            }).StartingIn((long)(data.Duration * 1000));
        }
    }
}
