using System.Runtime.Serialization.Json;
using Microsoft.AspNetCore.Mvc;
using KashirinDBApi.Controllers.DataContracts;
using KashirinDBApi.Controllers.Extensions;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;
using KashirinDBApi.Controllers.Helpers;
using KashirinDBApi.Controllers.SqlConstants;

namespace KashirinDBApi.Controllers
{
    public class PostController : Controller
    {

        #region Fields
        private readonly IConfiguration Configuration;
        #endregion
        #region Constructor
        public PostController(IConfiguration Configuration)
        {
            this.Configuration = Configuration;
        }
        #endregion

        private (PostFullDataContract, int) ExecuteSelectPost(NpgsqlCommand cmd,
                                                                 int id,
                                                                 bool relateAuthor = false,
                                                                 bool relateForum = false,
                                                                 bool relateThread = false)
        {
            var postFull = new PostFullDataContract();
            int returnCode = 404;
            cmd.CommandText = string.Format(
                        PostSqlConstants.SqlSelectPost,
                        relateAuthor ? PostSqlConstants.SqlUserFields : "",
                        relateForum ? PostSqlConstants.SqlForumFields : "",
                        relateThread ? PostSqlConstants.SqlThreadFields : "",
                        relateAuthor ? PostSqlConstants.SqlUserJoin : "",
                        relateForum ? PostSqlConstants.SqlForumJoin : "",
                        relateThread ? PostSqlConstants.SqlThreadJoin :  ""
                        );
            cmd.Parameters.Add(new NpgsqlParameter("@id", id){ NpgsqlDbType = NpgsqlDbType.Integer });

            using(var reader = cmd.ExecuteReader())
            {
                if(reader.Read())
                {
                    returnCode = 200;
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
                    postFull.Post.Parent = reader.GetValueOrDefault("post_parent", 0L);
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
                        postFull.Forum.Posts = reader.GetInt64(reader.GetOrdinal("forum_posts"));
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
                }
            }
            return (postFull, returnCode);
        }


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
                        cmd.CommandText = PostSqlConstants.SqlUpdatePost;
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
                                postDetails.Parent = reader.GetValueOrDefault(6, 0L);
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
                        PostFullDataContract full;
                        (full, Response.StatusCode) = ExecuteSelectPost(cmd, int.Parse(id));
                        postDetails = full.Post;
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
                    (postFull, Response.StatusCode) = ExecuteSelectPost(cmd, 
                                                                        int.Parse(id),
                                                                        relateAuthor,
                                                                        relateForum, 
                                                                        relateThread);
                }
            }
            return new JsonResult( Response.StatusCode == 200 ? (object)postFull : string.Empty );
        }
    }
}