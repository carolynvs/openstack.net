using System.Collections.Generic;
using Newtonsoft.Json;
using OpenStack.Serialization;

namespace OpenStack.Compute.v2_1.Serialization
{
    /// <summary>
    /// Represents a collection of flavor resources of the <see cref="ComputeService"/>.
    /// </summary>
    /// <threadsafety static="true" instance="false"/>
    public class ApiVersionCollection<T> : ResourceCollection<T>
    {
        /// <summary>
        /// The requested flavors.
        /// </summary>
        [JsonProperty("versions")]
        protected IList<T> Versions => Items;
    }

    /// <summary>
    /// Represents a collection of references to flavor resources of the <see cref="ComputeService"/>.
    /// </summary>
    /// <threadsafety static="true" instance="false"/>
    public class ApiVersionCollection : ApiVersionCollection<ApiVersion>
    { }
}