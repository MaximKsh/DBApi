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

        private (long?, string, long?, string) PreselectThreadAndForum(NpgsqlConnection conn,
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

                using(var reader = cmd.ExecuteReader())
                {
                    if(reader.Read())
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

        private (long?, string, long?) PreselectThreadAndUser(NpgsqlConnection conn,
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

                using(var reader = cmd.ExecuteReader())
                {
                    if(reader.Read())
                    {
                        threadID = reader.GetInt32(0);
                        threadSlug = reader.GetValueOrDefault(1, "");
                        userID = reader.GetInt32(2);
                    }
                }
            }
            return (threadID, threadSlug, userID);
        }

        private List<PostDetailsDataContract> InsertPosts(
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
                cmd.CommandText = ThreadSqlConstants.SqlInsertPosts;

                cmd.Parameters.Add(Helper.NewNullableParameter("@created", created, NpgsqlDbType.Timestamp));
                cmd.Parameters.Add(Helper.NewNullableParameter("@forum_id", forumID, NpgsqlDbType.Integer));
                cmd.Parameters.Add(Helper.NewNullableParameter("@forum_slug", forumSlug));
                cmd.Parameters.Add(Helper.NewNullableParameter("@thread_id", threadID, NpgsqlDbType.Integer));
                cmd.Parameters.Add(Helper.NewNullableParameter("@thread_slug", threadSlug));

                NpgsqlParameter authorNameParam = new NpgsqlParameter("@author_name", NpgsqlDbType.Varchar);
                NpgsqlParameter parentIDParam = new NpgsqlParameter("@parentID", NpgsqlDbType.Integer);
                NpgsqlParameter messageParam = new NpgsqlParameter("@message", NpgsqlDbType.Varchar);
                cmd.Parameters.AddRange(new NpgsqlParameter[]{authorNameParam, parentIDParam, messageParam});

                if(!cmd.IsPrepared && posts.Count > 3)
                {
                    cmd.Prepare();
                }
                bool conflict = false;
                var transaction = conn.BeginTransaction();
                foreach(var post in posts)
                {
                    authorNameParam.Value = post.Author;
                    parentIDParam.Value = post.Parent;
                    messageParam.Value = post.Message;
                    using(var reader = cmd.ExecuteReader())
                    {
                        if(reader.Read())
                        {
                            if(reader.IsDBNull(4))
                            {
                                conflict = true;
                                break;
                            }

                            var createdPost = new PostDetailsDataContract();
                            createdPost.ID = reader.GetInt32(0);
                            createdPost.Author = post.Author;
                            createdPost.Created = reader
                                            .GetTimeStamp(1)
                                            .DateTime
                                            .ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");
                            createdPost.Thread = threadID;
                            createdPost.Forum = forumSlug;
                            createdPost.IsEdited = reader.GetBoolean(2);
                            createdPost.Message = reader.GetValueOrDefault(3, "");
                            createdPost.Parent = reader.GetInt32(4);
                            createdPosts.Add(createdPost);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                if(createdPosts.Count == posts.Count)
                {
                    transaction.Commit();
                    Response.StatusCode = 201;
                }
                else
                {
                    transaction.Rollback();
                    Response.StatusCode = conflict ? 409 : 404;
                }

                
            }
            return createdPosts;
        }

        [Route("api/thread/{slug_or_id}/create")]
        [HttpPost]
        public JsonResult Create(string slug_or_id)
        {
            var created = DateTime.Now;
            DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(List<PostDetailsDataContract>));
            var posts = (List<PostDetailsDataContract>)js.ReadObject(Request.Body);

            List<PostDetailsDataContract> createdPosts = null;
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                conn.Open();
                var (threadID, threadSlug, forumID, forumSlug) = PreselectThreadAndForum(conn, slug_or_id);

                if( threadID.HasValue 
                    && forumID.HasValue)
                {
                    createdPosts = InsertPosts(
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
                                            .ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");
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
                                            .ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");
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
        public JsonResult Posts(
            string slug_or_id, 
            int? limit,
            string marker, 
            string sort = "flat",
            bool desc = false )
        {
            var postPage = new PostPageDataContract();
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                conn.Open();
                var (threadID, threadSlug, forumID, forumSlug) = PreselectThreadAndForum(conn, slug_or_id);

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
                        postPage = SelectFlat(conn, marker, limit, threadID.Value, desc);
                    }
                    else if(sort == "tree")
                    {
                        postPage = SelectTree(conn, marker, limit, threadID.Value, desc);
                    }
                    else if(sort == "parent_tree")
                    {
                        postPage = SelectParentTree(conn, marker, limit, threadID.Value, desc);
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
        public JsonResult Vote(string slug_or_id)
        {
            DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(VoteDataContract));
            var vote = (VoteDataContract)js.ReadObject(Request.Body);
            var updatedThread = new ThreadDetailsDataContract();
            
            using (var conn = new NpgsqlConnection(Configuration["connection_string"]))
            {
                conn.Open();
                var (threadID, threadSlug, userID) = PreselectThreadAndUser(conn, slug_or_id, vote.Nickname);

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
                            if(reader.Read())
                            {
                                updatedThread.Author = reader.GetValueOrDefault(0, "");
                                updatedThread.Created = reader
                                                .GetTimeStamp(1)
                                                .DateTime
                                                .ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");
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
        

        private PostPageDataContract SelectFlat(NpgsqlConnection conn,
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
                                            .ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"),
                                Forum = reader.GetValueOrDefault(3, ""),
                                IsEdited = reader.GetBoolean(4),
                                Message = reader.GetValueOrDefault(5, ""),
                                Parent = reader.GetValueOrDefault(6, 0L),
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

        private PostPageDataContract SelectTree(NpgsqlConnection conn,
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
                                            .ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"),
                                Forum = reader.GetString(3),
                                IsEdited = reader.GetBoolean(4),
                                Message = reader.GetValueOrDefault(5, ""),
                                Parent = reader.GetValueOrDefault(6, 0L),
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

        private PostPageDataContract SelectParentTree(NpgsqlConnection conn,
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
                                            .ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"),
                                Forum = reader.GetString(3),
                                IsEdited = reader.GetBoolean(4),
                                Message = reader.GetValueOrDefault(5, ""),
                                Parent = reader.GetValueOrDefault(6, 0L),
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