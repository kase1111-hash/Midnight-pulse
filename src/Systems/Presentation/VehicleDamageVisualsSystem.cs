// ============================================================================
// Nightflow - Vehicle Damage Visuals System
// Applies visual deformation and color changes based on damage state
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Buffers;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Applies visual damage effects to vehicle meshes.
    ///
    /// Effects:
    /// - Vertex deformation (crumpling) in damaged zones
    /// - Color shift toward red/orange at high damage
    /// - Reduced glow intensity (flickering) at critical damage
    /// - Broken headlights/taillights at zone damage > 0.7
    ///
    /// Phase 2 Enhancements:
    /// - Soft-body spring-damper deformation (smooth interpolation)
    /// - Component failure visual indicators
    /// - Enhanced deformation noise based on impact velocity
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ProceduralVehicleMeshSystem))]
    public partial struct VehicleDamageVisualsSystem : ISystem
    {
        // Damage thresholds
        private const float MinVisibleDamage = 0.1f;      // Damage below this is not visible
        private const float ModerateDamage = 0.4f;        // Noticeable deformation
        private const float SevereDamage = 0.7f;          // Major deformation, lights break
        private const float CriticalDamage = 0.9f;        // Near destruction

        // Deformation parameters
        private const float MaxDeformation = 0.4f;        // Maximum vertex displacement (meters)
        private const float DeformationNoise = 0.15f;     // Random variation in deformation

        // Phase 2: Soft-body spring-damper parameters
        private const float SoftBodyDefaultSpring = 8f;   // Spring constant (higher = faster response)
        private const float SoftBodyDefaultDamping = 0.7f;// Damping ratio (0.7 = slightly underdamped)

        // Color parameters
        private const float DamageColorBlend = 0.5f;      // How much damage affects color

        // Damage colors
        private static readonly float4 DamageColorMild = new float4(1f, 0.8f, 0.2f, 1f);    // Yellow-orange
        private static readonly float4 DamageColorSevere = new float4(1f, 0.3f, 0f, 1f);    // Orange-red
        private static readonly float4 DamageColorCritical = new float4(0.8f, 0.1f, 0.1f, 1f); // Dark red

        // Random seed for consistent deformation
        private uint _randomSeed;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _randomSeed = 12345;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float time = (float)SystemAPI.Time.ElapsedTime;
            float deltaTime = SystemAPI.Time.DeltaTime;

            // =============================================================
            // Phase 2: Update Soft-Body Deformation State (spring-damper)
            // =============================================================

            foreach (var (softBody, damageState) in
                SystemAPI.Query<RefRW<SoftBodyState>, RefRO<DamageState>>()
                    .WithAll<PlayerVehicleTag>())
            {
                ref var sb = ref softBody.ValueRW;
                var dmg = damageState.ValueRO;

                // Set target deformation based on current damage
                sb.TargetDeformation = new float4(dmg.Front, dmg.Rear, dmg.Left, dmg.Right);

                // Spring-damper physics: F = -k(x - target) - c*v
                float k = sb.SpringConstant > 0 ? sb.SpringConstant : SoftBodyDefaultSpring;
                float c = sb.Damping > 0 ? sb.Damping : SoftBodyDefaultDamping;

                // Calculate spring force and acceleration
                float4 displacement = sb.CurrentDeformation - sb.TargetDeformation;
                float4 springForce = -k * displacement;
                float4 dampingForce = -c * sb.DeformationVelocity;
                float4 acceleration = springForce + dampingForce;

                // Integrate velocity and position
                sb.DeformationVelocity += acceleration * deltaTime;
                sb.CurrentDeformation += sb.DeformationVelocity * deltaTime;

                // Clamp to valid range [0, 1]
                sb.CurrentDeformation = math.saturate(sb.CurrentDeformation);
            }

            // Process player vehicles with damage
            foreach (var (meshData, damageState, softBody, entity) in
                SystemAPI.Query<RefRW<VehicleMeshData>, RefRO<DamageState>, RefRO<SoftBodyState>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithEntityAccess())
            {
                // Check if deformation has changed significantly (use soft-body current, not raw damage)
                if (!HasDeformationChanged(ref meshData.ValueRW, softBody.ValueRO))
                    continue;

                // Skip if mesh not generated
                if (!meshData.ValueRO.IsGenerated)
                    continue;

                var vertices = SystemAPI.GetBuffer<MeshVertex>(entity);

                // Apply soft-body deformation (uses interpolated values)
                ApplySoftBodyDeformation(
                    ref vertices,
                    softBody.ValueRO,
                    damageState.ValueRO,
                    meshData.ValueRO.VehicleLength,
                    meshData.ValueRO.VehicleWidth,
                    meshData.ValueRO.WireframeColor,
                    time
                );

                // Update last damage values
                UpdateLastDamage(ref meshData.ValueRW, damageState.ValueRO);
            }

            // Fallback: Process player vehicles without SoftBodyState (use instant deformation)
            foreach (var (meshData, damageState, entity) in
                SystemAPI.Query<RefRW<VehicleMeshData>, RefRO<DamageState>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithNone<SoftBodyState>()
                    .WithEntityAccess())
            {
                // Check if damage has changed significantly
                if (!HasDamageChanged(ref meshData.ValueRW, damageState.ValueRO))
                    continue;

                // Skip if mesh not generated
                if (!meshData.ValueRO.IsGenerated)
                    continue;

                var vertices = SystemAPI.GetBuffer<MeshVertex>(entity);

                // Apply damage deformation (instant)
                ApplyDamageDeformation(
                    ref vertices,
                    damageState.ValueRO,
                    meshData.ValueRO.VehicleLength,
                    meshData.ValueRO.VehicleWidth,
                    meshData.ValueRO.WireframeColor,
                    time
                );

                // Update last damage values
                UpdateLastDamage(ref meshData.ValueRW, damageState.ValueRO);
            }

            // Process traffic vehicles with damage (if they have DamageState)
            foreach (var (meshData, damageState, entity) in
                SystemAPI.Query<RefRW<VehicleMeshData>, RefRO<DamageState>>()
                    .WithAll<TrafficVehicleTag>()
                    .WithEntityAccess())
            {
                if (!HasDamageChanged(ref meshData.ValueRW, damageState.ValueRO))
                    continue;

                if (!meshData.ValueRO.IsGenerated)
                    continue;

                var vertices = SystemAPI.GetBuffer<MeshVertex>(entity);

                ApplyDamageDeformation(
                    ref vertices,
                    damageState.ValueRO,
                    meshData.ValueRO.VehicleLength,
                    meshData.ValueRO.VehicleWidth,
                    meshData.ValueRO.WireframeColor,
                    time
                );

                UpdateLastDamage(ref meshData.ValueRW, damageState.ValueRO);
            }
        }

        private static bool HasDamageChanged(ref VehicleMeshData meshData, DamageState damage)
        {
            const float threshold = 0.05f;

            return math.abs(meshData.LastFrontDamage - damage.Front) > threshold ||
                   math.abs(meshData.LastRearDamage - damage.Rear) > threshold ||
                   math.abs(meshData.LastLeftDamage - damage.Left) > threshold ||
                   math.abs(meshData.LastRightDamage - damage.Right) > threshold ||
                   math.abs(meshData.LastTotalDamage - damage.Total) > threshold;
        }

        /// <summary>
        /// Phase 2: Check if soft-body deformation has changed enough to update mesh.
        /// Uses deformation velocity to detect active animation.
        /// </summary>
        private static bool HasDeformationChanged(ref VehicleMeshData meshData, SoftBodyState softBody)
        {
            const float threshold = 0.02f;
            const float velocityThreshold = 0.01f;

            // Check if deformation is actively changing (has velocity)
            float4 vel = softBody.DeformationVelocity;
            bool hasVelocity = math.abs(vel.x) > velocityThreshold ||
                              math.abs(vel.y) > velocityThreshold ||
                              math.abs(vel.z) > velocityThreshold ||
                              math.abs(vel.w) > velocityThreshold;

            if (hasVelocity) return true;

            // Check if current deformation differs from last rendered
            float4 current = softBody.CurrentDeformation;
            return math.abs(meshData.LastFrontDamage - current.x) > threshold ||
                   math.abs(meshData.LastRearDamage - current.y) > threshold ||
                   math.abs(meshData.LastLeftDamage - current.z) > threshold ||
                   math.abs(meshData.LastRightDamage - current.w) > threshold;
        }

        private static void UpdateLastDamage(ref VehicleMeshData meshData, DamageState damage)
        {
            meshData.LastFrontDamage = damage.Front;
            meshData.LastRearDamage = damage.Rear;
            meshData.LastLeftDamage = damage.Left;
            meshData.LastRightDamage = damage.Right;
            meshData.LastTotalDamage = damage.Total;
        }

        private void ApplyDamageDeformation(
            ref DynamicBuffer<MeshVertex> vertices,
            DamageState damage,
            float vehicleLength,
            float vehicleWidth,
            float4 baseColor,
            float time)
        {
            if (vehicleLength <= 0) vehicleLength = 4.5f;
            if (vehicleWidth <= 0) vehicleWidth = 1.8f;

            float halfLength = vehicleLength * 0.5f;
            float halfWidth = vehicleWidth * 0.5f;

            // Calculate overall damage for color effects
            float overallDamage = (damage.Front + damage.Rear + damage.Left + damage.Right) * 0.25f;

            // Determine damage color based on severity
            float4 damageColor = GetDamageColor(overallDamage);

            for (int i = 0; i < vertices.Length; i++)
            {
                MeshVertex vertex = vertices[i];
                float3 pos = vertex.Position;
                float4 color = vertex.Color;

                // Calculate which damage zone this vertex belongs to
                float frontInfluence = CalculateZoneInfluence(pos.z, halfLength * 0.5f, halfLength);
                float rearInfluence = CalculateZoneInfluence(-pos.z, halfLength * 0.5f, halfLength);
                float leftInfluence = CalculateZoneInfluence(-pos.x, halfWidth * 0.3f, halfWidth);
                float rightInfluence = CalculateZoneInfluence(pos.x, halfWidth * 0.3f, halfWidth);

                // Calculate total deformation for this vertex
                float deformAmount = 0f;
                float3 deformDirection = float3.zero;

                // Front damage: crumple inward and down
                if (damage.Front > MinVisibleDamage && frontInfluence > 0)
                {
                    float frontDeform = damage.Front * frontInfluence * MaxDeformation;
                    deformAmount += frontDeform;
                    deformDirection += new float3(0, -0.5f, -1f) * frontDeform;
                }

                // Rear damage: crumple inward and down
                if (damage.Rear > MinVisibleDamage && rearInfluence > 0)
                {
                    float rearDeform = damage.Rear * rearInfluence * MaxDeformation;
                    deformAmount += rearDeform;
                    deformDirection += new float3(0, -0.3f, 1f) * rearDeform;
                }

                // Left damage: crumple inward
                if (damage.Left > MinVisibleDamage && leftInfluence > 0)
                {
                    float leftDeform = damage.Left * leftInfluence * MaxDeformation;
                    deformAmount += leftDeform;
                    deformDirection += new float3(1f, -0.2f, 0) * leftDeform;
                }

                // Right damage: crumple inward
                if (damage.Right > MinVisibleDamage && rightInfluence > 0)
                {
                    float rightDeform = damage.Right * rightInfluence * MaxDeformation;
                    deformAmount += rightDeform;
                    deformDirection += new float3(-1f, -0.2f, 0) * rightDeform;
                }

                // Add noise to deformation for more organic look
                if (deformAmount > 0)
                {
                    // Pseudo-random noise based on vertex position
                    float noise = HashPosition(pos, _randomSeed) * DeformationNoise;
                    deformDirection += new float3(
                        HashPosition(pos + new float3(1, 0, 0), _randomSeed) - 0.5f,
                        HashPosition(pos + new float3(0, 1, 0), _randomSeed) - 0.5f,
                        HashPosition(pos + new float3(0, 0, 1), _randomSeed) - 0.5f
                    ) * noise * deformAmount;

                    pos += deformDirection;
                }

                // Apply color damage effects
                float colorDamageInfluence = math.max(
                    math.max(damage.Front * frontInfluence, damage.Rear * rearInfluence),
                    math.max(damage.Left * leftInfluence, damage.Right * rightInfluence)
                );

                if (colorDamageInfluence > MinVisibleDamage)
                {
                    // Blend toward damage color
                    float blendAmount = colorDamageInfluence * DamageColorBlend;
                    color = math.lerp(baseColor, damageColor, blendAmount);

                    // Add flickering at critical damage
                    if (colorDamageInfluence > CriticalDamage)
                    {
                        float flicker = math.sin(time * 15f + HashPosition(pos, _randomSeed) * 10f) * 0.3f + 0.7f;
                        color *= flicker;
                    }
                }

                // Reduce alpha/intensity for severely damaged areas (looks "broken")
                if (overallDamage > SevereDamage)
                {
                    float alphaReduction = (overallDamage - SevereDamage) / (1f - SevereDamage);
                    color.w *= (1f - alphaReduction * 0.5f);
                }

                vertex.Position = pos;
                vertex.Color = color;
                vertices[i] = vertex;
            }
        }

        /// <summary>
        /// Phase 2: Apply soft-body deformation using interpolated values.
        /// Uses spring-damper physics for smooth, realistic crumpling.
        /// </summary>
        private void ApplySoftBodyDeformation(
            ref DynamicBuffer<MeshVertex> vertices,
            SoftBodyState softBody,
            DamageState damage,
            float vehicleLength,
            float vehicleWidth,
            float4 baseColor,
            float time)
        {
            if (vehicleLength <= 0) vehicleLength = 4.5f;
            if (vehicleWidth <= 0) vehicleWidth = 1.8f;

            float halfLength = vehicleLength * 0.5f;
            float halfWidth = vehicleWidth * 0.5f;

            // Use soft-body interpolated deformation values
            float frontDeform = softBody.CurrentDeformation.x;
            float rearDeform = softBody.CurrentDeformation.y;
            float leftDeform = softBody.CurrentDeformation.z;
            float rightDeform = softBody.CurrentDeformation.w;

            // Use velocity to add "bounce" effect during active deformation
            float4 velocity = softBody.DeformationVelocity;
            float bounceAmount = math.length(velocity) * 0.5f;

            // Calculate overall damage for color effects
            float overallDamage = (frontDeform + rearDeform + leftDeform + rightDeform) * 0.25f;
            float4 damageColor = GetDamageColor(overallDamage);

            for (int i = 0; i < vertices.Length; i++)
            {
                MeshVertex vertex = vertices[i];
                float3 pos = vertex.Position;
                float4 color = vertex.Color;

                // Calculate zone influence
                float frontInfluence = CalculateZoneInfluence(pos.z, halfLength * 0.5f, halfLength);
                float rearInfluence = CalculateZoneInfluence(-pos.z, halfLength * 0.5f, halfLength);
                float leftInfluence = CalculateZoneInfluence(-pos.x, halfWidth * 0.3f, halfWidth);
                float rightInfluence = CalculateZoneInfluence(pos.x, halfWidth * 0.3f, halfWidth);

                float3 deformDirection = float3.zero;

                // Front: crumple inward and down with bounce
                if (frontDeform > MinVisibleDamage && frontInfluence > 0)
                {
                    float deform = frontDeform * frontInfluence * MaxDeformation;
                    float bounce = velocity.x * 0.3f * frontInfluence;
                    deformDirection += new float3(0, -0.5f - bounce, -1f) * deform;
                }

                // Rear: crumple inward with bounce
                if (rearDeform > MinVisibleDamage && rearInfluence > 0)
                {
                    float deform = rearDeform * rearInfluence * MaxDeformation;
                    float bounce = velocity.y * 0.3f * rearInfluence;
                    deformDirection += new float3(0, -0.3f - bounce, 1f) * deform;
                }

                // Left: crumple inward with bounce
                if (leftDeform > MinVisibleDamage && leftInfluence > 0)
                {
                    float deform = leftDeform * leftInfluence * MaxDeformation;
                    float bounce = velocity.z * 0.3f * leftInfluence;
                    deformDirection += new float3(1f, -0.2f - bounce, 0) * deform;
                }

                // Right: crumple inward with bounce
                if (rightDeform > MinVisibleDamage && rightInfluence > 0)
                {
                    float deform = rightDeform * rightInfluence * MaxDeformation;
                    float bounce = velocity.w * 0.3f * rightInfluence;
                    deformDirection += new float3(-1f, -0.2f - bounce, 0) * deform;
                }

                // Add noise for organic deformation
                float deformMagnitude = math.length(deformDirection);
                if (deformMagnitude > 0.01f)
                {
                    float noise = HashPosition(pos, _randomSeed) * DeformationNoise;

                    // Enhanced noise during active deformation (impact ripple)
                    if (bounceAmount > 0.1f)
                    {
                        noise *= (1f + bounceAmount * 2f);
                    }

                    deformDirection += new float3(
                        HashPosition(pos + new float3(1, 0, 0), _randomSeed) - 0.5f,
                        HashPosition(pos + new float3(0, 1, 0), _randomSeed) - 0.5f,
                        HashPosition(pos + new float3(0, 0, 1), _randomSeed) - 0.5f
                    ) * noise * deformMagnitude;

                    pos += deformDirection;
                }

                // Color effects using interpolated deformation
                float colorInfluence = math.max(
                    math.max(frontDeform * frontInfluence, rearDeform * rearInfluence),
                    math.max(leftDeform * leftInfluence, rightDeform * rightInfluence)
                );

                if (colorInfluence > MinVisibleDamage)
                {
                    float blendAmount = colorInfluence * DamageColorBlend;
                    color = math.lerp(baseColor, damageColor, blendAmount);

                    // Flash effect during active deformation
                    if (bounceAmount > 0.5f)
                    {
                        float flash = 1f + bounceAmount * 0.3f;
                        color.xyz *= flash;
                    }

                    // Flickering at critical damage
                    if (colorInfluence > CriticalDamage)
                    {
                        float flicker = math.sin(time * 15f + HashPosition(pos, _randomSeed) * 10f) * 0.3f + 0.7f;
                        color *= flicker;
                    }
                }

                // Reduce intensity for severely damaged areas
                if (overallDamage > SevereDamage)
                {
                    float alphaReduction = (overallDamage - SevereDamage) / (1f - SevereDamage);
                    color.w *= (1f - alphaReduction * 0.5f);
                }

                vertex.Position = pos;
                vertex.Color = color;
                vertices[i] = vertex;
            }
        }

        /// <summary>
        /// Calculate how much a vertex is influenced by a damage zone.
        /// Returns 0-1 based on distance from zone center.
        /// </summary>
        private static float CalculateZoneInfluence(float distance, float softStart, float hardEnd)
        {
            if (distance < softStart) return 0f;
            if (distance > hardEnd) return 1f;
            return (distance - softStart) / (hardEnd - softStart);
        }

        /// <summary>
        /// Get damage color based on overall damage level.
        /// </summary>
        private static float4 GetDamageColor(float damage)
        {
            if (damage < ModerateDamage)
            {
                return math.lerp(new float4(1, 1, 1, 1), DamageColorMild, damage / ModerateDamage);
            }
            else if (damage < SevereDamage)
            {
                float t = (damage - ModerateDamage) / (SevereDamage - ModerateDamage);
                return math.lerp(DamageColorMild, DamageColorSevere, t);
            }
            else
            {
                float t = (damage - SevereDamage) / (1f - SevereDamage);
                return math.lerp(DamageColorSevere, DamageColorCritical, t);
            }
        }

        /// <summary>
        /// Simple hash function for pseudo-random values based on position.
        /// Returns value in [0, 1].
        /// </summary>
        private static float HashPosition(float3 pos, uint seed)
        {
            uint h = seed;
            h ^= (uint)(pos.x * 73856093) ^ (uint)(pos.y * 19349663) ^ (uint)(pos.z * 83492791);
            h = h * 0x85ebca6b;
            h ^= h >> 13;
            h = h * 0xc2b2ae35;
            h ^= h >> 16;
            return (h & 0xFFFF) / 65535f;
        }
    }
}
