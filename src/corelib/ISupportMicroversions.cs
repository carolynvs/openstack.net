using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenStack
{
    /// <summary />
    public interface ISupportMicroversions
    {
        /// <summary />
        string MicroversionHeader { get; }

        /// <summary />
        string Microversion { get; }

        /// <summary />
        Task<IEnumerable<IHaveMicroversions>> GetVersionsAsync(CancellationToken cancellationToken = default(CancellationToken));
    }

    /// <summary />
    public interface IHaveMicroversions
    {
        /// <summary />
        decimal? Version { get; }

        /// <summary />
        decimal? MinimumVersion { get; }
    }
}