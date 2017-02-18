using System.Runtime.Serialization;

namespace KashirinDBApi.Controllers.DataContracts
{
    // ответная
    // Для
    // service/status
    [DataContract]
    public class ServiceStatusDataContract
    {
        [DataMember(Name = "forum")]
        public long Forum { get; set; }
        [DataMember(Name = "post")]
        public long Post { get; set; }
        [DataMember(Name = "thread")]
        public long Thread { get; set; }
        [DataMember(Name = "user")]
        public long User { get; set; }
    }

}