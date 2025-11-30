public interface IPlayMode
{
    void OnEnter(IPlayMode prev);
    void OnExit(IPlayMode next);
}

public sealed class EmptyMode : IPlayMode
{
    public void OnEnter(IPlayMode prev) { }
    public void OnExit(IPlayMode next) { }
}