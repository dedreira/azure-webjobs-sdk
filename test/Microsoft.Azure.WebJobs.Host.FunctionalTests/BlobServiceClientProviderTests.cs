﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests
{
    /// <summary>
    /// Tests whether the StorageClientProvider can properly create a client and send a request
    /// </summary>
    public class BlobServiceClientProviderTests
    {
        private const string StorageConnection = "AzureWebJobsStorage";

        private readonly BlobServiceClientProvider _blobServiceClientProvider;
        private readonly IConfiguration _configuration;

        public BlobServiceClientProviderTests()
        {
            _configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddTestSettings()
                .Build();

            IHost tempHost = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddAzureStorageCoreServices();
                }).Build();

            var componentFactory = tempHost.Services.GetRequiredService<AzureComponentFactory>();
            var logForwarder = tempHost.Services.GetRequiredService<AzureEventSourceLogForwarder>();
            _blobServiceClientProvider = new BlobServiceClientProvider(componentFactory, logForwarder);
        }

        [Fact]
        public async Task Create_ReturnsValidClient()
        {
            var client = _blobServiceClientProvider.Create(StorageConnection, _configuration);
            Assert.NotNull(client);
            await VerifyServiceAvailable(client);
        }

        [Fact]
        public async Task Create_NameResolverReturnsValidClient()
        {
            var resolver = new DefaultNameResolver(_configuration);

            var client = _blobServiceClientProvider.Create(StorageConnection, resolver, _configuration);
            await VerifyServiceAvailable(client);
        }

        [Fact]
        public async Task Create_ConnectionStringSectionChecked()
        {
            var testData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "ConnectionStrings:AzureWebJobsStorage", Environment.GetEnvironmentVariable(StorageConnection) },
                { "AzureWebJobsStorage", "" }
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(testData)
                .AddTestSettings()
                .Build();

            var client = _blobServiceClientProvider.Create(StorageConnection, configuration);
            await VerifyServiceAvailable(client);
        }

        [Fact]
        public void Create_SanitizesErrorMessages()
        {
            string connectionString = Environment.GetEnvironmentVariable(StorageConnection);

            var testData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "AzureWebJobsStorage", connectionString }
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(testData)
                .AddTestSettings()
                .Build();

            // here we simulate a case where a customer has mistakenly put their actual connection string
            // inline in code/config, instead of pointing at an app setting name
            var ex = Assert.Throws<InvalidOperationException>(() => _blobServiceClientProvider.Create(connectionString, configuration));

            Assert.Equal("Storage account connection string 'AzureWebJobs[Hidden Credential]' does not exist. Make sure that it is a defined App Setting.", ex.Message);
        }

        private async Task VerifyServiceAvailable(BlobServiceClient client)
        {
            try
            {
                var propertiesResponse = await client.GetPropertiesAsync();
                Assert.True(true);
            }
            catch (Exception e)
            {
                Assert.False(true, $"Could not establish connection to BlobService. {e}");
            }
        }
    }
}
