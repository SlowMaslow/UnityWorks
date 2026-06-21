# Graph Report - .  (2026-06-21)

## Corpus Check
- Corpus is ~22,053 words - fits in a single context window. You may not need a graph.

## Summary
- 714 nodes · 937 edges · 57 communities (50 shown, 7 thin omitted)
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS · INFERRED: 4 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- [[_COMMUNITY_Shared Unity Primitives|Shared Unity Primitives]]
- [[_COMMUNITY_Tutorial Conditions|Tutorial Conditions]]
- [[_COMMUNITY_Climb Leg Visuals|Climb Leg Visuals]]
- [[_COMMUNITY_Climb LineLeg Render|Climb Line/Leg Render]]
- [[_COMMUNITY_VFX Manager|VFX Manager]]
- [[_COMMUNITY_UI Layout|UI Layout]]
- [[_COMMUNITY_Tutorial Actions|Tutorial Actions]]
- [[_COMMUNITY_Skins & Tutorial Steps|Skins & Tutorial Steps]]
- [[_COMMUNITY_UI Controller|UI Controller]]
- [[_COMMUNITY_Level Manager|Level Manager]]
- [[_COMMUNITY_Skin Shop|Skin Shop]]
- [[_COMMUNITY_Platform Collision|Platform Collision]]
- [[_COMMUNITY_Scene Controller|Scene Controller]]
- [[_COMMUNITY_Sound Manager|Sound Manager]]
- [[_COMMUNITY_Win Screen|Win Screen]]
- [[_COMMUNITY_Analytics Manager|Analytics Manager]]
- [[_COMMUNITY_Win Countdown UI|Win Countdown UI]]
- [[_COMMUNITY_Ads Manager|Ads Manager]]
- [[_COMMUNITY_Camera Controller|Camera Controller]]
- [[_COMMUNITY_MonoBehaviour Hub (misc)|MonoBehaviour Hub (misc)]]
- [[_COMMUNITY_Platform Block (3D)|Platform Block (3D)]]
- [[_COMMUNITY_Community 21|Community 21]]
- [[_COMMUNITY_Screen Fader|Screen Fader]]
- [[_COMMUNITY_Tutorial Target|Tutorial Target]]
- [[_COMMUNITY_Parallax Background|Parallax Background]]
- [[_COMMUNITY_Stub Ads Service|Stub Ads Service]]
- [[_COMMUNITY_Stub Analytics Service|Stub Analytics Service]]
- [[_COMMUNITY_Leg Swing IK|Leg Swing IK]]
- [[_COMMUNITY_Two-Bone Arm IK|Two-Bone Arm IK]]
- [[_COMMUNITY_Community 29|Community 29]]
- [[_COMMUNITY_Community 30|Community 30]]
- [[_COMMUNITY_IAP Manager|IAP Manager]]
- [[_COMMUNITY_Skin Applier|Skin Applier]]
- [[_COMMUNITY_Tutorial Input Gate|Tutorial Input Gate]]
- [[_COMMUNITY_Tutorial Step Highlight|Tutorial Step Highlight]]
- [[_COMMUNITY_Platform Skin (2D sprite)|Platform Skin (2D sprite)]]
- [[_COMMUNITY_Platform Top|Platform Top]]
- [[_COMMUNITY_Star HUD|Star HUD]]
- [[_COMMUNITY_Tutorial Checkpoint|Tutorial Checkpoint]]
- [[_COMMUNITY_Community 39|Community 39]]
- [[_COMMUNITY_Win Trigger|Win Trigger]]
- [[_COMMUNITY_Body Stabilizer|Body Stabilizer]]
- [[_COMMUNITY_Fail Collider|Fail Collider]]
- [[_COMMUNITY_Music Player|Music Player]]
- [[_COMMUNITY_Stub IAP Service|Stub IAP Service]]
- [[_COMMUNITY_Win Script|Win Script]]
- [[_COMMUNITY_Community 46|Community 46]]
- [[_COMMUNITY_Skin Card View|Skin Card View]]
- [[_COMMUNITY_Tutorial Events|Tutorial Events]]
- [[_COMMUNITY_Community 49|Community 49]]
- [[_COMMUNITY_Coin Collision|Coin Collision]]
- [[_COMMUNITY_Level Loader|Level Loader]]
- [[_COMMUNITY_Star Collision|Star Collision]]
- [[_COMMUNITY_Community 53|Community 53]]
- [[_COMMUNITY_Subclass Selector Attr|Subclass Selector Attr]]

