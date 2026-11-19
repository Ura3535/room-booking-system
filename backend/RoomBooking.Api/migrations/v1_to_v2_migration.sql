-- Міграція БД RoomBooking з версії v1.0 до v2.0 (задача KAN-13)
-- Додає колонки аудиту адміністративного погодження зі збереженням усіх існуючих записів

BEGIN;

ALTER TABLE "Bookings"
    ADD COLUMN IF NOT EXISTS "ReviewedByUserId" integer NULL,
    ADD COLUMN IF NOT EXISTS "ReviewedAt" timestamp with time zone NULL,
    ADD COLUMN IF NOT EXISTS "ReviewComment" text NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_Bookings_Users_ReviewedByUserId'
    ) THEN
        ALTER TABLE "Bookings"
            ADD CONSTRAINT "FK_Bookings_Users_ReviewedByUserId"
            FOREIGN KEY ("ReviewedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT;
    END IF;
END $$;

COMMIT;