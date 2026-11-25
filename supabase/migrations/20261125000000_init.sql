-- Police Pursuit schema (TRD §2, SECURITY-AND-ACCESS). RLS on every table; grants are explicit.

-- Admin = listed in admins AND signed in with 2FA (aal2).
create table public.admins (
  user_id uuid primary key references auth.users on delete cascade
);

create function public.is_admin() returns boolean
language sql stable security definer set search_path = '' as $$
  select coalesce(auth.jwt() ->> 'aal', '') = 'aal2'
     and exists (select 1 from public.admins where user_id = auth.uid());
$$;

create table public.profiles (
  id uuid primary key references auth.users on delete cascade,
  display_name text not null check (char_length(display_name) between 1 and 20),
  created_at timestamptz not null default now(),
  last_seen_at timestamptz not null default now()
);

create table public.scores (
  id bigint generated always as identity primary key,
  user_id uuid not null references public.profiles on delete cascade,
  duration_s double precision not null check (duration_s between 0 and 3600),
  distance_m double precision not null check (distance_m >= 0),
  near_misses int not null check (near_misses >= 0),
  max_wanted smallint not null check (max_wanted between 1 and 5),
  car_id text not null,
  played_at timestamptz not null,
  -- Server is the single source of truth for the score formula (TRD §2).
  score bigint generated always as (
    (floor(distance_m) + near_misses * 250 + floor(duration_s * max_wanted * 10))::bigint
  ) stored,
  hidden boolean not null default false,
  hidden_reason text,
  unique (user_id, played_at)
);
create index scores_best_idx on public.scores (user_id, score desc) where not hidden;

create table public.saves (
  user_id uuid primary key references public.profiles on delete cascade,
  data jsonb not null check (octet_length(data::text) <= 65536),
  updated_at timestamptz not null default now()
);

create table public.push_tokens (
  user_id uuid primary key references public.profiles on delete cascade,
  token text not null,
  updated_at timestamptz not null default now()
);

create table public.app_config (
  id int primary key default 1 check (id = 1),
  latest_version text not null,
  critical_min_version text not null,
  message text
);
insert into public.app_config (id, latest_version, critical_min_version) values (1, '1.0.0', '1.0.0');

-- RLS everywhere.
alter table public.admins enable row level security;
alter table public.profiles enable row level security;
alter table public.scores enable row level security;
alter table public.saves enable row level security;
alter table public.push_tokens enable row level security;
alter table public.app_config enable row level security;

-- Start from nothing for app roles; service_role (Edge Functions) keeps its defaults.
revoke all on public.admins, public.profiles, public.scores, public.saves, public.push_tokens, public.app_config
  from anon, authenticated;

-- admins: aal2 admin reads; never writable from the app.
grant select on public.admins to authenticated;
create policy admins_read on public.admins for select to authenticated using (public.is_admin());

-- profiles: owner reads own row and edits display_name only.
grant select, update (display_name) on public.profiles to authenticated;
create policy profiles_read_own on public.profiles for select to authenticated
  using (id = (select auth.uid()));
create policy profiles_update_own on public.profiles for update to authenticated
  using (id = (select auth.uid())) with check (id = (select auth.uid()));

-- scores: written only by submit-score (service_role). Admin can hide.
grant select, update (hidden, hidden_reason) on public.scores to authenticated;
create policy scores_read on public.scores for select to authenticated
  using ((user_id = (select auth.uid()) and not hidden) or public.is_admin());
create policy scores_admin_update on public.scores for update to authenticated
  using (public.is_admin()) with check (public.is_admin());

-- saves, push_tokens: owner only.
grant select, insert, update, delete on public.saves, public.push_tokens to authenticated;
create policy saves_own on public.saves for all to authenticated
  using (user_id = (select auth.uid())) with check (user_id = (select auth.uid()));
create policy push_tokens_own on public.push_tokens for all to authenticated
  using (user_id = (select auth.uid())) with check (user_id = (select auth.uid()));

-- app_config: everyone reads; aal2 admin edits.
grant select on public.app_config to anon, authenticated;
grant update (latest_version, critical_min_version, message) on public.app_config to authenticated;
create policy app_config_read on public.app_config for select to anon, authenticated using (true);
create policy app_config_admin_update on public.app_config for update to authenticated
  using (public.is_admin()) with check (public.is_admin());

-- Leaderboard: best non-hidden score per player, top 100. Runs as owner (bypasses RLS) and exposes no user_id.
create view public.leaderboard as
select rank() over (order by b.score desc, b.played_at) as rank,
       p.display_name, b.score, b.car_id, b.played_at
from (
  select distinct on (user_id) user_id, score, car_id, played_at
  from public.scores where not hidden
  order by user_id, score desc, played_at
) b
join public.profiles p on p.id = b.user_id
order by b.score desc, b.played_at
limit 100;
revoke all on public.leaderboard from anon, authenticated;
grant select on public.leaderboard to anon, authenticated;

-- Caller's rank among all players (null if no score yet).
create function public.my_rank() returns bigint
language sql stable security definer set search_path = '' as $$
  with best as (
    select user_id, max(score) as s from public.scores where not hidden group by user_id
  ), me as (select s from best where user_id = auth.uid())
  select case when exists (select 1 from me)
    then (select count(*) + 1 from best where s > (select s from me)) end;
$$;
revoke execute on function public.my_rank() from public, anon;
grant execute on function public.my_rank() to authenticated;

-- New auth user → profile with a default name.
create function public.handle_new_user() returns trigger
language plpgsql security definer set search_path = '' as $$
begin
  insert into public.profiles (id, display_name)
  values (new.id, 'Driver' || lpad(floor(random() * 10000)::int::text, 4, '0'));
  return new;
end;
$$;
revoke execute on function public.handle_new_user() from public, anon, authenticated;
create trigger on_auth_user_created after insert on auth.users
  for each row execute function public.handle_new_user();