## God Nodes (most connected - your core abstractions)
1. `MonoBehaviour` - 42 edges
2. `TutorialPlayer` - 40 edges
3. `ClimbController` - 34 edges
4. `ClimbBodyPrototype` - 30 edges
5. `LevelSelectController` - 20 edges
6. `VFXManager` - 17 edges
7. `LevelManager` - 15 edges
8. `SkinShopController` - 15 edges
9. `PlatformCollisionLogic` - 14 edges
10. `SceneController` - 14 edges

## Surprising Connections (you probably didn't know these)
- `BodyStabilizer` --inherits--> `MonoBehaviour`  [EXTRACTED]
  Assets/Scripts/BodyStabilizer.cs → Assets/Scripts/Services/StubAdsService.cs
- `CameraController` --inherits--> `MonoBehaviour`  [EXTRACTED]
  Assets/Scripts/CameraController.cs → Assets/Scripts/Services/StubAdsService.cs
- `ClimbController` --inherits--> `MonoBehaviour`  [EXTRACTED]
  Assets/Scripts/Climb/ClimbController.cs → Assets/Scripts/Services/StubAdsService.cs
- `LegSwing` --inherits--> `MonoBehaviour`  [EXTRACTED]
  Assets/Scripts/Climb/LegSwing.cs → Assets/Scripts/Services/StubAdsService.cs
- `TwoBoneArmIK` --inherits--> `MonoBehaviour`  [EXTRACTED]
  Assets/Scripts/Climb/TwoBoneArmIK.cs → Assets/Scripts/Services/StubAdsService.cs

## Import Cycles
- None detected.

## Communities (57 total, 7 thin omitted)

### Community 0 - "Shared Unity Primitives"
Cohesion: 0.07
Nodes (25): bool, Camera, Color, float, Font, GameObject, IEnumerator, int (+17 more)

### Community 1 - "Tutorial Conditions"
Cohesion: 0.05
Nodes (17): string, GameState, float, ClimbUp.Tutorial, CompoundTutorialCondition, ClimbUp.Tutorial, EventTutorialCondition, ClimbUp.Tutorial (+9 more)

### Community 2 - "Climb Leg Visuals"
Cohesion: 0.11
Nodes (15): bool, Camera, Color, float, IEnumerator, int, LegSwing, LineRenderer (+7 more)

### Community 3 - "Climb Line/Leg Render"
Cohesion: 0.11
Nodes (16): bool, Camera, Color, float, GameObject, int, LegSwing, LineRenderer (+8 more)

### Community 4 - "VFX Manager"
Cohesion: 0.17
Nodes (9): Color, IEnumerator, Material, RectTransform, Transform, Vector3, ParticleSystem, VFXManager (+1 more)

### Community 5 - "UI Layout"
Cohesion: 0.16
Nodes (10): Color, Font, GameObject, IEnumerator, int, RectTransform, Transform, Vector2 (+2 more)

### Community 6 - "Tutorial Actions"
Cohesion: 0.10
Nodes (12): BlockInputAction, ClimbUp.Tutorial, CameraFocusAction, ClimbUp.Tutorial, ClimbUp.Tutorial, DelayAction, bool, float (+4 more)

### Community 7 - "Skins & Tutorial Steps"
Cohesion: 0.12
Nodes (13): SkinDefinition, bool, GameObject, int, Sprite, string, string, TutorialStep (+5 more)

### Community 8 - "UI Controller"
Cohesion: 0.17
Nodes (6): float, GameState, IEnumerator, LevelResult, Text, UIController

### Community 9 - "Level Manager"
Cohesion: 0.16
Nodes (5): float, GameObject, int, Transform, LevelManager

