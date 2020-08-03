using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;


namespace WorkerServiceProject
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly WorkerOptions _option;
        private List<WeatherResponse> data = null;

        private HttpClient client;

        public Worker(ILogger<Worker> logger, WorkerOptions options)
        {
            _logger = logger;
            _option = options;
        }


        public override Task StartAsync(CancellationToken cancellationToken)
        {
            client = new HttpClient(); 
            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            client.Dispose();
            return base.StopAsync(cancellationToken);
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Get Weather information for Lagos
                var result = await client.GetAsync(String.Format("http://api.openweathermap.org/data/2.5/weather?q=Lagos,ng&appid={0}", _option.WeatherAPIKey));

                if (result.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Data fetched successfully from weather API with status {status}", result.StatusCode);
                    /* Set current city variable and fetch all data from the DB to check if there
                     are existing logs */
                    string city = "Lagos";
                    var response = await result.Content.ReadAsStringAsync();
                    dynamic deserialized = JsonConvert.DeserializeObject(response);
                    string baseUrl = "https://weather-log-api.azurewebsites.net/api/log";
                    var getAllData = await client.GetAsync(baseUrl);
                    string temp = deserialized.main.temp;

                    // Post Data
                    var postObject = new { city, current_temperature = temp };
                    string seriObj = JsonConvert.SerializeObject(postObject);
                    var postContent = new StringContent(seriObj);
                    postContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                    // Post data end here

                    if (getAllData.IsSuccessStatusCode)
                    {
              
                        var cityResponse = await getAllData.Content.ReadAsStringAsync();
                        data = JsonConvert.DeserializeObject<List<WeatherResponse>>(cityResponse);
                        
                        
                        if (data.Count > 0)
                        {
                            if (data.Count > 5)
                            {
                                var deleteRange = data.ToList()[5];
                                var deleteUrl = String.Format("{0}/{1}", baseUrl, deleteRange.id);
                                var deleteRangeReq = await client.DeleteAsync(deleteUrl);
                                if (deleteRangeReq.IsSuccessStatusCode)
                                {
                                    _logger.LogInformation("All logs with ids below {id} are successfully deleted", deleteRange.id);
                                } else
                                {
                                    _logger.LogError("Delete request failed for logs with id below {id}", deleteRange.id);
                                }
                            }
                            // Log information for successful Get request
                            _logger.LogInformation("The service successfully fetched {count} log(s) from GET /api/logs", data.Count);
                            IEnumerable<WeatherResponse> isExist = data.Where(p => p.city.ToLower() == city.ToLower() && p.read_count < 10);
                            if (isExist.ToList().Count > 0)
                            {
                                var updatedItem = isExist.ToList()[0];
                               
                                var update = new { read_count = updatedItem.read_count + 1, current_temperature = temp };
                                string updateSerializedObj = JsonConvert.SerializeObject(update);
                                var httpContent = new StringContent(updateSerializedObj);
                                httpContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                                var apiUrl = String.Format("{0}/{1}", baseUrl, updatedItem.id);
                                var updateCount = await client.PutAsync(apiUrl, httpContent);
                       
                                if (updateCount.IsSuccessStatusCode)
                                {
                                    _logger.LogInformation("Update successful with status {status} for {city}", updateCount.StatusCode, city);
                                } else
                                {
                                    _logger.LogError("Update failed with status {status} for {city}", updateCount.StatusCode, city);
                                }
                    
                            } else
                            {
                                // Create new temperature log if the city has reached update limit of read_count=10
                                _logger.LogInformation("No item to update for now");
                                var postNoUpdate = await client.PostAsync(baseUrl, postContent);
                                if (postNoUpdate.IsSuccessStatusCode)
                                {
                                    _logger.LogInformation("New Temperature log successfully created for {city}", city);
                                } else
                                {
                                    _logger.LogError("Temperature log creation failed for {city}", city);
                                }
                            }
                           
                
                        } else
                        {
                            // Create First temperature log in the database
                            _logger.LogInformation("No data returned from DB");
                            var postFirstLog = await client.PostAsync(baseUrl, postContent);
                            if (postFirstLog.IsSuccessStatusCode)
                            {
                                _logger.LogInformation("First temperature log successfully created for {city}", city);
                            } else
                            {
                                _logger.LogError("Temperature log creation failed for {city}", city);
                            }
                        }
                    }
                } else
                {
                    _logger.LogError("Weather API down with Status code {StatusCode}", result.StatusCode);
                }
                await Task.Delay(300 * 1000, stoppingToken);
            }
        }
    }
}
