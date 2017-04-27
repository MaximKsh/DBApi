

namespace KashirinDBApi.Controllers.SqlConstants
{
    public static class UserSqlConstants
    {
        public static readonly string SqlInsertUser = @"
with ins as 
(
    insert into ""user"" (about, email, fullname, nickname)
    values (@about, @email, @fullname, @nickname)
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
where lower(u.email) = lower(@email)
    or lower(u.nickname) = lower(@nickname)
        ";

        public static readonly string SqlSelectProfile = @"
select 
    about,
    email,
    fullname,
    nickname
from ""user""
where 
    lower(nickname) = lower(@name)
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
    lower(nickname) = lower(@nickname)
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
        lower(email) = lower(@email)
        and lower(nickname) <> lower(@nickname)
),
upd as (
    update
        ""user""
    set
        email = case
            when exists(select * from same_email) then email
            else @email
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
        lower(nickname) = lower(@nickname)
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