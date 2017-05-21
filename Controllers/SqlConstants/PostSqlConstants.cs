

namespace KashirinDBApi.Controllers.SqlConstants
{
    public static class PostSqlConstants
    {
        public static readonly string SqlUpdatePost = @"
update
        post
    set
        isedited = case when message = @message
            then isedited
            else true
        end,
        message = @message
    where
        id = @id
    returning id, 
              author_id, 
              author_name,
              created, 
              forum_id, 
              forum_slug,
              isedited, 
              message, 
              parent_id, 
              thread_id,
              thread_slug
;
 ";


        public static readonly string SqlSelectPost = @"
select
    -- Пользователь
    {0}
    -- Форум
    {1}
    -- Тред
    {2}
    -- Пост
    p.author_name as post_author,
    p.created as post_created,
    p.forum_slug as post_forum,
    p.id as post_id,
    p.isedited as post_isedited,
    p.message as post_message,
    p.parent_id as post_parent,
    p.thread_id as post_thread_id
from post p
{3} -- джойн юзера
{4} -- джойн форума
{5} -- джойн треда
where
    p.id = @id
limit 1;
";

        public static readonly string SqlUserFields = @"
    u.about as user_about,
    u.email as user_email,
    u.fullname as user_fullname,
    u.nickname as user_nickname,
        ";

        public static readonly string SqlForumFields = @"
    f.posts as forum_posts,
    f.slug as forum_slug,
    f.threads as forum_threads,
    f.title as forum_title,
    f.user_name as forum_user, 
        ";

        public static readonly string SqlThreadFields = @"
    t.author_name as thread_author,
    t.created as thread_created,
    t.forum_slug as thread_forum,
    t.id as thread_id,
    t.message as thread_message,
    t.slug as thread_slug,
    t.title as thread_title,
    t.votes as thread_votes,
        ";

        public static readonly string SqlUserJoin = @"
join ""user"" u on p.author_id = u.id
        ";

        public static readonly string SqlForumJoin = @"
join forum f on p.forum_id = f.id
        ";

        public static readonly string SqlThreadJoin = @"
join thread t on p.thread_id = t.id
        ";
    }

}