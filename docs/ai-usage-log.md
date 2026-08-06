# AI 사용 기록

- 2026-08-05 — Claude Code로 "일섬 대시" 6단계 구현: 오픈 필드 + 거점 소탕 전환 (`EnemyCamp.cs`/`CampTotem.cs`/`FieldBounds.cs`/`CompassArrow.cs`/`HealthDisplay.cs`/`UiSprites.cs` 신규, `Enemy.cs` 배회/추격/복귀/도주 모드 머신 + 접촉 피해 간격화, `PlayerHealth.cs` 3칸 체력 + 무적, `GameManager.cs` 승리 상태, `HUD.cs` 거점 카운터/체력/승리 패널, `EnemySpawner.cs` 야생 적 역할로 축소).
- 2026-08-04 — Claude Code로 "일섬 대시" 5단계 구현: 번개 참격 연출 (`LightningBolt.cs`/`LightningPalette.cs`/`ScreenFlash.cs` 신규 — 프로시저럴 지그재그 볼트·겉광/심지 2겹·잔가지 분기·볼트 상한 20, `PlayerDash.cs` 궤적 번개 + 조준선 지터 + 처치 잔가지/플래시, `RockProjectile.cs` 반사 처치 연출 통일).
- 2026-08-04 — Claude Code로 "일섬 대시" 4단계 구현: 게임 루프 완성 (`GameManager.cs`/`ScoreSystem.cs`/`PlayerHealth.cs`/`HUD.cs` 신규 — 상태 머신·점수/콤보·한 방 사망·TMP HUD, `Enemy.cs` 접촉 피해 추가, `EnemySpawner.cs` 시간 기반 난이도 곡선, `PlayerDash.cs` 상태 게이팅 + 점수 연동).
- 2026-08-04 — Claude Code로 "일섬 대시" 3단계 구현: 원거리 적과 투사체 반격 (`RangedEnemy.cs`/`RockProjectile.cs`/`IDamageable.cs` 신규, `Enemy.cs` 상속 가능하게 리팩터링, `PlayerDash.cs` 캡슐 스윕에 반사 판정 통합, `EnemySpawner.cs` 근접:원거리 비율 추가).
- 2026-08-04 — Claude Code로 "일섬 대시" 2단계 구현: 적과 베기 판정 (`Enemy.cs`/`EnemySpawner.cs`/`SlashVfx.cs` 신규, `PlayerDash.cs`에 경로 스윕 베기 판정 + 히트스톱 + 처치 시 쿨다운 리셋 추가).
- 2026-08-04 — Claude Code로 "일섬 대시" 전투 메커니즘 1단계 구현 (`Assets/Scripts/PlayerDash.cs` 신규: 조준 중 슬로모션 + LineRenderer 조준선 + 코루틴 돌진 + 쿨다운, `PlayerMovement.cs`에 대시 중 이동 무시 가드 추가).
