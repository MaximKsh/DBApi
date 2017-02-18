using System.Runtime.Serialization.Json;
using Microsoft.AspNetCore.Mvc;
using KashirinDBApi.Controllers.DataContracts;
using System.Collections.Generic;

namespace KashirinDBApi.Controllers
{
    public class PostController : Controller
    {
        #region DataContracnts
       

        #endregion


        [Route("api/post/{id}/details")]
        [HttpPost]
        public JsonResult DetailsPost(string id)
        {
            DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(PostUpdateDataContract));
            var post = js.ReadObject(Request.Body);
            
            return new JsonResult( "" );
        }

        [Route("api/post/{id}/details")]
        [HttpGet]
        public JsonResult DetailsGet(string id, string related)
        {

            return new JsonResult( "" );
        }
        
    }
}