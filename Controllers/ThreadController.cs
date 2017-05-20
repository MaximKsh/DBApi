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
            string forumSlug,
            DateTime? created)
        {
            List<PostDetailsDataContract> createdPosts = new List<PostDetailsDataContract>();
            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = string.Format(
                    ThreadSqlConstants.SqlInsertPosts,
                    string.Join(",", Enumerable.Repeat("nextval('post_id_seq')", posts.Count))
                );
                cmd.Parameters.Add(Helper.NewNullableParameter("@created", created, NpgsqlDbType.Timestamp));
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
                using(var reader = await cmd.ExecuteReaderAsync())
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
                        createdPost.Created = created.Value.ToString("yyyy-MM-ddTHH:mm:ss.fff+03:00");
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
            return createdPosts;
        }

        [Route("api/thread/{slug_or_id}/create")]
        [HttpPost]
        public async Task<JsonResult> Create(string slug_or_id)
        {
            var created = DateTime.Now;
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
                        forumSlug,
                        created
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
        public JsonResult DetailsPost(string slug_or_id)
        {
            DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(ThreadUpdateDataContract));
            var threadUpdate = (ThreadUpdateDataContract)js.ReadObject(Request.Body);
            var updatedThread = new ThreadDetailsDataContract();
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                conn.Open();
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
                    
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        if(reader.Read())
                        {
                            updatedThread.Author = reader.GetValueOrDefault(0, "");
                            updatedThread.Created = reader
                                            .GetTimeStamp(1)
                                            .DateTime
                                            .ToString("yyyy-MM-ddTHH:mm:ss.fff+03:00");
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
        public JsonResult DetailsGet(string slug_or_id)
        {
            ThreadDetailsDataContract thread = new ThreadDetailsDataContract();
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                conn.Open();
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
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        if(reader.Read())
                        {
                            thread.Author = reader.GetValueOrDefault(0, "");
                            thread.Created = reader
                                            .GetTimeStamp(1)
                                            .DateTime
                                            .ToString("yyyy-MM-ddTHH:mm:ss.fff+03:00");
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
                    if(sort == "flat")
                    {
                        postPage = await SelectFlat(conn, marker, limit, threadID.Value, desc);
                    }
                    else if(sort == "tree")
                    {
                        postPage = await SelectTree(conn, marker, limit, threadID.Value, desc);
                    }
                    else if(sort == "parent_tree")
                    {
                        postPage = await SelectParentTree(conn, marker, limit, threadID.Value, desc);
                    }
                    else
                    {
                        Response.StatusCode = 400;
                    }
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

                        using (var reader = cmd.ExecuteReader())
                        {
                            if(await reader.ReadAsync())
                            {
                                updatedThread.Author = reader.GetValueOrDefault(0, "");
                                updatedThread.Created = reader
                                                .GetTimeStamp(1)
                                                .DateTime
                                                .ToString("yyyy-MM-ddTHH:mm:ss.fff+03:00");
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
        

        private async Task<PostPageDataContract> SelectFlat(NpgsqlConnection conn,
                                                string marker,
                                                int? limit,
                                                long threadID,
                                                bool desc)
        {
            var postPage = new PostPageDataContract();
            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = string.Format(
                    ThreadSqlConstants.SqlSelectPostsFlat,
                    desc ?  "desc": ""
                );

                int from = Int32.TryParse(marker, out int fromInt) ? fromInt : 1;
                int to = limit.HasValue ? from + limit.Value : int.MaxValue;
                cmd.Parameters.Add( new NpgsqlParameter("@id", threadID) );
                cmd.Parameters.Add( new NpgsqlParameter("@from", from));
                cmd.Parameters.Add( new NpgsqlParameter("@to", to));
            
                using (var reader = cmd.ExecuteReader())
                {
                    postPage.Posts = new List<PostDetailsDataContract>();
                    int? lastRN = null; 
                    while(await reader.ReadAsync())
                    {
                        postPage.Posts.Add(
                            new PostDetailsDataContract()
                            {
                                ID = reader.GetInt32(0),
                                Author = reader.GetString(1),
                                Created = reader
                                            .GetTimeStamp(2)
                                            .DateTime
                                            .ToString("yyyy-MM-ddTHH:mm:ss.fff+03:00"),
                                Forum = reader.GetValueOrDefault(3, ""),
                                IsEdited = reader.GetBoolean(4),
                                Message = reader.GetValueOrDefault(5, ""),
                                Parent = reader.GetValueOrDefault(6, 0),
                                Thread = threadID
                            }
                        );
                        lastRN = reader.GetInt32(7);
                    }
                    postPage.Marker = lastRN != null ?
                                     (lastRN.Value + 1).ToString():
                                      marker;
                }
            }

            return postPage;
        }

        private async Task<PostPageDataContract> SelectTree(NpgsqlConnection conn,
                                                string marker,
                                                int? limit,
                                                long threadID,
                                                bool desc)
        {
            var postPage = new PostPageDataContract();
            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = string.Format(
                        ThreadSqlConstants.SqlSelectPosts,
                        desc ? "desc": ""
                );

                int from = Int32.TryParse(marker, out int fromInt) ? fromInt : 1;
                int to = limit.HasValue ? from + limit.Value : int.MaxValue;
                cmd.Parameters.Add( new NpgsqlParameter("@id", threadID) );
                cmd.Parameters.Add( new NpgsqlParameter("@from", from));
                cmd.Parameters.Add( new NpgsqlParameter("@to", to));

                using (var reader = cmd.ExecuteReader())
                {
                    postPage.Posts = new List<PostDetailsDataContract>();
                    int? lastRN = null; 
                    while(await reader.ReadAsync())
                    {
                        postPage.Posts.Add(
                            new PostDetailsDataContract()
                            {
                                ID = reader.GetInt32(0),
                                Author = reader.GetString(1),
                                Created = reader
                                            .GetTimeStamp(2)
                                            .DateTime
                                            .ToString("yyyy-MM-ddTHH:mm:ss.fff+03:00"),
                                Forum = reader.GetString(3),
                                IsEdited = reader.GetBoolean(4),
                                Message = reader.GetValueOrDefault(5, ""),
                                Parent = reader.GetValueOrDefault(6, 0),
                                Thread = threadID
                            }
                        );
                        lastRN = reader.GetInt32(7);
                    }
                    postPage.Marker = lastRN != null ?
                                        (lastRN.Value + 1).ToString():
                                        marker;
                }
            }     
            return postPage;
        }

        private async Task<PostPageDataContract> SelectParentTree(NpgsqlConnection conn,
                                                      string marker,
                                                      int? limit,
                                                      long threadID,
                                                      bool desc)
        {
            var postPage = new PostPageDataContract();
            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = string.Format(
                        ThreadSqlConstants.SqlSelectPostsParentTree,
                        desc ? "desc": ""
                );

                int from = Int32.TryParse(marker, out int fromInt) ? fromInt : 1;
                int to = limit.HasValue ? from + limit.Value : int.MaxValue;
                cmd.Parameters.Add( new NpgsqlParameter("@id", threadID) );
                cmd.Parameters.Add( new NpgsqlParameter("@from", from));
                cmd.Parameters.Add( new NpgsqlParameter("@to", to));

                using (var reader = cmd.ExecuteReader())
                {
                    postPage.Posts = new List<PostDetailsDataContract>();
                    int? lastRN = null; 
                    while(reader.Read())
                    {
                        postPage.Posts.Add(
                            new PostDetailsDataContract()
                            {
                                ID = reader.GetInt32(0),
                                Author = reader.GetString(1),
                                Created = reader
                                            .GetTimeStamp(2)
                                            .DateTime
                                            .ToString("yyyy-MM-ddTHH:mm:ss.fff+03:00"),
                                Forum = reader.GetString(3),
                                IsEdited = reader.GetBoolean(4),
                                Message = reader.GetValueOrDefault(5, ""),
                                Parent = reader.GetValueOrDefault(6, 0),
                                Thread = threadID
                            }
                        );
                        lastRN = reader.GetInt32(7);
                    }
                    postPage.Marker = lastRN != null ?
                                        (lastRN.Value + 1).ToString():
                                        marker;
                }
            }     
            return postPage;
        }
    }
}