Procedural Freeway Spline Math
1. Core Representation

We represent the freeway as a piecewise cubic spline built from segments.

Each TrackSegment owns:

A centerline spline

Lane offsets

Elevation profile

Curvature constraints

Use Cubic Hermite Splines

Why:

Easy control of tangents

Stable curvature

Deterministic

Cheap evaluation

2. Hermite Spline Definition

For a segment with start and end:

Position:

𝑃
0
,
𝑃
1
P
0
	​

,P
1
	​


Tangents:

𝑇
0
,
𝑇
1
T
0
	​

,T
1
	​


Parameter 
𝑡
∈
[
0
,
1
]
t∈[0,1]

Position
𝑆
(
𝑡
)
=
(
2
𝑡
3
−
3
𝑡
2
+
1
)
𝑃
0
+
(
𝑡
3
−
2
𝑡
2
+
𝑡
)
𝑇
0
+
(
−
2
𝑡
3
+
3
𝑡
2
)
𝑃
1
+
(
𝑡
3
−
𝑡
2
)
𝑇
1
S(t)=(2t
3
−3t
2
+1)P
0
	​

+(t
3
−2t
2
+t)T
0
	​

+(−2t
3
+3t
2
)P
1
	​

+(t
3
−t
2
)T
1
	​

First Derivative (Direction)
𝑆
′
(
𝑡
)
=
(
6
𝑡
2
−
6
𝑡
)
𝑃
0
+
(
3
𝑡
2
−
4
𝑡
+
1
)
𝑇
0
+
(
−
6
𝑡
2
+
6
𝑡
)
𝑃
1
+
(
3
𝑡
2
−
2
𝑡
)
𝑇
1
S
′
(t)=(6t
2
−6t)P
0
	​

+(3t
2
−4t+1)T
0
	​

+(−6t
2
+6t)P
1
	​

+(3t
2
−2t)T
1
	​


Normalized:

𝐹
(
𝑡
)
=
𝑆
′
(
𝑡
)
∥
𝑆
′
(
𝑡
)
∥
F(t)=
∥S
′
(t)∥
S
′
(t)
	​

3. Local Coordinate Frame (Critical)

At every 
𝑡
t:

Forward:

𝐹
(
𝑡
)
F(t)

World up:

𝑈
=
(
0
,
1
,
0
)
U=(0,1,0)

Right:

𝑅
(
𝑡
)
=
𝐹
(
𝑡
)
×
𝑈
∥
𝐹
(
𝑡
)
×
𝑈
∥
R(t)=
∥F(t)×U∥
F(t)×U
	​


Corrected Up:

𝑈
′
(
𝑡
)
=
𝑅
(
𝑡
)
×
𝐹
(
𝑡
)
U
′
(t)=R(t)×F(t)

This frame is stable at high speed and lane-magnetism friendly.

4. Segment Length Control (No Stretching)

Hermite splines are not arc-length parameterized.

We approximate length by sampling:

𝐿
≈
∑
𝑖
=
0
𝑁
−
1
∥
𝑆
(
𝑡
𝑖
+
1
)
−
𝑆
(
𝑡
𝑖
)
∥
L≈
i=0
∑
N−1
	​

∥S(t
i+1
	​

)−S(t
i
	​

)∥

𝑁
=
16
N=16 is sufficient

Cache length per segment

Build a lookup table for 
𝑡
↔
𝑠
t↔s

This ensures:

Constant speed movement

Stable scoring

Predictable hazard spacing

5. Procedural Segment Generation

Each new segment is generated from the previous segment’s end.

Inputs

Previous end position 
𝑃
0
P
0
	​


Previous tangent 
𝑇
0
T
0
	​


Difficulty scalar 
𝑑
∈
[
0
,
1
]
d∈[0,1]

RNG seed

5.1 Forward Direction

Let:

𝜃
θ = yaw change

𝜙
ϕ = pitch change (small)

Sample:

𝜃
∼
𝑈
(
−
𝜃
𝑚
𝑎
𝑥
𝑑
,
𝜃
𝑚
𝑎
𝑥
𝑑
)
θ∼U(−θ
max
	​

d,θ
max
	​

d)
𝜙
∼
𝑈
(
−
𝜙
𝑚
𝑎
𝑥
,
𝜙
𝑚
𝑎
𝑥
)
ϕ∼U(−ϕ
max
	​

,ϕ
max
	​

)

Apply rotation to tangent direction.

5.2 Segment Length
𝐿
∼
𝑈
(
𝐿
𝑚
𝑖
𝑛
,
𝐿
𝑚
𝑎
𝑥
)
L∼U(L
min
	​

,L
max
	​

)

Typical:

𝐿
𝑚
𝑖
𝑛
=
40
𝑚
L
min
	​

=40m

𝐿
𝑚
𝑎
𝑥
=
120
𝑚
L
max
	​

=120m

5.3 End Position
𝑃
1
=
𝑃
0
+
𝑇
^
0
⋅
𝐿
P
1
	​

