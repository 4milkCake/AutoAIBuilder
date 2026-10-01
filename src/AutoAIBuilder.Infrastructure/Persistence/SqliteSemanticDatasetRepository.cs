using System.Globalization;
using System.Text.Json;
using AutoAIBuilder.Application.Semantics;
using AutoAIBuilder.Domain.Semantics;
using Microsoft.Data.Sqlite;

namespace AutoAIBuilder.Infrastructure.Persistence;

public sealed class SqliteSemanticDatasetRepository(SqliteDatabase database)
    : ISemanticDatasetRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SemanticWorkspaceSnapshot GetForProject(Guid projectId)
    {
        if (projectId == Guid.Empty)
        {
            return SemanticWorkspaceSnapshot.Empty;
        }

        lock (database.SyncRoot)
        {
            using var connection = database.OpenConnection();
            var dataset = ReadLatestDataset(connection, projectId);
            if (dataset is null)
            {
                return SemanticWorkspaceSnapshot.Empty;
            }

            return new SemanticWorkspaceSnapshot(
                dataset,
                ReadPoints(connection, dataset.Id),
                ReadComponents(connection, dataset.Id),
                ReadDirections(connection, dataset.Id),
                ReadRevisions(connection, dataset.Id),
                ReadKnowledgeEntries(
                    connection,
                    dataset.ProjectId,
                    dataset.Id));
        }
    }

    public bool Replace(SemanticImportPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        ValidatePackage(package);

        lock (database.SyncRoot)
        {
            using var connection = database.OpenConnection();
            using var transaction = connection.BeginTransaction();
            var existing = ReadExistingDataset(
                connection,
                transaction,
                package.Dataset.ProjectId,
                package.Dataset.DrawingPath);
            var datasetId = existing?.DatasetId ?? package.Dataset.Id;
            var importedAt = existing?.ImportedAt ?? package.Dataset.ImportedAt;
            var previousReviews = existing is null
                ? new Dictionary<string, PersistedReview>(
                    StringComparer.OrdinalIgnoreCase)
                : ReadReviews(connection, transaction, datasetId);
            var previousDirections = existing is null
                ? new Dictionary<string, PersistedDirection>(
                    StringComparer.OrdinalIgnoreCase)
                : ReadPersistedDirections(connection, transaction, datasetId);

            if (existing is not null)
            {
                DeleteDatasetChildren(connection, transaction, datasetId);
            }

            UpsertDataset(
                connection,
                transaction,
                package.Dataset with
                {
                    Id = datasetId,
                    ImportedAt = importedAt
                });

            foreach (var sourcePoint in package.Points)
            {
                var point = sourcePoint with
                {
                    DatasetId = datasetId
                };
                if (previousReviews.TryGetValue(
                        point.ExternalId,
                        out var review)
                    && review.Status != SemanticReviewStatus.Identified)
                {
                    point = point with
                    {
                        Id = review.PointId,
                        Discipline = review.CurrentPoint.Discipline,
                        SemanticCode = review.CurrentPoint.SemanticCode,
                        Description = review.CurrentPoint.Description,
                        HeightCm = review.CurrentPoint.HeightCm,
                        HeightSourceValue =
                            review.CurrentPoint.HeightSourceValue,
                        ReviewStatus = review.Status,
                        ReviewNote = review.Note,
                        ReviewedAt = review.ReviewedAt
                    };
                }

                InsertPoint(connection, transaction, point);
            }

            foreach (var component in package.Components)
            {
                InsertComponent(
                    connection,
                    transaction,
                    component with { DatasetId = datasetId });
            }

            foreach (var direction in package.Directions)
            {
                var persistedDirection = direction;
                if (previousDirections.TryGetValue(
                        direction.Handle,
                        out var previousDirection)
                    && previousDirection.Direction.ReviewStatus
                        != SemanticReviewStatus.Identified)
                {
                    persistedDirection = direction with
                    {
                        Id = previousDirection.Direction.Id,
                        ReviewStatus =
                            previousDirection.Direction.ReviewStatus,
                        CorrectedBuilderDirection =
                            previousDirection.Direction
                                .CorrectedBuilderDirection,
                        ReviewNote =
                            previousDirection.Direction.ReviewNote,
                        ReviewedAt =
                            previousDirection.Direction.ReviewedAt
                    };
                }

                InsertDirection(
                    connection,
                    transaction,
                    persistedDirection with { DatasetId = datasetId });
            }

            transaction.Commit();
            return existing is not null;
        }
    }

    public void UpdatePointReview(
        Guid projectId,
        Guid pointId,
        SemanticReviewStatus status,
        string? note,
        DateTimeOffset reviewedAt)
    {
        if (projectId == Guid.Empty || pointId == Guid.Empty)
        {
            throw new ArgumentException(
                "Projeto e ponto são obrigatórios para registrar a revisão.");
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        lock (database.SyncRoot)
        {
            using var connection = database.OpenConnection();
            using var transaction = connection.BeginTransaction();
            var before = ReadPointById(
                connection,
                transaction,
                projectId,
                pointId);
            var after = before with
            {
                ReviewStatus = status,
                ReviewNote = NormalizeOptional(note, 2_000),
                ReviewedAt = reviewedAt
            };
            UpdatePointPayload(connection, transaction, after, reviewedAt);
            InsertRevision(
                connection,
                transaction,
                before.DatasetId,
                SemanticReviewEntityKind.Point,
                pointId,
                before.ExternalId,
                status == SemanticReviewStatus.Approved
                    ? "Aprovação"
                    : "Estado de revisão",
                $"{GetReviewLabel(before.ReviewStatus)} → "
                + GetReviewLabel(status),
                after.ReviewNote,
                before,
                after,
                reviewedAt);
            transaction.Commit();
        }
    }

    public SemanticPoint CorrectPoint(
        Guid projectId,
        Guid pointId,
        SemanticPointCorrectionRequest request,
        DateTimeOffset occurredAt)
    {
        ValidateProjectAndEntity(projectId, pointId);
        ArgumentNullException.ThrowIfNull(request);
        if (request.Discipline is not (
                SemanticDiscipline.Electrical
                or SemanticDiscipline.Hydraulic))
        {
            throw new ArgumentException(
                "Selecione uma disciplina elétrica ou hidráulica.",
                nameof(request));
        }

        var code = NormalizeRequired(
            request.SemanticCode,
            160,
            "O código semântico é obrigatório.");
        var description = NormalizeRequired(
            request.Description,
            500,
            "A descrição semântica é obrigatória.");
        var height = NormalizeRequired(
            request.HeightSourceValue,
            100,
            "A altura ou condição de instalação é obrigatória.");
        var note = NormalizeOptional(request.Note, 2_000);

        lock (database.SyncRoot)
        {
            using var connection = database.OpenConnection();
            using var transaction = connection.BeginTransaction();
            var before = ReadPointById(
                connection,
                transaction,
                projectId,
                pointId);
            var after = before with
            {
                Discipline = request.Discipline,
                SemanticCode = code,
                Description = description,
                HeightCm = ParseOptionalHeight(height),
                HeightSourceValue = height,
                ReviewStatus = SemanticReviewStatus.Corrected,
                ReviewNote = note,
                ReviewedAt = occurredAt
            };
            if (after == before)
            {
                return before;
            }

            UpdatePointPayload(connection, transaction, after, occurredAt);
            InsertRevision(
                connection,
                transaction,
                before.DatasetId,
                SemanticReviewEntityKind.Point,
                pointId,
                before.ExternalId,
                "Correção semântica",
                BuildPointCorrectionSummary(before, after),
                note,
                before,
                after,
                occurredAt);
            if (request.AddToProjectKnowledge)
            {
                UpsertKnowledge(
                    connection,
                    transaction,
                    projectId,
                    before,
                    after,
                    evidenceDelta: 1,
                    occurredAt);
            }

            transaction.Commit();
            return after;
        }
    }

    public IReadOnlyList<SemanticPoint> FindSimilarPoints(
        Guid projectId,
        Guid pointId)
    {
        ValidateProjectAndEntity(projectId, pointId);
        var snapshot = GetForProject(projectId);
        var source = snapshot.Points.SingleOrDefault(
                point => point.Id == pointId)
            ?? throw new KeyNotFoundException(
                "O ponto semântico não pertence ao projeto ativo.");
        var hasBlock = !string.IsNullOrWhiteSpace(source.BlockName);
        return snapshot.Points
            .Where(point => point.Id != source.Id)
            .Where(point =>
                (hasBlock
                 && point.BlockName.Equals(
                     source.BlockName,
                     StringComparison.OrdinalIgnoreCase)
                 && point.SemanticLayer.Equals(
                     source.SemanticLayer,
                     StringComparison.OrdinalIgnoreCase))
                || (point.SemanticCode.Equals(
                        source.SemanticCode,
                        StringComparison.OrdinalIgnoreCase)
                    && point.SemanticLayer.Equals(
                        source.SemanticLayer,
                        StringComparison.OrdinalIgnoreCase)))
            .OrderBy(point => DistanceSquared(source, point))
            .ThenBy(point => point.ExternalId)
            .ToArray();
    }

    public int ApplyPointCorrection(
        Guid projectId,
        Guid sourcePointId,
        IReadOnlyList<Guid> targetPointIds,
        string? note,
        DateTimeOffset occurredAt)
    {
        ValidateProjectAndEntity(projectId, sourcePointId);
        ArgumentNullException.ThrowIfNull(targetPointIds);
        var targets = targetPointIds
            .Where(id => id != Guid.Empty && id != sourcePointId)
            .Distinct()
            .ToArray();
        if (targets.Length == 0 || targets.Length > 500)
        {
            throw new ArgumentException(
                "Selecione entre 1 e 500 pontos semelhantes.",
                nameof(targetPointIds));
        }

        var normalizedNote = NormalizeOptional(note, 2_000)
            ?? "Correção aplicada a partir de um ponto semelhante confirmado.";
        lock (database.SyncRoot)
        {
            using var connection = database.OpenConnection();
            using var transaction = connection.BeginTransaction();
            var source = ReadPointById(
                connection,
                transaction,
                projectId,
                sourcePointId);
            var updated = 0;
            foreach (var targetId in targets)
            {
                var before = ReadPointById(
                    connection,
                    transaction,
                    projectId,
                    targetId);
                var after = before with
                {
                    Discipline = source.Discipline,
                    SemanticCode = source.SemanticCode,
                    Description = source.Description,
                    HeightCm = source.HeightCm,
                    HeightSourceValue = source.HeightSourceValue,
                    ReviewStatus = SemanticReviewStatus.Corrected,
                    ReviewNote = normalizedNote,
                    ReviewedAt = occurredAt
                };
                UpdatePointPayload(connection, transaction, after, occurredAt);
                InsertRevision(
                    connection,
                    transaction,
                    before.DatasetId,
                    SemanticReviewEntityKind.Point,
                    before.Id,
                    before.ExternalId,
                    "Correção por semelhança",
                    $"Aplicado o padrão confirmado em {source.ExternalId}: "
                    + BuildPointCorrectionSummary(before, after),
                    normalizedNote,
                    before,
                    after,
                    occurredAt);
                updated++;
            }

            UpsertKnowledge(
                connection,
                transaction,
                projectId,
                source,
                source,
                evidenceDelta: updated,
                occurredAt);
            transaction.Commit();
            return updated;
        }
    }

    public SemanticDirectionDiagnostic ReviewDirection(
        Guid projectId,
        Guid directionId,
        SemanticDirectionReviewRequest request,
        DateTimeOffset occurredAt)
    {
        ValidateProjectAndEntity(projectId, directionId);
        ArgumentNullException.ThrowIfNull(request);
        var direction = NormalizeRequired(
            request.BuilderDirection,
            40,
            "A direção Builder é obrigatória.").ToUpperInvariant();
        if (direction is not ("DIREITA" or "ESQUERDA" or "CIMA" or "BAIXO"))
        {
            throw new ArgumentException(
                "Use DIREITA, ESQUERDA, CIMA ou BAIXO.",
                nameof(request));
        }

        if (request.Status is not (
                SemanticReviewStatus.Corrected
                or SemanticReviewStatus.Approved))
        {
            throw new ArgumentException(
                "A direção deve ser corrigida ou aprovada.",
                nameof(request));
        }

        var note = NormalizeOptional(request.Note, 2_000);
        lock (database.SyncRoot)
        {
            using var connection = database.OpenConnection();
            using var transaction = connection.BeginTransaction();
            var before = ReadDirectionById(
                connection,
                transaction,
                projectId,
                directionId);
            var after = before with
            {
                CorrectedBuilderDirection = direction,
                ReviewStatus = request.Status,
                ReviewNote = note,
                ReviewedAt = occurredAt
            };
            UpdateDirectionPayload(connection, transaction, after);
            InsertRevision(
                connection,
                transaction,
                before.DatasetId,
                SemanticReviewEntityKind.Direction,
                before.Id,
                before.Handle,
                request.Status == SemanticReviewStatus.Approved
                    ? "Direção aprovada"
                    : "Direção corrigida",
                $"{before.EffectiveBuilderDirection} → "
                + after.EffectiveBuilderDirection,
                note,
                before,
                after,
                occurredAt);
            transaction.Commit();
            return after;
        }
    }

    public bool UndoLatestRevision(
        Guid projectId,
        SemanticReviewEntityKind entityKind,
        Guid entityId,
        DateTimeOffset revertedAt)
    {
        ValidateProjectAndEntity(projectId, entityId);
        if (!Enum.IsDefined(entityKind))
        {
            throw new ArgumentOutOfRangeException(nameof(entityKind));
        }

        lock (database.SyncRoot)
        {
            using var connection = database.OpenConnection();
            using var transaction = connection.BeginTransaction();
            var revision = ReadLatestActiveRevision(
                connection,
                transaction,
                projectId,
                entityKind,
                entityId);
            if (revision is null)
            {
                return false;
            }

            if (entityKind == SemanticReviewEntityKind.Point)
            {
                var point = Deserialize<SemanticPoint>(
                    revision.BeforeJson,
                    "estado anterior do ponto");
                UpdatePointPayload(
                    connection,
                    transaction,
                    point,
                    revertedAt);
                if (revision.Action.Equals(
                        "Correção semântica",
                        StringComparison.Ordinal))
                {
                    DeleteKnowledgeForSource(
                        connection,
                        transaction,
                        projectId,
                        entityId);
                }
            }
            else
            {
                var direction = Deserialize<SemanticDirectionDiagnostic>(
                    revision.BeforeJson,
                    "estado anterior da direção");
                UpdateDirectionPayload(connection, transaction, direction);
            }

            MarkRevisionReverted(
                connection,
                transaction,
                revision.Id,
                revertedAt);
            transaction.Commit();
            return true;
        }
    }

    private static SemanticDataset? ReadLatestDataset(
        SqliteConnection connection,
        Guid projectId)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                Id,
                ProjectId,
                DrawingFileName,
                DrawingPath,
                SourceVersion,
                SourceFingerprint,
                SourceFilesJson,
                PointCount,
                ElectricalPointCount,
                HydraulicPointCount,
                ComponentCount,
                SemanticLayerCount,
                DirectionCount,
                DirectionReviewCount,
                BuilderValidatedDirectionCount,
                SymmetryInferredDirectionCount,
                MatchesHistoricalBaseline,
                ImportedAt,
                UpdatedAt
            FROM SemanticDatasets
            WHERE ProjectId = $projectId
            ORDER BY UpdatedAt DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$projectId", projectId.ToString("D"));
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadDataset(reader) : null;
    }

    private static SemanticDataset ReadDataset(SqliteDataReader reader)
    {
        var sourceFiles = JsonSerializer.Deserialize<string[]>(
                reader.GetString(6),
                JsonOptions)
            ?? [];
        return new SemanticDataset(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            sourceFiles,
            reader.GetInt32(7),
            reader.GetInt32(8),
            reader.GetInt32(9),
            reader.GetInt32(10),
            reader.GetInt32(11),
            reader.GetInt32(12),
            reader.GetInt32(13),
            reader.GetInt32(14),
            reader.GetInt32(15),
            reader.GetBoolean(16),
            ParseDate(reader.GetString(17)),
            ParseDate(reader.GetString(18)));
    }

    private static IReadOnlyList<SemanticPoint> ReadPoints(
        SqliteConnection connection,
        Guid datasetId)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                Id,
                ReviewStatus,
                ReviewNote,
                ReviewedAt,
                PayloadJson
            FROM SemanticPoints
            WHERE DatasetId = $datasetId
            ORDER BY SemanticCode, ExternalId;
            """;
        command.Parameters.AddWithValue("$datasetId", datasetId.ToString("D"));
        using var reader = command.ExecuteReader();
        var points = new List<SemanticPoint>();
        while (reader.Read())
        {
            var point = Deserialize<SemanticPoint>(
                reader.GetString(4),
                "ponto semântico");
            points.Add(point with
            {
                Id = Guid.Parse(reader.GetString(0)),
                DatasetId = datasetId,
                ReviewStatus = ReadEnum<SemanticReviewStatus>(
                    reader.GetInt32(1),
                    "estado de revisão"),
                ReviewNote = reader.IsDBNull(2) ? null : reader.GetString(2),
                ReviewedAt = reader.IsDBNull(3)
                    ? null
                    : ParseDate(reader.GetString(3))
            });
        }

        return points;
    }

    private static IReadOnlyList<SemanticComponent> ReadComponents(
        SqliteConnection connection,
        Guid datasetId)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT PayloadJson
            FROM SemanticComponents
            WHERE DatasetId = $datasetId
            ORDER BY PointExternalId, ExternalId;
            """;
        command.Parameters.AddWithValue("$datasetId", datasetId.ToString("D"));
        using var reader = command.ExecuteReader();
        var components = new List<SemanticComponent>();
        while (reader.Read())
        {
            components.Add(
                Deserialize<SemanticComponent>(
                    reader.GetString(0),
                    "componente semântico") with
                {
                    DatasetId = datasetId
                });
        }

        return components;
    }

    private static IReadOnlyList<SemanticDirectionDiagnostic> ReadDirections(
        SqliteConnection connection,
        Guid datasetId)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                PayloadJson,
                ReviewStatus,
                CorrectedDirection,
                ReviewNote,
                ReviewedAt
            FROM SemanticDirectionDiagnostics
            WHERE DatasetId = $datasetId
            ORDER BY NeedsReview DESC, Handle;
            """;
        command.Parameters.AddWithValue("$datasetId", datasetId.ToString("D"));
        using var reader = command.ExecuteReader();
        var directions = new List<SemanticDirectionDiagnostic>();
        while (reader.Read())
        {
            var direction = Deserialize<SemanticDirectionDiagnostic>(
                reader.GetString(0),
                "diagnóstico de direção");
            directions.Add(direction with
            {
                DatasetId = datasetId,
                ReviewStatus = ReadEnum<SemanticReviewStatus>(
                    reader.GetInt32(1),
                    "estado de revisão da direção"),
                CorrectedBuilderDirection =
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                ReviewNote =
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                ReviewedAt =
                    reader.IsDBNull(4) ? null : ParseDate(reader.GetString(4))
            });
        }

        return directions;
    }

    private static IReadOnlyList<SemanticReviewRevision> ReadRevisions(
        SqliteConnection connection,
        Guid datasetId)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                Id,
                DatasetId,
                EntityKind,
                EntityId,
                EntityExternalId,
                Action,
                Summary,
                Note,
                OccurredAt,
                RevertedAt
            FROM SemanticReviewRevisions
            WHERE DatasetId = $datasetId
            ORDER BY OccurredAt DESC
            LIMIT 500;
            """;
        command.Parameters.AddWithValue("$datasetId", datasetId.ToString("D"));
        using var reader = command.ExecuteReader();
        var revisions = new List<SemanticReviewRevision>();
        while (reader.Read())
        {
            revisions.Add(new SemanticReviewRevision(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                ReadEnum<SemanticReviewEntityKind>(
                    reader.GetInt32(2),
                    "tipo da revisão"),
                Guid.Parse(reader.GetString(3)),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                ParseDate(reader.GetString(8)),
                reader.IsDBNull(9) ? null : ParseDate(reader.GetString(9))));
        }

        return revisions;
    }

    private static IReadOnlyList<SemanticKnowledgeEntry> ReadKnowledgeEntries(
        SqliteConnection connection,
        Guid projectId,
        Guid datasetId)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                Id,
                ProjectId,
                DatasetId,
                SourcePointId,
                Signature,
                BlockName,
                SemanticLayer,
                PreviousCode,
                LearnedCode,
                LearnedDescription,
                LearnedHeight,
                EvidenceCount,
                CreatedAt,
                UpdatedAt
            FROM SemanticKnowledgeEntries
            WHERE ProjectId = $projectId
              AND DatasetId = $datasetId
            ORDER BY UpdatedAt DESC;
            """;
        command.Parameters.AddWithValue("$projectId", projectId.ToString("D"));
        command.Parameters.AddWithValue("$datasetId", datasetId.ToString("D"));
        using var reader = command.ExecuteReader();
        var entries = new List<SemanticKnowledgeEntry>();
        while (reader.Read())
        {
            entries.Add(new SemanticKnowledgeEntry(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                Guid.Parse(reader.GetString(2)),
                Guid.Parse(reader.GetString(3)),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetString(9),
                reader.GetString(10),
                reader.GetInt32(11),
                ParseDate(reader.GetString(12)),
                ParseDate(reader.GetString(13))));
        }

        return entries;
    }

    private static ExistingDataset? ReadExistingDataset(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid projectId,
        string drawingPath)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT Id, ImportedAt
            FROM SemanticDatasets
            WHERE ProjectId = $projectId
              AND DrawingPath = $drawingPath
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$projectId", projectId.ToString("D"));
        command.Parameters.AddWithValue("$drawingPath", drawingPath);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new ExistingDataset(
                Guid.Parse(reader.GetString(0)),
                ParseDate(reader.GetString(1)))
            : null;
    }

    private static Dictionary<string, PersistedReview> ReadReviews(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid datasetId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT
                Id,
                ExternalId,
                ReviewStatus,
                ReviewNote,
                ReviewedAt,
                PayloadJson
            FROM SemanticPoints
            WHERE DatasetId = $datasetId;
            """;
        command.Parameters.AddWithValue("$datasetId", datasetId.ToString("D"));
        using var reader = command.ExecuteReader();
        var reviews = new Dictionary<string, PersistedReview>(
            StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            reviews[reader.GetString(1)] = new PersistedReview(
                Guid.Parse(reader.GetString(0)),
                ReadEnum<SemanticReviewStatus>(
                    reader.GetInt32(2),
                    "estado de revisão"),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : ParseDate(reader.GetString(4)),
                Deserialize<SemanticPoint>(
                    reader.GetString(5),
                    "ponto semântico anterior"));
        }

        return reviews;
    }

    private static Dictionary<string, PersistedDirection>
        ReadPersistedDirections(
            SqliteConnection connection,
            SqliteTransaction transaction,
            Guid datasetId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT
                Handle,
                PayloadJson,
                ReviewStatus,
                CorrectedDirection,
                ReviewNote,
                ReviewedAt
            FROM SemanticDirectionDiagnostics
            WHERE DatasetId = $datasetId;
            """;
        command.Parameters.AddWithValue("$datasetId", datasetId.ToString("D"));
        using var reader = command.ExecuteReader();
        var directions = new Dictionary<string, PersistedDirection>(
            StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            var direction = Deserialize<SemanticDirectionDiagnostic>(
                reader.GetString(1),
                "direção semântica anterior");
            direction = direction with
            {
                ReviewStatus = ReadEnum<SemanticReviewStatus>(
                    reader.GetInt32(2),
                    "estado anterior da direção"),
                CorrectedBuilderDirection =
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                ReviewNote =
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                ReviewedAt =
                    reader.IsDBNull(5) ? null : ParseDate(reader.GetString(5))
            };
            directions[reader.GetString(0)] =
                new PersistedDirection(direction);
        }

        return directions;
    }

    private static void DeleteDatasetChildren(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid datasetId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            DELETE FROM SemanticDirectionDiagnostics
            WHERE DatasetId = $datasetId;
            DELETE FROM SemanticComponents
            WHERE DatasetId = $datasetId;
            DELETE FROM SemanticPoints
            WHERE DatasetId = $datasetId;
            """;
        command.Parameters.AddWithValue("$datasetId", datasetId.ToString("D"));
        command.ExecuteNonQuery();
    }

    private static void UpsertDataset(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SemanticDataset dataset)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO SemanticDatasets (
                Id,
                ProjectId,
                DrawingFileName,
                DrawingPath,
                SourceVersion,
                SourceFingerprint,
                SourceFilesJson,
                PointCount,
                ElectricalPointCount,
                HydraulicPointCount,
                ComponentCount,
                SemanticLayerCount,
                DirectionCount,
                DirectionReviewCount,
                BuilderValidatedDirectionCount,
                SymmetryInferredDirectionCount,
                MatchesHistoricalBaseline,
                ImportedAt,
                UpdatedAt)
            VALUES (
                $id,
                $projectId,
                $drawingFileName,
                $drawingPath,
                $sourceVersion,
                $sourceFingerprint,
                $sourceFilesJson,
                $pointCount,
                $electricalPointCount,
                $hydraulicPointCount,
                $componentCount,
                $semanticLayerCount,
                $directionCount,
                $directionReviewCount,
                $builderValidatedDirectionCount,
                $symmetryInferredDirectionCount,
                $matchesHistoricalBaseline,
                $importedAt,
                $updatedAt)
            ON CONFLICT(ProjectId, DrawingPath) DO UPDATE SET
                DrawingFileName = excluded.DrawingFileName,
                SourceVersion = excluded.SourceVersion,
                SourceFingerprint = excluded.SourceFingerprint,
                SourceFilesJson = excluded.SourceFilesJson,
                PointCount = excluded.PointCount,
                ElectricalPointCount = excluded.ElectricalPointCount,
                HydraulicPointCount = excluded.HydraulicPointCount,
                ComponentCount = excluded.ComponentCount,
                SemanticLayerCount = excluded.SemanticLayerCount,
                DirectionCount = excluded.DirectionCount,
                DirectionReviewCount = excluded.DirectionReviewCount,
                BuilderValidatedDirectionCount =
                    excluded.BuilderValidatedDirectionCount,
                SymmetryInferredDirectionCount =
                    excluded.SymmetryInferredDirectionCount,
                MatchesHistoricalBaseline =
                    excluded.MatchesHistoricalBaseline,
                UpdatedAt = excluded.UpdatedAt;
            """;
        command.Parameters.AddWithValue("$id", dataset.Id.ToString("D"));
        command.Parameters.AddWithValue(
            "$projectId",
            dataset.ProjectId.ToString("D"));
        command.Parameters.AddWithValue(
            "$drawingFileName",
            dataset.DrawingFileName);
        command.Parameters.AddWithValue("$drawingPath", dataset.DrawingPath);
        command.Parameters.AddWithValue("$sourceVersion", dataset.SourceVersion);
        command.Parameters.AddWithValue(
            "$sourceFingerprint",
            dataset.SourceFingerprint);
        command.Parameters.AddWithValue(
            "$sourceFilesJson",
            JsonSerializer.Serialize(dataset.SourceFiles, JsonOptions));
        command.Parameters.AddWithValue("$pointCount", dataset.PointCount);
        command.Parameters.AddWithValue(
            "$electricalPointCount",
            dataset.ElectricalPointCount);
        command.Parameters.AddWithValue(
            "$hydraulicPointCount",
            dataset.HydraulicPointCount);
        command.Parameters.AddWithValue(
            "$componentCount",
            dataset.ComponentCount);
        command.Parameters.AddWithValue(
            "$semanticLayerCount",
            dataset.SemanticLayerCount);
        command.Parameters.AddWithValue(
            "$directionCount",
            dataset.DirectionCount);
        command.Parameters.AddWithValue(
            "$directionReviewCount",
            dataset.DirectionReviewCount);
        command.Parameters.AddWithValue(
            "$builderValidatedDirectionCount",
            dataset.BuilderValidatedDirectionCount);
        command.Parameters.AddWithValue(
            "$symmetryInferredDirectionCount",
            dataset.SymmetryInferredDirectionCount);
        command.Parameters.AddWithValue(
            "$matchesHistoricalBaseline",
            dataset.MatchesHistoricalBaseline ? 1 : 0);
        command.Parameters.AddWithValue(
            "$importedAt",
            dataset.ImportedAt.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue(
            "$updatedAt",
            dataset.UpdatedAt.UtcDateTime.ToString("O"));
        command.ExecuteNonQuery();
    }

    private static void InsertPoint(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SemanticPoint point)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO SemanticPoints (
                Id,
                DatasetId,
                ExternalId,
                Handle,
                Discipline,
                SemanticCode,
                SemanticLayer,
                ReviewStatus,
                ReviewNote,
                ReviewedAt,
                PayloadJson,
                UpdatedAt)
            VALUES (
                $id,
                $datasetId,
                $externalId,
                $handle,
                $discipline,
                $semanticCode,
                $semanticLayer,
                $reviewStatus,
                $reviewNote,
                $reviewedAt,
                $payloadJson,
                $updatedAt);
            """;
        command.Parameters.AddWithValue("$id", point.Id.ToString("D"));
        command.Parameters.AddWithValue(
            "$datasetId",
            point.DatasetId.ToString("D"));
        command.Parameters.AddWithValue("$externalId", point.ExternalId);
        command.Parameters.AddWithValue("$handle", point.Handle);
        command.Parameters.AddWithValue("$discipline", (int)point.Discipline);
        command.Parameters.AddWithValue("$semanticCode", point.SemanticCode);
        command.Parameters.AddWithValue("$semanticLayer", point.SemanticLayer);
        command.Parameters.AddWithValue(
            "$reviewStatus",
            (int)point.ReviewStatus);
        command.Parameters.AddWithValue(
            "$reviewNote",
            point.ReviewNote ?? (object)DBNull.Value);
        command.Parameters.AddWithValue(
            "$reviewedAt",
            point.ReviewedAt?.UtcDateTime.ToString("O")
                ?? (object)DBNull.Value);
        command.Parameters.AddWithValue(
            "$payloadJson",
            JsonSerializer.Serialize(point, JsonOptions));
        command.Parameters.AddWithValue(
            "$updatedAt",
            DateTimeOffset.UtcNow.UtcDateTime.ToString("O"));
        command.ExecuteNonQuery();
    }

    private static void InsertComponent(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SemanticComponent component)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO SemanticComponents (
                Id,
                DatasetId,
                ExternalId,
                PointExternalId,
                ComponentClass,
                PayloadJson)
            VALUES (
                $id,
                $datasetId,
                $externalId,
                $pointExternalId,
                $componentClass,
                $payloadJson);
            """;
        command.Parameters.AddWithValue("$id", component.Id.ToString("D"));
        command.Parameters.AddWithValue(
            "$datasetId",
            component.DatasetId.ToString("D"));
        command.Parameters.AddWithValue("$externalId", component.ExternalId);
        command.Parameters.AddWithValue(
            "$pointExternalId",
            component.PointExternalId);
        command.Parameters.AddWithValue(
            "$componentClass",
            component.ComponentClass);
        command.Parameters.AddWithValue(
            "$payloadJson",
            JsonSerializer.Serialize(component, JsonOptions));
        command.ExecuteNonQuery();
    }

    private static void InsertDirection(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SemanticDirectionDiagnostic direction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO SemanticDirectionDiagnostics (
                Id,
                DatasetId,
                Handle,
                SuggestedDirection,
                NeedsReview,
                OffsetOrigin,
                PayloadJson,
                ReviewStatus,
                CorrectedDirection,
                ReviewNote,
                ReviewedAt)
            VALUES (
                $id,
                $datasetId,
                $handle,
                $suggestedDirection,
                $needsReview,
                $offsetOrigin,
                $payloadJson,
                $reviewStatus,
                $correctedDirection,
                $reviewNote,
                $reviewedAt);
            """;
        command.Parameters.AddWithValue("$id", direction.Id.ToString("D"));
        command.Parameters.AddWithValue(
            "$datasetId",
            direction.DatasetId.ToString("D"));
        command.Parameters.AddWithValue("$handle", direction.Handle);
        command.Parameters.AddWithValue(
            "$suggestedDirection",
            direction.SuggestedBuilderDirection);
        command.Parameters.AddWithValue(
            "$needsReview",
            direction.NeedsReview ? 1 : 0);
        command.Parameters.AddWithValue(
            "$offsetOrigin",
            direction.OffsetOrigin);
        command.Parameters.AddWithValue(
            "$payloadJson",
            JsonSerializer.Serialize(direction, JsonOptions));
        command.Parameters.AddWithValue(
            "$reviewStatus",
            (int)direction.ReviewStatus);
        command.Parameters.AddWithValue(
            "$correctedDirection",
            direction.CorrectedBuilderDirection ?? (object)DBNull.Value);
        command.Parameters.AddWithValue(
            "$reviewNote",
            direction.ReviewNote ?? (object)DBNull.Value);
        command.Parameters.AddWithValue(
            "$reviewedAt",
            direction.ReviewedAt?.UtcDateTime.ToString("O")
                ?? (object)DBNull.Value);
        command.ExecuteNonQuery();
    }

    private static SemanticPoint ReadPointById(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid projectId,
        Guid pointId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT
                p.PayloadJson,
                p.ReviewStatus,
                p.ReviewNote,
                p.ReviewedAt
            FROM SemanticPoints p
            INNER JOIN SemanticDatasets d ON d.Id = p.DatasetId
            WHERE p.Id = $pointId
              AND d.ProjectId = $projectId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$pointId", pointId.ToString("D"));
        command.Parameters.AddWithValue("$projectId", projectId.ToString("D"));
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new KeyNotFoundException(
                "O ponto semântico não pertence ao projeto ativo.");
        }

        var point = Deserialize<SemanticPoint>(
            reader.GetString(0),
            "ponto semântico");
        return point with
        {
            ReviewStatus = ReadEnum<SemanticReviewStatus>(
                reader.GetInt32(1),
                "estado de revisão"),
            ReviewNote = reader.IsDBNull(2) ? null : reader.GetString(2),
            ReviewedAt =
                reader.IsDBNull(3) ? null : ParseDate(reader.GetString(3))
        };
    }

    private static SemanticDirectionDiagnostic ReadDirectionById(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid projectId,
        Guid directionId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT
                x.PayloadJson,
                x.ReviewStatus,
                x.CorrectedDirection,
                x.ReviewNote,
                x.ReviewedAt
            FROM SemanticDirectionDiagnostics x
            INNER JOIN SemanticDatasets d ON d.Id = x.DatasetId
            WHERE x.Id = $directionId
              AND d.ProjectId = $projectId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue(
            "$directionId",
            directionId.ToString("D"));
        command.Parameters.AddWithValue("$projectId", projectId.ToString("D"));
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new KeyNotFoundException(
                "O diagnóstico de direção não pertence ao projeto ativo.");
        }

        var direction = Deserialize<SemanticDirectionDiagnostic>(
            reader.GetString(0),
            "diagnóstico de direção");
        return direction with
        {
            ReviewStatus = ReadEnum<SemanticReviewStatus>(
                reader.GetInt32(1),
                "estado de revisão da direção"),
            CorrectedBuilderDirection =
                reader.IsDBNull(2) ? null : reader.GetString(2),
            ReviewNote =
                reader.IsDBNull(3) ? null : reader.GetString(3),
            ReviewedAt =
                reader.IsDBNull(4) ? null : ParseDate(reader.GetString(4))
        };
    }

    private static void UpdatePointPayload(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SemanticPoint point,
        DateTimeOffset updatedAt)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE SemanticPoints
            SET Discipline = $discipline,
                SemanticCode = $semanticCode,
                SemanticLayer = $semanticLayer,
                ReviewStatus = $reviewStatus,
                ReviewNote = $reviewNote,
                ReviewedAt = $reviewedAt,
                PayloadJson = $payloadJson,
                UpdatedAt = $updatedAt
            WHERE Id = $id
              AND DatasetId = $datasetId;
            """;
        command.Parameters.AddWithValue("$discipline", (int)point.Discipline);
        command.Parameters.AddWithValue("$semanticCode", point.SemanticCode);
        command.Parameters.AddWithValue("$semanticLayer", point.SemanticLayer);
        command.Parameters.AddWithValue(
            "$reviewStatus",
            (int)point.ReviewStatus);
        command.Parameters.AddWithValue(
            "$reviewNote",
            point.ReviewNote ?? (object)DBNull.Value);
        command.Parameters.AddWithValue(
            "$reviewedAt",
            point.ReviewedAt?.UtcDateTime.ToString("O")
                ?? (object)DBNull.Value);
        command.Parameters.AddWithValue(
            "$payloadJson",
            JsonSerializer.Serialize(point, JsonOptions));
        command.Parameters.AddWithValue(
            "$updatedAt",
            updatedAt.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$id", point.Id.ToString("D"));
        command.Parameters.AddWithValue(
            "$datasetId",
            point.DatasetId.ToString("D"));
        if (command.ExecuteNonQuery() != 1)
        {
            throw new KeyNotFoundException(
                "O ponto semântico não pôde ser atualizado.");
        }
    }

    private static void UpdateDirectionPayload(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SemanticDirectionDiagnostic direction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE SemanticDirectionDiagnostics
            SET ReviewStatus = $reviewStatus,
                CorrectedDirection = $correctedDirection,
                ReviewNote = $reviewNote,
                ReviewedAt = $reviewedAt,
                PayloadJson = $payloadJson
            WHERE Id = $id
              AND DatasetId = $datasetId;
            """;
        command.Parameters.AddWithValue(
            "$reviewStatus",
            (int)direction.ReviewStatus);
        command.Parameters.AddWithValue(
            "$correctedDirection",
            direction.CorrectedBuilderDirection ?? (object)DBNull.Value);
        command.Parameters.AddWithValue(
            "$reviewNote",
            direction.ReviewNote ?? (object)DBNull.Value);
        command.Parameters.AddWithValue(
            "$reviewedAt",
            direction.ReviewedAt?.UtcDateTime.ToString("O")
                ?? (object)DBNull.Value);
        command.Parameters.AddWithValue(
            "$payloadJson",
            JsonSerializer.Serialize(direction, JsonOptions));
        command.Parameters.AddWithValue("$id", direction.Id.ToString("D"));
        command.Parameters.AddWithValue(
            "$datasetId",
            direction.DatasetId.ToString("D"));
        if (command.ExecuteNonQuery() != 1)
        {
            throw new KeyNotFoundException(
                "O diagnóstico de direção não pôde ser atualizado.");
        }
    }

    private static void InsertRevision<T>(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid datasetId,
        SemanticReviewEntityKind entityKind,
        Guid entityId,
        string entityExternalId,
        string action,
        string summary,
        string? note,
        T before,
        T after,
        DateTimeOffset occurredAt)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO SemanticReviewRevisions (
                Id,
                DatasetId,
                EntityKind,
                EntityId,
                EntityExternalId,
                Action,
                Summary,
                Note,
                BeforeJson,
                AfterJson,
                OccurredAt,
                RevertedAt)
            VALUES (
                $id,
                $datasetId,
                $entityKind,
                $entityId,
                $entityExternalId,
                $action,
                $summary,
                $note,
                $beforeJson,
                $afterJson,
                $occurredAt,
                NULL);
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue(
            "$datasetId",
            datasetId.ToString("D"));
        command.Parameters.AddWithValue("$entityKind", (int)entityKind);
        command.Parameters.AddWithValue("$entityId", entityId.ToString("D"));
        command.Parameters.AddWithValue(
            "$entityExternalId",
            entityExternalId);
        command.Parameters.AddWithValue("$action", action);
        command.Parameters.AddWithValue("$summary", summary);
        command.Parameters.AddWithValue("$note", note ?? (object)DBNull.Value);
        command.Parameters.AddWithValue(
            "$beforeJson",
            JsonSerializer.Serialize(before, JsonOptions));
        command.Parameters.AddWithValue(
            "$afterJson",
            JsonSerializer.Serialize(after, JsonOptions));
        command.Parameters.AddWithValue(
            "$occurredAt",
            occurredAt.UtcDateTime.ToString("O"));
        command.ExecuteNonQuery();
    }

    private static PersistedRevision? ReadLatestActiveRevision(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid projectId,
        SemanticReviewEntityKind entityKind,
        Guid entityId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT
                r.Id,
                r.BeforeJson,
                r.Action
            FROM SemanticReviewRevisions r
            INNER JOIN SemanticDatasets d ON d.Id = r.DatasetId
            WHERE d.ProjectId = $projectId
              AND r.EntityKind = $entityKind
              AND r.EntityId = $entityId
              AND r.RevertedAt IS NULL
            ORDER BY r.OccurredAt DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$projectId", projectId.ToString("D"));
        command.Parameters.AddWithValue("$entityKind", (int)entityKind);
        command.Parameters.AddWithValue("$entityId", entityId.ToString("D"));
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new PersistedRevision(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2))
            : null;
    }

    private static void MarkRevisionReverted(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid revisionId,
        DateTimeOffset revertedAt)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE SemanticReviewRevisions
            SET RevertedAt = $revertedAt
            WHERE Id = $id
              AND RevertedAt IS NULL;
            """;
        command.Parameters.AddWithValue("$id", revisionId.ToString("D"));
        command.Parameters.AddWithValue(
            "$revertedAt",
            revertedAt.UtcDateTime.ToString("O"));
        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException(
                "A revisão já foi desfeita ou não existe.");
        }
    }

    private static void UpsertKnowledge(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid projectId,
        SemanticPoint before,
        SemanticPoint after,
        int evidenceDelta,
        DateTimeOffset occurredAt)
    {
        if (evidenceDelta < 1)
        {
            return;
        }

        var signature = BuildSignature(before);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO SemanticKnowledgeEntries (
                Id,
                ProjectId,
                DatasetId,
                SourcePointId,
                Signature,
                BlockName,
                SemanticLayer,
                PreviousCode,
                LearnedCode,
                LearnedDescription,
                LearnedHeight,
                EvidenceCount,
                CreatedAt,
                UpdatedAt)
            VALUES (
                $id,
                $projectId,
                $datasetId,
                $sourcePointId,
                $signature,
                $blockName,
                $semanticLayer,
                $previousCode,
                $learnedCode,
                $learnedDescription,
                $learnedHeight,
                $evidenceCount,
                $createdAt,
                $updatedAt)
            ON CONFLICT(ProjectId, Signature, LearnedCode) DO UPDATE SET
                DatasetId = excluded.DatasetId,
                SourcePointId = excluded.SourcePointId,
                LearnedDescription = excluded.LearnedDescription,
                LearnedHeight = excluded.LearnedHeight,
                EvidenceCount =
                    SemanticKnowledgeEntries.EvidenceCount
                    + excluded.EvidenceCount,
                UpdatedAt = excluded.UpdatedAt;
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$projectId", projectId.ToString("D"));
        command.Parameters.AddWithValue(
            "$datasetId",
            after.DatasetId.ToString("D"));
        command.Parameters.AddWithValue(
            "$sourcePointId",
            after.Id.ToString("D"));
        command.Parameters.AddWithValue("$signature", signature);
        command.Parameters.AddWithValue(
            "$blockName",
            before.BlockName);
        command.Parameters.AddWithValue(
            "$semanticLayer",
            before.SemanticLayer);
        command.Parameters.AddWithValue(
            "$previousCode",
            before.SemanticCode);
        command.Parameters.AddWithValue(
            "$learnedCode",
            after.SemanticCode);
        command.Parameters.AddWithValue(
            "$learnedDescription",
            after.Description);
        command.Parameters.AddWithValue(
            "$learnedHeight",
            after.HeightSourceValue);
        command.Parameters.AddWithValue("$evidenceCount", evidenceDelta);
        command.Parameters.AddWithValue(
            "$createdAt",
            occurredAt.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue(
            "$updatedAt",
            occurredAt.UtcDateTime.ToString("O"));
        command.ExecuteNonQuery();
    }

    private static void DeleteKnowledgeForSource(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid projectId,
        Guid sourcePointId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            DELETE FROM SemanticKnowledgeEntries
            WHERE ProjectId = $projectId
              AND SourcePointId = $sourcePointId;
            """;
        command.Parameters.AddWithValue("$projectId", projectId.ToString("D"));
        command.Parameters.AddWithValue(
            "$sourcePointId",
            sourcePointId.ToString("D"));
        command.ExecuteNonQuery();
    }

    private static void ValidateProjectAndEntity(
        Guid projectId,
        Guid entityId)
    {
        if (projectId == Guid.Empty || entityId == Guid.Empty)
        {
            throw new ArgumentException(
                "Projeto e item semântico são obrigatórios.");
        }
    }

    private static string NormalizeRequired(
        string? value,
        int maximumLength,
        string error)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException(error);
        }

        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"{error} Limite: {maximumLength} caracteres.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(
        string? value,
        int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"O texto excede {maximumLength} caracteres.");
        }

        return normalized;
    }

    private static double? ParseOptionalHeight(string value)
    {
        if (double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var invariant)
            && double.IsFinite(invariant))
        {
            return invariant;
        }

        if (double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.GetCultureInfo("pt-BR"),
                out var local)
            && double.IsFinite(local))
        {
            return local;
        }

        return null;
    }

    private static string BuildPointCorrectionSummary(
        SemanticPoint before,
        SemanticPoint after)
    {
        var changes = new List<string>();
        if (before.Discipline != after.Discipline)
        {
            changes.Add(
                $"{GetDisciplineLabel(before.Discipline)} → "
                + GetDisciplineLabel(after.Discipline));
        }

        if (!before.SemanticCode.Equals(
                after.SemanticCode,
                StringComparison.Ordinal))
        {
            changes.Add($"{before.SemanticCode} → {after.SemanticCode}");
        }

        if (!before.Description.Equals(
                after.Description,
                StringComparison.Ordinal))
        {
            changes.Add("descrição atualizada");
        }

        if (!before.HeightSourceValue.Equals(
                after.HeightSourceValue,
                StringComparison.OrdinalIgnoreCase))
        {
            changes.Add(
                $"altura {before.HeightSourceValue} → "
                + after.HeightSourceValue);
        }

        return changes.Count == 0
            ? "Revisão confirmada sem mudança de classificação."
            : string.Join(" • ", changes);
    }

    private static string BuildSignature(SemanticPoint point)
    {
        var block = string.IsNullOrWhiteSpace(point.BlockName)
            ? "(SEM_BLOCO)"
            : point.BlockName.Trim().ToUpperInvariant();
        return $"BLOCO:{block}|LAYER:"
            + point.SemanticLayer.Trim().ToUpperInvariant();
    }

    private static double DistanceSquared(
        SemanticPoint source,
        SemanticPoint candidate)
    {
        var dx = candidate.CenterX - source.CenterX;
        var dy = candidate.CenterY - source.CenterY;
        return (dx * dx) + (dy * dy);
    }

    private static string GetReviewLabel(
        SemanticReviewStatus status) => status switch
    {
        SemanticReviewStatus.NeedsReview => "Revisar",
        SemanticReviewStatus.Corrected => "Corrigido",
        SemanticReviewStatus.Approved => "Aprovado",
        _ => "Identificado"
    };

    private static string GetDisciplineLabel(
        SemanticDiscipline discipline) => discipline switch
    {
        SemanticDiscipline.Electrical => "Elétrico",
        SemanticDiscipline.Hydraulic => "Hidráulico",
        _ => "Não identificado"
    };

    private static void ValidatePackage(SemanticImportPackage package)
    {
        var dataset = package.Dataset;
        if (dataset.Id == Guid.Empty
            || dataset.ProjectId == Guid.Empty
            || string.IsNullOrWhiteSpace(dataset.DrawingFileName)
            || string.IsNullOrWhiteSpace(dataset.DrawingPath)
            || string.IsNullOrWhiteSpace(dataset.SourceFingerprint)
            || package.Points.Count != dataset.PointCount
            || package.Components.Count != dataset.ComponentCount
            || package.Directions.Count != dataset.DirectionCount)
        {
            throw new InvalidDataException(
                "O pacote semântico não atende ao contrato de persistência.");
        }
    }

    private static T Deserialize<T>(string json, string label)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions)
                ?? throw new InvalidDataException(
                    $"O {label} persistido está vazio.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"O {label} persistido é inválido e foi preservado.",
                exception);
        }
    }

    private static TEnum ReadEnum<TEnum>(int value, string label)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(typeof(TEnum), value))
        {
            throw new InvalidDataException(
                $"O {label} persistido é desconhecido.");
        }

        return (TEnum)(object)value;
    }

    private static DateTimeOffset ParseDate(string value) =>
        DateTimeOffset.Parse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);

    private sealed record ExistingDataset(
        Guid DatasetId,
        DateTimeOffset ImportedAt);

    private sealed record PersistedReview(
        Guid PointId,
        SemanticReviewStatus Status,
        string? Note,
        DateTimeOffset? ReviewedAt,
        SemanticPoint CurrentPoint);

    private sealed record PersistedDirection(
        SemanticDirectionDiagnostic Direction);

    private sealed record PersistedRevision(
        Guid Id,
        string BeforeJson,
        string Action);
}