### Community 10 - "Skin Shop"
Cohesion: 0.19
Nodes (8): bool, GameObject, List, SkinDefinition, Text, Transform, SkinCardView, SkinShopController

### Community 11 - "Platform Collision"
Cohesion: 0.17
Nodes (6): Bounds, Collider, HashSet, List, Rigidbody, PlatformCollisionLogic

### Community 12 - "Scene Controller"
Cohesion: 0.23
Nodes (3): Action, int, SceneController

### Community 13 - "Sound Manager"
Cohesion: 0.18
Nodes (5): AudioClip, AudioSource, float, LevelResult, SoundManager

### Community 14 - "Win Screen"
Cohesion: 0.16
Nodes (8): Button, Color, IEnumerator, LevelResult, RectTransform, Text, Vector2, WinScreenController

### Community 15 - "Analytics Manager"
Cohesion: 0.14
Nodes (5): IAnalyticsService, IDictionary, LevelResult, RuntimeInitializeOnLoadMethod, AnalyticsManager

### Community 16 - "Win Countdown UI"
Cohesion: 0.22
Nodes (5): Color, IEnumerator, LevelResult, Text, WinCountdownUI

### Community 17 - "Ads Manager"
Cohesion: 0.22
Nodes (6): Action, float, IAdsService, int, RuntimeInitializeOnLoadMethod, AdsManager

### Community 18 - "Camera Controller"
Cohesion: 0.23
Nodes (5): bool, float, Transform, Vector3, CameraController

### Community 19 - "MonoBehaviour Hub (misc)"
Cohesion: 0.17
Nodes (6): float, MonoBehaviour, ButtonSound, CoinSpin, ClimbUp.Tutorial, IgnoreTutorialBlock

### Community 20 - "Platform Block (3D)"
Cohesion: 0.21
Nodes (6): Bounds, float, Material, Transform, MaterialPropertyBlock, PlatformBlock

### Community 22 - "Screen Fader"
Cohesion: 0.26
Nodes (4): float, IEnumerator, Image, ScreenFader

### Community 23 - "Tutorial Target"
Cohesion: 0.20
Nodes (6): Dictionary, string, Transform, Vector3, ClimbUp.Tutorial, TutorialTarget

### Community 24 - "Parallax Background"
Cohesion: 0.24
Nodes (6): bool, float, Transform, Vector2, Vector3, ParallaxLayer

### Community 25 - "Stub Ads Service"
Cohesion: 0.27
Nodes (4): Action, IEnumerator, IAdsService, StubAdsService

### Community 26 - "Stub Analytics Service"
Cohesion: 0.18
Nodes (3): IDictionary, IAnalyticsService, StubAnalyticsService

### Community 27 - "Leg Swing IK"
Cohesion: 0.27
Nodes (6): bool, float, Quaternion, Rigidbody, Transform, LegSwing

### Community 28 - "Two-Bone Arm IK"
Cohesion: 0.24
Nodes (6): bool, float, Quaternion, Transform, Vector3, TwoBoneArmIK

### Community 31 - "IAP Manager"
Cohesion: 0.22
Nodes (5): Action, IIapService, RuntimeInitializeOnLoadMethod, string, IapManager

### Community 32 - "Skin Applier"
Cohesion: 0.27
Nodes (5): Dictionary, SkinDefinition, Transform, SkinnedMeshRenderer, SkinApplier

### Community 33 - "Tutorial Input Gate"
Cohesion: 0.27
Nodes (5): bool, string, CanvasGroup, ClimbUp.Tutorial, TutorialInputGate

### Community 34 - "Tutorial Step Highlight"
Cohesion: 0.24
Nodes (9): bool, Color, float, string, TutorialAction, TutorialCondition, ClimbUp.Tutorial, StepHighlight (+1 more)

### Community 35 - "Platform Skin (2D sprite)"
Cohesion: 0.25
Nodes (5): float, int, Sprite, PlatformSkin, SpriteRenderer

### Community 36 - "Platform Top"
Cohesion: 0.25
Nodes (5): Collider, float, HashSet, Rigidbody, PlatformTop

