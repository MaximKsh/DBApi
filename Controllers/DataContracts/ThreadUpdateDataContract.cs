using System.Runtime.Serialization;

namespace KashirinDBApi.Controllers.DataContracts
{
    // ответная
    // Для forum/create
    // forum/slug/details
    [DataContract]
    public class ThreadUpdateDataContract 
    {
        [DataMember(Name = "message")]
        public string Message { get; set; }
        [DataMember(Name = "title")]
        public string Title { get; set; }
    }

}