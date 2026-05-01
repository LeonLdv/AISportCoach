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
    VALUES ('20260320191805_InitialCreate', '10.0.7');
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
    VALUES ('20260327191635_AddVideoFileUri', '10.0.7');
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
    VALUES ('20260327193037_RemoveJobFrameTracking', '10.0.7');
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
    VALUES ('20260331174107_AddNtrpRating', '10.0.7');
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
    VALUES ('20260401160135_RemoveStoragePath', '10.0.7');
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
    VALUES ('20260401161843_RemoveAnalysisJobs', '10.0.7');
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
    ALTER TABLE "ReportEmbeddings" ADD COLUMN "Embedding" vector(768) NOT NULL DEFAULT '[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]'::vector
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
    VALUES ('20260410150000_AddReportEmbeddings', '10.0.7');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260417115213_RemovePlayerSkillLevel') THEN
    ALTER TABLE "CoachingReports" DROP COLUMN "PlayerSkillLevel";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260417115213_RemovePlayerSkillLevel') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260417115213_RemovePlayerSkillLevel', '10.0.7');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260419182703_AddAuditFields') THEN
    ALTER TABLE "VideoUploads" RENAME COLUMN "UploadedAt" TO "CreatedAt";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260419182703_AddAuditFields') THEN
    ALTER TABLE "CoachingReports" RENAME COLUMN "GeneratedAt" TO "CreatedAt";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260419182703_AddAuditFields') THEN
    ALTER TABLE "VideoUploads" ADD "CreatedBy" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260419182703_AddAuditFields') THEN
    ALTER TABLE "VideoUploads" ADD "LastModifiedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260419182703_AddAuditFields') THEN
    ALTER TABLE "VideoUploads" ADD "LastModifiedBy" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260419182703_AddAuditFields') THEN
    ALTER TABLE "ReportEmbeddings" ADD "CreatedBy" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260419182703_AddAuditFields') THEN
    ALTER TABLE "ReportEmbeddings" ADD "LastModifiedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260419182703_AddAuditFields') THEN
    ALTER TABLE "ReportEmbeddings" ADD "LastModifiedBy" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260419182703_AddAuditFields') THEN
    ALTER TABLE "CoachingReports" ADD "CreatedBy" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260419182703_AddAuditFields') THEN
    ALTER TABLE "CoachingReports" ADD "LastModifiedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260419182703_AddAuditFields') THEN
    ALTER TABLE "CoachingReports" ADD "LastModifiedBy" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260419182703_AddAuditFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260419182703_AddAuditFields', '10.0.7');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260421182506_ReplaceIvfflatWithHnsw') THEN
    DROP INDEX IF EXISTS "IX_ReportEmbeddings_Embedding";
    CREATE INDEX ON "ReportEmbeddings" USING hnsw ("Embedding" vector_cosine_ops);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260421182506_ReplaceIvfflatWithHnsw') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260421182506_ReplaceIvfflatWithHnsw', '10.0.7');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426121453_AddCoachingReportsCreatedAtIndex') THEN
    CREATE INDEX idx_coaching_reports_created_at ON "CoachingReports" ("CreatedAt" DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426121453_AddCoachingReportsCreatedAtIndex') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260426121453_AddCoachingReportsCreatedAtIndex', '10.0.7');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430095110_AddAuthentication') THEN
    CREATE TABLE "AspNetRoles" (
        "Id" uuid NOT NULL,
        "Name" character varying(256),
        "NormalizedName" character varying(256),
        "ConcurrencyStamp" text,
        CONSTRAINT "PK_AspNetRoles" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430095110_AddAuthentication') THEN
    CREATE TABLE "AspNetUsers" (
        "Id" uuid NOT NULL,
        "UserName" character varying(256),
        "NormalizedUserName" character varying(256),
        "Email" character varying(256),
        "NormalizedEmail" character varying(256),
        "EmailConfirmed" boolean NOT NULL,
        "PasswordHash" text,
        "SecurityStamp" text,
        "ConcurrencyStamp" text,
        "PhoneNumber" text,
        "PhoneNumberConfirmed" boolean NOT NULL,
        "TwoFactorEnabled" boolean NOT NULL,
        "LockoutEnd" timestamp with time zone,
        "LockoutEnabled" boolean NOT NULL,
        "AccessFailedCount" integer NOT NULL,
        CONSTRAINT "PK_AspNetUsers" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430095110_AddAuthentication') THEN
    CREATE TABLE "AspNetRoleClaims" (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "RoleId" uuid NOT NULL,
        "ClaimType" text,
        "ClaimValue" text,
        CONSTRAINT "PK_AspNetRoleClaims" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AspNetRoleClaims_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430095110_AddAuthentication') THEN
    CREATE TABLE "AspNetUserClaims" (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "UserId" uuid NOT NULL,
        "ClaimType" text,
        "ClaimValue" text,
        CONSTRAINT "PK_AspNetUserClaims" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AspNetUserClaims_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430095110_AddAuthentication') THEN
    CREATE TABLE "AspNetUserLogins" (
        "LoginProvider" text NOT NULL,
        "ProviderKey" text NOT NULL,
        "ProviderDisplayName" text,
        "UserId" uuid NOT NULL,
        CONSTRAINT "PK_AspNetUserLogins" PRIMARY KEY ("LoginProvider", "ProviderKey"),
        CONSTRAINT "FK_AspNetUserLogins_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430095110_AddAuthentication') THEN
    CREATE TABLE "AspNetUserRoles" (
        "UserId" uuid NOT NULL,
        "RoleId" uuid NOT NULL,
        CONSTRAINT "PK_AspNetUserRoles" PRIMARY KEY ("UserId", "RoleId"),
        CONSTRAINT "FK_AspNetUserRoles_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_AspNetUserRoles_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430095110_AddAuthentication') THEN
    CREATE TABLE "AspNetUserTokens" (
        "UserId" uuid NOT NULL,
        "LoginProvider" text NOT NULL,
        "Name" text NOT NULL,
        "Value" text,
        CONSTRAINT "PK_AspNetUserTokens" PRIMARY KEY ("UserId", "LoginProvider", "Name"),
        CONSTRAINT "FK_AspNetUserTokens_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430095110_AddAuthentication') THEN
    CREATE TABLE "RefreshTokens" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Token" character varying(256) NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "RevokedAt" timestamp with time zone,
        "RevokedByIp" character varying(45),
        "ReplacedByToken" character varying(256),
        "CreatedByIp" character varying(45) NOT NULL,
        CONSTRAINT "PK_RefreshTokens" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_RefreshTokens_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430095110_AddAuthentication') THEN
    CREATE TABLE "UserProfiles" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "DisplayName" character varying(100) NOT NULL,
        "SubscriptionTier" text NOT NULL,
        "ProfileImageUrl" character varying(2048),
        "LastLoginAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_UserProfiles" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_UserProfiles_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430095110_AddAuthentication') THEN
    CREATE TABLE "WebAuthnCredentials" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "CredentialId" bytea NOT NULL,
        "PublicKey" bytea NOT NULL,
        "SignatureCounter" bigint NOT NULL,
        "DeviceInfo" character varying(1000) NOT NULL,
        "RegisteredAt" timestamp with time zone NOT NULL,
        "LastUsedAt" timestamp with time zone NOT NULL,
        "IsActive" boolean NOT NULL,
        CONSTRAINT "PK_WebAuthnCredentials" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_WebAuthnCredentials_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430095110_AddAuthentication') THEN
    INSERT INTO "AspNetUsers" ("Id", "UserName", "NormalizedUserName", "Email", "NormalizedEmail", "EmailConfirmed", "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "PhoneNumber", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnd", "LockoutEnabled", "AccessFailedCount")
    VALUES ('00000000-0000-0000-0000-000000000001', 'system@aisportcoach.com', 'SYSTEM@AISPORTCOACH.COM', 'system@aisportcoach.com', 'SYSTEM@AISPORTCOACH.COM', TRUE, 'SYSTEM_ACCOUNT_NO_LOGIN', '0d7d1f50-e2b3-4e93-a228-956b42bac231', '4594eb5f-43d4-4aa6-bb40-9bbee668985e', NULL, FALSE, FALSE, NULL, FALSE, 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430095110_AddAuthentication') THEN
    INSERT INTO "UserProfiles" ("Id", "UserId", "DisplayName", "SubscriptionTier", "ProfileImageUrl", "LastLoginAt", "CreatedAt", "UpdatedAt")
    VALUES ('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000001', 'System Account', 'Admin', NULL, NULL, TIMESTAMPTZ '2026-05-01T09:13:25.611554Z', NULL);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430095110_AddAuthentication') THEN
    UPDATE "VideoUploads"
                       SET "UserId" = '00000000-0000-0000-0000-000000000001'
                       WHERE "UserId" IS NOT NULL OR "UserId" != '00000000-0000-0000-0000-000000000001'
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430095110_AddAuthentication') THEN
    CREATE INDEX "IX_VideoUploads_UserId" ON "VideoUploads" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430095110_AddAuthentication') THEN
    CREATE INDEX "IX_AspNetRoleClaims_RoleId" ON "AspNetRoleClaims" ("RoleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430095110_AddAuthentication') THEN
    CREATE UNIQUE INDEX "RoleNameIndex" ON "AspNetRoles" ("NormalizedName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430095110_AddAuthentication') THEN
    CREATE INDEX "IX_AspNetUserClaims_UserId" ON "AspNetUserClaims" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430095110_AddAuthentication') THEN
    CREATE INDEX "IX_AspNetUserLogins_UserId" ON "AspNetUserLogins" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430095110_AddAuthentication') THEN
    CREATE INDEX "IX_AspNetUserRoles_RoleId" ON "AspNetUserRoles" ("RoleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430095110_AddAuthentication') THEN
    CREATE INDEX "EmailIndex" ON "AspNetUsers" ("NormalizedEmail");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430095110_AddAuthentication') THEN
    CREATE UNIQUE INDEX "UserNameIndex" ON "AspNetUsers" ("NormalizedUserName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430095110_AddAuthentication') THEN
    CREATE UNIQUE INDEX "IX_RefreshTokens_Token" ON "RefreshTokens" ("Token");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430095110_AddAuthentication') THEN
    CREATE INDEX "IX_RefreshTokens_UserId_ExpiresAt" ON "RefreshTokens" ("UserId", "ExpiresAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430095110_AddAuthentication') THEN
    CREATE UNIQUE INDEX "IX_UserProfiles_UserId" ON "UserProfiles" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430095110_AddAuthentication') THEN
    CREATE INDEX "IX_WebAuthnCredentials_UserId_CredentialId" ON "WebAuthnCredentials" ("UserId", "CredentialId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430095110_AddAuthentication') THEN
    ALTER TABLE "VideoUploads" ADD CONSTRAINT "FK_VideoUploads_UserProfiles_UserId" FOREIGN KEY ("UserId") REFERENCES "UserProfiles" ("Id") ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430095110_AddAuthentication') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260430095110_AddAuthentication', '10.0.7');
    END IF;
END $EF$;
COMMIT;

