using System.Runtime.Serialization;

namespace KashirinDBApi.Controllers.DataContracts
{
    // запрос ответ
    // Для post/id/details
    [DataContract]
    public class PostFullDataContract 
    {
        [DataMember(Name = "author", EmitDefaultValue = false)]
        public UserProfileDataContract User { get; set; }
        [DataMember(Name = "forum", EmitDefaultValue = false)]
        public ForumDetailsDataContract Forum { get; set; }
        [DataMember(Name = "post", EmitDefaultValue = false)]
        public PostDetailsDataContract Post { get; set; }
        [DataMember(Name = "thread", EmitDefaultValue = false)]
        public ThreadDetailsDataContract Thread { get; set; }
    }

}