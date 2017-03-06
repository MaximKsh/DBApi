using System.Runtime.Serialization.Json;
using Microsoft.AspNetCore.Mvc;
using KashirinDBApi.Controllers.DataContracts;
using KashirinDBApi.Controllers.Extensions;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;
using KashirinDBApi.Controllers.Helpers;

namespace KashirinDBApi.Controllers
{
    public class PostController : Controller
    {
        #region sql
       
        private static readonly string sqlUpdatePost = @"
            with upd as
            (
                update
                    post
                set
                    isedited =  case when message = @message
                        then isedited
                        else true
                    end,
                    message = @message
                where
                    id = @id
                returning id, author_id, created, forum_id, isedited, message, parent_id, thread_id
            )
            select
                up.id,
                u.nickname,
                up.created,
                f.slug,
                up.isedited,
                up.message,
                up.parent_id,
                up.thread_id
            from upd up
            inner join ""user"" u on up.author_id = u.id
            inner join forum f on up.forum_id = f.id
            ;

        ";


        private static readonly string sqlSelectPost = @"
            select
                -- Пользователь
                {0}
                -- Форум
                {1}
                -- Тред
                {2}
                -- Пост
                u.nickname as post_author,
                p.created as post_created,
                f.slug as post_forum,
                p.id as post_id,
                p.isedited as post_isedited,
                p.message as post_message,
                p.parent_id as post_parent,
                p.thread_id as post_thread_id
            from post p
            inner join ""user"" u on p.author_id = u.id
            inner join forum f on p.forum_id = f.id
            {3} -- джойн треда
            where
                p.id = @id
            limit 1;
        ";

        private static readonly string sqlUserFields = @"
                u.about as user_about,
                u.email as user_email,
                u.fullname as user_fullname,
                u.nickname as user_nickname,
        ";

        private static readonly string sqlForumFields = @"
                (select count(ID) from post where forum_id = f.ID) as forum_posts,
                f.slug as forum_slug,
                (select count(ID) from thread where forum_id = f.ID) as forum_threads,
                f.title as forum_title,
                (select nickname from ""user"" where id = f.user_id) as forum_user,
        
        ";

        private static readonly string sqlThreadFields = @"
                (select nickname from ""user"" where id = t.author_id) as thread_author,
                t.created as thread_created,
                f.slug as thread_forum,
                t.id as thread_id,
                t.message as thread_message,
                t.slug as thread_slug,
                t.title as thread_title,
                (select coalesce(sum(vote), 0) from vote where thread_id = t.id) as thread_votes,
        ";

        private static readonly string sqlThreadJoin = @"
            inner join thread t on p.thread_id = t.id
        ";

        #endregion

    #region Fields
        private readonly IConfiguration Configuration;
    #endregion
    #region Constructor
        public PostController(IConfiguration Configuration)
        {
            this.Configuration = Configuration;
        }
    #endregion

        [Route("api/post/{id}/details")]
        [HttpPost]
        public JsonResult DetailsPost(string id)
        {
            DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(PostUpdateDataContract));
            var postUpdate = (PostUpdateDataContract)js.ReadObject(Request.Body);
            

