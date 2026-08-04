# AI 사용 기록

- 2026-08-04 — Claude Code로 "일섬 대시" 3단계 구현: 원거리 적과 투사체 반격 (`RangedEnemy.cs`/`RockProjectile.cs`/`IDamageable.cs` 신규, `Enemy.cs` 상속 가능하게 리팩터링, `PlayerDash.cs` 캡슐 스윕에 반사 판정 통합, `EnemySpawner.cs` 근접:원거리 비율 추가).
- 2026-08-04 — Claude Code로 "일섬 대시" 2단계 구현: 적과 베기 판정 (`Enemy.cs`/`EnemySpawner.cs`/`SlashVfx.cs` 신규, `PlayerDash.cs`에 경로 스윕 베기 판정 + 히트스톱 + 처치 시 쿨다운 리셋 추가).
- 2026-08-04 — Claude Code로 "일섬 대시" 전투 메커니즘 1단계 구현 (`Assets/Scripts/PlayerDash.cs` 신규: 조준 중 슬로모션 + LineRenderer 조준선 + 코루틴 돌진 + 쿨다운, `PlayerMovement.cs`에 대시 중 이동 무시 가드 추가).
