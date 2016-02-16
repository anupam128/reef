﻿// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Org.Apache.REEF.Client.API;
using Org.Apache.REEF.Client.Local;
using Org.Apache.REEF.Client.Yarn;
using Org.Apache.REEF.Client.Yarn.RestClient;
using Org.Apache.REEF.Client.YARN;
using Org.Apache.REEF.Client.YARN.RestClient;
using Org.Apache.REEF.Driver;
using Org.Apache.REEF.IO.FileSystem.AzureBlob;
using Org.Apache.REEF.Tang.Annotations;
using Org.Apache.REEF.Tang.Implementations.Configuration;
using Org.Apache.REEF.Tang.Implementations.Tang;
using Org.Apache.REEF.Tang.Interface;
using Org.Apache.REEF.Tang.Util;
using Org.Apache.REEF.Utilities.Logging;

namespace Org.Apache.REEF.Examples.HelloREEF
{
    /// <summary>
    /// A Tool that submits HelloREEFDriver for execution.
    /// </summary>
    public sealed class HelloREEF
    {
        private static readonly Logger Logger = Logger.GetLogger(typeof(HelloREEF));
        private const string Local = "local";
        private const string YARN = "yarn";
        private const string YARNRest = "yarnrest";
        private const string HDInsight = "hdi";
        private readonly IREEFClient _reefClient;
        private readonly JobSubmissionBuilderFactory _jobSubmissionBuilderFactory;

        [Inject]
        private HelloREEF(IREEFClient reefClient, JobSubmissionBuilderFactory jobSubmissionBuilderFactory)
        {
            _reefClient = reefClient;
            _jobSubmissionBuilderFactory = jobSubmissionBuilderFactory;
        }

        /// <summary>
        /// Runs HelloREEF using the IREEFClient passed into the constructor.
        /// </summary>
        private void Run()
        {
            // The driver configuration contains all the needed bindings.
            var helloDriverConfiguration = DriverConfiguration.ConfigurationModule
                .Set(DriverConfiguration.OnEvaluatorAllocated, GenericType<HelloDriver>.Class)
                .Set(DriverConfiguration.OnDriverStarted, GenericType<HelloDriver>.Class)
                .Build();

            // The JobSubmission contains the Driver configuration as well as the files needed on the Driver.
            var helloJobSubmission = _jobSubmissionBuilderFactory.GetJobSubmissionBuilder()
                .AddDriverConfiguration(helloDriverConfiguration)
                .AddGlobalAssemblyForType(typeof(HelloDriver))
                .SetJobIdentifier("HelloREEF")
                .Build();

            _reefClient.Submit(helloJobSubmission);
        }

        /// <summary>
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private static IConfiguration GetRuntimeConfiguration(string name)
        {
            switch (name)
            {
                case Local:
                    return LocalRuntimeClientConfiguration.ConfigurationModule
                        .Set(LocalRuntimeClientConfiguration.NumberOfEvaluators, "2")
                        .Build();
                case YARN:
                    return YARNClientConfiguration.ConfigurationModule.Build();
                case YARNRest:
                    return YARNClientConfiguration.ConfigurationModuleYARNRest.Build();
                case HDInsight:
                    const string connectionString =
                        "DefaultEndpointsProtocol=https;AccountName=reefhdi;AccountKey=fwaTXTQHP21kaHhSAPCeaUexRV3gm5ZjxGX5s4wmMeOzPy3gNVh/zKxqUBYHZiLfDYGFo6qnjviRTTqO9bV0pA==";
                    var azureBlockBlobConfig = AzureBlockBlobFileSystemConfiguration.ConfigurationModule
                        .Set(AzureBlockBlobFileSystemConfiguration.ConnectionString, connectionString)
                        .Build();
                    var yarnClientConfig = YARNClientConfiguration.ConfigurationModuleYARNRest
                        .Set(YARNClientConfiguration.YarnRestClientCredential,
                            GenericType<HDInsightTestCredential>.Class)
                        .Set(YARNClientConfiguration.YarnRmUrlProvider, GenericType<HDInsightRMUrlProvider>.Class)
                        .Set(YARNClientConfiguration.JobResourceUploader, GenericType<FileSystemJobResourceUploader>.Class)
                        .Set(YARNClientConfiguration.YarnCommandLineEnvironment, GenericType<HDInsightCommandLineEnvironment>.Class)
                        .Build();
                    return Configurations.Merge(
                        yarnClientConfig,
                        azureBlockBlobConfig);
                default:
                    throw new Exception("Unknown runtime: " + name);
            }
        }

        public static void Main(string[] args)
        {
            TangFactory.GetTang().NewInjector(GetRuntimeConfiguration(args.Length > 0 ? args[0] : Local)).GetInstance<HelloREEF>().Run();
        }

        public class HDInsightTestCredential : IYarnRestClientCredential
        {
            private const string UserName = @"reefdev";
            private const string Password = @"Coffee@@2015"; // TODO: Do not checkin!!!
            private readonly ICredentials _credentials = new NetworkCredential(UserName, Password);

            [Inject]
            private HDInsightTestCredential()
            {
            }

            public ICredentials Credentials
            {
                get { return _credentials; }
            }
        }

        public class HDInsightRMUrlProvider : IUrlProvider
        {
            private const string HDInsightUrl = "https://reefdev.azurehdinsight.net/";

            [Inject]
            private HDInsightRMUrlProvider()
            {
            }

            public Task<IEnumerable<Uri>> GetUrlAsync()
            {
                return Task.FromResult(Enumerable.Repeat(new Uri(HDInsightUrl), 1));
            }
        }
    }
}