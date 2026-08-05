# Procedural Assembly Demo

`SampleScene`을 열고 Play를 누르면 외부 모델 없이 조각난 골렘 데모가 자동 생성됩니다.

- Space: 연출 처음부터 다시 재생
- Assembled → Scatter → Orbit → Reassemble 순서로 반복
- `ProceduralAssemblyDemo` 컴포넌트에서 시간, 궤도 반경, 높이, 회전 속도를 조절할 수 있습니다.

## 실제 캐릭터에 적용하는 법

현재 데모의 각 primitive는 실제 캐릭터의 분리된 mesh transform에 해당합니다. 모델을 머리/갑옷/비늘/관절 등 여러 GameObject로 분리한 뒤, `Part`에 각 transform의 시작 position과 rotation을 저장하고 동일한 궤도 및 복귀 계산을 적용하면 됩니다.

SkinnedMeshRenderer 하나로 된 캐릭터는 먼저 DCC 도구(Blender 등)에서 파츠를 분리하거나, 연출 전용 파편 mesh를 별도로 제작하는 방식이 안정적입니다.