### Community 37 - "Star HUD"
Cohesion: 0.25
Nodes (4): Color, string, Text, StarHUDController

### Community 38 - "Tutorial Checkpoint"
Cohesion: 0.36
Nodes (3): Collider, string, TutorialCheckpointTrigger

### Community 40 - "Win Trigger"
Cohesion: 0.29
Nodes (4): Collider, string, WinCollider, WinScript

### Community 41 - "Body Stabilizer"
Cohesion: 0.29
Nodes (4): float, Rigidbody, Vector3, BodyStabilizer

### Community 42 - "Fail Collider"
Cohesion: 0.43
Nodes (3): Collider, Collision, FailCollider

### Community 43 - "Music Player"
Cohesion: 0.29
Nodes (4): AudioClip, AudioSource, float, MusicPlayer

### Community 44 - "Stub IAP Service"
Cohesion: 0.38
Nodes (3): Action, IIapService, StubIapService

### Community 45 - "Win Script"
Cohesion: 0.29
Nodes (4): bool, float, int, WinScript

### Community 47 - "Skin Card View"
Cohesion: 0.33
Nodes (5): Button, GameObject, Image, Text, SkinCardView

### Community 48 - "Tutorial Events"
Cohesion: 0.33
Nodes (4): string, ClimbUp.Tutorial, TutorialEventIds, TutorialEvents

### Community 50 - "Coin Collision"
Cohesion: 0.40
Nodes (3): bool, Collider, CoinCollision

### Community 51 - "Level Loader"
Cohesion: 0.40
Nodes (3): int, string, LevelLoader

### Community 52 - "Star Collision"
Cohesion: 0.40
Nodes (3): bool, Collider, StarCollision

### Community 54 - "Subclass Selector Attr"
Cohesion: 0.50
Nodes (3): PropertyAttribute, ClimbUp.Tutorial, SubclassSelectorAttribute

## Knowledge Gaps
- **195 isolated node(s):** `Rigidbody`, `Vector3`, `float`, `float`, `bool` (+190 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **7 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `MonoBehaviour` connect `MonoBehaviour Hub (misc)` to `Shared Unity Primitives`, `Climb Leg Visuals`, `Climb Line/Leg Render`, `VFX Manager`, `UI Layout`, `UI Controller`, `Level Manager`, `Skin Shop`, `Platform Collision`, `Scene Controller`, `Sound Manager`, `Win Screen`, `Analytics Manager`, `Win Countdown UI`, `Ads Manager`, `Camera Controller`, `Platform Block (3D)`, `Screen Fader`, `Tutorial Target`, `Parallax Background`, `Stub Ads Service`, `Leg Swing IK`, `Two-Bone Arm IK`, `Community 29`, `IAP Manager`, `Skin Applier`, `Platform Skin (2D sprite)`, `Platform Top`, `Star HUD`, `Tutorial Checkpoint`, `Win Trigger`, `Body Stabilizer`, `Fail Collider`, `Music Player`, `Win Script`, `Skin Card View`, `Coin Collision`, `Star Collision`, `Community 53`?**
  _High betweenness centrality (0.542) - this node is a cross-community bridge._
- **Why does `TutorialPlayer` connect `Shared Unity Primitives` to `MonoBehaviour Hub (misc)`?**
  _High betweenness centrality (0.098) - this node is a cross-community bridge._
- **Why does `ClimbController` connect `Climb Leg Visuals` to `MonoBehaviour Hub (misc)`?**
  _High betweenness centrality (0.069) - this node is a cross-community bridge._
- **What connects `Rigidbody`, `Vector3`, `float` to the rest of the system?**
  _195 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Shared Unity Primitives` be split into smaller, more focused modules?**
  _Cohesion score 0.06857142857142857 - nodes in this community are weakly interconnected._
- **Should `Tutorial Conditions` be split into smaller, more focused modules?**
  _Cohesion score 0.05426356589147287 - nodes in this community are weakly interconnected._
- **Should `Climb Leg Visuals` be split into smaller, more focused modules?**
  _Cohesion score 0.10588235294117647 - nodes in this community are weakly interconnected._