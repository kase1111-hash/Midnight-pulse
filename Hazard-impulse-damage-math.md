Hazard Impulse & Damage Math
1. Design Goals

Small hazards = momentum + damage, not instant death

Big hazards = crash if hit badly

Damage degrades handling before ending run

Speed matters more than mass

Forward motion is preserved unless crash is explicit

Damage math must work with wireframe MVP and future soft-body

2. Hazard Classification

Each hazard has two orthogonal properties:

Hazard Component
Severity        ∈ [0, 1]
MassFactor      ∈ [0, 1]
DamageType      ∈ { Cosmetic, Mechanical, Lethal }


Examples:

Hazard	Severity	Mass	Type
Loose tire	0.2	0.1	Cosmetic
Road debris	0.4	0.3	Mechanical
Construction cone	0.3	0.2	Cosmetic
Barrier	0.9	0.9	Lethal
Crashed car	1.0	1.0	Lethal
3. Collision Frame Setup

At collision moment:

Vehicle velocity: 
𝑉
V

Hazard normal: 
𝑁
N (unit)

Impact speed:

𝑣
𝑖
𝑚
𝑝
𝑎
𝑐
𝑡
=
max
⁡
(
0
,
−
𝑉
⋅
𝑁
)
v
impact
	​

=max(0,−V⋅N)

We ignore glancing contacts automatically.

4. Impulse Magnitude (Key Formula)

Impulse is speed-weighted, not mass-simulated:

𝐽
=
𝑘
𝑖
⋅
𝑣
𝑖
𝑚
𝑝
𝑎
𝑐
𝑡
⋅
(
0.5
+
𝑆
𝑒
𝑣
𝑒
𝑟
𝑖
𝑡
𝑦
)
J=k
i
	​

⋅v
impact
	​

⋅(0.5+Severity)

Where:

𝑘
𝑖
k
i
	​

 = global impulse scale

This ensures:

High speed = meaningful hit

Small junk never stops you cold

5. Impulse Direction Decomposition

Impulse direction:

𝐼
=
𝐽
⋅
𝑁
I=J⋅N

Decompose into lane frame:

𝐼
𝑓
=
𝐼
⋅
𝐹
I
f
	​

=I⋅F
𝐼
𝑙
=
𝐼
⋅
𝑅
I
l
	​

=I⋅R
6. Velocity Response (Non-Fatal)
Lateral Response (Always Applies)
𝑣
𝑙
←
𝑣
𝑙
+
𝐼
𝑙
𝑚
𝑣
𝑖
𝑟
𝑡
𝑢
𝑎
𝑙
v
l
	​

←v
l
	​

+
m
virtual
	​

I
l
	​

	​


Creates:

Kick sideways

Lane destabilization

Recovery via magnetism

Forward Response (Clamped)
𝑣
𝑓
←
𝑣
𝑓
−
∣
𝐼
𝑓
∣
𝑚
𝑣
𝑖
𝑟
𝑡
𝑢
𝑎
𝑙
v
f
	​

←v
f
	​

−
m
virtual
	​

∣I
f
	​

∣
	​


Then enforce:

𝑣
𝑓
←
max
⁡
(
𝑣
𝑓
,
𝑣
𝑚
𝑖
𝑛
)
v
f
	​

←max(v
f
	​

,v
min
	​

)

This is what keeps runs alive.

7. Yaw Kick (Visual & Control Feedback)

Yaw impulse adds drama without killing control:

Δ
𝜓
˙
=
𝑘
𝑦
⋅
𝐼
𝑙
𝑣
𝑓
+
𝜖
Δ
ψ
˙
	​

=k
y
	​

⋅
v
f
	​

+ϵ
I
l
	​

	​


Applied instantly.

8. Damage Accumulation Model

Damage is energy-based, not collision count.

Damage Energy
𝐸
𝑑
=
𝑘
𝑑
⋅
𝑣
𝑖
𝑚
𝑝
𝑎
𝑐
𝑡
2
⋅
𝑆
𝑒
𝑣
𝑒
𝑟
𝑖
𝑡
𝑦
E
d
	​

=k
d
	​

⋅v
impact
2
	​

⋅Severity
9. Damage Direction Distribution

Let contact normal projected into vehicle space:

Front: 
𝑤
𝑓
w
f
	​


Rear: 
𝑤
𝑟
w
r
	​


Left: 
𝑤
𝑙
w
l
	​


