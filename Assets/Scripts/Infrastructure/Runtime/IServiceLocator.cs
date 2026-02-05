namespace LittleBeakCluck.Infrastructure
{
    public interface IServiceLocator
    {
        void Register<T>(T service) where T : IGameService;
        T Get<T>() where T : IGameService;
    }
}
