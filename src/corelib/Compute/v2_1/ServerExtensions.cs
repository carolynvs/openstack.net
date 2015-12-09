﻿using System;
using OpenStack.Compute.v2_1;
using OpenStack.Synchronous.Extensions;

// ReSharper disable once CheckNamespace
namespace OpenStack.Synchronous
{
    /// <summary />
    public static class ServerExtensions_v2_1
    {
        /// <inheritdoc cref="ServerReference.GetServerAsync"/>
        public static Server GetServer(this ServerReference server)
        {
            return server.GetServerAsync().ForceSynchronous();
        }

        /// <inheritdoc cref="Server.WaitUntilActiveAsync"/>
        public static void WaitUntilActive(this Server server, TimeSpan? refreshDelay = null, TimeSpan? timeout = null, IProgress<bool> progress = null)
        {
            server.WaitUntilActiveAsync(refreshDelay, timeout, progress).ForceSynchronous();
        }

        /// <inheritdoc cref="Server.WaitUntilDeletedAsync"/>
        public static void WaitUntilDeleted(this Server server, TimeSpan? refreshDelay = null, TimeSpan? timeout = null, IProgress<bool> progress = null)
        {
            server.WaitUntilDeletedAsync(refreshDelay, timeout, progress).ForceSynchronous();
        }

        /// <inheritdoc cref="Server.UpdateAsync"/>
        public static void Update(this Server server)
        {
            server.UpdateAsync().ForceSynchronous();
        }

        /// <inheritdoc cref="ServerReference.DeleteAsync"/>
        public static void Delete(this Server server)
        {
            server.DeleteAsync().ForceSynchronous();
        }

        /// <inheritdoc cref="ServerReference.SnapshotAsync"/>
        public static Image Snapshot(this ServerReference server, SnapshotRequest request)
        {
            return server.SnapshotAsync(request).ForceSynchronous();
        }
    }
}
