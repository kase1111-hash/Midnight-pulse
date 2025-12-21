Traffic AI Lane-Change Decision Math
1. Core Idea

Each traffic vehicle continuously evaluates lane desirability using weighted scalar fields.

The car doesn’t “decide” — it slides downhill in a desirability landscape.

2. Lane Candidates

For current lane index 
𝑖
i, candidates are:

𝐿
=
{
𝑖
−
1
,
𝑖
,
𝑖
+
1
}
L={i−1,i,i+1}

Filtered by:

Lane existence

Merge legality

Barrier presence

3. Lane Score Function

Each lane gets a score:

𝑆
𝑖
=
𝑤
𝑠
𝑆
𝑠
𝑝
𝑒
𝑒
𝑑
+
𝑤
𝑑
𝑆
𝑑
𝑒
𝑛
𝑠
𝑖
𝑡
𝑦
+
𝑤
𝑒
𝑆
𝑒
𝑚
𝑒
𝑟
𝑔
𝑒
𝑛
𝑐
𝑦
+
𝑤
ℎ
𝑆
ℎ
𝑎
𝑧
𝑎
𝑟
𝑑
+
𝑤
𝑝
𝑆
𝑝
𝑙
𝑎
𝑦
𝑒
𝑟
+
𝑤
𝑚
𝑆
𝑚
𝑒
𝑟
𝑔
𝑒
S
i
	​

=w
s
	​

S
speed
	​

+w
d
	​

S
density
	​

+w
e
	​

S
emergency
	​

+w
h
	​

S
hazard
	​

+w
p
	​

S
player
	​

+w
m
	​

S
merge
	​


All terms normalized to 
[
0
,
1
]
[0,1].

4. Speed Advantage Term

Traffic prefers lanes that allow its target speed.

Let:

𝑣
𝑖
v
i
	​

 = mean speed of vehicles ahead in lane

𝑣
𝑡
v
t
	​

 = AI’s desired speed

𝑆
𝑠
𝑝
𝑒
𝑒
𝑑
=
𝑐
𝑙
𝑎
𝑚
𝑝
(
𝑣
𝑖
𝑣
𝑡
,
0
,
1
)
S
speed
	​

=clamp(
v
t
	​

v
i
	​

	​

,0,1)
5. Density Term

Cars avoid crowded lanes.

Let:

𝑛
𝑖
n
i
	​

 = vehicle count in lookahead window

𝑆
𝑑
𝑒
𝑛
𝑠
𝑖
𝑡
𝑦
=
𝑒
−
𝑘
𝑑
𝑛
𝑖
S
density
	​

=e
−k
d
	​

n
i
	​


Creates natural lane spreading.

6. Emergency Vehicle Pressure

Same urgency scalar 
𝑢
u used everywhere.

If emergency in lane 
𝑖
i:

𝑆
𝑒
𝑚
𝑒
𝑟
𝑔
𝑒
𝑛
𝑐
𝑦
=
1
−
𝑢
S
emergency
	​

=1−u

Otherwise:

𝑆
𝑒
𝑚
𝑒
𝑟
𝑔
𝑒
𝑛
𝑐
𝑦
=
1
S
emergency
	​

=1
7. Hazard Avoidance Term

For hazards detected ahead:

Let:

𝑑
ℎ
d
h
	​

 = distance to nearest hazard in lane

𝑑
𝑠
𝑎
𝑓
𝑒
d
safe
	​

 = safety distance

𝑆
ℎ
𝑎
𝑧
𝑎
𝑟
𝑑
=
𝑐
𝑙
𝑎
𝑚
𝑝
(
𝑑
ℎ
𝑑
𝑠
𝑎
𝑓
𝑒
,
0
,
1
)
S
hazard
	​

=clamp(
d
safe
	​

d
h
	​

	​

,0,1)

Zero if blocked.

8. Player Proximity Bias

Traffic subtly avoids crowding the player.

Let:

𝑑
𝑝
d
p
	​

 = lateral distance to player

𝑆
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
𝑑
𝑝
𝑤
𝑙
𝑎
𝑛
𝑒
,
0.3
,
1
)
S
player
	​

=clamp(
w
lane
	​

d
p
	​

	​

,0.3,1)

This prevents unfair pinches.

9. Merge Logic Term

At merges or forks:

Let:

𝑑
𝑚
d
m
	​

 = distance to merge

𝑙
𝑣
𝑎
𝑙
𝑖
𝑑
∈
{
0
,
1
}
l
valid
	​

∈{0,1}

𝑆
𝑚
𝑒
𝑟
𝑔
𝑒
=
{
𝑙
𝑣
𝑎
𝑙
𝑖
𝑑
⋅
(
1
−
𝑑
𝑚
𝑑
𝑚
𝑒
𝑟
𝑔
𝑒
)
,
	
𝑑
𝑚
<
𝑑
𝑚
𝑒
𝑟
𝑔
𝑒


1
,
	
otherwise
S
merge
	​

={
l
valid
	​

⋅(1−
d
merge
	​

d
m
	​

	​

),
1,
	​

d
m
	​

<d
merge
	​

otherwise
	​


Creates early, smooth merges.

10. Decision Hysteresis (No Jitter)

Lane changes only if improvement exceeds threshold:

Δ
𝑆
=
𝑆
𝑏
𝑒
𝑠
𝑡
−
𝑆
𝑐
𝑢
𝑟
𝑟
𝑒
𝑛
𝑡
ΔS=S
best
	​

−S
current
	​


Change allowed only if:

Δ
𝑆
>
𝜃
ΔS>θ

Suggested:

𝜃
=
0.15
θ=0.15

11. Commitment Lock

Once changing lanes:

Lock decision for 
𝑡
𝑙
𝑜
𝑐
𝑘
t
lock
	​


Ignore re-scoring

This prevents mid-change dithering.

12. Convert Decision → Motion

If target lane 
𝑗
j selected:

𝑥
𝑡
𝑎
𝑟
𝑔
𝑒
𝑡
=
(
𝑗
−
𝑖
)
⋅
𝑤
𝑙
𝑎
𝑛
𝑒
x
target
	​

=(j−i)⋅w
lane
	​


Then apply existing lane change math:

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
	​

)−2ω
x
˙
13. Yielding Behavior (Emergency / Player)

If emergency urgency 
𝑢
>
0.7
u>0.7:

Reduce forward speed:

𝑣
𝑓
←
𝑣
𝑓
(
1
−
0.3
𝑢
)
v
f
	​

←v
f
	​

(1−0.3u)

Prefer outermost lane

14. Unity DOTS–Style Pseudocode
foreach (lane in candidates)
{
    float S = 0f;
    S += ws * speedScore(lane);
    S += wd * densityScore(lane);
    S += we * emergencyScore(lane);
    S += wh * hazardScore(lane);
    S += wp * playerScore(lane);
    S += wm * mergeScore(lane);

    scores[lane] = S;
}

int bestLane = argmax(scores);

if (scores[bestLane] - scores[currentLane] > threshold)
{
    targetLane = bestLane;
    lockTimer = lockTime;
}

15. Parameter Defaults
Weight	Value
Speed	0.35
Density	0.25
Emergency	0.4
Hazard	0.3
Player	0.15
Merge	0.3
Threshold	0.15
Lock time	1.2 s
16. Emergent Behavior You Get “For Free”

Natural lane waves

Emergency clearing corridors

Traffic bunching before hazards

Player gets breathing room

No AI cheating

Fully deterministic replay

17. Why This Fits Your Game

Continuous, not scripted

Zero branching AI logic

ECS-friendly scalar math

Works at 200+ km/h

Scales to thousands of vehicles
