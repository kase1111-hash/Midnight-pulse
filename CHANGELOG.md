# Changelog

All notable changes to Nightflow will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [0.1.0-alpha] - 2026-01-02

### Added

#### Core Systems (MVP Phases 1-11)
- **Vehicle Movement System** - Forward movement, drift, yaw dynamics with lane magnetism
- **Track Generation** - Procedural Hermite spline-based freeway generation
- **Lane Magnetism** - Smooth lane-following assist with configurable strength
- **Lane Change System** - Smoothstep-based lane transitions
- **Handbrake Drift** - Drift mechanics maintaining minimum forward velocity (8 m/s)
- **Collision System** - Zone-based collision detection
- **Impulse Physics** - Impact-based physics responses
- **Damage System** - Zone-based damage (front/rear/left/right)
- **Crash Detection** - Crash loop with instant reset and autopilot recovery
- **Traffic AI** - Lane decisions, movement, and behavior
- **Emergency Vehicles** - Ambulance/police with sirens and yielding behavior
- **Hazard Spawning** - Road debris and hazard generation
- **Off-Screen Signaling** - Warning indicators for approaching hazards
- **Scoring System** - Score calculation with speed tiers and risk multipliers
- **Camera System** - Chase camera with dynamic positioning
- **Wireframe Rendering** - Neon wireframe visual style
- **Audio Systems** - Engine, collision, siren, ambient, and music layers
- **Autopilot System** - AI driving during post-crash recovery
- **HUD Overlay** - Speed, score, damage indicators
- **Replay System** - Input log recording and deterministic playback

#### Post-MVP Features
- **Soft-Body Deformation** - Spring-damper physics for visual mesh deformation
- **Component Failures** - Suspension, steering, tires, engine, transmission failures
- **Progressive Handling Degradation** - Per-component handling effects
- **Cascade Failure Detection** - 3+ component failures trigger crash
- **Raytracing** - Dynamic headlight reflections, emergency light bouncing
- **SSR Fallback** - Screen-space reflections for non-RT hardware
- **Ghost Racing** - Async multiplayer using recorded input logs
- **Live Spectator Mode** - 7 camera modes (Follow, Cinematic, Overhead, Trackside, Free Cam, First Person, Chase)
- **Leaderboards** - Multiple categories (High Score, Best Time, Longest Run, Max Speed, Total Distance, Weekly, Friends)
- **Network Replication** - Input-based deterministic replication
- **Procedural City** - GPU-light buildings (256 buildings, 512 impostors)
- **City LOD System** - Aggressive LOD (LOD0=50m, LOD1=150m, LOD2=400m, Cull=600m)
- **City Skyline Renderer** - Star field and moon rendering
- **Input Rebinding** - Full input rebinding support
- **Logitech Wheel Support** - SDK integration with force feedback
- **Daily Challenges** - Procedurally generated challenges with leaderboards
- **Adaptive Difficulty** - Skill-based scaling for traffic, hazards, and emergency vehicles

#### Architecture
- 131 C# source files organized in modular ECS architecture
- 21 component files defining 60+ component types
- 75+ systems across Simulation, Presentation, Audio, UI, Network, and World groups
- 20+ entity tags and 10+ buffer types
- Configuration-driven design with JSON and C# config files
- Burst-compiled hot paths for performance
- Deterministic simulation for ghost racing and network replication

#### Documentation
- Complete technical specification (SPEC-SHEET.md)
- 11 detailed spec documents covering all systems
- Build system documentation with CI/CD integration
- Editor setup wizard for one-click project configuration

#### Build System
- PowerShell build scripts with batch wrapper
- GitHub Actions CI/CD pipeline
- Automated installer generation with Inno Setup

### Technical Notes

This is the first public alpha release of Nightflow. All core gameplay systems are implemented and functional. The game follows the "no scene reloads" critical rule - crashes result in instant reset with autopilot recovery.

**Known Limitations:**
- Alpha release - expect bugs and balance issues
- Performance may vary on lower-end hardware
- Raytracing requires compatible GPU (SSR fallback available)

---

## [Unreleased]

### Planned
- Performance optimizations (additional Burst compilation)
- Console controller profiles
- Steam Deck verification
- Additional vehicle types
- Weather effects
- Time of day variations
- Additional track environments
