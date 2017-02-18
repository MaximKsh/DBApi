using System.Runtime.Serialization;

namespace KashirinDBApi.Controllers.DataContracts
{
    // запрос ответ
    // Для forum/slug/create
    // Для thread/slug/details
    [DataContract]
    public class ThreadDetailsDataContract 
    {
        [DataMember(Name = "author")]
        public string Author { get; set; }
        [DataMember(Name = "created")]
        public string Created { get; set; }
        [DataMember(Name = "forum")]
        public string Forum { get; set; }
        [DataMember(Name = "id")]
        public long ID { get; set; }
        [DataMember(Name = "message")]
        public string Message { get; set; }
        [DataMember(Name = "slug")]
        public string Slug { get; set; }
        [DataMember(Name = "title")]
        public string Title { get; set; }
        [DataMember(Name = "votes")]
        public long Votes { get; set; }
    }

}