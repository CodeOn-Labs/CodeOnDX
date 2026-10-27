-- =============================================================================
-- V013 - Who wrote the justification of a late milestone, and when.
-- Shown next to the reason on the project timeline; updated_by / updated_at are
-- not enough because renaming or moving the milestone also changes them.
-- =============================================================================

ALTER TABLE dx.project_milestones
    ADD COLUMN justification_by  uuid,
    ADD COLUMN justification_at  timestamptz;

ALTER TABLE dx.project_milestones
    ADD CONSTRAINT fk_project_milestones_justification_by FOREIGN KEY (justification_by) REFERENCES dx.users (id) ON DELETE SET NULL;
