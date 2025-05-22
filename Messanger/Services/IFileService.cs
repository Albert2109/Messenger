using Microsoft.AspNetCore.Mvc;

namespace Messanger.Services
{
    public interface IFileService
    {
        Task<string> SaveAsync(IFormFile file, string subFolder = "");
        IActionResult Serve(string relativePath, string downloadName);
    }
}
