// ============================================================================
// Nightflow - Warning Indicator System
// Manages off-screen threat indicators and emergency vehicle warnings
// ============================================================================

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Nightflow.Components;

namespace Nightflow.Systems.UI
{
    /// <summary>
    /// Updates UI state with warning indicators for off-screen threats.
    /// Tracks emergency vehicles, high-speed traffic, and hazards.
    ///
    /// From spec:
    /// - Off-screen threat signaling
    /// - Chevron arrows for emergency vehicles
    /// - Distance-based urgency
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WarningIndicatorSystem : ISystem
    {
        // Warning thresholds
        private const float EmergencyWarningDistance = 200f;
        private const float EmergencyUrgentDistance = 100f;
        private const float EmergencyCriticalDistance = 50f;

        private const float HazardWarningDistance = 150f;
        private const float TrafficWarningDistance = 100f;

        // Maximum signals to track
        private const int MaxSignals = 4;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<UIState>();
            state.RequireForUpdate<PlayerVehicleTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get player position
            float3 playerPos = float3.zero;
            float3 playerForward = new float3(0, 0, 1);

            foreach (var (transform, _) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<PlayerVehicleTag>>())
            {
                playerPos = transform.ValueRO.Position;
                playerForward = math.forward(transform.ValueRO.Rotation);
                break;
            }

            // Get UI state
            RefRW<UIState> uiState = SystemAPI.GetSingletonRW<UIState>();

            // Reset signals
            uiState.ValueRW.SignalCount = 0;
            uiState.ValueRW.Signal0 = float4.zero;
            uiState.ValueRW.Signal1 = float4.zero;
            uiState.ValueRW.Signal2 = float4.zero;
            uiState.ValueRW.Signal3 = float4.zero;

            // Track closest emergency vehicle
            float closestEmergencyDist = float.MaxValue;
            float2 closestEmergencyDir = float2.zero;
            bool hasEmergency = false;

            // Find emergency vehicles
            foreach (var (transform, emergency) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<EmergencyVehicle>>())
            {
                if (!emergency.ValueRO.IsActive)
                    continue;

                float3 toVehicle = transform.ValueRO.Position - playerPos;
                float distance = math.length(toVehicle);

                if (distance < EmergencyWarningDistance && distance < closestEmergencyDist)
                {
                    closestEmergencyDist = distance;
                    hasEmergency = true;

                    // Calculate direction relative to player
                    float3 localDir = math.normalize(toVehicle);
                    float dotForward = math.dot(localDir, playerForward);
                    float3 right = math.cross(new float3(0, 1, 0), playerForward);
                    float dotRight = math.dot(localDir, right);

                    closestEmergencyDir = new float2(dotRight, -dotForward); // -forward because behind = positive y

                    // Add as signal
                    if (uiState.ValueRO.SignalCount < MaxSignals)
                    {
                        float urgency = 1f - math.saturate(distance / EmergencyWarningDistance);
                        float4 signal = new float4(
                            GetScreenX(dotRight),
                            GetScreenY(dotForward),
                            urgency,
                            1f // Type: emergency
                        );

                        SetSignal(ref uiState.ValueRW, uiState.ValueRO.SignalCount, signal);
                        uiState.ValueRW.SignalCount++;
                    }
                }
            }

            // Update emergency warning in UI state
            if (hasEmergency)
            {
                uiState.ValueRW.EmergencyDistance = closestEmergencyDist;
                uiState.ValueRW.EmergencyETA = closestEmergencyDist / 50f; // Rough ETA at 180 km/h difference
                uiState.ValueRW.WarningPriority = closestEmergencyDist < EmergencyCriticalDistance ? 3 : 2;
            }
            else
            {
                uiState.ValueRW.EmergencyDistance = float.MaxValue;
                uiState.ValueRW.EmergencyETA = float.MaxValue;

                // Check for other warnings
                UpdateOtherWarnings(ref state, ref uiState.ValueRW, playerPos, playerForward);
            }
        }

        [BurstCompile]
        private void UpdateOtherWarnings(ref SystemState state, ref UIState uiState, float3 playerPos, float3 playerForward)
        {
            int warningPriority = 0;

            // Check for critical damage warning
            if (uiState.CriticalDamage)
            {
                warningPriority = math.max(warningPriority, 2);
            }
            else if (uiState.DamageTotal > 0.5f)
            {
                warningPriority = math.max(warningPriority, 1);
            }

            // Check high risk
            if (uiState.RiskPercent > 0.8f)
            {
                warningPriority = math.max(warningPriority, 1);
            }

            uiState.WarningPriority = warningPriority;
        }

        [BurstCompile]
        private float GetScreenX(float dotRight)
        {
            // Convert dot product to screen X (0-1)
            // -1 (left) -> 0, 0 (center) -> 0.5, 1 (right) -> 1
            return (dotRight + 1f) * 0.5f;
        }

        [BurstCompile]
        private float GetScreenY(float dotForward)
        {
            // Convert dot product to screen Y (0-1)
            // -1 (behind) -> 1, 0 (side) -> 0.5, 1 (ahead) -> 0
            return (1f - dotForward) * 0.5f;
        }

        [BurstCompile]
        private void SetSignal(ref UIState uiState, int index, float4 signal)
        {
            switch (index)
            {
                case 0: uiState.Signal0 = signal; break;
                case 1: uiState.Signal1 = signal; break;
                case 2: uiState.Signal2 = signal; break;
                case 3: uiState.Signal3 = signal; break;
            }
        }
    }

    /// <summary>
    /// Tag component for player vehicle (should already exist in vehicle components).
    /// </summary>
    public struct PlayerVehicleTag : IComponentData { }

    /// <summary>
    /// Component for emergency vehicles (should already exist in vehicle components).
    /// </summary>
    public struct EmergencyVehicle : IComponentData
    {
        public bool IsActive;
        public int EmergencyType; // 0=Police, 1=Ambulance, 2=Fire
        public float SirenPhase;
    }
}
