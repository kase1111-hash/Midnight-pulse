Lane Magnetism — Exact Math & Control Model
1. Problem Definition

We want:

Vehicle naturally centers in lane

Player can smoothly override magnetism

Autopilot strengthens magnetism

No snapping

No oscillation

Stable at very high speed

Drift (handbrake) weakens magnetism

This is essentially a critically damped spring toward a spline centerline, modulated by player input and state.

2. Coordinate Setup (Per Frame)

Assume:

Lane defined by a spline 
𝑆
(
𝑡
)
S(t)

Vehicle position 
𝑃
P

Closest point on spline 
𝐶
C

Lane right vector 
𝑅
R (normalized)

Lane forward vector 
𝐹
F

Step 1: Find Closest Point on Lane

For spline segment 
𝑖
i:

𝑡
∗
=
arg
⁡
min
⁡
𝑡
∥
𝑆
𝑖
(
𝑡
)
−
𝑃
∥
2
t
∗
=arg
t
min
	​

∥S
i
	​

(t)−P∥
2

Result:

𝐶
=
𝑆
(
𝑡
∗
)
C=S(t
∗
)
Step 2: Lateral Offset

Project vehicle displacement onto lane right vector:

Δ
=
𝑃
−
𝐶
Δ=P−C
𝑥
=
Δ
⋅
𝑅
x=Δ⋅R

Where:

𝑥
>
0
x>0 = right of center

𝑥
<
0
x<0 = left of center

3. Magnetic Force Model

We apply lateral acceleration, not position snapping.

Core Spring-Damper Equation
𝑎
𝑚
𝑎
𝑔
=
−
𝑘
⋅
𝑥
−
𝑐
⋅
𝑥
˙
a
mag
	​

=−k⋅x−c⋅
x
˙

Where:

𝑘
k = stiffness

𝑐
c = damping

𝑥
˙
x
˙
 = lateral velocity

4. Critical Damping (No Oscillation)

To avoid oscillation:

𝑐
=
2
𝑘
c=2
k
	​


We’ll define natural frequency instead:

𝑘
=
𝜔
2
k=ω
2
𝑐
=
2
𝜔
c=2ω

Where:

𝜔
ω ≈ 6–12 (game feel)

5. Player Input Modulation

Let:

𝑠
∈
[
−
1
,
1
]
s∈[−1,1] = steering input magnitude

𝑚
𝑖
𝑛
𝑝
𝑢
𝑡
m
input
	​

 = magnetism scaling

𝑚
𝑖
𝑛
𝑝
𝑢
𝑡
=
1
−
∣
𝑠
∣
m
input
	​

=1−∣s∣

So:

No steering → full magnetism

Full steering → zero magnetism