Right: 
𝑤
𝑟
w
r
	​


Weights sum to 1.

Apply:

Damage.Front += E_d * w_front
Damage.Left  += E_d * w_left
...
Damage.Total += E_d

10. Damage → Handling Degradation

Handling penalties are continuous, not binary.

Steering Response
𝑘
𝑠
𝑡
𝑒
𝑒
𝑟
←
𝑘
𝑠
𝑡
𝑒
𝑒
𝑟
⋅
(
1
−
0.4
⋅
𝐷
𝑓
𝑟
𝑜
𝑛
𝑡
)
k
steer
	​

←k
steer
	​

⋅(1−0.4⋅D
front
	​

)
Magnetism Reduction
𝜔
←
𝜔
⋅
(
1
−
0.5
⋅
𝐷
𝑠
𝑖
𝑑
𝑒
)
ω←ω⋅(1−0.5⋅D
side
	​

)
Drift Stability Loss
𝑘
𝑠
𝑙
𝑖
𝑝
←
𝑘
𝑠
𝑙
𝑖
𝑝
⋅
(
1
+
0.6
⋅
𝐷
𝑟
𝑒
𝑎
𝑟
)
k
slip
	​

←k
slip
	​

⋅(1+0.6⋅D
rear
	​

)

The car becomes:

Sloppier

Harder to recover

But still driveable

11. Crash Determination (Explicit)

A crash occurs only if one of the following is true:

A. Lethal Hazard + Speed
𝑆
𝑒
𝑣
𝑒
𝑟
𝑖
𝑡
𝑦
>
0.8
  
∧
  
𝑣
𝑖
𝑚
𝑝
𝑎
𝑐
𝑡
>
𝑣
𝑐
𝑟
𝑎
𝑠
ℎ
Severity>0.8∧v
impact
	​

>v
crash
	​

B. Structural Damage Exceeded
𝐷
𝑎
𝑚
𝑎
𝑔
𝑒
.
𝑇
𝑜
𝑡
𝑎
𝑙
>
𝐷
𝑚
𝑎
𝑥
Damage.Total>D
max
	​

C. Compound Failure
(
∣
𝜓
∣
>
𝜓
𝑓
𝑎
𝑖
𝑙
)
∧
(
𝑣
𝑓
≈
𝑣
𝑚
𝑖
𝑛
)
∧
(
𝐷
𝑎
𝑚
𝑎
𝑔
𝑒
.
𝑇
𝑜
𝑡
𝑎
𝑙
>
0.6
𝐷
𝑚
𝑎
𝑥
)
(∣ψ∣>ψ
fail
	​

)∧(v
f
	​

≈v
min
	​

)∧(Damage.Total>0.6D
max
	​

)

Spins alone never crash you.

12. Camera & Feedback Coupling

Camera shake magnitude:

𝑆
=
𝑐
𝑙
𝑎
𝑚
𝑝
(
𝐸
𝑑
𝐸
𝑟
𝑒
𝑓
,
0
,
1
)
S=clamp(
E
ref
	​

E
d
	​

	​

,0,1)

Screen flash intensity:

𝐹
=
0.6
⋅
𝑆
𝑒
𝑣
𝑒
𝑟
𝑖
𝑡
𝑦
F=0.6⋅Severity
13. Unity DOTS–Style Pseudocode
float vImpact = math.max(0f, -math.dot(velocity, hitNormal));

float J = impulseScale * vImpact * (0.5f + hazard.Severity);
float3 impulse = J * hitNormal;

// Decompose
float If = math.dot(impulse, laneForward);
float Il = math.dot(impulse, laneRight);

// Apply velocity
vf = math.max(vf - math.abs(If) / virtualMass, minForwardSpeed);
vl += Il / virtualMass;

// Yaw kick
yawRate += yawKick * Il / (vf + 0.1f);

// Damage
float Ed = damageScale * vImpact * vImpact * hazard.Severity;
damage.Total += Ed;

14. Tuning Defaults
Parameter	Value
Impulse scale	1.2
Damage scale	0.04
Virtual mass	1200
Crash speed	25 m/s
Max damage	100
Min forward speed	8 m/s
15. Why This Works

Junk feels dangerous but fair

Big hits matter instantly

Damage tells a story over time

Handling degrades before failure

Forward motion is sacred

BeamNG-level detail slots in cleanly later

This completes the core physical interaction loop of your game
