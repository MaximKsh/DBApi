using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using Microsoft.AspNetCore.Mvc;
using KashirinDBApi.Controllers.DataContracts;

namespace KashirinDBApi.Controllers
{
    public class ThreadController : Controller
    {
        #region sql
       

        #endregion

        [Route("api/thread/{slug_or_id}/creat sluge")]
        [HttpPost]
        public JsonResult Create(string slug_or_id)
        {
            DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(List<PostDetailsDataContract>));
            var posts = js.ReadObject(Request.Body);

            return new JsonResult( "" );
        }


        [Route("api/thread/{slug_or_id}/details")]
        [HttpPost]
        public JsonResult DetailsPost(string slug_or_id)
        {
            DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(ThreadUpdateDataContract));
            var threadUpdate = js.ReadObject(Request.Body);

            return new JsonResult( "" );
        }

        [Route("api/thread/{slug_or_id}/details")]
        [HttpGet]
        public JsonResult DetailsGet(string slug_or_id)
        {
            return new JsonResult( "" );
        }


        [Route("api/thread/{slug_or_id}/posts")]
        [HttpGet]
        public JsonResult Posts(string slug_or_id)
        {
            return new JsonResult( "" );
        }
        
        [Route("api/thread/{slug_or_id}/vote")]
        [HttpPost]
        public JsonResult Vote(string slug_or_id)
        {
            DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(VoteDataContract));
            var vote = js.ReadObject(Request.Body);

            return new JsonResult( "" );
        }
        
    }
}