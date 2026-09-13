// ============================================================================
// Nightflow - HUD Notification System
// Decays HUD notification timers. HUD values themselves are written by
// UISystem (Presentation group), the single writer of UIState gameplay data.
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Nightflow.Components;

namespace Nightflow.Systems.UI
{
    /// <summary>
    /// Manages HUD notifications (popups, bonuses, warnings).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Nightflow.Systems.UISystem))]
    public partial struct HUDNotificationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<UIControllerTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Update notification timers
            foreach (var notifications in
                SystemAPI.Query<DynamicBuffer<HUDNotification>>()
                    .WithAll<UIControllerTag>())
            {
                // Decay notification timers
                for (int i = notifications.Length - 1; i >= 0; i--)
                {
                    var notif = notifications[i];
                    notif.TimeRemaining -= deltaTime;

                    if (notif.TimeRemaining <= 0)
                    {
                        notifications.RemoveAt(i);
                    }
                    else
                    {
                        notifications[i] = notif;
                    }
                }
            }
        }
    }
}
