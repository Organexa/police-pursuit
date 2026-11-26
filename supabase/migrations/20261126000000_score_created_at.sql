-- Upload time, for submit-score's rate limit (played_at comes from the phone).
alter table public.scores add column created_at timestamptz not null default now();
create index scores_recent_idx on public.scores (user_id, created_at);
