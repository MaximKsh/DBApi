using System.Runtime.Serialization;

namespace KashirinDBApi.Controllers.DataContracts
{
    // запрос ответ
    // Для thread/slug/vote
    [DataContract]
    public class VoteDataContract 
    {
        [DataMember(Name = "nickname")]
        public string Nickname { get; set; }
        [DataMember(Name = "voice")]
        public long Voice { get; set; }
    }

}