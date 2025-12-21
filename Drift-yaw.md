Drift + Yaw + Forward-Velocity Constraint
1. Design Goals (Hard Constraints)

Player can drift and spin

Car may rotate freely (even 360°)

Forward velocity along lane never reaches zero

Car never “backs up” during scoring

Drift weakens lane magnetism but doesn’t kill it

Handbrake is the intent signal, not chaos

2. Coordinate Decomposition (Critical)

We decompose motion into lane-relative space, not world space.

At closest spline point:

Forward unit vector: 
𝐹
F

Right unit vector: 
𝑅
R

Velocity Components

Let world velocity be 
𝑉
V.

𝑣
𝑓
=
𝑉
⋅
𝐹
(forward)
v
f
	​

=V⋅F(forward)
𝑣
𝑙
=
𝑉
⋅
𝑅
(lateral)
v
l
	​

=V⋅R(lateral)

Yaw angle:

𝜓
=
signed angle between vehicle forward and 
𝐹
ψ=signed angle between vehicle forward and F
3. Forward-Velocity Constraint (Core Rule)

We enforce:

𝑣
𝑓
≥
𝑣
𝑚
𝑖
𝑛
v
f
	​

≥v
min
	​


Where:

𝑣
𝑚
𝑖
𝑛
≈
6
–
10
 
𝑚
/
𝑠
v
min
	​

≈6–10m/s

Constraint Enforcement

If:

𝑣
𝑓
<
𝑣
𝑚
𝑖
𝑛
v
f
	​

<v
min
	​


Then:

𝑣
𝑓
←
𝑣
𝑚
𝑖
𝑛
v
f
	​

←v
min
	​


Recompose velocity:

𝑉
←
𝑣
𝑓
𝐹
+
𝑣
𝑙
𝑅
V←v
f
	​

F+v
l
	​

R

This is why spins never stall the run.

4. Yaw Dynamics Model

Yaw is explicitly controlled, not emergent.

State Variables

𝜓
ψ — yaw offset from lane

𝜓
˙
ψ
˙
	​

 — yaw rate

Yaw Equation
𝜓
¨
=
𝜏
𝑠
𝑡
𝑒
𝑒
𝑟
+
𝜏
𝑑
𝑟
𝑖
𝑓
𝑡
−
𝑐
𝜓
𝜓
˙
ψ
¨
	​

=τ
steer
	​

+τ
drift
	​

−c
ψ
	​

ψ
˙
	​


Where:

𝑐
𝜓
c
ψ
	​

 = yaw damping

5. Steering Torque

Let:

𝑠
∈
[
−
1
,
1
]
s∈[−1,1] steering input

𝑣
𝑓
v
f
	​

 forward speed

𝜏
𝑠
𝑡
𝑒
𝑒
𝑟
=
𝑘
𝑠
⋅
𝑠
⋅
𝑣
𝑓
𝑣
𝑟
𝑒
𝑓
τ
steer
	​

=k
s
	​

⋅s⋅
v
ref
	​

v
f
	​

	​


Steering gets more authority at speed

Prevents low-speed pirouettes

6. Drift Torque (Handbrake)

When handbrake is engaged:

