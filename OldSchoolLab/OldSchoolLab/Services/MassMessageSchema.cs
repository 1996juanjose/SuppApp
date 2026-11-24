using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Data;

namespace OldSchoolLab.Services;

public static class MassMessageSchema
{
    public static Task EnsureAsync(ApplicationDbContext db) => db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "MassMessageCampaigns" (
          "Id" uuid PRIMARY KEY, "CompanyId" integer NOT NULL, "UserId" varchar(450) NOT NULL,
          "UserName" varchar(120) NOT NULL, "Name" varchar(120) NOT NULL, "Message" varchar(4000) NOT NULL,
          "IntervalSeconds" integer NOT NULL CHECK ("IntervalSeconds" BETWEEN 1 AND 86400),
          "Status" varchar(20) NOT NULL, "SenderKey" varchar(600) NOT NULL,
          "Notice" varchar(250) NOT NULL, "CreatedAt" timestamptz NOT NULL);
        CREATE TABLE IF NOT EXISTS "MassMessageRecipients" (
          "Id" uuid PRIMARY KEY, "CampaignId" uuid NOT NULL REFERENCES "MassMessageCampaigns"("Id"),
          "Position" integer NOT NULL, "RecordId" integer NULL, "Name" varchar(120) NOT NULL,
          "Phone" varchar(15) NOT NULL, "Status" varchar(20) NOT NULL,
          "MessageId" varchar(150) NOT NULL, "Detail" varchar(250) NOT NULL, "AttemptedAt" timestamptz NULL);
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_MassMessageRecipients_CampaignId_Phone"
          ON "MassMessageRecipients" ("CampaignId","Phone");
        CREATE TABLE IF NOT EXISTS "MassMessageSenders" (
          "Key" varchar(600) PRIMARY KEY, "NextSendAt" timestamptz NOT NULL);
        """);
}
