// ============================================================================
// Nightflow - Procedural Vehicle Mesh Generation System
// Generates wireframe vehicles with headlights and taillights
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Nightflow.Components;
using Nightflow.Buffers;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Component for vehicle mesh generation state.
    /// </summary>
    public struct VehicleMeshData : IComponentData
    {
        /// <summary>Whether mesh has been generated.</summary>
        public bool IsGenerated;

        /// <summary>Vehicle body style: 0=sedan, 1=SUV, 2=truck, 3=police, 4=ambulance.</summary>
        public int BodyStyle;

        /// <summary>Base wireframe color.</summary>
        public float4 WireframeColor;

        /// <summary>Glow intensity multiplier.</summary>
        public float GlowIntensity;
    }

    /// <summary>
    /// Generates procedural wireframe vehicle meshes with lights.
    /// All vehicles rendered as neon wireframe with glowing headlights/taillights.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ProceduralHazardMeshSystem))]
    public partial struct ProceduralVehicleMeshSystem : ISystem
    {
        // =====================================================================
        // Vehicle Color Palette
        // =====================================================================

        // Player: Neon Cyan
        private static readonly float4 PlayerColor = new float4(0f, 1f, 1f, 1f);              // #00FFFF

        // Traffic: Neon Magenta
        private static readonly float4 TrafficColor = new float4(1f, 0f, 1f, 1f);             // #FF00FF

        // Emergency Police: Red/Blue alternating
        private static readonly float4 PoliceRed = new float4(1f, 0f, 0f, 1f);                // #FF0000
        private static readonly float4 PoliceBlue = new float4(0f, 0.4f, 1f, 1f);             // #0066FF

        // Emergency Ambulance: Red/White
        private static readonly float4 AmbulanceRed = new float4(1f, 0.2f, 0.2f, 1f);
        private static readonly float4 AmbulanceWhite = new float4(1f, 1f, 1f, 0.9f);

        // Ghost: Dim cyan with transparency
        private static readonly float4 GhostColor = new float4(0f, 0.8f, 0.8f, 0.5f);

        // Lights
        private static readonly float4 HeadlightColor = new float4(1f, 1f, 0.95f, 1f);        // Warm white
        private static readonly float4 TaillightColor = new float4(1f, 0f, 0f, 1f);           // Red
        private static readonly float4 BrakeLightColor = new float4(1f, 0.1f, 0.1f, 1f);      // Bright red

        // =====================================================================
        // Vehicle Dimensions (meters)
        // =====================================================================

        // Sedan (default traffic/player)
        private const float SedanLength = 4.5f;
        private const float SedanWidth = 1.8f;
        private const float SedanHeight = 1.4f;
        private const float SedanHoodHeight = 0.9f;
        private const float SedanRoofStart = 1.2f;      // Distance from front where roof starts
        private const float SedanRoofEnd = 3.5f;        // Distance from front where roof ends

        // SUV (traffic variant)
        private const float SUVLength = 4.8f;
        private const float SUVWidth = 2.0f;
        private const float SUVHeight = 1.8f;

        // Truck/Van (traffic variant)
        private const float TruckLength = 5.5f;
        private const float TruckWidth = 2.1f;
        private const float TruckHeight = 2.2f;

        // Light dimensions
        private const float HeadlightRadius = 0.12f;
        private const float TaillightWidth = 0.2f;
        private const float TaillightHeight = 0.08f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Player vehicles
            foreach (var (meshData, entity) in
                SystemAPI.Query<RefRW<VehicleMeshData>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithEntityAccess())
            {
                if (!meshData.ValueRO.IsGenerated)
                {
                    GenerateVehicleMesh(ref ecb, entity, PlayerColor, 0, 1.5f, true);
                    meshData.ValueRW.IsGenerated = true;
                    meshData.ValueRW.WireframeColor = PlayerColor;
                    meshData.ValueRW.GlowIntensity = 1.5f;
                }
            }

            // Traffic vehicles
            foreach (var (meshData, trafficAI, entity) in
                SystemAPI.Query<RefRW<VehicleMeshData>, RefRO<TrafficAI>>()
                    .WithAll<TrafficVehicleTag>()
                    .WithEntityAccess())
            {
                if (!meshData.ValueRO.IsGenerated)
                {
                    // Vary body style based on some property
                    int bodyStyle = meshData.ValueRO.BodyStyle;
                    GenerateVehicleMesh(ref ecb, entity, TrafficColor, bodyStyle, 1.0f, true);
                    meshData.ValueRW.IsGenerated = true;
                    meshData.ValueRW.WireframeColor = TrafficColor;
                    meshData.ValueRW.GlowIntensity = 1.0f;
                }
            }

            // Emergency vehicles
            foreach (var (meshData, emergencyAI, entity) in
                SystemAPI.Query<RefRW<VehicleMeshData>, RefRO<EmergencyAI>>()
                    .WithAll<EmergencyVehicleTag>()
                    .WithEntityAccess())
            {
                if (!meshData.ValueRO.IsGenerated)
                {
                    // Police or ambulance based on body style
                    int bodyStyle = meshData.ValueRO.BodyStyle;
                    float4 color = bodyStyle == 3 ? PoliceBlue : AmbulanceRed;
                    GenerateEmergencyVehicleMesh(ref ecb, entity, bodyStyle, 1.2f);
                    meshData.ValueRW.IsGenerated = true;
                    meshData.ValueRW.WireframeColor = color;
                    meshData.ValueRW.GlowIntensity = 1.2f;
                }
            }

            // Ghost vehicles
            foreach (var (meshData, entity) in
                SystemAPI.Query<RefRW<VehicleMeshData>>()
                    .WithAll<GhostVehicleTag>()
                    .WithEntityAccess())
            {
                if (!meshData.ValueRO.IsGenerated)
                {
                    GenerateVehicleMesh(ref ecb, entity, GhostColor, 0, 0.6f, false);
                    meshData.ValueRW.IsGenerated = true;
                    meshData.ValueRW.WireframeColor = GhostColor;
                    meshData.ValueRW.GlowIntensity = 0.6f;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        /// <summary>
        /// Generates a wireframe vehicle mesh with headlights and taillights.
        /// </summary>
        private void GenerateVehicleMesh(
            ref EntityCommandBuffer ecb,
            Entity entity,
            float4 bodyColor,
            int bodyStyle,
            float glowIntensity,
            bool generateLights)
        {
            // Get dimensions based on body style
            float length, width, height, hoodHeight, roofStart, roofEnd;
            GetVehicleDimensions(bodyStyle, out length, out width, out height,
                                 out hoodHeight, out roofStart, out roofEnd);

            var vertices = ecb.AddBuffer<MeshVertex>(entity);
            var triangles = ecb.AddBuffer<MeshTriangle>(entity);
            var subMeshes = ecb.AddBuffer<SubMeshRange>(entity);

            int bodyStartIndex = 0;

            // Generate body wireframe
            GenerateBodyGeometry(vertices, triangles, bodyColor, glowIntensity,
                                length, width, height, hoodHeight, roofStart, roofEnd);

            int bodyEndIndex = triangles.Length;

            // Add body sub-mesh
            subMeshes.Add(new SubMeshRange
            {
                StartIndex = bodyStartIndex,
                IndexCount = bodyEndIndex,
                MaterialType = 6 // Vehicle body
            });

            if (generateLights)
            {
                int lightsStartIndex = triangles.Length;

                // Generate headlights (front)
                GenerateHeadlights(vertices, triangles, length, width, hoodHeight);

                // Generate taillights (rear)
                GenerateTaillights(vertices, triangles, length, width, hoodHeight);

                int lightsEndIndex = triangles.Length;

                // Add lights sub-mesh
                subMeshes.Add(new SubMeshRange
                {
                    StartIndex = lightsStartIndex,
                    IndexCount = lightsEndIndex - lightsStartIndex,
                    MaterialType = 7 // Vehicle lights
                });
            }
        }

        /// <summary>
        /// Generates an emergency vehicle with light bar.
        /// </summary>
        private void GenerateEmergencyVehicleMesh(
            ref EntityCommandBuffer ecb,
            Entity entity,
            int bodyStyle,
            float glowIntensity)
        {
            float4 primaryColor = bodyStyle == 3 ? PoliceBlue : AmbulanceWhite;
            float4 secondaryColor = bodyStyle == 3 ? PoliceRed : AmbulanceRed;

            float length, width, height, hoodHeight, roofStart, roofEnd;
            GetVehicleDimensions(bodyStyle == 4 ? 2 : 0, out length, out width, out height,
                                 out hoodHeight, out roofStart, out roofEnd);

            var vertices = ecb.AddBuffer<MeshVertex>(entity);
            var triangles = ecb.AddBuffer<MeshTriangle>(entity);
            var subMeshes = ecb.AddBuffer<SubMeshRange>(entity);

            // Generate body with alternating colors for emergency look
            GenerateEmergencyBodyGeometry(vertices, triangles, primaryColor, secondaryColor,
                                         glowIntensity, length, width, height, hoodHeight, roofStart, roofEnd);

            // Generate light bar on roof
            GenerateLightBar(vertices, triangles, length, width, height, roofStart, roofEnd,
                            primaryColor, secondaryColor);

            // Generate headlights and taillights
            GenerateHeadlights(vertices, triangles, length, width, hoodHeight);
            GenerateTaillights(vertices, triangles, length, width, hoodHeight);

            subMeshes.Add(new SubMeshRange
            {
                StartIndex = 0,
                IndexCount = triangles.Length,
                MaterialType = 8 // Emergency vehicle
            });
        }

        private static void GetVehicleDimensions(int bodyStyle,
            out float length, out float width, out float height,
            out float hoodHeight, out float roofStart, out float roofEnd)
        {
            switch (bodyStyle)
            {
                case 1: // SUV
                    length = SUVLength;
                    width = SUVWidth;
                    height = SUVHeight;
                    hoodHeight = 1.1f;
                    roofStart = 1.0f;
                    roofEnd = 4.0f;
                    break;
                case 2: // Truck/Van
                    length = TruckLength;
                    width = TruckWidth;
                    height = TruckHeight;
                    hoodHeight = 1.0f;
                    roofStart = 0.8f;
                    roofEnd = 5.0f;
                    break;
                default: // Sedan
                    length = SedanLength;
                    width = SedanWidth;
                    height = SedanHeight;
                    hoodHeight = SedanHoodHeight;
                    roofStart = SedanRoofStart;
                    roofEnd = SedanRoofEnd;
                    break;
            }
        }

        /// <summary>
        /// Generates the main vehicle body wireframe.
        /// Shape: Low hood in front, cabin in middle, trunk in back.
        /// </summary>
        private static void GenerateBodyGeometry(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            float4 color,
            float glowIntensity,
            float length, float width, float height,
            float hoodHeight, float roofStart, float roofEnd)
        {
            float hw = width * 0.5f;
            float hl = length * 0.5f;

            // Adjust color intensity
            float4 wireColor = color * glowIntensity;
            wireColor.w = color.w;

            // Vehicle is centered at origin, front is +Z, rear is -Z
            // Cross-section points (viewed from front):
            //
            //     4-------5     <- roof
            //    /         \
            //   3           6   <- window corners
            //   |           |
            //   2-----------7   <- body sides
            //   |           |
            //   1-----------8   <- wheel wells
            //   |           |
            //   0-----------9   <- ground level

            // We'll define 5 cross-sections along the length:
            // 0: Front bumper
            // 1: Hood/windshield base
            // 2: Roof front
            // 3: Roof back
            // 4: Rear bumper

            float[] zPositions = {
                hl,                          // Front
                hl - roofStart * 0.3f,       // Hood end
                hl - roofStart,              // Windshield top
                -hl + (length - roofEnd),    // Rear window top
                -hl                          // Rear
            };

            float[] heights = {
                hoodHeight * 0.7f,           // Front (bumper height)
                hoodHeight,                  // Hood
                height,                      // Roof front
                height,                      // Roof back
                hoodHeight * 0.9f            // Rear (trunk height)
            };

            // Generate cross-sections
            int vertsPerSection = 8; // Simplified octagonal cross-section
            int numSections = 5;

            for (int s = 0; s < numSections; s++)
            {
                float z = zPositions[s];
                float h = heights[s];
                float bodyWidth = hw;

                // Taper slightly at front and rear
                if (s == 0 || s == numSections - 1)
                    bodyWidth *= 0.95f;

                // Generate 8 vertices for this cross-section
                // Bottom left corner and go clockwise
                float groundY = 0.15f; // Slight ground clearance
                float wheelWellY = 0.4f;

                vertices.Add(new MeshVertex { Position = new float3(-bodyWidth, groundY, z), Normal = new float3(-1, 0, 0), UV = new float2(0, s / 4f), Color = wireColor });
                vertices.Add(new MeshVertex { Position = new float3(-bodyWidth, wheelWellY, z), Normal = new float3(-1, 0, 0), UV = new float2(0.15f, s / 4f), Color = wireColor });
                vertices.Add(new MeshVertex { Position = new float3(-bodyWidth, h * 0.7f, z), Normal = new float3(-1, 0.3f, 0), UV = new float2(0.3f, s / 4f), Color = wireColor });
                vertices.Add(new MeshVertex { Position = new float3(-bodyWidth * 0.9f, h, z), Normal = new float3(-0.3f, 1, 0), UV = new float2(0.5f, s / 4f), Color = wireColor });
                vertices.Add(new MeshVertex { Position = new float3(bodyWidth * 0.9f, h, z), Normal = new float3(0.3f, 1, 0), UV = new float2(0.5f, s / 4f), Color = wireColor });
                vertices.Add(new MeshVertex { Position = new float3(bodyWidth, h * 0.7f, z), Normal = new float3(1, 0.3f, 0), UV = new float2(0.7f, s / 4f), Color = wireColor });
                vertices.Add(new MeshVertex { Position = new float3(bodyWidth, wheelWellY, z), Normal = new float3(1, 0, 0), UV = new float2(0.85f, s / 4f), Color = wireColor });
                vertices.Add(new MeshVertex { Position = new float3(bodyWidth, groundY, z), Normal = new float3(1, 0, 0), UV = new float2(1, s / 4f), Color = wireColor });
            }

            // Generate triangles connecting sections
            for (int s = 0; s < numSections - 1; s++)
            {
                int sectionStart = s * vertsPerSection;
                int nextSectionStart = (s + 1) * vertsPerSection;

                for (int v = 0; v < vertsPerSection; v++)
                {
                    int nextV = (v + 1) % vertsPerSection;

                    int bl = sectionStart + v;
                    int br = sectionStart + nextV;
                    int tl = nextSectionStart + v;
                    int tr = nextSectionStart + nextV;

                    // Two triangles per quad
                    triangles.Add(new MeshTriangle { Index = bl });
                    triangles.Add(new MeshTriangle { Index = tl });
                    triangles.Add(new MeshTriangle { Index = tr });

                    triangles.Add(new MeshTriangle { Index = bl });
                    triangles.Add(new MeshTriangle { Index = tr });
                    triangles.Add(new MeshTriangle { Index = br });
                }
            }

            // Cap front and back with simple triangles
            // Front cap
            int frontCenter = vertices.Length;
            float frontZ = zPositions[0];
            vertices.Add(new MeshVertex { Position = new float3(0, heights[0] * 0.5f, frontZ), Normal = new float3(0, 0, 1), UV = new float2(0.5f, 0), Color = wireColor });

            for (int v = 0; v < vertsPerSection; v++)
            {
                int nextV = (v + 1) % vertsPerSection;
                triangles.Add(new MeshTriangle { Index = frontCenter });
                triangles.Add(new MeshTriangle { Index = v });
                triangles.Add(new MeshTriangle { Index = nextV });
            }

            // Rear cap
            int rearCenter = vertices.Length;
            int rearSectionStart = (numSections - 1) * vertsPerSection;
            float rearZ = zPositions[numSections - 1];
            vertices.Add(new MeshVertex { Position = new float3(0, heights[numSections - 1] * 0.5f, rearZ), Normal = new float3(0, 0, -1), UV = new float2(0.5f, 1), Color = wireColor });

            for (int v = 0; v < vertsPerSection; v++)
            {
                int nextV = (v + 1) % vertsPerSection;
                triangles.Add(new MeshTriangle { Index = rearCenter });
                triangles.Add(new MeshTriangle { Index = rearSectionStart + nextV });
                triangles.Add(new MeshTriangle { Index = rearSectionStart + v });
            }
        }

        /// <summary>
        /// Generates emergency vehicle body with alternating color stripes.
        /// </summary>
        private static void GenerateEmergencyBodyGeometry(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            float4 primaryColor,
            float4 secondaryColor,
            float glowIntensity,
            float length, float width, float height,
            float hoodHeight, float roofStart, float roofEnd)
        {
            // Similar to regular body but with alternating colors
            // For simplicity, we use the primary color for main body
            GenerateBodyGeometry(vertices, triangles, primaryColor, glowIntensity,
                               length, width, height, hoodHeight, roofStart, roofEnd);
        }

        /// <summary>
        /// Generates headlight geometry (circular/rectangular glowing elements).
        /// </summary>
        private static void GenerateHeadlights(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            float length, float width, float hoodHeight)
        {
            float hw = width * 0.5f;
            float hl = length * 0.5f;
            float frontZ = hl - 0.05f; // Slightly recessed

            // Left headlight
            GenerateCircularLight(vertices, triangles,
                new float3(-hw * 0.7f, hoodHeight * 0.6f, frontZ),
                HeadlightRadius, HeadlightColor, new float3(0, 0, 1));

            // Right headlight
            GenerateCircularLight(vertices, triangles,
                new float3(hw * 0.7f, hoodHeight * 0.6f, frontZ),
                HeadlightRadius, HeadlightColor, new float3(0, 0, 1));
        }

        /// <summary>
        /// Generates taillight geometry (rectangular glowing elements).
        /// </summary>
        private static void GenerateTaillights(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            float length, float width, float hoodHeight)
        {
            float hw = width * 0.5f;
            float hl = length * 0.5f;
            float rearZ = -hl + 0.05f; // Slightly recessed

            float tailY = hoodHeight * 0.5f;

            // Left taillight (rectangular)
            GenerateRectangularLight(vertices, triangles,
                new float3(-hw * 0.8f, tailY, rearZ),
                TaillightWidth, TaillightHeight, TaillightColor, new float3(0, 0, -1));

            // Right taillight
            GenerateRectangularLight(vertices, triangles,
                new float3(hw * 0.8f, tailY, rearZ),
                TaillightWidth, TaillightHeight, TaillightColor, new float3(0, 0, -1));
        }

        /// <summary>
        /// Generates emergency light bar on roof.
        /// </summary>
        private static void GenerateLightBar(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            float length, float width, float height,
            float roofStart, float roofEnd,
            float4 color1, float4 color2)
        {
            float barLength = 0.8f;
            float barWidth = width * 0.6f;
            float barHeight = 0.15f;
            float barY = height + 0.02f;
            float barZ = (roofStart + roofEnd) * 0.25f - length * 0.5f + length * 0.3f;

            // Light bar base
            float4 barColor = new float4(0.2f, 0.2f, 0.2f, 1f);
            GenerateBox(vertices, triangles,
                new float3(0, barY + barHeight * 0.5f, barZ),
                new float3(barWidth * 0.5f, barHeight * 0.5f, barLength * 0.5f),
                barColor);

            // Left light (color1)
            GenerateCircularLight(vertices, triangles,
                new float3(-barWidth * 0.25f, barY + barHeight, barZ),
                0.08f, color1 * 1.5f, new float3(0, 1, 0));

            // Right light (color2)
            GenerateCircularLight(vertices, triangles,
                new float3(barWidth * 0.25f, barY + barHeight, barZ),
                0.08f, color2 * 1.5f, new float3(0, 1, 0));
        }

        /// <summary>
        /// Helper: Generate a circular light (6-sided polygon).
        /// </summary>
        private static void GenerateCircularLight(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            float3 center, float radius, float4 color, float3 normal)
        {
            int segments = 6;
            int centerIndex = vertices.Length;

            // Center vertex
            vertices.Add(new MeshVertex
            {
                Position = center,
                Normal = normal,
                UV = new float2(0.5f, 0.5f),
                Color = color * 2f // Extra bright for glow
            });

            // Outer vertices
            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * math.PI * 2f;
                float3 offset = new float3(math.cos(angle) * radius, math.sin(angle) * radius, 0);

                // Rotate offset to align with normal
                if (math.abs(normal.z) > 0.9f)
                    offset = new float3(offset.x, offset.y, 0);
                else if (math.abs(normal.y) > 0.9f)
                    offset = new float3(offset.x, 0, offset.y);

                vertices.Add(new MeshVertex
                {
                    Position = center + offset,
                    Normal = normal,
                    UV = new float2(0.5f + math.cos(angle) * 0.5f, 0.5f + math.sin(angle) * 0.5f),
                    Color = color
                });
            }

            // Triangles
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                triangles.Add(new MeshTriangle { Index = centerIndex });
                triangles.Add(new MeshTriangle { Index = centerIndex + 1 + i });
                triangles.Add(new MeshTriangle { Index = centerIndex + 1 + next });
            }
        }

        /// <summary>
        /// Helper: Generate a rectangular light.
        /// </summary>
        private static void GenerateRectangularLight(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            float3 center, float width, float height, float4 color, float3 normal)
        {
            int baseIndex = vertices.Length;
            float hw = width * 0.5f;
            float hh = height * 0.5f;

            // Bright color for glow effect
            float4 glowColor = color * 1.5f;
            glowColor.w = 1f;

            vertices.Add(new MeshVertex { Position = center + new float3(-hw, -hh, 0), Normal = normal, UV = new float2(0, 0), Color = glowColor });
            vertices.Add(new MeshVertex { Position = center + new float3(hw, -hh, 0), Normal = normal, UV = new float2(1, 0), Color = glowColor });
            vertices.Add(new MeshVertex { Position = center + new float3(hw, hh, 0), Normal = normal, UV = new float2(1, 1), Color = glowColor });
            vertices.Add(new MeshVertex { Position = center + new float3(-hw, hh, 0), Normal = normal, UV = new float2(0, 1), Color = glowColor });

            triangles.Add(new MeshTriangle { Index = baseIndex });
            triangles.Add(new MeshTriangle { Index = baseIndex + 1 });
            triangles.Add(new MeshTriangle { Index = baseIndex + 2 });

            triangles.Add(new MeshTriangle { Index = baseIndex });
            triangles.Add(new MeshTriangle { Index = baseIndex + 2 });
            triangles.Add(new MeshTriangle { Index = baseIndex + 3 });
        }

        /// <summary>
        /// Helper: Generate a simple box.
        /// </summary>
        private static void GenerateBox(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            float3 center, float3 halfExtents, float4 color)
        {
            int baseIndex = vertices.Length;

            // 8 corners
            float3[] corners = new float3[8];
            corners[0] = center + new float3(-halfExtents.x, -halfExtents.y, -halfExtents.z);
            corners[1] = center + new float3(halfExtents.x, -halfExtents.y, -halfExtents.z);
            corners[2] = center + new float3(halfExtents.x, -halfExtents.y, halfExtents.z);
            corners[3] = center + new float3(-halfExtents.x, -halfExtents.y, halfExtents.z);
            corners[4] = center + new float3(-halfExtents.x, halfExtents.y, -halfExtents.z);
            corners[5] = center + new float3(halfExtents.x, halfExtents.y, -halfExtents.z);
            corners[6] = center + new float3(halfExtents.x, halfExtents.y, halfExtents.z);
            corners[7] = center + new float3(-halfExtents.x, halfExtents.y, halfExtents.z);

            for (int i = 0; i < 8; i++)
            {
                vertices.Add(new MeshVertex
                {
                    Position = corners[i],
                    Normal = math.normalize(corners[i] - center),
                    UV = new float2(0, 0),
                    Color = color
                });
            }

            // 6 faces (12 triangles)
            int[] indices = {
                0, 2, 1, 0, 3, 2, // Bottom
                4, 5, 6, 4, 6, 7, // Top
                0, 1, 5, 0, 5, 4, // Front
                2, 3, 7, 2, 7, 6, // Back
                0, 4, 7, 0, 7, 3, // Left
                1, 2, 6, 1, 6, 5  // Right
            };

            for (int i = 0; i < indices.Length; i++)
            {
                triangles.Add(new MeshTriangle { Index = baseIndex + indices[i] });
            }
        }
    }
}
