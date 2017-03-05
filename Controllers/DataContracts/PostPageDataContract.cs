using System.Collections.Generic;
using System.Runtime.Serialization;

namespace KashirinDBApi.Controllers.DataContracts
{
    [DataContract]
    public class PostPageDataContract 
    {
        [DataMember(Name = "marker", EmitDefaultValue = false)]
        public string Marker { get; set; }
        [DataMember(Name = "posts")]
        public List<PostDetailsDataContract> Posts { get; set; }
    }

}