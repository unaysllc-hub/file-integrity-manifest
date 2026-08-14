# File Integrity Manifest

A cross-platform C#/.NET 8 command-line tool that creates deterministic SHA-256 file manifests and verifies directories for changed, missing, or added files. It reports differences and never modifies the inspected files.

## Build

```bash
dotnet build src/FileIntegrityManifest.csproj --configuration Release
```

## Create a manifest

```bash
dotnet run --project src -- create ./important-files ./manifest.json
```

## Verify later

```bash
dotnet run --project src -- verify ./important-files ./manifest.json
```

Exit codes:

- `0`: verification succeeded
- `1`: one or more differences were found
- `2`: invalid usage or an input error

The manifest records relative paths, byte lengths and SHA-256 hashes. It is useful for backups, static datasets, release bundles and classroom resource folders. A matching hash verifies file content, not the trustworthiness of its source.

## License

MIT License.
