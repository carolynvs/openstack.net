using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenStack.Serialization;

namespace OpenStack.Compute.v2_1
{
    /// <summary />
    public class ApiVersion : IHaveExtraData
    {
        /// <summary />
        [JsonProperty("id")]
        public Identifier Id { get; set; }

        /// <summary />
        [JsonProperty("status")]
        public string Status { get; set; }

        /// <summary />
        [JsonProperty("version")]
        public string Version { get; set; }

        /// <summary />
        [JsonProperty("min_version")]
        public string MinimumVersion { get; set; }

        /// <summary />
        [JsonProperty("updated")]
        public DateTimeOffset Updated { get; set; }

        /// <summary />
        [JsonExtensionData]
        IDictionary<string, JToken> IHaveExtraData.Data { get; set; } = new Dictionary<string, JToken>();
    }
}