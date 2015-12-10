using System;
using System.Extensions;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Flurl;
using Flurl.Extensions;
using Flurl.Http;
using Newtonsoft.Json.Linq;
using OpenStack.Authentication;
using OpenStack.Extensions;
using OpenStack.Serialization;

namespace OpenStack.Compute.v2_1
{
    /// <summary>
    /// Builds requests to the Compute API which can be further customized and then executed.
    /// <para>Intended for custom implementations.</para>
    /// </summary>
    /// <seealso href="http://developer.openstack.org/api-ref-compute-v2.1.html">OpenStack Compute API v2.1 Overview</seealso>
    public class ComputeApiBuilder : ISupportMicroversions
    {
        /// <summary />
        protected readonly IAuthenticationProvider AuthenticationProvider;

        /// <summary />
        protected readonly ServiceUrlBuilder UrlBuilder;

        /// <summary />
        public ComputeApiBuilder(IServiceType serviceType, IAuthenticationProvider authenticationProvider, string region)
            : this(serviceType, authenticationProvider, region, "2.1")
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ComputeApiBuilder"/> class.
        /// </summary>
        /// <param name="serviceType">The service type for the desired compute provider.</param>
        /// <param name="authenticationProvider">The authentication provider.</param>
        /// <param name="region">The region.</param>
        /// <param name="microversion">The requested microversion.</param>
        protected ComputeApiBuilder(IServiceType serviceType, IAuthenticationProvider authenticationProvider, string region, string microversion)
        {
            if (serviceType == null)
                throw new ArgumentNullException("serviceType");
            if (authenticationProvider == null)
                throw new ArgumentNullException("authenticationProvider");
            if (string.IsNullOrEmpty(region))
                throw new ArgumentException("region cannot be null or empty", "region");

            AuthenticationProvider = authenticationProvider;
            UrlBuilder = new ServiceUrlBuilder(serviceType, authenticationProvider, region);
            Microversion = microversion;
        }

        /// <summary />
        string ISupportMicroversions.MicroversionHeader => "X-OpenStack-Nova-API-Version";

        /// <summary />
        public string Microversion { get; }

        private void SetOwner(IServiceResource resource)
        {
            resource.PropogateOwner(this);
        }

        #region Servers

        /// <summary />
        public virtual async Task<T> GetServerAsync<T>(string serverId, CancellationToken cancellationToken = default(CancellationToken))
            where T : IServiceResource
        {
            var result = await BuildGetServerAsync(serverId, cancellationToken)
                .SendAsync()
                .ReceiveJson<T>();
            SetOwner(result);
            return result;
        }

        /// <summary />
        public virtual async Task<PreparedRequest> BuildGetServerAsync(string serverId, CancellationToken cancellationToken = default(CancellationToken))
        {
            Url endpoint = await UrlBuilder.GetEndpoint(cancellationToken).ConfigureAwait(false);

            return endpoint
                .AppendPathSegments($"servers/{serverId}")
                .Authenticate(AuthenticationProvider)
                .PrepareGet(cancellationToken);
        }

        /// <summary />
        public virtual async Task<PreparedRequest> BuildCreateServerAsync(object server, CancellationToken cancellationToken = default(CancellationToken))
        {
            Url endpoint = await UrlBuilder.GetEndpoint(cancellationToken).ConfigureAwait(false);

            return endpoint
                .AppendPathSegments("servers")
                .Authenticate(AuthenticationProvider)
                .PreparePostJson(server, cancellationToken);
        }

        /// <summary />
        public virtual async Task<T> CreateServerAsync<T>(object server, CancellationToken cancellationToken = default(CancellationToken))
            where T : IServiceResource
        {
            var result = await BuildCreateServerAsync(server, cancellationToken).SendAsync().ReceiveJson<T>();
            SetOwner(result);
            return result;
        }

