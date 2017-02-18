using System.Runtime.Serialization;

namespace KashirinDBApi.Controllers.DataContracts
{
    // запрос
    // Для post/id/details
    [DataContract]
    public class PostUpdateDataContract 
    {
        [DataMember(Name = "message")]
        public string Message { get; set; }
    }

}