=P
0
	​

+
T
^
0
	​

⋅L
5.4 Tangents

Tangents are scaled by length:

𝑇
0
=
𝑇
^
0
⋅
𝐿
⋅
𝛼
T
0
	​

=
T
^
0
	​

⋅L⋅α
𝑇
1
=
𝑇
^
1
⋅
𝐿
⋅
𝛼
T
1
	​

=
T
^
1
	​

⋅L⋅α

Where:

𝛼
∈
[
0.4
,
0.6
]
α∈[0.4,0.6] controls curvature softness

6. Curvature Constraint (High-Speed Safe)

Compute curvature:

𝜅
(
𝑡
)
=
∥
𝑆
′
(
𝑡
)
×
𝑆
′
′
(
𝑡
)
∥
∥
𝑆
′
(
𝑡
)
∥
3
κ(t)=
∥S
′
(t)∥
3
∥S
′
(t)×S
′′
(t)∥
	​


We enforce:

𝜅
𝑚
𝑎
𝑥
=
1
𝑅
𝑚
𝑖
𝑛
κ
max
	​

=
R
min
	​

1
	​


If exceeded:

Reduce 
𝜃
θ

Regenerate segment

This prevents:

Impossible turns

Handbrake-only curves unless desired

7. Lane Generation (Offsets)

For each lane 
𝑖
i:

Let:

Lane width 
𝑤
w

Lane index 
𝑖
∈
[
−
𝑛
,
𝑛
]
i∈[−n,n]

Offset spline:

𝑆
𝑖
(
𝑡
)
=
𝑆
(
𝑡
)
+
𝑅
(
𝑡
)
⋅
(
𝑖
⋅
𝑤
)
S
i
	​

(t)=S(t)+R(t)⋅(i⋅w)

Each lane gets:

Its own spline buffer

Shared arc-length mapping

This keeps magnetism math simple.

8. Elevation & Overpasses

Elevation is handled as a secondary spline.

Height Offset
ℎ
(
𝑡
)
=
𝐴
sin
⁡
(
𝜋
𝑡
)
h(t)=Asin(πt)

𝐴
A = elevation gain

Used for bridges / ramps

Apply:

𝑆
(
𝑡
)
.
𝑦
+
=
ℎ
(
𝑡
)
S(t).y+=h(t)
Stacked Overpasses

Duplicate segment at higher 
𝑦
y

Independent lane entities

No physical intersection

Visual overlap only.

9. Tunnels

Tunnel flag on segment:

Spawn tunnel mesh aligned to frame

Reduce lighting radius

Increase reverb

Spline math unchanged.

10. Fork Generation

At fork point 
𝑃
𝑓
P
f
	​

:

Create two child splines:

Same 
𝑃
0
,
𝑇
0
P
0
	​

,T
0
	​


Diverging yaw:

𝜃
𝐿
=
−
𝜃
𝑓
𝑜
𝑟
𝑘
θ
L
	​

=−θ
fork
	​

𝜃
𝑅
=
+
𝜃
𝑓
𝑜
𝑟
𝑘
θ
R
	​

=+θ
fork
	​


Gradually separate:

𝑆
𝑓
𝑜
𝑟
𝑘
(
𝑡
)
+
=
𝑅
(
𝑡
)
⋅
(
𝑑
𝑓
𝑜
𝑟
𝑘
⋅
𝑡
2
)
S
fork
	​

(t)+=R(t)⋅(d
fork
	​

⋅t
2
)

This ensures:

Gentle split

Clear player choice

No snapping

11. Deterministic Generation

Use:

𝑠
𝑒
𝑒
𝑑
=
ℎ
𝑎
𝑠
ℎ
(
𝑔
𝑙
𝑜
𝑏
𝑎
𝑙
𝑆
𝑒
𝑒
𝑑
,
𝑠
𝑒
𝑔
𝑚
𝑒
𝑛
𝑡
𝐼
𝑛
𝑑
𝑒
𝑥
)
seed=hash(globalSeed,segmentIndex)

Guarantees:

Replayable runs

Ghost driving

Network determinism

12. Unity DOTS Pseudocode (Core)
float3 P0 = prevEnd;
float3 T0 = prevTangent;

float yaw = rand.Range(-maxYaw * difficulty, maxYaw * difficulty);
float pitch = rand.Range(-maxPitch, maxPitch);

quaternion rot = quaternion.Euler(pitch, yaw, 0);
float3 T1Dir = math.mul(rot, math.normalize(T0));

float L = rand.Range(minLen, maxLen);

float3 P1 = P0 + T1Dir * L;

float3 T0s = math.normalize(T0) * L * alpha;
float3 T1s = math.normalize(T1Dir) * L * alpha;

13. Why This Works for Your Game

Endless but readable

Forks are natural

Lanes are mathematically stable

Magnetism math plugs in directly

High-speed safe

Minimal geometry