        /// <summary>
        /// Waits for the server to become active.
        /// </summary>
        /// <param name="serverId">The server identifier.</param>
        /// <param name="refreshDelay">The amount of time to wait between requests.</param>
        /// <param name="timeout">The amount of time to wait before throwing a <see cref="TimeoutException"/>.</param>
        /// <param name="progress">The progress callback.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <exception cref="TimeoutException">If the <paramref name="timeout"/> value is reached.</exception>
        /// <exception cref="FlurlHttpException">If the API call returns a bad <see cref="HttpStatusCode"/>.</exception>
        public async Task<Server> WaitUntilServerIsActiveAsync(string serverId, TimeSpan? refreshDelay = null, TimeSpan? timeout = null, IProgress<bool> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(serverId))
                throw new ArgumentNullException("serverId");

            refreshDelay = refreshDelay ?? TimeSpan.FromSeconds(5);
            timeout = timeout ?? TimeSpan.FromMinutes(5);

            using (var timeoutSource = new CancellationTokenSource(timeout.Value))
            using (var rootCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token))
            {
                while (true)
                {
                    Server server = await GetServerAsync<Server>(serverId, cancellationToken).ConfigureAwait(false);
                    if (server.Status == ServerStatus.Error)
                        throw new ComputeOperationFailedException();

                    bool complete = server.Status == ServerStatus.Active;

                    progress?.Report(complete);

                    if (complete)
                        return server;

                    try
                    {
                        await Task.Delay(refreshDelay.Value, rootCancellationToken.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException ex)
                    {
                        if (timeoutSource.IsCancellationRequested)
                            throw new TimeoutException($"The requested timeout of {timeout.Value.TotalSeconds} seconds has been reached while waiting for the server ({serverId}) to be deleted.", ex);

                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Waits for the server to be deleted.
        /// </summary>
        /// <param name="serverId">The server identifier.</param>
        /// <param name="refreshDelay">The amount of time to wait between requests.</param>
        /// <param name="timeout">The amount of time to wait before throwing a <see cref="TimeoutException"/>.</param>
        /// <param name="progress">The progress callback.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <exception cref="TimeoutException">If the <paramref name="timeout"/> value is reached.</exception>
        /// <exception cref="FlurlHttpException">If the API call returns a bad <see cref="HttpStatusCode"/>.</exception>
        public async Task WaitUntilServerIsDeletedAsync(string serverId, TimeSpan? refreshDelay = null, TimeSpan? timeout = null, IProgress<bool> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(serverId))
                throw new ArgumentNullException("serverId");

            refreshDelay = refreshDelay ?? TimeSpan.FromSeconds(5);
            timeout = timeout ?? TimeSpan.FromMinutes(5);

            using (var timeoutSource = new CancellationTokenSource(timeout.Value))
            using (var rootCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token))
            {
                while (true)
                {
                    bool complete;
                    try
                    {
                        Server server = await GetServerAsync<Server>(serverId, cancellationToken).ConfigureAwait(false);
                        if (server.Status == ServerStatus.Error)
                            throw new ComputeOperationFailedException();

                        complete = server.Status == ServerStatus.Deleted;
                    }
                    catch (FlurlHttpException httpError)
                    {
                        if (httpError.Call.HttpStatus == HttpStatusCode.NotFound)
                            complete = true;
                        else
                            throw;
                    }
                    
                    progress?.Report(complete);

                    if (complete)
                        return;

                    try
                    {
                        await Task.Delay(refreshDelay.Value, rootCancellationToken.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException ex)
                    {
                        if (timeoutSource.IsCancellationRequested)
                            throw new TimeoutException($"The requested timeout of {timeout.Value.TotalSeconds} seconds has been reached while waiting for the server ({serverId}) to be deleted.", ex);

                        throw;
                    }
                }
            }
        }

        /// <summary />
        public virtual async Task<TPage>  ListServersAsync<TPage>(IQueryStringBuilder queryString, CancellationToken cancellationToken = default(CancellationToken))
            where TPage : IPageBuilder<TPage>, IServiceResource
        {
            Url initialRequestUrl = await BuildListServersUrlAsync(queryString, cancellationToken);
            return await ListServersAsync<TPage>(initialRequestUrl, cancellationToken);
        }

        /// <summary />
        public virtual async Task<TPage> ListServersAsync<TPage>(Url url, CancellationToken cancellationToken)
            where TPage : IPageBuilder<TPage>, IServiceResource
        {
            var results = await url
                .Authenticate(AuthenticationProvider)
                .SetMicroversion(this)
                .PrepareGet(cancellationToken)
                .SendAsync()
                .ReceiveJson<TPage>();

            results.SetNextPageHandler(ListServersAsync<TPage>);
            SetOwner(results);

            return results;
        }

        /// <summary />
        public virtual async Task<Url> BuildListServersUrlAsync(IQueryStringBuilder queryString, CancellationToken cancellationToken = default(CancellationToken))
        {
            Url endpoint = await UrlBuilder.GetEndpoint(cancellationToken).ConfigureAwait(false);

            return endpoint
                .AppendPathSegment("servers")
                .SetQueryParams(queryString?.Build());
        }

        /// <summary />
        public virtual async Task<TPage> ListServerDetailsAsync<TPage>(IQueryStringBuilder queryString, CancellationToken cancellationToken = default(CancellationToken))
            where TPage : IPageBuilder<TPage>, IServiceResource
        {
            Url initialRequestUrl = await BuildListServerDetailsUrlAsync(queryString, cancellationToken);
            return await ListServersAsync<TPage>(initialRequestUrl, cancellationToken);
        }

        /// <summary />
        public virtual async Task<TPage> ListServerDetailsAsync<TPage>(Url url, CancellationToken cancellationToken)
            where TPage : IPageBuilder<TPage>, IServiceResource
        {
            var results = await url
                .Authenticate(AuthenticationProvider)
                .SetMicroversion(this)
                .PrepareGet(cancellationToken)
                .SendAsync()
                .ReceiveJson<TPage>();

            results.SetNextPageHandler(ListServerDetailsAsync<TPage>);
            SetOwner(results);
            return results;
        }

        /// <summary />
        public virtual async Task<Url> BuildListServerDetailsUrlAsync(IQueryStringBuilder queryString, CancellationToken cancellationToken = default(CancellationToken))
        {
            Url endpoint = await UrlBuilder.GetEndpoint(cancellationToken).ConfigureAwait(false);

            return endpoint
                .AppendPathSegment("servers/detail")
                .SetQueryParams(queryString?.Build());
        }

        /// <summary />
        public virtual async Task<PreparedRequest> BuildUpdateServerAsync(string serverId, object server, CancellationToken cancellationToken = default(CancellationToken))
        {
            Url endpoint = await UrlBuilder.GetEndpoint(cancellationToken).ConfigureAwait(false);

            return endpoint
                .AppendPathSegments($"servers/{serverId}")
                .Authenticate(AuthenticationProvider)
                .PreparePutJson(server, cancellationToken);
        }

        /// <summary />
        public virtual async Task<T> UpdateServerAsync<T>(string serverId, object server, CancellationToken cancellationToken = default(CancellationToken))
            where T : IServiceResource
        {
            var result = await BuildUpdateServerAsync(serverId, server, cancellationToken).SendAsync().ReceiveJson<T>();
            SetOwner(result);
            return result;
        }

        /// <summary />
        public virtual Task DeleteServerAsync(string serverId, CancellationToken cancellationToken = default(CancellationToken))
        {
            return BuildDeleteServerAsync(serverId, cancellationToken).SendAsync();
        }

        /// <summary />
        public virtual async Task<PreparedRequest> BuildDeleteServerAsync(string serverId, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (serverId == null)
                throw new ArgumentNullException("serverId");

            Url endpoint = await UrlBuilder.GetEndpoint(cancellationToken).ConfigureAwait(false);

            return (PreparedRequest)endpoint
                .AppendPathSegments("servers", serverId)
                .Authenticate(AuthenticationProvider)
                .PrepareDelete(cancellationToken)
                .AllowHttpStatus(HttpStatusCode.NotFound);
        }

        /// <summary />
        public virtual async Task<T> CreateSnapshotAsync<T>(string serverId, object snapshot, CancellationToken cancellationToken = default(CancellationToken))
            where T : IServiceResource
        {
            var response = await BuildCreateSnapshotAsync(serverId, snapshot, cancellationToken).SendAsync();
            Identifier imageId = response.Headers.Location.Segments.Last(); // grab id off the end of the url, e.g. http://172.29.236.100:9292/images/baaab9b9-3635-429e-9969-2899a7cf2d97
            return await GetImageAsync<T>(imageId, cancellationToken);
        }

        /// <summary>
        /// Waits for an image to become active.
        /// </summary>
        /// <param name="imageId">The image identifier.</param>
        /// <param name="refreshDelay">The amount of time to wait between requests.</param>
        /// <param name="timeout">The amount of time to wait before throwing a <see cref="TimeoutException"/>.</param>
        /// <param name="progress">The progress callback.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <exception cref="TimeoutException">If the <paramref name="timeout"/> value is reached.</exception>
        /// <exception cref="FlurlHttpException">If the API call returns a bad <see cref="HttpStatusCode"/>.</exception>
        public async Task<Image> WaitUntilImageIsActiveAsync(string imageId, TimeSpan? refreshDelay = null, TimeSpan? timeout = null, IProgress<bool> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(imageId))
                throw new ArgumentNullException("imageId");

            refreshDelay = refreshDelay ?? TimeSpan.FromSeconds(5);
            timeout = timeout ?? TimeSpan.FromMinutes(5);

            using (var timeoutSource = new CancellationTokenSource(timeout.Value))
            using (var rootCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token))
            {
                while (true)
                {
                    Image image = await GetImageAsync<Image>(imageId, cancellationToken).ConfigureAwait(false);
                    if (image.Status == ImageStatus.Error)
                        throw new ComputeOperationFailedException();

                    bool complete = image.Status == ImageStatus.Active;

                    progress?.Report(complete);

                    if (complete)
                        return image;

                    try
                    {
                        await Task.Delay(refreshDelay.Value, rootCancellationToken.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException ex)
                    {
                        if (timeoutSource.IsCancellationRequested)
                            throw new TimeoutException($"The requested timeout of {timeout.Value.TotalSeconds} seconds has been reached while waiting for the snapshot ({imageId}) to complete.", ex);

                        throw;
                    }
                }
            }
        }

        /// <summary />
        public virtual async Task<PreparedRequest> BuildCreateSnapshotAsync(string serverId, object snapshot, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (serverId == null)
                throw new ArgumentNullException("serverId");

            Url endpoint = await UrlBuilder.GetEndpoint(cancellationToken).ConfigureAwait(false);

            return endpoint
                .AppendPathSegments("servers", serverId, "action")
                .Authenticate(AuthenticationProvider)
                .PreparePostJson(snapshot, cancellationToken);
        }

        /// <summary>
        /// Waits for the image to be deleted.
        /// </summary>
        /// <param name="imageId">The image identifier.</param>
        /// <param name="refreshDelay">The amount of time to wait between requests.</param>
        /// <param name="timeout">The amount of time to wait before throwing a <see cref="TimeoutException"/>.</param>
        /// <param name="progress">The progress callback.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <exception cref="TimeoutException">If the <paramref name="timeout"/> value is reached.</exception>
        /// <exception cref="FlurlHttpException">If the API call returns a bad <see cref="HttpStatusCode"/>.</exception>
        public async Task WaitUntilImageIsDeletedAsync(string imageId, TimeSpan? refreshDelay = null, TimeSpan? timeout = null, IProgress<bool> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(imageId))
                throw new ArgumentNullException("imageId");

            refreshDelay = refreshDelay ?? TimeSpan.FromSeconds(5);
            timeout = timeout ?? TimeSpan.FromMinutes(5);

            using (var timeoutSource = new CancellationTokenSource(timeout.Value))
            using (var rootCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token))
            {
                while (true)
                {
                    bool complete;
                    try
                    {
                        Image server = await GetImageAsync<Image>(imageId, cancellationToken).ConfigureAwait(false);
                        if (server.Status == ImageStatus.Error)
                            throw new ComputeOperationFailedException();

                        complete = server.Status == ImageStatus.Deleted;
                    }
                    catch (FlurlHttpException httpError)
                    {
                        if (httpError.Call.HttpStatus == HttpStatusCode.NotFound)
                            complete = true;
                        else
                            throw;
                    }

                    progress?.Report(complete);

                    if (complete)
                        return;

                    try
                    {
                        await Task.Delay(refreshDelay.Value, rootCancellationToken.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException ex)
                    {
                        if (timeoutSource.IsCancellationRequested)
                            throw new TimeoutException($"The requested timeout of {timeout.Value.TotalSeconds} seconds has been reached while waiting for the image ({imageId}) to be deleted.", ex);

                        throw;
                    }
                }
            }
        }

        /// <summary />
        public virtual Task<T> GetVncConsoleAsync<T>(string serverId, object type, CancellationToken cancellationToken = default(CancellationToken))
        {
            return BuildGetVncConsoleRequestAsync(serverId, type, cancellationToken)
                .SendAsync().ReceiveJson<T>();
        }

        /// <summary />
        public virtual async Task<PreparedRequest> BuildGetVncConsoleRequestAsync(string serverId, object type, CancellationToken cancellationToken = default(CancellationToken))
        {
            Url endpoint = await UrlBuilder.GetEndpoint(cancellationToken).ConfigureAwait(false);

            var request = JObject.Parse($"{{ 'os-getVNCConsole': {{ 'type': '{type}' }} }}");
            return endpoint
                .AppendPathSegments("servers", serverId, "action")
                .Authenticate(AuthenticationProvider)
                .SetMicroversion(this)
                .PreparePostJson(request, cancellationToken);
        }

        #endregion

        #region Flavors
        /// <summary />
        public virtual async Task<T> GetFlavorAsync<T>(string flavorId, CancellationToken cancellationToken = default(CancellationToken))
            where T : IServiceResource
        {
            var result = await BuildGetFlavorAsync(flavorId, cancellationToken)
                .SendAsync()
                .ReceiveJson<T>();
            SetOwner(result);
            return result;
        }

        /// <summary />
        public virtual async Task<PreparedRequest> BuildGetFlavorAsync(string flavorId, CancellationToken cancellationToken = default(CancellationToken))
        {
            Url endpoint = await UrlBuilder.GetEndpoint(cancellationToken).ConfigureAwait(false);

            return endpoint
                .AppendPathSegments($"flavors/{flavorId}")
                .Authenticate(AuthenticationProvider)
                .PrepareGet(cancellationToken);
        }

        /// <summary />
        public virtual async Task<T> ListFlavorsAsync<T>(CancellationToken cancellationToken = default(CancellationToken))
            where T : IServiceResource
        {
            var result = await BuildListFlavorsAsync(cancellationToken)
                .SendAsync()
                .ReceiveJson<T>();
            SetOwner(result);
            return result;
        }

        /// <summary />
        public virtual async Task<PreparedRequest> BuildListFlavorsAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            Url endpoint = await UrlBuilder.GetEndpoint(cancellationToken).ConfigureAwait(false);

            return endpoint
                .AppendPathSegments("flavors")
                .Authenticate(AuthenticationProvider)
                .PrepareGet(cancellationToken);
        }

