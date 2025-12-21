Lane Change & Merge Math
1. Design Goals (Non-Negotiable)

Lane change feels analog, not binary

Player steering initiates, system completes

No sideways teleport

Works at 200+ km/h

Compatible with:

Lane magnetism

Autopilot

Forks

Drift / handbrake

Merges never “steal control”

2. Conceptual Model

A lane change is not:

Switching lane IDs instantly

A lane change is:

Temporarily following a blended virtual spline between lanes

We introduce a Lane Transition State.

3. Data Additions (ECS)
LaneTransition Component
Active          : bool
FromLane        : Entity
ToLane          : Entity
Progress        : float   // 0 → 1
Duration        : float   // seconds
Direction       : int     // -1 left, +1 right

4. Lane Change Trigger Logic
Trigger Conditions

A lane change may begin when:

Player steering exceeds threshold:

∣
𝑠
∣
>
𝑠
𝑡
𝑟
𝑖
𝑔
𝑔
𝑒
𝑟
≈
0.35
∣s∣>s
trigger
	​

≈0.35

Steering direction matches lane direction

Target lane exists and is not blocked

Not already transitioning

Lane Selection

Let:

Current lane index = 
𝑖
i

Steering sign = 
sign
(
𝑠
)
sign(s)

Target lane:

𝑖
𝑡
𝑎
𝑟
𝑔
𝑒
𝑡
=
𝑖
+
sign
(
𝑠
)
i
target
	​

=i+sign(s)
5. Transition Spline Math (Key Insight)

We do not change forward motion.

We only blend lateral offset.

Lane Offset Definitions

Let:

Lane width = 
𝑤
w

Lane center offsets:

𝑥
𝑓
𝑟
𝑜
𝑚
=
𝑖
⋅
𝑤
x
from
	​

=i⋅w
𝑥
𝑡
𝑜
=
𝑖
𝑡
𝑎
𝑟
𝑔
𝑒
𝑡
⋅
𝑤
x
to
	​

=i
target
	​

⋅w
Smooth Transition Function

Use smoothstep (C¹ continuous):

𝜆
(
𝑡
)
=
3
𝑡
2
−
2
𝑡
3
λ(t)=3t
2
−2t
3

Where:

𝑡
=
𝑐
𝑙
𝑎
𝑚
𝑝
(
𝑃
𝑟
𝑜
𝑔
𝑟
𝑒
𝑠
𝑠
𝐷
𝑢
𝑟
𝑎
𝑡
𝑖
𝑜
𝑛
,
0
,
1
)
t=clamp(
Duration
Progress
	​

,0,1)
Blended Lateral Offset
𝑥
(
𝑡
)
=
(
1
−
𝜆
)
𝑥
𝑓
𝑟
𝑜
𝑚
+
𝜆
𝑥
𝑡
𝑜
x(t)=(1−λ)x
from
	​

+λx
to
	​


This is the virtual lane center during transition.

6. Applying Lane Magnetism During Transition

Instead of pulling toward a lane spline center:

𝑥
𝑒
𝑟
𝑟
𝑜
𝑟
=
𝑥
𝑣
𝑒
ℎ
𝑖
𝑐
𝑙
𝑒
−
𝑥
(
𝑡
)
x
error
	​

=x
vehicle
	​

−x(t)

Plug this directly into your existing magnetism equation:

𝑎
𝑙
𝑎
𝑡
=
−
𝜔
2
𝑥
𝑒
𝑟
𝑟
𝑜
𝑟
−
2
𝜔
𝑥
˙
a
lat
	​

=−ω
2
x
error
	​

−2ω
x
˙

👉 This is why the system stays stable.

7. Transition Duration (Speed-Aware)

Lane change should take longer at higher speeds (feels grounded).

𝑇
=
𝑐
𝑙
𝑎
𝑚
𝑝
(
𝑇
𝑏
𝑎
𝑠
𝑒
⋅
𝑣
𝑣
𝑟
𝑒
𝑓
,
𝑇
𝑚
𝑖
𝑛
,
𝑇
𝑚
𝑎
𝑥
)
T=clamp(T
base
	​

⋅
v
ref
	​

v
	​

	​

,T
min
	​

,T
max
	​

)

Suggested:

𝑇
𝑏
𝑎
𝑠
𝑒
=
0.6
𝑠
T
base
	​

=0.6s

𝑇
𝑚
𝑖
𝑛
=
0.45
𝑠
T
min
	​

=0.45s

𝑇
𝑚
𝑎
𝑥
=
1.0
𝑠
T
max
	​

=1.0s

8. Steering During Transition

Player steering still matters, but is attenuated:

𝑠
𝑒
𝑓
𝑓
𝑒
𝑐
𝑡
𝑖
𝑣
𝑒
=
𝑠
⋅
(
1
−
𝜆
)
s
effective
	​

=s⋅(1−λ)

Early transition → player strong influence

Late transition → lane pull dominates

Prevents oscillation mid-change

9. Completion Rule

When:

𝑃
𝑟
𝑜
𝑔
𝑟
𝑒
𝑠
𝑠
≥
𝐷
𝑢
𝑟
𝑎
𝑡
𝑖
𝑜
𝑛
Progress≥Duration

Then:

Set current lane = target lane

Clear LaneTransition

Restore full magnetism

No snap, because offset is already aligned.

10. Emergency Abort (Player Safety)

If:

Hazard detected in target lane

Player counter-steers hard:

∣
𝑠
∣
>
𝑠
𝑎
𝑏
𝑜
𝑟
𝑡
≈
0.7
opposite direction
∣s∣>s
abort
	​

≈0.7opposite direction

Then:

Reverse transition direction

Swap From/To lanes

Reset progress using:

𝑃
𝑟
𝑜
𝑔
𝑟
𝑒
𝑠
𝑠
=
𝐷
𝑢
𝑟
𝑎
𝑡
𝑖
𝑜
𝑛
⋅
(
1
−
𝑡
)
Progress=Duration⋅(1−t)

Feels natural — like correcting mid-move.

11. Lane Merging Math (On-Ramp / Fork Merge)

Merges are forced geometry, not player choice.

Merge Offset Function

Let:

Merge length = 
𝐿
𝑚
L
m
	​


Distance along spline = 
𝑠
s

𝜆
=
𝑐
𝑙
𝑎
𝑚
𝑝
(
𝑠
𝐿
𝑚
,
0
,
1
)
λ=clamp(
L
m
	​

s
	​

,0,1)

Apply lateral offset:

𝑥
(
𝑠
)
=
(
1
−
𝜆
)
𝑥
𝑚
𝑒
𝑟
𝑔
𝑒
+
𝜆
𝑥
𝑚
𝑎
𝑖
𝑛
x(s)=(1−λ)x
merge
	​

+λx
main
	​


This is exactly the same math as lane change — just driven by distance, not input.

12. Fork Exit Handling

At forks:

Disable lane changes

Reduce magnetism slightly

Lock chosen lane after commit distance

𝑚
𝑓
𝑜
𝑟
𝑘
(
𝑠
)
=
𝑠
𝑚
𝑜
𝑜
𝑡
ℎ
𝑠
𝑡
𝑒
𝑝
(
1
,
0.7
,
𝑠
/
𝐿
𝑓
𝑜
𝑟
𝑘
)
m
fork
	​

(s)=smoothstep(1,0.7,s/L
fork
	​

)

Prevents “yo-yo” behavior.

13. Autopilot Lane Changes

Autopilot triggers lane change when:

Traffic ahead slower

Emergency vehicle approaching

Hazard detected

Autopilot sets:

Target lane

Transition duration

Overrides steering input

But uses same math.

14. Unity DOTS Pseudocode
float t = math.saturate(transition.Progress / transition.Duration);
float lambda = t * t * (3f - 2f * t);

float xTarget = math.lerp(xFrom, xTo, lambda);
float xError  = currentX - xTarget;

// Existing magnetism force
float aLat = -omega * omega * xError - 2f * omega * velocity.Lateral;

15. Why This Feels Right

Lane change is a process, not an event

Player never loses control

Magnetism never fights steering

Works identically for:

Player

Autopilot

Traffic

Merges

Forks feel deliberate, not chaotic

16. Recommended Defaults
Parameter	Value
Steering trigger	0.35
Abort threshold	0.7
Base duration	0.6 s
Lane width	3.6 m
Fork magnetism	70%
