Emergency Vehicle Avoidance Math
1. Design Intent

Emergency vehicles should:

Be detected before they are visible

Create directional pressure, not instant danger

Encourage lane clearing, not panic

Punish ignoring them only if stubborn

Work identically for player, traffic, autopilot

2. Detection Space (Behind the Player)

Define a rear influence cone.

Geometry

Let:

Player position 
𝑃
P

Player forward vector 
𝐹
F

Emergency vehicle position 
𝐸
E

Vector to emergency vehicle:

𝐷
=
𝐸
−
𝑃
D=E−P

Projection behind:

𝑑
𝑓
=
𝐷
⋅
𝐹
d
f
	​

=D⋅F

Lateral offset:

𝑑
𝑙
=
𝐷
⋅
𝑅
d
l
	​

=D⋅R
Detection Conditions

Emergency is “pressuring” if:

𝑑
𝑓
<
0
(behind)
d
f
	​

<0(behind)
∣
𝑑
𝑙
∣
<
𝑤
𝑑
𝑒
𝑡
𝑒
𝑐
𝑡
∣d
l
	​

∣<w
detect
	​

∣
𝑑
𝑓
∣
<
𝑑
𝑚
𝑎
𝑥
∣d
f
	​

∣<d
max
	​


Suggested:

𝑤
𝑑
𝑒
𝑡
𝑒
𝑐
𝑡
=
6
–
8
𝑚
w
detect
	​

=6–8m

𝑑
𝑚
𝑎
𝑥
=
120
𝑚
d
max
	​

=120m

3. Urgency Scalar (Key Signal)

Urgency increases smoothly as the emergency vehicle approaches.

𝑢
=
𝑐
𝑙
𝑎
𝑚
𝑝
(
1
−
∣
𝑑
𝑓
∣
𝑑
𝑚
𝑎
𝑥
,
0
,
1
)
u=clamp(1−
d
max
	​

∣d
f
	​

∣
	​

,0,1)

This single scalar drives:

Light intensity

Audio volume

Steering pressure

Scoring penalties

4. Directional Avoidance Force

We apply a lane bias, not a shove.

Desired Direction

If emergency is centered:

Bias toward outer lanes

Let:

Lane index = 
𝑖
i

Emergency lateral sign:

𝑠
𝑒
=
𝑠
𝑖
𝑔
𝑛
(
𝑑
𝑙
)
s
e
	​

=sign(d
l
	​

)

Desired lateral direction:

𝑑
𝑖
𝑟
=
−
𝑠
𝑒
dir=−s
e
	​

5. Avoidance Offset Target

Let:

Lane width = 
𝑤
w

Avoidance strength 
𝑘
𝑎
k
a
	​


𝑥
𝑎
𝑣
𝑜
𝑖
𝑑
=
𝑑
𝑖
𝑟
⋅
𝑘
𝑎
⋅
𝑢
⋅
𝑤
x
avoid
	​

=dir⋅k
a
	​

⋅u⋅w

This is added to the lane magnetism target.

6. Integration With Lane Magnetism

Recall magnetism target:

𝑥
𝑡
𝑎
𝑟
𝑔
𝑒
𝑡
x
target
	​


Emergency-adjusted target:

𝑥
𝑡
𝑎
𝑟
𝑔
𝑒
𝑡
′
=
𝑥
𝑡
𝑎
𝑟
𝑔
𝑒
𝑡
+
𝑥
𝑎
𝑣
𝑜
𝑖
𝑑
x
target
′
	​

=x
target
	​

+x
avoid
	​


Then reuse exact same spring-damper:

𝑎
𝑙
𝑎
𝑡
=
−
𝜔
2
(
𝑥
−
𝑥
𝑡
𝑎
𝑟
𝑔
𝑒
𝑡
′
)
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
(x−x
target
′
	​

)−2ω
x
˙

No new forces, no chaos.

7. Player Override Logic

Player steering reduces avoidance force, but does not invert it.

Let:

𝑚
𝑝
𝑙
𝑎
𝑦
𝑒
𝑟
=
𝑐
𝑙
𝑎
𝑚
𝑝
(
1
−
∣
𝑠
∣
,
0.3
,
1
)
m
player
	​

=clamp(1−∣s∣,0.3,1)

Final avoidance:

𝑥
𝑎
𝑣
𝑜
𝑖
𝑑
←
𝑥
𝑎
𝑣
𝑜
𝑖
𝑑
⋅
𝑚
𝑝
𝑙
𝑎
𝑦
𝑒
𝑟
x
avoid
	​

←x
avoid
	​

⋅m
player
	​


This ensures:

Player can resist

But feels pressure

8. Escalation Logic (Ignored Too Long)

If:

𝑢
>
0.6
u>0.6

AND time > 
𝑡
𝑤
𝑎
𝑟
𝑛
≈
1.5
𝑠
t
warn
	​

≈1.5s

AND player has not changed lanes

Then:

Emergency vehicle initiates aggressive overtake

Flash rate increases

Audio spikes

Optional Minor Penalty

Score multiplier decays:

𝑀
←
𝑀
⋅
(
1
−
0.1
⋅
𝑢
⋅
Δ
𝑡
)
M←M⋅(1−0.1⋅u⋅Δt)

No instant fail.

9. Collision Rule (Fairness)

If collision occurs:

Only crash if:

Player is braking

OR speed < threshold

OR vehicle is already damaged

Otherwise:

Heavy shake

Forced lane displacement

Score ends

Emergency vehicles should feel invincible, not lethal.

10. Autopilot Behavior

Autopilot treats emergency avoidance as highest priority.

Autopilot target lane:

𝑖
𝑡
𝑎
𝑟
𝑔
𝑒
𝑡
=
farthest valid lane from emergency
i
target
	​

=farthest valid lane from emergency

Triggers lane change using same lane change math.

11. Traffic Vehicle Reaction (Emergent Behavior)

Traffic vehicles:

Reduce speed slightly

Bias away using same 
𝑥
𝑎
𝑣
𝑜
𝑖
𝑑
x
avoid
	​


Creates natural “clearing wave”

This makes the world feel alive with no extra AI logic.

12. Off-Screen Light Signaling Math (Bonus)

Emergency lights become visible before geometry.

Light intensity boost:

𝐼
=
𝐼
0
(
1
+
2
𝑢
)
I=I
0
	​

(1+2u)

Strobe rate:

𝑓
=
𝑓
0
+
4
𝑢
f=f
0
	​

+4u

Light radius:

𝑟
=
𝑟
0
(
1
+
𝑢
)
r=r
0
	​

(1+u)

This ensures players react instinctively.

13. Unity DOTS–Style Pseudocode
if (df < 0 && math.abs(dl) < detectWidth && math.abs(df) < maxDist)
{
    float u = 1f - math.abs(df) / maxDist;

    float dir = -math.sign(dl);
    float xAvoid = dir * avoidStrength * u * laneWidth;

    float playerScale = math.clamp(1f - math.abs(input.Steer), 0.3f, 1f);
    xAvoid *= playerScale;

    xTarget += xAvoid;
}

14. Tuning Defaults
Parameter	Value
Detection distance	120 m
Avoid strength	0.8
Warning time	1.5 s
Player min override	0.3
Light intensity boost	×3
15. Why This Feels Right

Pressure builds gradually

Lights tell you before danger

Lane magnetism does the work

Player never loses control

Ignoring feels bad, not unfair

Autopilot looks smart

This completes the core freeway interaction loop.
