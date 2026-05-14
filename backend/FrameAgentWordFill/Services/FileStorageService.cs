namespace FrameAgentWordFill.Services;

public sealed class FileStorageService
{
    private readonly string _storageRoot;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(IConfiguration configuration, IHostEnvironment hostEnvironment, ILogger<FileStorageService> logger)
    {
        _logger = logger;
        var rootPath = configuration["Storage:RootPath"] ?? "..\\..\\storage";
        _storageRoot = Path.IsPathRooted(rootPath)
            ? rootPath
            : Path.GetFullPath(Path.Combine(hostEnvironment.ContentRootPath, rootPath));
        
        InitializeDirectories();
    }

    private void InitializeDirectories()
    {
        var directories = new[]
        {
            Path.Combine(_storageRoot, "data"),
            Path.Combine(_storageRoot, "templates"),
            Path.Combine(_storageRoot, "output"),
            Path.Combine(_storageRoot, "uploads")
        };

        foreach (var dir in directories)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                _logger.LogInformation("Created storage directory: {Directory}", dir);
            }
        }
    }

    public string GetTemplatesPath() => Path.Combine(_storageRoot, "templates");
    public string GetOutputPath() => Path.Combine(_storageRoot, "output");
    public string GetUploadsPath() => Path.Combine(_storageRoot, "uploads");

    public async Task<string> SaveTemplateAsync(IFormFile file, string fileName)
    {
        var path = Path.Combine(GetTemplatesPath(), fileName);
        await using var stream = new FileStream(path, FileMode.Create);
        await file.CopyToAsync(stream);
        _logger.LogInformation("Saved template file: {FileName}", fileName);
        return path;
    }

    public bool TemplateExists(string fileName)
    {
        var path = Path.Combine(GetTemplatesPath(), fileName);
        return File.Exists(path);
    }

    public async Task<string> SaveUploadFileAsync(IFormFile file)
    {
        var uniqueName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
        var path = Path.Combine(GetUploadsPath(), uniqueName);
        await using var stream = new FileStream(path, FileMode.Create);
        await file.CopyToAsync(stream);
        _logger.LogInformation("Saved upload file: {FileName}", uniqueName);
        return uniqueName;
    }

    public string GetUploadFilePath(string relativeName)
    {
        return Path.Combine(GetUploadsPath(), relativeName);
    }
}
