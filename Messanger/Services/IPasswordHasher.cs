namespace Messanger.Services
{
    public interface IPasswordHasher
    {
        string Hash(string password);
    }
}
