-- Apply after 001. Timestamps are UTC stored without a zone, normalized by the adapter.
BEGIN;
CREATE TABLE IF NOT EXISTS public."AuthenticationUsers" (
    "UserId" varchar(200) PRIMARY KEY,
    "UserJson" text NOT NULL,
    "Enabled" boolean NOT NULL);
CREATE TABLE IF NOT EXISTS public."AuthenticationSessions" (
    "SessionId" varchar(32) PRIMARY KEY,
    "UserId" varchar(200) NOT NULL REFERENCES public."AuthenticationUsers"("UserId"),
    "RefreshTokenId" varchar(32) NOT NULL,
    "ExpiresUtc" timestamp without time zone NOT NULL,
    "Revoked" boolean NOT NULL DEFAULT false);
CREATE INDEX IF NOT EXISTS "IX_AuthenticationSessions_Expiry" ON public."AuthenticationSessions"("ExpiresUtc");
COMMIT;
