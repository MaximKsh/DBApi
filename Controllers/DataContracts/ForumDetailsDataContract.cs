using System.Runtime.Serialization;

namespace KashirinDBApi.Controllers.DataContracts
{
    // ответная
    // Для forum/create
    // forum/slug/details
    [DataContract]
    public class ForumDetailsDataContract 
    {
        [DataMember(Name = "posts",  EmitDefaultValue = false)]
        public long Posts { get; set; }
        [DataMember(Name = "slug")]
        public string Slug { get; set; }
        [DataMember(Name = "threads")]
        public long Threads { get; set; }
        [DataMember(Name = "title")]
        public string Title { get; set; }
        [DataMember(Name = "user")]
        public string User { get; set; }
    }

}