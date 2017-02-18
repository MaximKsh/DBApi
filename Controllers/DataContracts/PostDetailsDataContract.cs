using System.Runtime.Serialization;

namespace KashirinDBApi.Controllers.DataContracts
{
    // запрос ответ
    // Для thread/slug/vote
    [DataContract]
    public class PostDetailsDataContract 
    {
        [DataMember(Name = "author")]
        public string Author { get; set; }
        [DataMember(Name = "created")]
        public string Created { get; set; }
        [DataMember(Name = "forum")]
        public string Forum { get; set; }
        [DataMember(Name = "id")]
        public long ID { get; set; }
        [DataMember(Name = "isEdited")]
        public bool IsEdited { get; set; }
        [DataMember(Name = "message")]
        public string Message { get; set; }
        [DataMember(Name = "parent")]
        public long Parent { get; set; }
        [DataMember(Name = "thread")]
        public long Thread { get; set; }
    }

}