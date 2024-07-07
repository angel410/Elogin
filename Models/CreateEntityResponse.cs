using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace eLogin.Models
{
    public class CreateEntityResponse
    {
        [JsonIgnore]
        public Guid Id { get; set; }

        [JsonIgnore]
        public Guid RequestId { get; set; }

        public bool IsSuccess { get; set; }

        public string FailureReason { get; set; }

        public Guid EntityId { get; set; }

        public DateTime DateTime { get; set; }
    }
}