𝜏
𝑑
𝑟
𝑖
𝑓
𝑡
=
{
𝑘
𝑑
⋅
sign
(
𝑠
)
⋅
𝑣
𝑓
	
if handbrake


0
	
otherwise
τ
drift
	​

={
k
d
	​

⋅sign(s)⋅
v
f
	​

	​

0
	​

if handbrake
otherwise
	​


Why √speed?

Fast spins at speed

Still controllable

7. Yaw Integration
𝜓
˙
←
𝜓
˙
+
𝜓
¨
Δ
𝑡
ψ
˙
	​

←
ψ
˙
	​

+
ψ
¨
	​

Δt

Clamp:

𝜓
˙
∈
[
−
𝜓
˙
𝑚
𝑎
𝑥
,
𝜓
˙
𝑚
𝑎
𝑥
]
ψ
˙
	​

∈[−
ψ
˙
	​

max
	​

,
ψ
˙
	​

max
	​

]
𝜓
←
𝜓
+
𝜓
˙
Δ
𝑡
ψ←ψ+
ψ
˙
	​

Δt

Allow:

𝜓
∈
(
−
∞
,
+
∞
)
ψ∈(−∞,+∞)

(No hard clamp — full spins allowed.)

8. Drift Slip Angle (Key Feel Component)

Slip angle controls lateral velocity gain during drift.

𝛽
=
𝜓
−
arctan
⁡
(
𝑣
𝑙
𝑣
𝑓
)
β=ψ−arctan(
v
f
	​

v
l
	​

	​

)

During handbrake:

𝑣
𝑙
←
𝑣
𝑙
+
𝑘
𝑠
𝑙
𝑖
𝑝
⋅
sin
⁡
(
𝛽
)
⋅
𝑣
𝑓
⋅
Δ
𝑡
v
l
	​

←v
l
	​

+k
slip
	​

⋅sin(β)⋅v
f
	​

⋅Δt

This creates:

Sideways slide

Drift continuation

Recovery when releasing handbrake

9. Lane Magnetism Interaction (Important)

During drift:

𝑚
𝑑
𝑟
𝑖
𝑓
𝑡
=
0.3
m
drift
	​

=0.3

Total magnetism multiplier becomes:

𝑚
𝑡
𝑜
𝑡
𝑎
𝑙
=
𝑚
𝑙
𝑎
𝑛
𝑒
⋅
𝑚
𝑑
𝑟
𝑖
𝑓
𝑡
m
total
	​

=m
lane
	​

⋅m
drift
	​


Lane pull exists, but is weak — you feel the road without rails.

10. Drift Recovery (Letting Go of Handbrake)

When handbrake is released:

We apply yaw correction torque toward lane:

𝜏
𝑟
𝑒
𝑐
𝑜
𝑣
𝑒
𝑟
=
−
𝑘
𝑟
⋅
𝜓
τ
recover
	​

=−k
r
	​

⋅ψ

Added to yaw equation.

This is why the car naturally straightens.

11. Crash Condition (Spin Too Hard)

If:

∣
𝜓
∣
>
𝜓
𝑐
𝑟
𝑎
𝑠
ℎ
∣ψ∣>ψ
crash
	​


AND forward velocity lost suddenly

AND large obstacle collision

→ crash

Spinning alone is never fatal.

12. Unity DOTS–Style Pseudocode
// Decompose velocity
float vf = math.dot(vel, laneForward);
float vl = math.dot(vel, laneRight);

// Enforce forward constraint
vf = math.max(vf, minForwardSpeed);

// Yaw dynamics
float steerTorque = ks * input.Steer * (vf / refSpeed);
float driftTorque = input.Handbrake ? kd * math.sign(input.Steer) * math.sqrt(vf) : 0f;

float yawAccel = steerTorque + driftTorque - yawDamping * yawRate;

yawRate += yawAccel * dt;
yaw += yawRate * dt;

// Slip angle drift
if (input.Handbrake)
{
    float beta = yaw - math.atan2(vl, vf);
    vl += slipGain * math.sin(beta) * vf * dt;
}

// Recompose velocity
vel = vf * laneForward + vl * laneRight;

13. Tuning Defaults (Starting Point)
Parameter	Value
min forward speed	8 m/s
steering gain	1.2
drift gain	2.5
yaw damping	0.8
max yaw rate	6 rad/s
slip gain	1.1
drift magnetism	0.3
14. Why This System Works

Forward motion never dies

Spins are allowed, not punished

Drift is expressive but bounded

Lane magnetism still whispers

No physics engine fighting you

Consistent at all speeds

This is the exact model used in modern “flow driving” games — simplified, controlled, and tuned for feel.
