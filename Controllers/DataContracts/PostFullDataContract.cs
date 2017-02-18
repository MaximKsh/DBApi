using System.Runtime.Serialization;

namespace KashirinDBApi.Controllers.DataContracts
{
    // запрос ответ
    // Для post/id/details
    [DataContract]
    public class PostFullDataContract 
    {
        [DataMember(Name = "user")]
        public UserProfileDataContract User { get; set; }
        [DataMember(Name = "forum")]
        public ForumDetailsDataContract Forum { get; set; }
        [DataMember(Name = "post")]
        public PostDetailsDataContract Post { get; set; }
        [DataMember(Name = "thread")]
        public ThreadDetailsDataContract Thread { get; set; }
    }

}