            PostDetailsDataContract postDetails = null;
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                conn.Open();
                if(postUpdate.Message != null)
                {
                    using (var cmd = new NpgsqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = sqlUpdatePost;
                        cmd.Parameters.Add(new NpgsqlParameter("@id", id){ NpgsqlDbType = NpgsqlDbType.Integer });
                        cmd.Parameters.Add(new NpgsqlParameter("@message", postUpdate.Message));

                        using(var reader = cmd.ExecuteReader())
                        {
                            if(reader.Read())
                            {
                                postDetails = new PostDetailsDataContract();
                                postDetails.ID = reader.GetInt32(0);
                                postDetails.Author = reader.GetString(1);
                                postDetails.Created = reader
                                                .GetTimeStamp(2)
                                                .DateTime
                                                .ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");
                                postDetails.Forum = reader.GetValueOrDefault(3, "");
                                postDetails.IsEdited = reader.GetBoolean(4);
                                postDetails.Message = reader.GetValueOrDefault(5, "");
                                postDetails.Parent = reader.GetValueOrDefault(6, 0);
                                postDetails.Thread = reader.GetInt32(7);
                                Response.StatusCode = 200;
                            }
                            else
                            {
                                Response.StatusCode = 404;
                            }
                        }
                    }
                }
                else
                {
                    using (var cmd = new NpgsqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = string.Format(
                            sqlSelectPost,
                                "",
                                "", 
                                "", 
                                ""
                            );
                        cmd.Parameters.Add(new NpgsqlParameter("@id", id){ NpgsqlDbType = NpgsqlDbType.Integer });

                        using(var reader = cmd.ExecuteReader())
                        {
                            if(reader.Read())
                            {
                                postDetails = new PostDetailsDataContract();
                                postDetails.Author = reader.GetValueOrDefault("post_author", "");
                                postDetails.Created = reader
                                                    .GetTimeStamp(reader.GetOrdinal("post_created"))
                                                    .DateTime
                                                    .ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");
                                postDetails.Forum = reader.GetValueOrDefault("post_forum", "");
                                postDetails.ID = reader.GetInt32(reader.GetOrdinal("post_id"));
                                postDetails.IsEdited = reader.GetBoolean(reader.GetOrdinal("post_isedited"));   
                                postDetails.Message = reader.GetValueOrDefault("post_message", "");
                                postDetails.Parent = reader.GetValueOrDefault("post_parent", 0);
                                postDetails.Thread = reader.GetInt32(reader.GetOrdinal("post_thread_id"));

                                Response.StatusCode = 200;
                            }
                            else
                            {
                                Response.StatusCode = 404;
                            }
                        }
                    }
                }
            }
            return new JsonResult( Response.StatusCode == 200 ? (object)postDetails : string.Empty );
        }

        [Route("api/post/{id}/details")]
        [HttpGet]
        public JsonResult DetailsGet(string id, string related = "")
        {
            bool relateAuthor = related.Contains("user");
            bool relateForum = related.Contains("forum");
            bool relateThread = related.Contains("thread");

            var postFull = new PostFullDataContract();
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = string.Format(
                        sqlSelectPost,
                        relateAuthor ? 
                            sqlUserFields : 
                            "",
                        relateForum ? 
                            sqlForumFields : 
                            "",
                        relateThread ? 
                            sqlThreadFields : 
                            "",
                        relateThread ? 
                            sqlThreadJoin : 
                            ""
                        );
                    cmd.Parameters.Add(new NpgsqlParameter("@id", id){ NpgsqlDbType = NpgsqlDbType.Integer });

                    using(var reader = cmd.ExecuteReader())
                    {
                        if(reader.Read())
                        {
                            postFull.Post = new PostDetailsDataContract();
                            postFull.Post.Author = reader.GetValueOrDefault("post_author", "");
                            postFull.Post.Created = reader
                                                .GetTimeStamp(reader.GetOrdinal("post_created"))
                                                .DateTime
                                                .ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");
                            postFull.Post.Forum = reader.GetValueOrDefault("post_forum", "");
                            postFull.Post.ID = reader.GetInt32(reader.GetOrdinal("post_id"));
                            postFull.Post.IsEdited = reader.GetBoolean(reader.GetOrdinal("post_isedited"));   
                            postFull.Post.Message = reader.GetValueOrDefault("post_message", "");
                            postFull.Post.Parent = reader.GetValueOrDefault("post_parent", 0);
                            postFull.Post.Thread = reader.GetInt32(reader.GetOrdinal("post_thread_id"));

                            if(relateAuthor)
                            {
                                postFull.User = new UserProfileDataContract();
                                postFull.User.About = reader.GetValueOrDefault("user_about", "");
                                postFull.User.Email = reader.GetValueOrDefault("user_email", "");
                                postFull.User.Fullname = reader.GetValueOrDefault("user_fullname", "");
                                postFull.User.Nickname = reader.GetValueOrDefault("user_nickname", "");   
                            }

                            if(relateForum)
                            {
                                postFull.Forum = new ForumDetailsDataContract();
                                // postFull.Forum.Posts = reader.GetInt64(reader.GetOrdinal("forum_posts"));
                                postFull.Forum.Slug = reader.GetValueOrDefault("forum_slug", "");
                                postFull.Forum.Threads = reader.GetInt64(reader.GetOrdinal("forum_threads"));
                                postFull.Forum.Title = reader.GetValueOrDefault("forum_title", ""); 
                                postFull.Forum.User = reader.GetValueOrDefault("forum_user", ""); 
                            }

                            if(relateThread)
                            {
                                postFull.Thread = new ThreadDetailsDataContract();
                                postFull.Thread.Author = reader.GetValueOrDefault("thread_author", ""); 
                                postFull.Thread.Created = reader
                                                .GetTimeStamp(reader.GetOrdinal("thread_created"))
                                                .DateTime
                                                .ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");
                                postFull.Thread.Forum = reader.GetValueOrDefault("thread_forum", "");
                                postFull.Thread.ID = reader.GetInt32(reader.GetOrdinal("thread_id"));
                                postFull.Thread.Message = reader.GetValueOrDefault("thread_message", "");
                                postFull.Thread.Slug = reader.GetValueOrDefault("thread_slug", "");
                                postFull.Thread.Title = reader.GetValueOrDefault("thread_title", "");
                                postFull.Thread.Votes = reader.GetInt64(reader.GetOrdinal("thread_votes")); 
                            }
                            Response.StatusCode = 200;
                        }
                        else
                        {
                            Response.StatusCode = 404;
                        }
                    }
                }
            }
            return new JsonResult( Response.StatusCode == 200 ? (object)postFull : string.Empty );
        }
        
    }
}