namespace Messanger.Services
{
    public interface IHandler<T>
    {
        IHandler<T> SetNext(IHandler<T> next);
        Task<string?> HandleAsync(T context);
    }
}
