namespace Messanger.Services
{
    public abstract class HandlerBase<T> : IHandler<T>
    {
        private IHandler<T>? _next;
        public IHandler<T> SetNext(IHandler<T> next)
        {
            _next = next;
            return next;
        }
        public async Task<string?> HandleAsync(T context)
        {
            var error = await ProcessAsync(context);
            if (error != null)
                return error;
            if (_next != null)
                return await _next.HandleAsync(context);

            return null;
        }
        protected abstract Task<string?> ProcessAsync(T context);
    }
}
