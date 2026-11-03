using Microsoft.EntityFrameworkCore;
using OldSchoolApi.Data;
namespace OldSchoolApi.Services;
public static class LeadCallsSchema
{
    public static Task EnsureAsync(ApiDbContext db) => db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "LeadCallAttempts" (
                "Id" uuid PRIMARY KEY,
                "CompanyId" integer NOT NULL,
                "RecordId" integer NOT NULL REFERENCES "CustomerRecords" ("Id") ON DELETE CASCADE,
                "CreatedAt" timestamp without time zone NOT NULL,
                "Outcome" character varying(40) NOT NULL,
                "Notes" character varying(250) NOT NULL DEFAULT '',
                "CalledByUserId" character varying(450) NOT NULL DEFAULT '',
                "CalledByUserName" character varying(120) NOT NULL DEFAULT '',
                "NextCallAt" timestamp without time zone NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_LeadCallAttempts_CompanyId_RecordId_CreatedAt"
                ON "LeadCallAttempts" ("CompanyId", "RecordId", "CreatedAt");
        """);
}
