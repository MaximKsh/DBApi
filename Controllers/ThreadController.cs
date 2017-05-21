using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using System.Runtime.Serialization.Json;
using Microsoft.AspNetCore.Mvc;
using KashirinDBApi.Controllers.DataContracts;
using Npgsql;
using KashirinDBApi.Controllers.Extensions;
using KashirinDBApi.Controllers.Helpers;
using NpgsqlTypes;
using System.Linq;
using KashirinDBApi.Controllers.SqlConstants;
using System.Text;
using Npgsql.Logging;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace KashirinDBApi.Controllers
{
    public class ThreadController : Controller
    {
        #region Fields
        private readonly IConfiguration Configuration;
        #endregion
        #region Constructor
        public ThreadController(IConfiguration Configuration)
        {
           this.Configuration = Configuration;
        }
        #endregion

        private async Task<(long?, string, long?, string)> PreselectThreadAndForum(NpgsqlConnection conn,
                                                                                   string slugOrID)
        {
            string slug = slugOrID;
            bool useID = long.TryParse(slugOrID, out long id);

            // Преселект треда и форума
            long? forumID = null, threadID = null;
            string forumSlug = string.Empty, threadSlug = string.Empty;
            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = useID ? 
                                    ThreadSqlConstants.SqlPreselectThreadAndForumByID: 
                                    ThreadSqlConstants.SqlPreselectThreadAndForumBySlug;
                cmd.Parameters.Add( 
                    useID ? 
                        new NpgsqlParameter("@id", id){ NpgsqlDbType = NpgsqlDbType.Integer }:
                        new NpgsqlParameter("@slug", slug)
                );

                using(var reader = await cmd.ExecuteReaderAsync())
                {
                    if(await reader.ReadAsync())
                    {
                        threadID = reader.GetInt32(0);
                        threadSlug = reader.GetValueOrDefault(1, "");
                        forumID = reader.GetInt32(2);
                        forumSlug = reader.GetValueOrDefault(3, "");
                    }
                }
            }
            return (threadID, threadSlug, forumID, forumSlug);
        }

        private async Task<(long?, string, long?)> PreselectThreadAndUser(NpgsqlConnection conn,
                                                              string slugOrID,
                                                              string authorName)
        {
            string slug = slugOrID;
            bool useID = long.TryParse(slugOrID, out long id);

            // Преселект треда и форума
            long? userID = null, threadID = null;
            string threadSlug = string.Empty;
            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = useID ? 
                                    ThreadSqlConstants.SqlPreselectThreadAndUserByID: 
                                    ThreadSqlConstants.SqlPreselectThreadAndUserBySlug;
                cmd.Parameters.Add( 
                    useID ? 
                        new NpgsqlParameter("@id", id){ NpgsqlDbType = NpgsqlDbType.Integer }:
                        new NpgsqlParameter("@slug", slug)
                );
                cmd.Parameters.Add(new NpgsqlParameter("@nickname", authorName));

                using(var reader = await cmd.ExecuteReaderAsync())
                {
                    if(await reader.ReadAsync())
                    {
                        threadID = reader.GetInt32(0);
                        threadSlug = reader.GetValueOrDefault(1, "");
                        userID = reader.GetInt32(2);
                    }
                }
            }
            return (threadID, threadSlug, userID);
        }

        private async Task<List<PostDetailsDataContract>> InsertPosts(
            NpgsqlConnection conn,
            List<PostDetailsDataContract> posts,
            long threadID,
            string threadSlug,
            long forumID,
            string forumSlug)
        {
            var created = DateTime.UtcNow;
            List<PostDetailsDataContract> createdPosts = new List<PostDetailsDataContract>();
            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = string.Format(
                    ThreadSqlConstants.SqlInsertPosts,
                    string.Join(",", Enumerable.Repeat("nextval('post_id_seq')", posts.Count))
                );
                cmd.Parameters.Add(Helper.NewNullableParameter("@created", created, NpgsqlDbType.TimestampTZ));
                cmd.Parameters.Add(Helper.NewNullableParameter("@forum_id", forumID, NpgsqlDbType.Integer));
                cmd.Parameters.Add(Helper.NewNullableParameter("@forum_slug", forumSlug));
                cmd.Parameters.Add(Helper.NewNullableParameter("@thread_id", threadID, NpgsqlDbType.Integer));
                cmd.Parameters.Add(Helper.NewNullableParameter("@thread_slug", threadSlug));
                cmd.Parameters.Add(Helper.NewNullableParameter("@parents", posts.Select(p => (int)p.Parent).ToList(), NpgsqlDbType.Array | NpgsqlDbType.Integer));
                cmd.Parameters.Add(Helper.NewNullableParameter("@authors",  posts.Select(p => p.Author).ToList(), NpgsqlDbType.Array | NpgsqlDbType.Text));
                cmd.Parameters.Add(Helper.NewNullableParameter("@messages", posts.Select(p => p.Message).ToList(), NpgsqlDbType.Array | NpgsqlDbType.Text));
                
                var transact = conn.BeginTransaction();
                cmd.Transaction = transact;
                bool conflict = false;
                using(var reader = cmd.ExecuteReader())
                {
                    while(await reader.ReadAsync())
                    {
                        if(reader.IsDBNull(3))
                        {
                            conflict = true;
                            break;
                        }
                        
                        var createdPost = new PostDetailsDataContract();
                        createdPost.ID = reader.GetInt32(0);
                        createdPost.Author = reader.GetString(1);
                        createdPost.Created = reader
                                                .GetDateTime(4)
                                                .ToUniversalTime()
                                                .ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                        createdPost.Thread = threadID;
                        createdPost.Forum = forumSlug;
                        createdPost.IsEdited = false;
                        createdPost.Message = reader.GetString(2);
                        createdPost.Parent = reader.GetInt32(3);
                        createdPosts.Add(createdPost);
                    }
                }
                if(createdPosts.Count != posts.Count)
                {
                    Response.StatusCode = conflict ? 409 : 404;
                    await transact.RollbackAsync();
                }
                else
                {
                    Response.StatusCode = 201;
                    await transact.CommitAsync();
                }
            }
            if(Response.StatusCode == 201)
            {
                using (var cmdUpd = new NpgsqlCommand())
                {
                    cmdUpd.Connection = conn;
                    cmdUpd.CommandText = "update forum set posts = posts + @cnt where id = @id";
                    cmdUpd.Parameters.Add(new NpgsqlParameter("@id", forumID));
                    cmdUpd.Parameters.Add(new NpgsqlParameter("@cnt", createdPosts.Count));
                    await cmdUpd.ExecuteNonQueryAsync();
                }
            }
            
            return createdPosts;
        }

        [Route("api/thread/{slug_or_id}/create")]
        [HttpPost]
        public async Task<JsonResult> Create(string slug_or_id)
        {
            DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(List<PostDetailsDataContract>));
            var posts = (List<PostDetailsDataContract>)js.ReadObject(Request.Body);

            List<PostDetailsDataContract> createdPosts = null;
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                await conn.OpenAsync();
                var (threadID, threadSlug, forumID, forumSlug) = await PreselectThreadAndForum(conn, slug_or_id);

                if( threadID.HasValue 
                    && forumID.HasValue)
                {
                    createdPosts = await InsertPosts(
                        conn,
                        posts, 
                        threadID.Value, 
                        threadSlug, 
                        forumID.Value, 
                        forumSlug
                    );   
                }
                else
                {
                    Response.StatusCode = 404;
                }
            }

            return new JsonResult( Response.StatusCode == 201 ? (object)createdPosts : string.Empty);
        }


        [Route("api/thread/{slug_or_id}/details")]
        [HttpPost]
        public async Task<JsonResult> DetailsPost(string slug_or_id)
        {
            DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(ThreadUpdateDataContract));
            var threadUpdate = (ThreadUpdateDataContract)js.ReadObject(Request.Body);
            var updatedThread = new ThreadDetailsDataContract();
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                await conn.OpenAsync();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;

                    string slug = slug_or_id;
                    bool useID = long.TryParse(slug_or_id, out long id);
                    cmd.CommandText = useID ?
                                        ThreadSqlConstants.SqlUpdateThreadDetailsByID:
                                        ThreadSqlConstants.SqlUpdateThreadDetailsByName;
                    cmd.Parameters.Add(
                            useID ?
                                new NpgsqlParameter("@id", id):
                                new NpgsqlParameter("@slug", slug_or_id));
                    cmd.Parameters.Add(Helper.NewNullableParameter("@title", threadUpdate.Title));
                    cmd.Parameters.Add(Helper.NewNullableParameter("@message", threadUpdate.Message));
                    
                    
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if(await reader.ReadAsync())
                        {
                            updatedThread.Author = reader.GetValueOrDefault(0, "");
                            updatedThread.Created = reader
                                            .GetDateTime(1)
                                            .ToUniversalTime()
                                            .ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                            updatedThread.Forum = reader.GetValueOrDefault(2, "");
                            updatedThread.ID = reader.GetInt32(3);
                            updatedThread.Message = reader.GetValueOrDefault(4, "");
                            updatedThread.Slug = reader.GetValueOrDefault(5, "");
                            updatedThread.Title = reader.GetValueOrDefault(6, "");
                            updatedThread.Votes = reader.GetValueOrDefault(7, 0);
                            Response.StatusCode = 200;
                        }
                        else
                        {
                            Response.StatusCode = 404;
                        }
                    }
                }
            }
            return new JsonResult(Response.StatusCode == 200  ? updatedThread as object : string.Empty);
        
        }

        [Route("api/thread/{slug_or_id}/details")]
        [HttpGet]
        public async Task<JsonResult> DetailsGet(string slug_or_id)
        {
            ThreadDetailsDataContract thread = new ThreadDetailsDataContract();
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                await conn.OpenAsync();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    
                    string slug = slug_or_id;
                    bool useID = long.TryParse(slug_or_id, out long id);
                    cmd.CommandText = useID ?
                                        ThreadSqlConstants.SqlSelectThreadDetailsByID:
                                        ThreadSqlConstants.SqlSelectThreadDetailsByName;
                    cmd.Parameters.Add(
                            useID ?
                                new NpgsqlParameter("@id", id):
                                new NpgsqlParameter("@slug", slug_or_id));
                    
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if(await reader.ReadAsync())
                        {
                            thread.Author = reader.GetValueOrDefault(0, "");
                            thread.Created = reader
                                            .GetDateTime(1)
                                            .ToUniversalTime()
                                            .ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                            thread.Forum = reader.GetValueOrDefault(2, "");
                            thread.ID = reader.GetInt32(3);
                            thread.Message = reader.GetValueOrDefault(4, "");
                            thread.Slug = reader.GetValueOrDefault(5, "");
                            thread.Title = reader.GetValueOrDefault(6, "");
                            thread.Votes = reader.GetValueOrDefault(7, 0);
                            Response.StatusCode = 200;
                        }
                        else
                        {
                            Response.StatusCode = 404;
                        }
                    }
                }
            }
            return new JsonResult(Response.StatusCode == 200  ? thread as object : string.Empty);
        }


        [Route("api/thread/{slug_or_id}/posts")]
        [HttpGet]
        public async Task<JsonResult> Posts(
            string slug_or_id, 
            int? limit,
            string marker, 
            string sort = "flat",
            bool desc = false )
        {
            var postPage = new PostPageDataContract();
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                await conn.OpenAsync();
                var (threadID, threadSlug, forumID, forumSlug) = await PreselectThreadAndForum(conn, slug_or_id);

                if(!threadID.HasValue 
                    || !forumID.HasValue)
                {
                    Response.StatusCode = 404;
                    return new JsonResult(string.Empty);
                }

                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    string query = GetPostsQuery(sort, desc);
                    postPage = await SelectPosts(conn, query, marker, limit, threadID.Value, desc);
                }
            }

            return new JsonResult( postPage );
        }
        
        [Route("api/thread/{slug_or_id}/vote")]
        [HttpPost]
        public async Task<JsonResult> Vote(string slug_or_id)
        {
            DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(VoteDataContract));
            var vote = (VoteDataContract)js.ReadObject(Request.Body);
            var updatedThread = new ThreadDetailsDataContract();
            
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                await conn.OpenAsync();
                var (threadID, threadSlug, userID) = await PreselectThreadAndUser(conn, slug_or_id, vote.Nickname);

                if( threadID.HasValue 
                    && userID.HasValue)
                {
                    using (var cmd = new NpgsqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = ThreadSqlConstants.SqlInsertVote;
                        cmd.Parameters.Add(new NpgsqlParameter("@thread_id", threadID.Value));
                        cmd.Parameters.Add(new NpgsqlParameter("@user_id", userID.Value));
                        
                        cmd.Parameters.Add(new NpgsqlParameter("@vote", vote.Voice > 0 ? 1 : -1){NpgsqlDbType = NpgsqlDbType.Integer});

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if(await reader.ReadAsync())
                            {
                                updatedThread.Author = reader.GetValueOrDefault(0, "");
                                updatedThread.Created = reader
                                                .GetDateTime(1)
                                                .ToUniversalTime()
                                                .ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                                updatedThread.Forum = reader.GetValueOrDefault(2, "");
                                updatedThread.ID = reader.GetInt32(3);
                                updatedThread.Message = reader.GetValueOrDefault(4, "");
                                updatedThread.Slug = reader.GetValueOrDefault(5, "");
                                updatedThread.Title = reader.GetValueOrDefault(6, "");
                                updatedThread.Votes = reader.GetValueOrDefault(7, 0);
                            }
                        }
                    }
                }
                else
                {
                    Response.StatusCode = 404;
                }        
            }

            return new JsonResult( Response.StatusCode == 200  ? updatedThread as object : string.Empty );
        }
        
        private string GetPostsQuery(string sort, bool desc)
        {
            string query = "{0}";
            if(sort == "flat")
            {
                query = ThreadSqlConstants.SqlSelectPostsFlat;
            }
            else if(sort == "tree")
            {
                query = ThreadSqlConstants.SqlSelectPosts;
            }
            else if(sort == "parent_tree")
            {
                query = ThreadSqlConstants.SqlSelectPostsParentTree;
            }
            return string.Format(query, desc ?  "desc": "");
        }

        private async Task<PostPageDataContract> SelectPosts(
                                                NpgsqlConnection conn,
                                                string query,
                                                string marker,
                                                int? limit,
                                                long threadID,
                                                bool desc)
        {
            var postPage = new PostPageDataContract();
            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = query;
                int from = Int32.TryParse(marker, out int fromInt) ? fromInt : 0;
                int lim = limit ?? int.MaxValue;
                cmd.Parameters.Add( new NpgsqlParameter("@id", threadID) );
                cmd.Parameters.Add( new NpgsqlParameter("@from", from));
                cmd.Parameters.Add( new NpgsqlParameter("@limit", lim));
            
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    postPage.Posts = new List<PostDetailsDataContract>();
                    while(await reader.ReadAsync())
                    {
                        postPage.Posts.Add(
                            new PostDetailsDataContract()
                            {
                                ID = reader.GetInt32(0),
                                Author = reader.GetString(1),
                                Created = reader
                                            .GetDateTime(2)
                                            .ToUniversalTime()
                                            .ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                                Forum = reader.GetValueOrDefault(3, ""),
                                IsEdited = reader.GetBoolean(4),
                                Message = reader.GetValueOrDefault(5, ""),
                                Parent = reader.GetValueOrDefault(6, 0),
                                Thread = threadID
                            }
                        );
                    }
                    if(postPage.Posts.Count != 0)
                    {
                        postPage.Marker = (lim != int.MaxValue ? from + lim : int.MaxValue).ToString();
                    }
                    else
                    {
                        postPage.Marker = marker ?? "0";
                    }
                }
            }

            return postPage;
        }
    }
}