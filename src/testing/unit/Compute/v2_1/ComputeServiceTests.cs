using System;
using System.Linq;
using OpenStack.Compute.v2_1.Serialization;
using OpenStack.Serialization;
using OpenStack.Synchronous;
using OpenStack.Testing;
using Xunit;

namespace OpenStack.Compute.v2_1
{
    public class ComputeServiceTests
    {
        private readonly ComputeService _compute;

        public ComputeServiceTests()
        {
            _compute = new ComputeService(Stubs.AuthenticationProvider, "region");
        }

        [Fact]
        public void ListSupportedVersions()
        {
            using (var httpTest = new HttpTest())
            {
                httpTest.RespondWithJson(new ApiVersionCollection
                {
                    new ApiVersion {Id = "v2.0", Status = "SUPPORTED"},
                    new ApiVersion {Id = "v2.1", Status = "CURRENT", Version = "2.12", MinimumVersion = "2.1"},
                });

                var result = _compute.ListSupportedVersions();

                httpTest.ShouldHaveCalled("*/");
                Assert.NotNull(result);
                Assert.Equal(2, result.Count());
            }
        }

        [Theory]
        [InlineData("2.1")]
        [InlineData("2.5")]
        [InlineData("2.12")]
        public void SupportsVersion(string microversion)
        {
            using (var httpTest = new HttpTest())
            {
                httpTest.RespondWithJson(new ApiVersionCollection
                {
                    new ApiVersion {Id = "v2.0", Status = "SUPPORTED"},
                    new ApiVersion {Id = "v2.1", Status = "CURRENT", Version = "2.12", MinimumVersion = "2.1"},
                });

                Assert.True(_compute.SupportsMicroversion(microversion));
            }
        }
    }
}
