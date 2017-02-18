using System.Runtime.Serialization;

namespace KashirinDBApi.Controllers.DataContracts
{
    // ответная
    // Для
    // forum/slug/users
    // user/nickname/create
    // user/nickname/profile
    [DataContract]
    public class UserProfileDataContract 
    {
        [DataMember(Name = "about")]
        public string About { get; set; }
        [DataMember(Name = "email")]
        public string Email { get; set; }
        [DataMember(Name = "fullname")]
        public string Fullname { get; set; }
        [DataMember(Name = "nickname")]
        public string Nickname { get; set; }
    }

}