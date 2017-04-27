

namespace KashirinDBApi.Controllers.SqlConstants
{
    public static class ForumSqlConstants
    {
       

        public static readonly string SqlInsertForum = @"
with tuple as 
(
    select
        @slug as slug,
        @title as title,
        u.id as user_id,
        u.nickname as nickname
    from ""user"" u
        where lower(nickname) = lower(@nickname)
),
ins as 
(
    insert into forum (slug, title, user_id, user_name)
        select 
            slug, 
            title, 
            user_id,
            nickname
        from tuple where user_id is not null
    on conflict do nothing
    returning id, slug, title, user_id, user_name
)
select 'inserted' as status, ins.id, ins.slug, ins.title, ins.user_id, ins.user_name 
FROM ins
union all
select 'selected' AS status, f.id, f.slug, f.title, f.user_id, f.user_name
from forum as f
where
    f.user_id = (select user_id from tuple);
";


        public static readonly string SqlInsertThread = @"
with tuple as 
(
    select
        case when @created is not null
            then @created
            else now()
        end as created,
        @message as message,
        @slug as slug,
        @title as title,
        u.id as author_id,
        u.nickname as author_name,
        ff.ID as forum_id,
        ff.slug as forum_slug
    from ""user"" u, forum ff
    where
        lower(u.nickname) = lower(@nickname)
        and lower(slug) = lower(@forum_slug)
),
ins as 
(
    insert into thread (created, message, slug, title, author_id, author_name, forum_id, forum_slug)
        select 
            created, 
            message, 
            slug, 
            title, 
            author_id,
            author_name,
            forum_id,
            forum_slug
        from tuple 
        where forum_id is not null and author_id is not null
    on conflict do nothing
    returning id, author_id, author_name, created, forum_id, forum_slug, message, slug, title
)
select
    'inserted' as status,
    id,
    author_name,
    created,
    forum_slug,
    message,
    slug,
    title,
    (select sum(vote) from vote where thread_id = id) as votes
from ins
union all
select
    'selected' as status,
    th.id,
    th.author_name,
    th.created,
    th.forum_slug,
    th.message,
    th.slug,
    th.title,
    (select sum(vote) from vote where thread_id = th.id) as votes
FROM thread as th 
where lower(th.slug) = (select lower(slug) from tuple)
        ";


        public static readonly string SqlSelectForumDetails = @"
select
    (select count(1) from post where forum_id = f.ID) as posts,
    slug,
    (select count(1) from thread where forum_id = f.ID) as threads,
    title,
    user_name
from forum f
where
   lower(f.slug) = lower(@slug)
;
        ";

        public static readonly string SqlGetForumBySlug = @"
select
    ID
from forum
where lower(slug) = lower(@slug)
limit 1;
        ";
        public static readonly string SqlSelectForumThreads = @"
select
    t.author_name,
    t.created,
    t.forum_slug,
    t.id,
    t.message,
    t.slug,
    t.title,
    (select sum(vote) from vote where thread_id = t.id) as votes
from thread t
where
    t.forum_id = @id
    {0}
order by t.created {1}
{2} 
;
        ";

        public static readonly string SqlSelectForumUsers = @"
select distinct
    u.about,
    u.email,
    u.fullname,
    u.nickname,
    convert_to(lower(u.nickname), 'utf8')
from ""user"" u
left join thread t on u.ID = t.author_ID and t.forum_id = @forum_id
left join post p on u.ID = p.author_ID and p.forum_id = @forum_id
where (p.ID is not null or t.ID is not null) {0}
order by convert_to(lower(u.nickname), 'utf8') {1}
limit @limit
;
        ";
        
    }

}