        /// <summary />
        public virtual async Task<T> ListFlavorDetailsAsync<T>(CancellationToken cancellationToken = default(CancellationToken))
            where T : IServiceResource
        {
            var result = await BuildListFlavorDetailsAsync(cancellationToken)
                .SendAsync()
                .ReceiveJson<T>();
            SetOwner(result);
            return result;
        }

        /// <summary />
        public virtual async Task<PreparedRequest> BuildListFlavorDetailsAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            Url endpoint = await UrlBuilder.GetEndpoint(cancellationToken).ConfigureAwait(false);

            return endpoint
                .AppendPathSegments("flavors/detail")
                .Authenticate(AuthenticationProvider)
                .PrepareGet(cancellationToken);
        }

        #endregion

        #region Images
        /// <summary />
        public virtual async Task<T> GetImageAsync<T>(string imageId, CancellationToken cancellationToken = default(CancellationToken))
            where T : IServiceResource
        {
            var result = await BuildGetImageAsync(imageId, cancellationToken)
                .SendAsync()
                .ReceiveJson<T>();
            SetOwner(result);
            return result;
        }

        /// <summary />
        public virtual async Task<PreparedRequest> BuildGetImageAsync(string imageId, CancellationToken cancellationToken = default(CancellationToken))
        {
            Url endpoint = await UrlBuilder.GetEndpoint(cancellationToken).ConfigureAwait(false);

            return endpoint
                .AppendPathSegments($"images/{imageId}")
                .Authenticate(AuthenticationProvider)
                .PrepareGet(cancellationToken);
        }

        /// <summary />
        public virtual async Task<T> GetImageMetadataAsync<T>(string imageId, CancellationToken cancellationToken = default(CancellationToken))
            where T : IServiceResource
        {
            var result = await BuildGetImageMetadataAsync(imageId, cancellationToken)
                .SendAsync()
                .ReceiveJson<T>();
            SetOwner(result);
            return result;
        }

        /// <summary />
        public virtual async Task<PreparedRequest> BuildGetImageMetadataAsync(string imageId, CancellationToken cancellationToken = default(CancellationToken))
        {
            Url endpoint = await UrlBuilder.GetEndpoint(cancellationToken).ConfigureAwait(false);

            return endpoint
                .AppendPathSegments($"images/{imageId}/metadata")
                .Authenticate(AuthenticationProvider)
                .PrepareGet(cancellationToken);
        }

        /// <summary />
        public virtual async Task<TPage> ListImagesAsync<TPage>(IQueryStringBuilder queryString, CancellationToken cancellationToken = default(CancellationToken))
            where TPage : IPageBuilder<TPage>, IServiceResource
        {
            Url initialRequestUrl = await BuildListImagesUrlAsync(queryString, cancellationToken);
            return await ListImagesAsync<TPage>(initialRequestUrl, cancellationToken);
        }

        /// <summary />
        public virtual async Task<TPage> ListImagesAsync<TPage>(Url url, CancellationToken cancellationToken)
            where TPage : IPageBuilder<TPage>, IServiceResource
        {
            var results = await url
                .Authenticate(AuthenticationProvider)
                .SetMicroversion(this)
                .PrepareGet(cancellationToken)
                .SendAsync()
                .ReceiveJson<TPage>();

            results.SetNextPageHandler(ListImagesAsync<TPage>);
            SetOwner(results);

            return results;
        }

        /// <summary />
        public virtual async Task<Url> BuildListImagesUrlAsync(IQueryStringBuilder queryString, CancellationToken cancellationToken = default(CancellationToken))
        {
            Url endpoint = await UrlBuilder.GetEndpoint(cancellationToken).ConfigureAwait(false);

            return endpoint
                .AppendPathSegment("images")
                .SetQueryParams(queryString?.Build());
        }

        /// <summary />
        public virtual async Task<TPage> ListImageDetailsAsync<TPage>(IQueryStringBuilder queryString, CancellationToken cancellationToken = default(CancellationToken))
            where TPage : IPageBuilder<TPage>, IServiceResource
        {
            Url initialRequestUrl = await BuildListImageDetailsUrlAsync(queryString, cancellationToken);
            return await ListImagesAsync<TPage>(initialRequestUrl, cancellationToken);
        }

        /// <summary />
        public virtual async Task<TPage> ListImageDetailsAsync<TPage>(Url url, CancellationToken cancellationToken)
            where TPage : IPageBuilder<TPage>, IServiceResource
        {
            var results = await url
                .Authenticate(AuthenticationProvider)
                .SetMicroversion(this)
                .PrepareGet(cancellationToken)
                .SendAsync()
                .ReceiveJson<TPage>();

            results.SetNextPageHandler(ListImageDetailsAsync<TPage>);
            SetOwner(results);
            return results;
        }

        /// <summary />
        public virtual async Task<Url> BuildListImageDetailsUrlAsync(IQueryStringBuilder queryString, CancellationToken cancellationToken = default(CancellationToken))
        {
            Url endpoint = await UrlBuilder.GetEndpoint(cancellationToken).ConfigureAwait(false);

            return endpoint
                .AppendPathSegment("images/detail")
                .SetQueryParams(queryString?.Build());
        }

        /// <summary /> // this keeps existing, but omitted values
        public virtual async Task<T> UpdateImageMetadataAsync<T>(string imageId, object metadata, bool overwrite = false, CancellationToken cancellationToken = default(CancellationToken))
            where T : IServiceResource
        {
            var result = await BuildUpdateImageMetadataAsync(imageId, metadata, overwrite, cancellationToken)
                .SendAsync()
                .ReceiveJson<T>();
            SetOwner(result);
            return result;
        }

        /// <summary />
        public virtual async Task<PreparedRequest> BuildUpdateImageMetadataAsync(string imageId, object metadata, bool overwrite = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            Url endpoint = await UrlBuilder.GetEndpoint(cancellationToken).ConfigureAwait(false);

            PreparedRequest request = endpoint
                .AppendPathSegments($"images/{imageId}/metadata")
                .Authenticate(AuthenticationProvider);

            if (overwrite)
                return request.PreparePutJson(metadata, cancellationToken);

            return request.PreparePostJson(metadata, cancellationToken);
        }

        /// <summary />
        public virtual Task DeleteImageAsync(string imageId, CancellationToken cancellationToken = default(CancellationToken))
        {
            return BuildDeleteImageAsync(imageId, cancellationToken).SendAsync();
        }

        /// <summary />
        public virtual async Task<PreparedRequest> BuildDeleteImageAsync(string imageId, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (imageId == null)
                throw new ArgumentNullException("imageId");

            Url endpoint = await UrlBuilder.GetEndpoint(cancellationToken).ConfigureAwait(false);

            return (PreparedRequest)endpoint
                .AppendPathSegments("images", imageId)
                .Authenticate(AuthenticationProvider)
                .PrepareDelete(cancellationToken)
                .AllowHttpStatus(HttpStatusCode.NotFound);
        }
        #endregion

        #region Keypairs

        /// <summary />
        public virtual async Task<T> CreateKeyPairAsync<T>(object keypair, CancellationToken cancellationToken = default(CancellationToken))
        {
            PreparedRequest request = await BuildCreateKeyPairRequestAsync(keypair, cancellationToken);
            return await request.SendAsync().ReceiveJson<T>();
        }

        /// <summary />
        public virtual async Task<PreparedRequest> BuildCreateKeyPairRequestAsync(object keypair, CancellationToken cancellationToken = default(CancellationToken))
        {
            Url endpoint = await UrlBuilder.GetEndpoint(cancellationToken).ConfigureAwait(false);

            return endpoint
                .AppendPathSegment("os-keypairs")
                .Authenticate(AuthenticationProvider)
                .SetMicroversion(this)
                .PreparePostJson(keypair, cancellationToken);
        }

        #endregion
    }
}
