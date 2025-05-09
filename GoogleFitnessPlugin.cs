using System.ComponentModel;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Fitness.v1;
using Google.Apis.Fitness.v1.Data;
using Google.Apis.Services;
using Microsoft.SemanticKernel;

namespace FirstPlugin;

public class GoogleFitnessPlugin
{
    private readonly FitnessService _fitnessService;

    public GoogleFitnessPlugin()
    {
        // Initialize the Fitness API service with the provided token
        _fitnessService = new FitnessService(new BaseClientService.Initializer
        {
            HttpClientInitializer = GoogleCredential.FromAccessToken(Environment.GetEnvironmentVariable("FITNESS_API_TOKEN")),
        });
    }

    [KernelFunction("get_step_count")]
    [Description("Gets the user's step count for a specified time range")]
    public async Task<int> GetStepCountAsync(
        [Description("Start time in ISO 8601 format")] string startTime,
        [Description("End time in ISO 8601 format")] string endTime)
    {
        Console.WriteLine($"Start Time: {startTime}, End Time: {endTime}");
        var request = new AggregateRequest
        {
            AggregateBy = new List<AggregateBy>
            {
                new AggregateBy
                {
                    DataTypeName = "com.google.step_count.delta"
                }
            },
            BucketByTime = new BucketByTime
            {
                DurationMillis = (long)(DateTime.Parse(endTime) - DateTime.Parse(startTime)).TotalMilliseconds
            },
            StartTimeMillis = new DateTimeOffset(DateTime.Parse(startTime)).ToUnixTimeMilliseconds(),
            EndTimeMillis = new DateTimeOffset(DateTime.Parse(endTime)).ToUnixTimeMilliseconds()
        };

        var response = await _fitnessService.Users.Dataset.Aggregate(request, "me").ExecuteAsync();
        
        int totalSteps = 0;
        foreach (var bucket in response.Bucket ?? Enumerable.Empty<AggregateBucket>())
        {
            foreach (var dataset in bucket.Dataset ?? Enumerable.Empty<Dataset>())
            {
                foreach (var point in dataset.Point ?? Enumerable.Empty<DataPoint>())
                {
                    foreach (var value in point.Value ?? Enumerable.Empty<Value>())
                    {
                        if (value.IntVal.HasValue)
                        {
                            totalSteps += value.IntVal.Value;
                        }
                    }
                }
            }
        }

        return totalSteps;
    }

    [KernelFunction("get_activity_minutes")]
    [Description("Gets the user's activity minutes for a specified time range")]
    public async Task<Dictionary<string, double>> GetActivityMinutesAsync(
        [Description("Start time in ISO 8601 format")] string startTime,
        [Description("End time in ISO 8601 format")] string endTime)
    {
        Console.WriteLine($"Start Time: {startTime}, End Time: {endTime}");
        var request = new AggregateRequest
        {
            AggregateBy = new List<AggregateBy>
            {
                new AggregateBy
                {
                    DataTypeName = "com.google.activity.segment"
                }
            },
            BucketByTime = new BucketByTime
            {
                DurationMillis = (long)(DateTime.Parse(endTime) - DateTime.Parse(startTime)).TotalMilliseconds
            },
            StartTimeMillis = new DateTimeOffset(DateTime.Parse(startTime)).ToUnixTimeMilliseconds(),
            EndTimeMillis = new DateTimeOffset(DateTime.Parse(endTime)).ToUnixTimeMilliseconds()
        };

        var response = await _fitnessService.Users.Dataset.Aggregate(request, "me").ExecuteAsync();
        var activityMinutes = new Dictionary<string, double>();

        foreach (var bucket in response.Bucket ?? Enumerable.Empty<AggregateBucket>())
        {
            foreach (var dataset in bucket.Dataset ?? Enumerable.Empty<Dataset>())
            {
                foreach (var point in dataset.Point ?? Enumerable.Empty<DataPoint>())
                {
                    foreach (var value in point.Value ?? Enumerable.Empty<Value>())
                    {
                        if (value.IntVal.HasValue)
                        {
                            var activityType = value.IntVal.Value.ToString();
                            var durationMinutes = (point.EndTimeNanos ?? 0 - point.StartTimeNanos ?? 0) / (60.0 * 1000000000);
                            
                            if (!string.IsNullOrEmpty(activityType))
                            {
                                if (activityMinutes.ContainsKey(activityType))
                                {
                                    activityMinutes[activityType] += durationMinutes;
                                }
                                else
                                {
                                    activityMinutes[activityType] = durationMinutes;
                                }
                            }
                        }
                    }
                }
            }
        }

        return activityMinutes;
    }

    [KernelFunction("get_heart_rate")]
    [Description("Gets the user's heart rate data for a specified time range")]
    public async Task<List<HeartRateDataPoint>> GetHeartRateAsync(
        [Description("Start time in ISO 8601 format")] string startTime,
        [Description("End time in ISO 8601 format")] string endTime)
    {
        var request = new AggregateRequest
        {
            AggregateBy = new List<AggregateBy>
            {
                new AggregateBy
                {
                    DataTypeName = "com.google.heart_rate.bpm"
                }
            },
            BucketByTime = new BucketByTime
            {
                DurationMillis =  1 * 1000 // 1 minutes in milliseconds
            },
            StartTimeMillis = new DateTimeOffset(DateTime.Parse(startTime)).ToUnixTimeMilliseconds(),
            EndTimeMillis = new DateTimeOffset(DateTime.Parse(endTime)).ToUnixTimeMilliseconds()
        };

        var response = await _fitnessService.Users.Dataset.Aggregate(request, "me").ExecuteAsync();

        var heartRateData = new List<HeartRateDataPoint>();

        foreach (var bucket in response.Bucket ?? Enumerable.Empty<AggregateBucket>())
        {
            foreach (var dataset in bucket.Dataset ?? Enumerable.Empty<Dataset>())
            {
                foreach (var point in dataset.Point ?? Enumerable.Empty<DataPoint>())
                {
                    foreach (var value in point.Value ?? Enumerable.Empty<Value>())
                    {
                        if (value.FpVal.HasValue && point.StartTimeNanos.HasValue)
                        {
                            heartRateData.Add(new HeartRateDataPoint
                            {
                                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(point.StartTimeNanos.Value / 1000000).DateTime,
                                Bpm = (int)value.FpVal.Value
                            });
                        }
                    }
                }
            }
        }

        return heartRateData;
    }
}

public class HeartRateDataPoint
{
    public DateTime Timestamp { get; set; }
    public int Bpm { get; set; }
}