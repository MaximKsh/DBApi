

namespace KashirinDBApi.Controllers.SqlConstants
{
    public static class UserSqlConstants
    {
        public static readonly string SqlInsertUser = @"
with ins as 
(
    insert into ""user"" (about, email, fullname, nickname)
    values (@about, @email::citext, @fullname, @nickname::citext)
    on conflict do nothing
    returning id, about, email, fullname, nickname
)
select 
    'inserted' AS status, 
    id,
    about, 
    email, 
    fullname, 
    nickname
from ins
union all
select 
    'selected' AS status, 
    u.id, 
    u.about, 
    u.email, 
    u.fullname, 
    u.nickname
from ""user"" u
where u.email = @email::citext
    or u.nickname = @nickname::citext
        ";

        public static readonly string SqlSelectProfile = @"
select 
    about,
    email,
    fullname,
    nickname
from ""user""
where 
    nickname = @name::citext
;
        ";

        public static readonly string SqlUpdateProfileWithoutConstraintChecking = @"
update 
    ""user""
set 
    about = case
        when @about is not null then @about
        else about
    end,
    fullname = case
        when @fullname is not null then @fullname
        else fullname
    end
where
    nickname = @nickname::citext
returning about, email, fullname, nickname, 'updated'
;
        ";
        public static readonly string SqlUpdateProfileWithEmailConflictChecking = @"
with same_email(ID) as
(
    select
        ID
    from ""user""
    where
        email = @email::citext
        and nickname <> @nickname::citext
),
upd as (
    update
        ""user""
    set
        email = case
            when exists(select * from same_email) then email
            else @email::citext
        end,
        about = case
            when @about is not null then @about
            else about
        end,
        fullname = case
            when @fullname is not null then @fullname
            else fullname
        end
    where
        nickname = @nickname::citext
    returning about, email, fullname, nickname
)
select
    about,
    email,
    fullname,
    nickname,
    case
        when exists(select * from same_email)
        then 'conflicted'
        else 'updated'
    end as status
from upd;
        ";
        
    }

}