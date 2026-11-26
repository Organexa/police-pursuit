-- What the admin panel (F14) needs on top of the first schema.

-- Admins with 2FA can read every profile; everyone else still only their own.
create policy profiles_admin_read on public.profiles for select to authenticated using (public.is_admin());

-- security_invoker: the caller's own row rules apply, so only an aal2 admin sees other players.
create view public.admin_players with (security_invoker = true) as
select p.id, p.display_name, p.last_seen_at, max(s.score) filter (where not s.hidden) as best_score
from public.profiles p
left join public.scores s on s.user_id = p.id
group by p.id;
revoke all on public.admin_players from anon, authenticated;
grant select on public.admin_players to authenticated;

-- "Last seen": the game writes the cloud save on every signed-in launch and after each run.
create function public.touch_last_seen() returns trigger
language plpgsql security definer set search_path = '' as $$
begin
  update public.profiles set last_seen_at = now() where id = new.user_id;
  return new;
end;
$$;
revoke execute on function public.touch_last_seen() from public, anon, authenticated;
create trigger saves_touch_last_seen after insert or update on public.saves
  for each row execute function public.touch_last_seen();
