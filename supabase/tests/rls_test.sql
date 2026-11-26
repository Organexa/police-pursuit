-- RLS matrix (TESTING §2.4). Runs in CI via `supabase test db`.
begin;
create extension if not exists pgtap with schema extensions;
select plan(29);

-- Fixtures (as postgres). Trigger creates the profiles.
insert into auth.users (id, email) values
  ('00000000-0000-0000-0000-00000000000a', 'a@test.local'),
  ('00000000-0000-0000-0000-00000000000b', 'b@test.local'),
  ('00000000-0000-0000-0000-00000000000c', 'admin@test.local');
update public.profiles set display_name = 'PlayerA' where id = '00000000-0000-0000-0000-00000000000a';
update public.profiles set display_name = 'PlayerB' where id = '00000000-0000-0000-0000-00000000000b';
update public.profiles set display_name = 'Admin'   where id = '00000000-0000-0000-0000-00000000000c';
insert into public.admins values ('00000000-0000-0000-0000-00000000000c');
insert into public.scores (user_id, duration_s, distance_m, near_misses, max_wanted, car_id, played_at, hidden) values
  ('00000000-0000-0000-0000-00000000000a', 60, 1000, 2, 3, 'starter', now(), false),
  ('00000000-0000-0000-0000-00000000000b', 90, 5000, 9, 5, 'starter', now(), true);
insert into public.saves (user_id, data) values
  ('00000000-0000-0000-0000-00000000000a', '{"a":1}'),
  ('00000000-0000-0000-0000-00000000000b', '{"b":1}');
insert into public.push_tokens (user_id, token) values ('00000000-0000-0000-0000-00000000000b', 'tok-b');

select is((select count(*) from pg_tables where schemaname = 'public' and not rowsecurity), 0::bigint,
  'RLS is on for every public table');

-- Player A
set local role authenticated;
select set_config('request.jwt.claims', '{"sub":"00000000-0000-0000-0000-00000000000a","role":"authenticated","aal":"aal1"}', true);

select is_empty($$select 1 from public.saves where user_id = '00000000-0000-0000-0000-00000000000b'$$, 'A cannot read B save');
select is_empty($$select 1 from public.push_tokens where user_id = '00000000-0000-0000-0000-00000000000b'$$, 'A cannot read B push token');
select is_empty($$select 1 from public.profiles where id = '00000000-0000-0000-0000-00000000000b'$$, 'A cannot read B profile');
select isnt_empty($$select 1 from public.profiles where id = '00000000-0000-0000-0000-00000000000a'$$, 'A reads own profile');
select throws_ok($$insert into public.saves (user_id, data) values ('00000000-0000-0000-0000-00000000000b', '{}') $$,
  '42501', null, 'A cannot write B save');
select throws_ok($$insert into public.scores (user_id, duration_s, distance_m, near_misses, max_wanted, car_id, played_at)
  values ('00000000-0000-0000-0000-00000000000a', 1, 1, 0, 1, 'x', now())$$, '42501', null, 'A cannot insert scores directly');
select throws_ok($$insert into public.admins values ('00000000-0000-0000-0000-00000000000a')$$, '42501', null, 'A cannot make self admin');
select throws_ok($$update public.saves set data = jsonb_build_object('x', repeat('a', 70000))
  where user_id = '00000000-0000-0000-0000-00000000000a'$$, '23514', null, 'save over 64 KB rejected');
select throws_ok($$update public.profiles set display_name = '' where id = '00000000-0000-0000-0000-00000000000a'$$,
  '23514', null, 'empty display name rejected');
select throws_ok($$update public.profiles set display_name = repeat('x', 21) where id = '00000000-0000-0000-0000-00000000000a'$$,
  '23514', null, '21-char display name rejected');
select is(public.my_rank(), 1::bigint, 'A rank is 1 (B score hidden)');
update public.saves set data = '{"hacked":1}' where user_id = '00000000-0000-0000-0000-00000000000b';
update public.profiles set display_name = 'Hacked' where id = '00000000-0000-0000-0000-00000000000b';
reset role;

select is((select data from public.saves where user_id = '00000000-0000-0000-0000-00000000000b'), '{"b":1}'::jsonb, 'B save unchanged');
select is((select display_name from public.profiles where id = '00000000-0000-0000-0000-00000000000b'), 'PlayerB', 'B name unchanged');

-- Admin without 2FA (aal1) is a normal user
set local role authenticated;
select set_config('request.jwt.claims', '{"sub":"00000000-0000-0000-0000-00000000000c","role":"authenticated","aal":"aal1"}', true);
select is_empty($$select 1 from public.admins$$, 'aal1 admin cannot read admins');
select is_empty($$select 1 from public.profiles where id <> '00000000-0000-0000-0000-00000000000c'$$,
  'aal1 admin cannot read other profiles');
select is_empty($$select 1 from public.admin_players where id <> '00000000-0000-0000-0000-00000000000c'$$,
  'aal1 admin sees no other players');
update public.scores set hidden = true where user_id = '00000000-0000-0000-0000-00000000000a';
update public.app_config set message = 'aal1' where id = 1;
reset role;

select is((select hidden from public.scores where user_id = '00000000-0000-0000-0000-00000000000a'), false, 'aal1 admin cannot hide scores');
select is((select message from public.app_config), null, 'aal1 admin cannot edit app_config');

-- Admin with 2FA (aal2)
set local role authenticated;
select set_config('request.jwt.claims', '{"sub":"00000000-0000-0000-0000-00000000000c","role":"authenticated","aal":"aal2"}', true);
select isnt_empty($$select 1 from public.admins$$, 'aal2 admin reads admins');
select isnt_empty($$select 1 from public.admin_players where display_name = 'PlayerA' and best_score is not null$$,
  'aal2 admin sees players and their best score');
update public.app_config set message = 'aal2' where id = 1;
update public.scores set hidden_reason = 'checked' where user_id = '00000000-0000-0000-0000-00000000000a';
reset role;

select is((select message from public.app_config), 'aal2', 'aal2 admin edits app_config');
select is((select hidden_reason from public.scores where user_id = '00000000-0000-0000-0000-00000000000a'), 'checked', 'aal2 admin edits scores');

-- Anonymous
set local role anon;
select set_config('request.jwt.claims', '{"role":"anon"}', true);
select throws_ok($$select 1 from public.admin_players$$, '42501', null, 'anon cannot read the player list');
select isnt_empty($$select 1 from public.leaderboard$$, 'anon reads leaderboard');
select isnt_empty($$select 1 from public.app_config$$, 'anon reads app_config');
select is_empty($$select 1 from public.leaderboard where display_name = 'PlayerB'$$, 'hidden score not on leaderboard');
select throws_ok($$insert into public.scores (user_id, duration_s, distance_m, near_misses, max_wanted, car_id, played_at)
  values ('00000000-0000-0000-0000-00000000000a', 1, 1, 0, 1, 'x', now())$$, '42501', null, 'anon cannot insert scores');
reset role;

-- Account deletion (delete-account removes the auth user): nothing of theirs stays.
delete from auth.users where id = '00000000-0000-0000-0000-00000000000b';
select is((
  (select count(*) from public.profiles where id = '00000000-0000-0000-0000-00000000000b')
  + (select count(*) from public.scores where user_id = '00000000-0000-0000-0000-00000000000b')
  + (select count(*) from public.saves where user_id = '00000000-0000-0000-0000-00000000000b')
  + (select count(*) from public.push_tokens where user_id = '00000000-0000-0000-0000-00000000000b')
), 0::bigint, 'deleting the auth user removes every row');

select * from finish();
rollback;
