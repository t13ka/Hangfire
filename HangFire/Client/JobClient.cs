﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using HangFire.States;
using ServiceStack.Redis;

namespace HangFire.Client
{
    internal class JobClient : IJobClient
    {
        private readonly JobCreator _jobCreator;
        private readonly IRedisClient _redis;

        public JobClient(IRedisClientsManager redisManager)
            : this(redisManager, JobCreator.Current)
        {
        }

        public JobClient(IRedisClientsManager redisManager, JobCreator jobCreator)
        {
            if (redisManager == null) throw new ArgumentNullException("redisManager");
            if (jobCreator == null) throw new ArgumentNullException("jobCreator");

            _redis = redisManager.GetClient();
            _jobCreator = jobCreator;
        }

        public string CreateJob(
            string jobId, Type jobType, JobState state, object args)
        {
            return CreateJob(jobId, jobType, state, PropertiesToDictionary(args));
        }

        public string CreateJob(
            string jobId, Type jobType, JobState state, IDictionary<string, string> args)
        {
            if (String.IsNullOrEmpty(jobId)) throw new ArgumentNullException("jobId");
            if (jobType == null) throw new ArgumentNullException("jobType");
            if (state == null) throw new ArgumentNullException("state");
            if (args == null) throw new ArgumentNullException("args");

            if (!typeof(BackgroundJob).IsAssignableFrom(jobType))
            {
                throw new ArgumentException(
                    String.Format("The type '{0}' must inherit '{1}'.", jobType, typeof(BackgroundJob)),
                    "jobType");
            }

            var jobParameters = CreateJobParameters(jobType, args);

            var context = new CreateContext(
                new ClientJobDescriptor(_redis, jobId, jobParameters, state));

            _jobCreator.CreateJob(context);

            return jobId;
        }

        public virtual void Dispose()
        {
            _redis.Dispose();
        }

        private static Dictionary<string, string> CreateJobParameters(
            Type jobType, IDictionary<string, string> jobArgs)
        {
            var job = new Dictionary<string, string>();
            job["Type"] = jobType.AssemblyQualifiedName;
            job["Args"] = JobHelper.ToJson(jobArgs);
            job["CreatedAt"] = JobHelper.ToStringTimestamp(DateTime.UtcNow);

            return job;
        }

        private static IDictionary<string, string> PropertiesToDictionary(object obj)
        {
            var result = new Dictionary<string, string>();
            if (obj == null) return result;

            foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(obj))
            {
                var propertyValue = descriptor.GetValue(obj);
                string value = null;

                if (propertyValue != null)
                {
                    try
                    {
                        var converter = TypeDescriptor.GetConverter(propertyValue.GetType());
                        value = converter.ConvertToInvariantString(propertyValue);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            String.Format(
                                "Could not convert property '{0}' of type '{1}' to a string. See the inner exception for details.",
                                descriptor.Name,
                                descriptor.PropertyType),
                            ex);
                    }
                }

                result.Add(descriptor.Name, value);
            }

            return result;
        }
    }
}
