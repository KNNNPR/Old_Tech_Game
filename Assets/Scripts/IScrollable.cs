/// <summary>
/// Объект, который реагирует на колесо мыши, пока на него смотрит прицел
/// (ползунки, ручки, регуляторы).
/// </summary>
public interface IScrollable
{
    /// <param name="direction">+1 — колесо вверх, -1 — вниз.</param>
    void Scroll(int direction);
}
