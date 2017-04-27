

namespace KashirinDBApi.Controllers.SqlConstants
{
    public static class ThreadSqlConstants
    {
        private static readonly string SqlPreselectThreadAndForum = @"
select
    t.ID,
    t.slug,
    t.forum_id,
    t.forum_slug
from thread t
where 
    {0}
;
        ";

        public static readonly string SqlPreselectThreadAndForumByID =
            string.Format(SqlPreselectThreadAndForum, "t.id = @id");
        public static readonly string SqlPreselectThreadAndForumBySlug = 
            string.Format(SqlPreselectThreadAndForum, "lower(t.slug) = lower(@slug)");

        private static readonly string SqlPreselectThreadAndUser = @"
select
    t.ID,
    t.slug,
    u.id
from thread t, ""user"" u
where 
    {0}
    and lower(u.nickname) = lower(@nickname)
;
        ";

        public static readonly string SqlPreselectThreadAndUserByID =
            string.Format(SqlPreselectThreadAndUser, "t.id = @id");
        public static readonly string SqlPreselectThreadAndUserBySlug = 
            string.Format(SqlPreselectThreadAndUser, "lower(t.slug) = lower(@slug)");

        public static readonly string SqlInsertPosts = @"
insert into post(id, author_id, author_name, created, forum_id, forum_slug,
                     message, parent_id, path, thread_id, thread_slug)
(
    with parent as
    (
        select 
            ID,
            path 
        from post p 
        where 
            p.ID = @parentID and p.thread_id = @thread_id
    )
    select
        nextval('post_id_seq'),
        u.id,
        u.nickname,
        case when @created is null
            then now()
            else @created
        end as created,
        @forum_id,
        @forum_slug,
        @message,
        case when @parentID = 0 
            then 0
            else (select ID from parent)
        end,
        case when @parentID = 0 
            then array_append('{}' :: BIGINT [], currval('post_id_seq'))
            else array_append((select path from parent), currval('post_id_seq'))
        end,
        @thread_id,
        @thread_slug
    from ""user"" u
    where
        lower(u.nickname) = lower(@author_name)
)
on conflict do nothing
returning ID, created, isedited, message, parent_id
;  
        ";

        private static readonly string SqlSelectThreadDetails = @"
select
    t.author_name as author,
    t.created,
    t.forum_slug as forum,
    t.id,
    t.message,
    t.slug,
    t.title,
    t.votes
from thread t
where {0} ;
        ";

        public static readonly string SqlSelectThreadDetailsByID = 
            string.Format(SqlSelectThreadDetails, "t.id = @id") ;

        public static readonly string SqlSelectThreadDetailsByName = 
             string.Format(SqlSelectThreadDetails, "lower(t.slug) = lower(@slug)") ;

        private static readonly string SqlUpdateThreadDetails = @"
with ins as
(
    update thread
    set
        title = case when @title is not null 
                    then @title
                    else title
        end,
        message = case when @message is not null 
                    then @message
                    else message
        end
    where {0}
    returning author_name, created, forum_slug, id, message, slug, title, votes
)
select
    t.author_name as author,
    t.created,
    t.forum_slug as forum,
    t.id,
    t.message,
    t.slug,
    t.title,
    t.votes
from ins t
;
        ";

        public static readonly string SqlUpdateThreadDetailsByID = 
            string.Format(SqlUpdateThreadDetails, "id = @id") ;

        public static readonly string SqlUpdateThreadDetailsByName = 
             string.Format(SqlUpdateThreadDetails, "lower(slug) = lower(@slug)") ;


        public static readonly string SqlInsertVote = @"
insert into vote(thread_id, user_id, vote)
    values (@thread_id, @user_id, @vote)
on conflict(thread_id, user_id) do update set
    vote = @vote
;
update thread
set
    votes = (select sum(vote) from vote where thread_id = @thread_id)
where 
    id = @thread_id
returning
    author_name as author,
    created,
    forum_slug as forum,
    id,
    message,
    slug,
    title,
    votes
;
        ";

        public static readonly string SqlSelectPostsFlat = @"
select
    *
from
(
    select
        p.id,
        p.author_name,
        p.created,
        p.forum_slug,
        p.isedited,
        p.message,
        p.parent_id,
        row_number() over (order by created {0}, p.id {0}) as rn
    from post p
    where p.thread_id = @id
    order by created {0}, p.id {0} -- asc desc
) t
where 
    @from <= rn and rn < @to
        ;";

        public static readonly string SqlSelectPosts = @"
select 
    *
from 
(
    select
        id,
        author_name,
        created,
        forum_slug,
        isedited,
        message,
        parent_id,
        row_number() over (order by p.path {0}) as rn
    from post p 
    where thread_id = @id
) t
where 
    @from <= rn and rn < @to
order by rn 
        ";


        public static readonly string SqlSelectPostsParentTree = @"
with roots as
(
    select 
        id,
        rn
    from
    (
        select
            id,
            row_number() over (order by id {0}) as rn
        from post
        where
            parent_id = 0
            and thread_id = @id
    )t
    where @from <= rn and rn < @to
),
new_marker as
(
    select max(rn) as marker from roots
)        
select
    id,
    author_name,
    created,
    forum_slug,
    isedited,
    message,
    parent_id,
    (select marker from new_marker)
from post
where
    post.path[1] in (select id from roots)
order by post.path {0}
        ";
    }
}