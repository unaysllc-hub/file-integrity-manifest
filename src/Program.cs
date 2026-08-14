using System.Security.Cryptography;
using System.Text.Json;

namespace Unays.FileIntegrity;

public sealed record ManifestEntry(string Path, long Length, string Sha256);
public sealed record Manifest(int Version, DateTimeOffset CreatedAt, IReadOnlyList<ManifestEntry> Files);

public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 3 || (args[0] != "create" && args[0] != "verify"))
        {
            Console.Error.WriteLine("Usage: file-integrity <create|verify> <directory> <manifest.json>");
            return 2;
        }
        try
        {
            string root = Path.GetFullPath(args[1]);
            string manifestPath = Path.GetFullPath(args[2]);
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
            return args[0] == "create"
                ? await CreateAsync(root, manifestPath)
                : await VerifyAsync(root, manifestPath);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            Console.Error.WriteLine($"file-integrity: {error.Message}");
            return 2;
        }
    }

    private static async Task<int> CreateAsync(string root, string manifestPath)
    {
        List<ManifestEntry> files = [];
        foreach (string file in EnumerateFiles(root, manifestPath))
        {
            files.Add(await DescribeAsync(root, file));
        }
        var manifest = new Manifest(1, DateTimeOffset.UtcNow, files);
        string? parent = Path.GetDirectoryName(manifestPath);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions) + Environment.NewLine);
        Console.WriteLine($"Created manifest for {files.Count} files: {manifestPath}");
        return 0;
    }

    private static async Task<int> VerifyAsync(string root, string manifestPath)
    {
        Manifest manifest = JsonSerializer.Deserialize<Manifest>(await File.ReadAllTextAsync(manifestPath), JsonOptions)
            ?? throw new JsonException("manifest is empty");
        var expected = manifest.Files.ToDictionary(entry => entry.Path, StringComparer.Ordinal);
        var currentPaths = EnumerateFiles(root, manifestPath)
            .Select(file => Normalize(Path.GetRelativePath(root, file))).ToHashSet(StringComparer.Ordinal);
        int problems = 0;

        foreach (ManifestEntry entry in manifest.Files.OrderBy(item => item.Path, StringComparer.Ordinal))
        {
            string fullPath = Path.Combine(root, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"MISSING  {entry.Path}");
                problems++;
                continue;
            }
            ManifestEntry current = await DescribeAsync(root, fullPath);
            if (current.Length != entry.Length || !StringComparer.OrdinalIgnoreCase.Equals(current.Sha256, entry.Sha256))
            {
                Console.WriteLine($"CHANGED  {entry.Path}");
                problems++;
            }
        }
        foreach (string path in currentPaths.Where(path => !expected.ContainsKey(path)).OrderBy(path => path, StringComparer.Ordinal))
        {
            Console.WriteLine($"ADDED    {path}");
            problems++;
        }
        Console.WriteLine(problems == 0 ? $"Verified {manifest.Files.Count} files." : $"Verification failed with {problems} difference(s)." );
        return problems == 0 ? 0 : 1;
    }

    private static IEnumerable<string> EnumerateFiles(string root, string manifestPath) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .Where(path => !StringComparer.OrdinalIgnoreCase.Equals(path, manifestPath))
            .OrderBy(path => Normalize(Path.GetRelativePath(root, path)), StringComparer.Ordinal);

    private static async Task<ManifestEntry> DescribeAsync(string root, string path)
    {
        await using FileStream stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream);
        return new ManifestEntry(Normalize(Path.GetRelativePath(root, path)), stream.Length, Convert.ToHexString(hash).ToLowerInvariant());
    }

    private static string Normalize(string path) => path.Replace(Path.DirectorySeparatorChar, '/');
}
