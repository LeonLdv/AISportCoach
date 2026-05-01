CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260320191805_InitialCreate') THEN
    CREATE TABLE "VideoUploads" (
        "Id" uuid NOT NULL,
        "OriginalFileName" character varying(500) NOT NULL,
        "StoragePath" character varying(1000) NOT NULL,
        "FileSizeBytes" bigint NOT NULL,
        "Status" text NOT NULL,
        "UploadedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_VideoUploads" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260320191805_InitialCreate') THEN
    CREATE TABLE "AnalysisJobs" (
        "Id" uuid NOT NULL,
        "VideoUploadId" uuid NOT NULL,
        "Status" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "StartedAt" timestamp with time zone,
        "CompletedAt" timestamp with time zone,
        "ErrorMessage" text,
        "FramesExtracted" integer NOT NULL,
        "FramesAnalyzed" integer NOT NULL,
        "ProgressPercent" integer NOT NULL,
        CONSTRAINT "PK_AnalysisJobs" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AnalysisJobs_VideoUploads_VideoUploadId" FOREIGN KEY ("VideoUploadId") REFERENCES "VideoUploads" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260320191805_InitialCreate') THEN
    CREATE TABLE "CoachingReports" (
        "Id" uuid NOT NULL,
        "AnalysisJobId" uuid NOT NULL,
        "PlayerSkillLevel" text NOT NULL,
        "OverallScore" integer NOT NULL,
        "ExecutiveSummary" character varying(2000) NOT NULL,
        "GeneratedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_CoachingReports" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CoachingReports_AnalysisJobs_AnalysisJobId" FOREIGN KEY ("AnalysisJobId") REFERENCES "AnalysisJobs" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260320191805_InitialCreate') THEN
    CREATE TABLE "ImprovementRecommendations" (
        "Id" uuid NOT NULL,
        "CoachingReportId" uuid NOT NULL,
        "Title" character varying(500) NOT NULL,
        "DetailedDescription" character varying(2000) NOT NULL,
        "Priority" integer NOT NULL,
        "TargetStroke" text NOT NULL,
        "DrillSuggestions" jsonb NOT NULL,
        CONSTRAINT "PK_ImprovementRecommendations" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ImprovementRecommendations_CoachingReports_CoachingReportId" FOREIGN KEY ("CoachingReportId") REFERENCES "CoachingReports" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260320191805_InitialCreate') THEN
    CREATE TABLE "TechniqueObservations" (
        "Id" uuid NOT NULL,
        "CoachingReportId" uuid NOT NULL,
        "Stroke" text NOT NULL,
        "Description" character varying(1000) NOT NULL,
        "Severity" text NOT NULL,
        "FrameTimestamp" character varying(50) NOT NULL,
        "BodyPart" character varying(100),
        CONSTRAINT "PK_TechniqueObservations" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_TechniqueObservations_CoachingReports_CoachingReportId" FOREIGN KEY ("CoachingReportId") REFERENCES "CoachingReports" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260320191805_InitialCreate') THEN
    CREATE INDEX "IX_AnalysisJobs_VideoUploadId" ON "AnalysisJobs" ("VideoUploadId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260320191805_InitialCreate') THEN
    CREATE INDEX "IX_CoachingReports_AnalysisJobId" ON "CoachingReports" ("AnalysisJobId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260320191805_InitialCreate') THEN
    CREATE INDEX "IX_ImprovementRecommendations_CoachingReportId" ON "ImprovementRecommendations" ("CoachingReportId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260320191805_InitialCreate') THEN
    CREATE INDEX "IX_TechniqueObservations_CoachingReportId" ON "TechniqueObservations" ("CoachingReportId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260320191805_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260320191805_InitialCreate', '10.0.5');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260327191635_AddVideoFileUri') THEN
    ALTER TABLE "VideoUploads" ADD "GeminiFileUri" character varying(2000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260327191635_AddVideoFileUri') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260327191635_AddVideoFileUri', '10.0.5');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260327193037_RemoveJobFrameTracking') THEN
    ALTER TABLE "AnalysisJobs" DROP COLUMN "FramesAnalyzed";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260327193037_RemoveJobFrameTracking') THEN
    ALTER TABLE "AnalysisJobs" DROP COLUMN "FramesExtracted";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260327193037_RemoveJobFrameTracking') THEN
    ALTER TABLE "AnalysisJobs" DROP COLUMN "ProgressPercent";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260327193037_RemoveJobFrameTracking') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260327193037_RemoveJobFrameTracking', '10.0.5');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331174107_AddNtrpRating') THEN
    ALTER TABLE "CoachingReports" ADD "NtrpConfidence" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331174107_AddNtrpRating') THEN
    ALTER TABLE "CoachingReports" ADD "NtrpRating" double precision;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331174107_AddNtrpRating') THEN
    ALTER TABLE "CoachingReports" ADD "NtrpRatingJustification" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331174107_AddNtrpRating') THEN
    ALTER TABLE "CoachingReports" ADD "NtrpRatingMax" double precision;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331174107_AddNtrpRating') THEN
    ALTER TABLE "CoachingReports" ADD "NtrpRatingMin" double precision;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331174107_AddNtrpRating') THEN
    CREATE TABLE "NtrpEvidenceItems" (
        "Id" uuid NOT NULL,
        "CoachingReportId" uuid NOT NULL,
        "Observation" character varying(1000) NOT NULL,
        "NtrpIndicator" character varying(500) NOT NULL,
        "SupportedLevel" double precision NOT NULL,
        "Weight" character varying(20) NOT NULL,
        CONSTRAINT "PK_NtrpEvidenceItems" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_NtrpEvidenceItems_CoachingReports_CoachingReportId" FOREIGN KEY ("CoachingReportId") REFERENCES "CoachingReports" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331174107_AddNtrpRating') THEN
    CREATE INDEX "IX_NtrpEvidenceItems_CoachingReportId" ON "NtrpEvidenceItems" ("CoachingReportId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331174107_AddNtrpRating') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260331174107_AddNtrpRating', '10.0.5');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260401160135_RemoveStoragePath') THEN
    ALTER TABLE "VideoUploads" DROP COLUMN "StoragePath";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260401160135_RemoveStoragePath') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260401160135_RemoveStoragePath', '10.0.5');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260401161843_RemoveAnalysisJobs') THEN
    ALTER TABLE "CoachingReports" DROP CONSTRAINT "FK_CoachingReports_AnalysisJobs_AnalysisJobId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260401161843_RemoveAnalysisJobs') THEN
    DROP TABLE "AnalysisJobs";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260401161843_RemoveAnalysisJobs') THEN
    ALTER TABLE "CoachingReports" RENAME COLUMN "AnalysisJobId" TO "VideoUploadId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260401161843_RemoveAnalysisJobs') THEN
    ALTER INDEX "IX_CoachingReports_AnalysisJobId" RENAME TO "IX_CoachingReports_VideoUploadId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260401161843_RemoveAnalysisJobs') THEN
    ALTER TABLE "CoachingReports" ADD CONSTRAINT "FK_CoachingReports_VideoUploads_VideoUploadId" FOREIGN KEY ("VideoUploadId") REFERENCES "VideoUploads" ("Id") ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260401161843_RemoveAnalysisJobs') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260401161843_RemoveAnalysisJobs', '10.0.5');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410150000_AddReportEmbeddings') THEN
    CREATE EXTENSION IF NOT EXISTS vector
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410150000_AddReportEmbeddings') THEN
    ALTER TABLE "VideoUploads" ADD "UserId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410150000_AddReportEmbeddings') THEN
    CREATE TABLE "ReportEmbeddings" (
        "Id" uuid NOT NULL,
        "CoachingReportId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ReportEmbeddings" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ReportEmbeddings_CoachingReports_CoachingReportId" FOREIGN KEY ("CoachingReportId") REFERENCES "CoachingReports" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410150000_AddReportEmbeddings') THEN
    ALTER TABLE "ReportEmbeddings" ADD COLUMN "Embedding" vector(768) NOT NULL DEFAULT '[0]'::vector
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410150000_AddReportEmbeddings') THEN
    ALTER TABLE "ReportEmbeddings" ALTER COLUMN "Embedding" DROP DEFAULT
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410150000_AddReportEmbeddings') THEN
    CREATE INDEX ON "ReportEmbeddings" USING ivfflat ("Embedding" vector_cosine_ops)
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410150000_AddReportEmbeddings') THEN
    CREATE INDEX "IX_ReportEmbeddings_CoachingReportId" ON "ReportEmbeddings" ("CoachingReportId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410150000_AddReportEmbeddings') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260410150000_AddReportEmbeddings', '10.0.5');
    END IF;
END $EF$;
COMMIT;