6. Autopilot Scaling
𝑚
𝑎
𝑢
𝑡
𝑜
=
{
1.5
	
if autopilot enabled


1.0
	
otherwise
m
auto
	​

={
1.5
1.0
	​

if autopilot enabled
otherwise
	​

7. Speed Compensation (Critical)

At high speed, magnetism must increase slightly, or you drift too far.

Let:

𝑣
v = forward speed

𝑣
𝑟
𝑒
𝑓
v
ref
	​

 = reference speed

𝑚
𝑠
𝑝
𝑒
𝑒
𝑑
=
𝑣
𝑣
𝑟
𝑒
𝑓
m
speed
	​

=
v
ref
	​

v
	​

	​


Clamp:

𝑚
𝑠
𝑝
𝑒
𝑒
𝑑
∈
[
0.75
,
1.25
]
m
speed
	​

∈[0.75,1.25]
8. Handbrake & Drift Scaling
𝑚
ℎ
𝑎
𝑛
𝑑
𝑏
𝑟
𝑎
𝑘
𝑒
=
{
0.25
	
if handbrake engaged


1.0
	
otherwise
m
handbrake
	​

={
0.25
1.0
	​

if handbrake engaged
otherwise
	​

9. Final Magnetism Strength
𝑚
=
𝑚
𝑖
𝑛
𝑝
𝑢
𝑡
⋅
𝑚
𝑎
𝑢
𝑡
𝑜
⋅
𝑚
𝑠
𝑝
𝑒
𝑒
𝑑
⋅
𝑚
ℎ
𝑎
𝑛
𝑑
𝑏
𝑟
𝑎
𝑘
𝑒
m=m
input
	​

⋅m
auto
	​

⋅m
speed
	​

⋅m
handbrake
	​

10. Final Lateral Acceleration
𝑎
𝑙
𝑎
𝑡
=
𝑚
⋅
(
−
𝜔
2
𝑥
−
2
𝜔
𝑥
˙
)
a
lat
	​

=m⋅(−ω
2
x−2ω
x
˙
)
11. Integrate Lateral Motion
Update Lateral Velocity
𝑥
˙
𝑛
𝑒
𝑤
=
𝑥
˙
+
𝑎
𝑙
𝑎
𝑡
⋅
Δ
𝑡
x
˙
new
	​

=
x
˙
+a
lat
	​

⋅Δt
Clamp Max Lateral Speed
𝑥
˙
𝑛
𝑒
𝑤
=
𝑐
𝑙
𝑎
𝑚
𝑝
(
𝑥
˙
𝑛
𝑒
𝑤
,
−
𝑣
𝑙
𝑎
𝑡
,
𝑚
𝑎
𝑥
,
𝑣
𝑙
𝑎
𝑡
,
𝑚
𝑎
𝑥
)
x
˙
new
	​

=clamp(
x
˙
new
	​

,−v
lat,max
	​

,v
lat,max
	​

)
Update World Position
𝑃
𝑛
𝑒
𝑤
=
𝑃
+
𝑅
⋅
𝑥
˙
𝑛
𝑒
𝑤
⋅
Δ
𝑡
P
new
	​

=P+R⋅
x
˙
new
	​

⋅Δt

Forward movement handled separately by vehicle system.

12. Lane Width Soft Constraint

We do not hard clamp inside lane.
Instead apply a nonlinear edge force near boundaries.

Let:

Lane half-width = 
𝑤
w

Soft zone = 
𝑤
𝑠
𝑜
𝑓
𝑡
=
0.85
𝑤
w
soft
	​

=0.85w

If 
∣
𝑥
∣
>
𝑤
𝑠
𝑜
𝑓
𝑡
∣x∣>w
soft
	​

:

𝑥
𝑒
𝑑
𝑔
𝑒
=
∣
𝑥
∣
−
𝑤
𝑠
𝑜
𝑓
𝑡
x
edge
	​

=∣x∣−w
soft
	​

𝑎
𝑒
𝑑
𝑔
𝑒
=
−
𝑠
𝑖
𝑔
𝑛
(
𝑥
)
⋅
𝑘
𝑒
𝑑
𝑔
𝑒
⋅
𝑥
𝑒
𝑑
𝑔
𝑒
2
a
edge
	​

=−sign(x)⋅k
edge
	​

⋅x
edge
2
	​


Add:

𝑎
𝑙
𝑎
𝑡
+
=
𝑎
𝑒
𝑑
𝑔
𝑒
a
lat
	​

+=a
edge
	​


This creates a rubber-band feel, not a wall.

13. Fork Transition Handling

When changing lanes or entering forks:

Fade magnetism over time 
𝑇
T

𝑚
𝑓
𝑜
𝑟
𝑘
(
𝑡
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
0
,
𝑡
/
𝑇
)
m
fork
	​

(t)=smoothstep(1,0,t/T)

Prevents sudden pulls during decisions.

14. Unity DOTS Pseudocode
float x = math.dot(pos - closestPoint, laneRight);
float dx = velocity.Lateral;

float mInput = 1f - math.abs(input.Steer);
float mAuto  = autopilot.Enabled ? 1.5f : 1f;
float mSpeed = math.clamp(math.sqrt(speed / refSpeed), 0.75f, 1.25f);
float mBrake = input.Handbrake ? 0.25f : 1f;

float m = mInput * mAuto * mSpeed * mBrake;

float omega = lane.MagnetStrength;
float aLat = m * (-omega * omega * x - 2f * omega * dx);

velocity.Lateral += aLat * deltaTime;
velocity.Lateral = math.clamp(velocity.Lateral, -maxLatSpeed, maxLatSpeed);

pos += laneRight * velocity.Lateral * deltaTime;

15. Why This Feels Right

Critically damped → no wobble

Steering always wins

Autopilot feels “locked in”

Drift naturally breaks magnetism

High-speed stability without rails

Forks feel intentional, not forced

16. Tuning Defaults (Starting Point)
Parameter	Value
ω (omega)	8.0
v_ref	40 m/s
max lateral speed	6 m/s
edge stiffness	20
soft zone	85% lane
