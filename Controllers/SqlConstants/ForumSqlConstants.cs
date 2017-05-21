

namespace KashirinDBApi.Controllers.SqlConstants
{
    public static class ForumSqlConstants
    {
        public static readonly string SqlUpdateThreadCount = 
            "update forum set threads = threads + 1 where id = @id";
       
        public static readonly string SqlInsertForumUsers = @"
insert into forum_users(forum_ID, user_ID)
values(@forum_ID, @user_ID)
on conflict do nothing
;
        ";

        public static readonly string SqlInsertForum = @"
with tuple as 
(
    select
        @slug as slug,
        @title as title,
        u.id as user_id,
        u.nickname as nickname
    from ""user"" u
        where nickname = @nickname::citext
),
ins as 
(
    insert into forum (slug, title, user_id, user_name)
        select 
            slug, 
            title, 
            user_id,
            nickname
        from tuple 
    on conflict do nothing
    returning id, slug, title, user_id, user_name
)
select 'inserted' as status, ins.id, ins.slug, ins.title, ins.user_id, ins.user_name 
FROM ins
union all
select 'selected' AS status, f.id, f.slug, f.title, f.user_id, f.user_name
from forum as f
where
    f.slug = @slug::citext;
";


        public static readonly string SqlInsertThread = @"
with tuple as 
(
    select
        @created as created,
        @message as message,
        @slug::citext as slug,
        @title as title,
        u.id as author_id,
        u.nickname as author_name,
        ff.ID as forum_id,
        ff.slug as forum_slug
    from ""user"" u, forum ff
    where
        u.nickname = @nickname::citext
        and slug = @forum_slug::citext
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
    returning id, author_id, author_name, created, forum_id, forum_slug, message, slug, title, votes
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
    votes,
    ins.forum_id,
    ins.author_id
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
    th.votes,
    th.forum_id,
    th.author_id
FROM thread as th 
where th.slug = (select slug from tuple)
        ";


        public static readonly string SqlSelectForumDetails = @"
select
    posts,
    slug,
    threads,
    title,
    user_name
from forum f
where
   f.slug = @slug::citext
;
        ";

        public static readonly string SqlGetForumBySlug = @"
select
    ID
from forum
where slug = @slug::citext
limit 1;
        ";
        private static readonly string SqlSelectForumThreads = @"
select
    t.author_name,
    t.created,
    t.forum_slug,
    t.id,
    t.message,
    t.slug,
    t.title,
    t.votes
from thread t
where
    t.forum_id = @id
    and (@since is null or created {0} @since)
order by t.created {1}
limit @limit
;
        ";

        public static readonly string SqlSelectForumThreadsAsc = 
            string.Format(SqlSelectForumThreads, ">=", "");

        public static readonly string SqlSelectForumThreadsDesc = 
            string.Format(SqlSelectForumThreads, "<=", "desc");

        public static readonly string SqlSelectForumUsers = @"
select
    u.about,
    u.email,
    u.fullname,
    u.nickname
from forum_users fu
join ""user"" u on fu.user_ID = u.ID
where 
    fu.forum_ID = @forum_id 
        and (@since is null or u.nickname {0} @since::citext)
order by u.nickname {1}
limit @limit
;
        ";

        public static readonly string SqlSelectForumUsersAsc = 
            string.Format(SqlSelectForumUsers, ">", "");

        public static readonly string SqlSelectForumUsersDesc = 
            string.Format(SqlSelectForumUsers, "<", "desc");
        
    }

}