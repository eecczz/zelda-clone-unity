/// <summary>
/// 피해를 받을 수 있는 대상. 나중에 플레이어 체력 시스템을 붙일 때
/// Player 오브젝트에 이 인터페이스를 구현한 컴포넌트만 얹으면
/// RockProjectile이 자동으로 찾아 데미지를 넣는다.
/// </summary>
public interface IDamageable
{
    bool IsDead { get; }
    void TakeDamage(int amount);
}
