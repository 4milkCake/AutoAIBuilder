namespace AutoAIBuilder.Domain.Projects;

public sealed record ProjectWorkspace(
    Guid Id,
    string Name,
    string Type,
    int Floors,
    int Units,
    IReadOnlyList<string> Disciplines,
    IReadOnlyList<ProjectFile> Files,
    IReadOnlyList<LayerDefinition> Layers,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public bool IsArchived { get; init; }

    public DateTimeOffset? ArchivedAt { get; init; }

    public ProjectRules Rules { get; init; } = ProjectRules.CreateDefault();

    public static ProjectWorkspace Create(
        string name,
        string type,
        int floors,
        int units,
        IReadOnlyList<string>? disciplines = null,
        DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("O nome do projeto é obrigatório.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("O tipo do projeto é obrigatório.", nameof(type));
        }

        if (floors < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(floors), "O projeto deve possuir ao menos um pavimento.");
        }

        if (units < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(units), "O projeto deve possuir ao menos uma unidade.");
        }

        var timestamp = now ?? DateTimeOffset.Now;

        return new ProjectWorkspace(
            Guid.NewGuid(),
            name.Trim(),
            type.Trim(),
            floors,
            units,
            disciplines is { Count: > 0 }
                ? disciplines.Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                : ["Elétrico", "Hidrossanitário"],
            [],
            DefaultLayerCatalog.Create(),
            timestamp,
            timestamp);
    }

    public ProjectWorkspace RegisterFile(
        string sourcePath,
        long sizeBytes,
        DateTimeOffset? now = null,
        DateTimeOffset? lastKnownWriteTime = null)
    {
        EnsureActive("catalogar arquivos");

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new ArgumentException("O caminho do arquivo é obrigatório.", nameof(sourcePath));
        }

        var normalizedPath = Path.GetFullPath(sourcePath);
        var extension = Path.GetExtension(normalizedPath).ToLowerInvariant();

        if (Files.Any(file => string.Equals(
                file.SourcePath,
                normalizedPath,
                StringComparison.OrdinalIgnoreCase)))
        {
            return this;
        }

        var timestamp = now ?? DateTimeOffset.Now;
        var projectFile = new ProjectFile(
            Guid.NewGuid(),
            Path.GetFileName(normalizedPath),
            normalizedPath,
            extension,
            sizeBytes,
            Classify(extension),
            timestamp)
        {
            LastKnownWriteTime = lastKnownWriteTime
        };

        return this with
        {
            Files = [.. Files, projectFile],
            UpdatedAt = timestamp
        };
    }

    public ProjectWorkspace RemoveFileReference(
        Guid fileId,
        DateTimeOffset? now = null)
    {
        EnsureActive("remover referências de arquivos");

        var remainingFiles = Files
            .Where(file => file.Id != fileId)
            .ToArray();

        if (remainingFiles.Length == Files.Count)
        {
            return this;
        }

        return this with
        {
            Files = remainingFiles,
            UpdatedAt = now ?? DateTimeOffset.Now
        };
    }

    public ProjectWorkspace UpdateFileMetadata(
        Guid fileId,
        long sizeBytes,
        DateTimeOffset lastWriteTime,
        DateTimeOffset? now = null)
    {
        EnsureActive("atualizar metadados de arquivos");

        var existing = Files.FirstOrDefault(file => file.Id == fileId)
            ?? throw new InvalidOperationException("A referência de arquivo não foi encontrada.");

        var updatedFile = existing with
        {
            SizeBytes = sizeBytes,
            LastKnownWriteTime = lastWriteTime
        };

        return this with
        {
            Files = Files
                .Select(file => file.Id == fileId ? updatedFile : file)
                .ToArray(),
            UpdatedAt = now ?? DateTimeOffset.Now
        };
    }

    public ProjectWorkspace UpdateDetails(
        string name,
        string type,
        int floors,
        int units,
        DateTimeOffset? now = null)
    {
        ValidateDetails(name, type, floors, units);

        if (IsArchived)
        {
            throw new InvalidOperationException("Restaure o projeto antes de editá-lo.");
        }

        return this with
        {
            Name = name.Trim(),
            Type = type.Trim(),
            Floors = floors,
            Units = units,
            UpdatedAt = now ?? DateTimeOffset.Now
        };
    }

    public ProjectWorkspace UpdateRules(
        ProjectRules rules,
        DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        EnsureActive("editar as regras");

        return this with
        {
            Rules = rules.ValidateAndNormalize(),
            UpdatedAt = now ?? DateTimeOffset.Now
        };
    }

    public ProjectWorkspace Duplicate(
        string? name = null,
        DateTimeOffset? now = null)
    {
        var duplicateName = string.IsNullOrWhiteSpace(name)
            ? $"{Name} - Cópia"
            : name.Trim();
        var timestamp = now ?? DateTimeOffset.Now;

        ValidateDetails(duplicateName, Type, Floors, Units);

        return this with
        {
            Id = Guid.NewGuid(),
            Name = duplicateName,
            Files = Files
                .Select(file => file with { Id = Guid.NewGuid() })
                .ToArray(),
            Layers = Layers.ToArray(),
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            IsArchived = false,
            ArchivedAt = null
        };
    }

    public ProjectWorkspace Archive(DateTimeOffset? now = null)
    {
        if (IsArchived)
        {
            return this;
        }

        var timestamp = now ?? DateTimeOffset.Now;
        return this with
        {
            IsArchived = true,
            ArchivedAt = timestamp,
            UpdatedAt = timestamp
        };
    }

    public ProjectWorkspace Restore(DateTimeOffset? now = null)
    {
        if (!IsArchived)
        {
            return this;
        }

        return this with
        {
            IsArchived = false,
            ArchivedAt = null,
            UpdatedAt = now ?? DateTimeOffset.Now
        };
    }

    public ProjectSummary ToSummary() => new(
        Id,
        Name,
        Type,
        Floors,
        Units,
        Disciplines,
        CreatedAt,
        UpdatedAt);

    private static ProjectFileKind Classify(string extension) => extension switch
    {
        ".dwg" or ".dxf" or ".ifc" => ProjectFileKind.Drawing,
        ".pdf" or ".doc" or ".docx" => ProjectFileKind.Document,
        ".xls" or ".xlsx" or ".csv" => ProjectFileKind.Spreadsheet,
        ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tif" or ".tiff" => ProjectFileKind.Image,
        _ => ProjectFileKind.Other
    };

    private static void ValidateDetails(
        string name,
        string type,
        int floors,
        int units)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("O nome do projeto é obrigatório.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("O tipo do projeto é obrigatório.", nameof(type));
        }

        if (floors < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(floors), "O projeto deve possuir ao menos um pavimento.");
        }

        if (units < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(units), "O projeto deve possuir ao menos uma unidade.");
        }
    }

    private void EnsureActive(string operation)
    {
        if (IsArchived)
        {
            throw new InvalidOperationException($"Restaure o projeto antes de {operation}.");
        }
    }
}
