using Microsoft.AspNetCore.Mvc;

namespace Messanger.Services
{
    public class FileService : IFileService
    {
        public readonly IWebHostEnvironment _env;
        private const string UploadsRoot = "uploads";

        public FileService(IWebHostEnvironment env) => _env = env;

        public async Task<string> SaveAsync(IFormFile file, string subFolder = "")
        {
            var uploadsPath = Path.Combine(_env.WebRootPath, UploadsRoot, subFolder);
            Directory.CreateDirectory(uploadsPath);

            var uniqueName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var fullPath = Path.Combine(uploadsPath, uniqueName);

            await using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);

            var relativeUrl = $"/{UploadsRoot}/{(string.IsNullOrEmpty(subFolder) ? "" : subFolder + "/")}{uniqueName}";
            return relativeUrl;
        }

        public IActionResult Serve(string relativePath, string downloadName)
        {
            var filePath = Path.Combine(_env.WebRootPath, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(filePath))
                return new NotFoundResult();

            return new PhysicalFileResult(filePath, "application/octet-stream"){ FileDownloadName = downloadName };
        }
    }
}
