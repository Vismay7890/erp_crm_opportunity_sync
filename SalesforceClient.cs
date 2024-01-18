using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using OpenQA.Selenium;
using latlog;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Xml.Linq;
using LogLevel = latlog.LogLevel;

namespace OpportunitySync
{
    internal class SalesforceClient
    {
        private const string LOGIN_ENDPOINT = "https://login.salesforce.com/services/oauth2/token";
        private const string API_ENDPOINT = "/services/data/v51.0";

        public string Username { get; set; }
        public string Password { get; set; }
        public string Token { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }

        public string AuthToken { get; set; }
        public string InstanceUrl { get; set; }

        static SalesforceClient()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls;
        }

        public void Login()
        {
            try
            {
                var clientId = ClientId;
                var clientSecret = ClientSecret;
                var username = Username;
                var password = Password + Token;

                var client = new HttpClient();
                var tokenRequest = new HttpRequestMessage(HttpMethod.Post, LOGIN_ENDPOINT);
                tokenRequest.Content = new FormUrlEncodedContent(new[]
                {
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password)
            });

                // Request the token
                var tokenResponse = client.SendAsync(tokenRequest).Result;
                var body = tokenResponse.Content.ReadAsStringAsync().Result;

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Error getting access token. Status Code: {tokenResponse.StatusCode}, Reason: {tokenResponse.ReasonPhrase}");
                    return;
                }

                var values = JsonConvert.DeserializeObject<Dictionary<string, string>>(body);

                if (values.ContainsKey("access_token"))
                {
                    AuthToken = values["access_token"];
                    Console.WriteLine("AuthToken = " + AuthToken);
                }
                else
                {
                    Console.WriteLine("Access token not found in the response.");
                    return;
                }

                if (values.ContainsKey("instance_url"))
                {
                    InstanceUrl = values["instance_url"];
                    Console.WriteLine("Instance URL = " + InstanceUrl);
                }
                else
                {
                    Console.WriteLine("Instance URL not found in the response.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred during login: {ex.Message}");
            }
        }
        public string Query()
        {
            Latlog.Log(latlog.LogLevel.Info, "Started to Query the table");

            using (var client = new HttpClient())
            {
                string restRequest = InstanceUrl + API_ENDPOINT + "/query?q=SELECT Id, Name , Amount ,AccountId ,TotalOpportunityQuantity from Opportunity where StageName = 'Closed Won' AND AccountId='0015j00001WTWmuAAH'";
                Latlog.Log(latlog.LogLevel.Info, "REST Request URL: " + restRequest);

                var request = new HttpRequestMessage(HttpMethod.Get, restRequest);
                request.Headers.Add("Authorization", "Bearer " + AuthToken);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Add("X-PreetyPrint", "1");

                var response = client.SendAsync(request).Result;
                string jsonResponse = response.Content.ReadAsStringAsync().Result;
                Latlog.Log(latlog.LogLevel.Info, $"oppotunities in closed won:{jsonResponse}");
                return jsonResponse;
            }

        }
        public string GetCustomerRefValue(string sfdata)
        {
            var opportunityData = JsonConvert.DeserializeObject<dynamic>(sfdata);
            var opportunity = opportunityData.records[0];
            string AccountId = opportunity.AccountId;
            Latlog.Log(latlog.LogLevel.Info, "Started to Query the table");

            using (var client = new HttpClient())
            {
                string restRequest = InstanceUrl + API_ENDPOINT + $"/query?q=SELECT ErId__c from Account where Id = '{AccountId}'";
                Latlog.Log(latlog.LogLevel.Info, "REST Request URL: " + restRequest);

                var request = new HttpRequestMessage(HttpMethod.Get, restRequest);
                request.Headers.Add("Authorization", "Bearer " + AuthToken);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Add("X-PreetyPrint", "1");

                var response = client.SendAsync(request).Result;
                string jsonResponse = response.Content.ReadAsStringAsync().Result;
                var accountData = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
                var account = accountData.records[0];
                string erId = account.ErId__c;

                Latlog.Log(latlog.LogLevel.Info, $"ErId for Rough: {erId}");

                return erId;
            }

        }

        public string GetOpportunityProducts(string sfdata , string accessToken,  string realmId)
        {
            Latlog.Log(latlog.LogLevel.Info, "Started to Query the table");
            var opportunityData = JsonConvert.DeserializeObject<dynamic>(sfdata);
            var opportunity = opportunityData.records[0];
            string opportunityId = opportunity.Id;

            using (var client = new HttpClient())
            {
                string restRequest = InstanceUrl + API_ENDPOINT + $"/query?q=SELECT Id, Product2Id, Quantity, UnitPrice, TotalPrice FROM OpportunityLineItem WHERE OpportunityId = '{opportunityId}'";
                Latlog.Log(latlog.LogLevel.Info, "REST Request URL: " + restRequest);

                var request = new HttpRequestMessage(HttpMethod.Get, restRequest);
                request.Headers.Add("Authorization", "Bearer " + AuthToken);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Add("X-PreetyPrint", "1");

                var response = client.SendAsync(request).Result;
                string jsonResponse = response.Content.ReadAsStringAsync().Result;

                Latlog.Log(latlog.LogLevel.Info, $"Opportunity Products: {jsonResponse}");

                var opportunityProducts = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
                var product2Ids = ((IEnumerable<dynamic>)opportunityProducts.records)
                    .Select(item => item.Product2Id.Value.ToString())
                    .ToList();

                List<string> product2IdsAsStrings = product2Ids.Select(x => (string)x).ToList();

                string productNames = GetProductNames(product2IdsAsStrings , accessToken, realmId);
               // Latlog.Log(LogLevel.Info, $"Productnames with quickbook id:{productNames}");
                string jsonResponseWithProductNames = AddProductNamesToOpportunityProducts(jsonResponse, productNames);

                return jsonResponseWithProductNames;
            }
        }


        private string GetProductNames(List<string> productIds , string accessToken, string realmId)
        {
            using (var client = new HttpClient())
            {
                string productIdsString = string.Join("','", productIds);
                string restRequest = $"{InstanceUrl}{API_ENDPOINT}/query?q=SELECT Id, Name FROM Product2 WHERE Id IN ('{productIdsString}')";

                var request = new HttpRequestMessage(HttpMethod.Get, restRequest);
                request.Headers.Add("Authorization", "Bearer " + AuthToken);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Add("X-PreetyPrint", "1");

                var response = client.SendAsync(request).Result;
                string jsonResponse = response.Content.ReadAsStringAsync().Result;
                var productNames = JsonConvert.DeserializeObject<dynamic>(jsonResponse);

                using (var quickbooksClient = new HttpClient())
                {
                    foreach (var product in productNames.records)
                    {
                        string productName = product.Name.ToString();

                        string quickbooksQuery = $"SELECT Id FROM item WHERE Name = '{productName}'";


                        var quickbooksRequest = new HttpRequestMessage(HttpMethod.Get, $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/query?query=" + quickbooksQuery);
                        quickbooksRequest.Headers.Add("Authorization", "Bearer " + accessToken);
                        quickbooksRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                        var quickbooksResponse = quickbooksClient.SendAsync(quickbooksRequest).Result;
                        string quickbooksJsonResponse = quickbooksResponse.Content.ReadAsStringAsync().Result;
                        var quickbooksProduct = JsonConvert.DeserializeObject<dynamic>(quickbooksJsonResponse);
                        Console.WriteLine($"Product {productName} in QuickBooks: {quickbooksJsonResponse}");
                        if (quickbooksProduct.QueryResponse != null && quickbooksProduct.QueryResponse.Item != null && quickbooksProduct.QueryResponse.Item.Count > 0)
                        {
                            string quickbooksProductId = quickbooksProduct.QueryResponse.Item[0].Id.ToString();
                        product.IdFromQuickBooks = quickbooksProductId;
                            
                        }
                        else
                        {
                            string createProductData = $@"
          {{
                ""TrackQtyOnHand"": true,
                ""Name"": ""{productName}"",
                ""QtyOnHand"": 1000000, 
                  ""IncomeAccountRef"": {{
                    ""name"": ""Sales of Product Income"", 
                    ""value"": ""79""
                  }}, 
                  ""AssetAccountRef"": {{
                    ""name"": ""Inventory Asset"", 
                    ""value"": ""81""
                  }}, 
                  ""InvStartDate"": ""2015-01-01"", 
                  ""Type"": ""Inventory"",
                ""ExpenseAccountRef"": {{
                    ""name"": ""Cost of Goods Sold"", 
                    ""value"": ""80""
                }}
            }}";
                            var createProductRequest = new HttpRequestMessage(HttpMethod.Post, $"https://sandbox-quickbooks.api.intuit.com/v3/company/{realmId}/item");
                            createProductRequest.Headers.Add("Authorization", "Bearer " + accessToken);
                            createProductRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                            createProductRequest.Content = new StringContent(createProductData, Encoding.UTF8, "application/json");

                            var createProductResponse = quickbooksClient.SendAsync(createProductRequest).Result;
                            string createProductJsonResponse = createProductResponse.Content.ReadAsStringAsync().Result;

                            var createdProduct = JsonConvert.DeserializeObject<dynamic>(createProductJsonResponse);
                            Latlog.Log(LogLevel.Debug,$"Product created in QuickBooks: {createProductJsonResponse}");

                            product.IdFromQuickBooks = createdProduct.Item[0].Id.ToString();
                        }

                        
                    }
                }
                return JsonConvert.SerializeObject(productNames);

            }
        }

        private string AddProductNamesToOpportunityProducts(string opportunityProductsData, string productNamesData)
        {
            var opportunityProducts = JsonConvert.DeserializeObject<dynamic>(opportunityProductsData);
            var productNames = JsonConvert.DeserializeObject<dynamic>(productNamesData);

            foreach (var product in opportunityProducts.records)
            {
                string productId = product.Product2Id.ToString();

                var matchingProduct = ((IEnumerable<dynamic>)productNames.records).FirstOrDefault(p => p.Id.ToString() == productId);

                if (matchingProduct != null)
                {
                    string productName = matchingProduct.Name.ToString();
                    string quickbooksProductId = matchingProduct.IdFromQuickBooks.ToString();

                    product.ProductName = productName;
                    product.QuickBooksId = quickbooksProductId;  
                }
                else
                {
                    product.ProductName = "Unknown";
                    product.QuickBooksId = "Not found";
                }
            }

            string jsonResponseWithProductNames = JsonConvert.SerializeObject(opportunityProducts);
            Latlog.Log(LogLevel.Debug,$"----Final Json Response With Product ID-----");
            Latlog.Log(LogLevel.Debug, $"{jsonResponseWithProductNames}");
            return jsonResponseWithProductNames;
        }




        public void upsertcustomer(string jsontable)
        {
            Latlog.Log(latlog.LogLevel.Info, "userting data to salesforce 👍");
            const int batchsize = 200;

            try
            {
                using (var client = new HttpClient())
                {
                    var customerarray = JArray.Parse(jsontable);

                    for (int i = 0; i < customerarray.Count; i += batchsize)
                    {
                        var batchcustomers = customerarray.Skip(i).Take(batchsize).ToList();

                        var batchrequest = new
                        {
                            allornone = false,
                            records = new List<object>()
                        };

                        foreach (var customer in batchcustomers)
                        {
                            var externalid = customer.Value<string>("customer_id__c");

                            customer["attributes"] = new JObject
                    {
                        { "type", "customer__c" },
                        { "customer_id__c", externalid }
                    };



                            batchrequest.records.Add(customer);
                        }

                        string restrequest = $"{InstanceUrl}{API_ENDPOINT}/composite/sobjects/Customers__c/Cusotmer_Id__C";

                        string batchrequestjson = JsonConvert.SerializeObject(batchrequest);

                        // create the http request
                        var request = new HttpRequestMessage(HttpMethod.Patch, restrequest);
                        request.Headers.Add("authorization", "bearer " + AuthToken);
                        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        request.Headers.Add("x-prettyprint", "1");
                        request.Content = new StringContent(batchrequestjson, Encoding.UTF8, "application/json");

                        // send the request and get the response
                        var response = client.SendAsync(request).Result;

                        if (response.IsSuccessStatusCode)
                        {
                            JArray responsearray = JArray.Parse(response.Content.ReadAsStringAsync().Result);
                            Latlog.Log(latlog.LogLevel.Info, "data upsertted successfully 👍");
                        }
                        else
                        {
                            Latlog.Log(latlog.LogLevel.Error, $"data upsert failed http status code: {response.StatusCode}, response: {response.Content.ReadAsStringAsync().Result}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Latlog.Log(latlog.LogLevel.Error, $"exception in upsertcustomers: {ex.Message}");
            }
        }
        // Update the UpsertCustomers method to accept a List<object> with ErId__c and message
        // Update the UpsertCustomers method to accept a string argument
        public void Upsert(List<JObject> jsonObjects)
        {
            const int batchSize = 200;

            try
            {
                using (var client = new HttpClient())
                {
                    for (int i = 0; i < jsonObjects.Count; i += batchSize)
                    {
                        var batchObjects = jsonObjects.Skip(i).Take(batchSize).ToList();
                        var batchRequest = new
                        {
                            allOrNone = false,
                            records = new List<object>()
                        };

                        foreach (var jsonObject in batchObjects)
                        {
                            if (jsonObject["attributes"] == null)
                            {
                                jsonObject["attributes"] = new JObject();
                            }

                            if (jsonObject["Id"] != null)
                            {
                                // If "Id" is present, assume it's an existing record and use PATCH
                                string recordId = jsonObject.Value<string>("Id");
                                jsonObject.Remove("Id");
                                jsonObject["attributes"]["type"] = "CustomerClone__c";
                                jsonObject["Id"] = recordId;  // Include the Id in the payload for PATCH
                            }
                            else
                            {
                                // If "Id" is not present, assume it's a new record and use POST
                                jsonObject["attributes"]["type"] = "CustomerClone__c";
                            }

                            foreach (var property in jsonObject.Properties())
                            {
                                if (property.Value.Type == JTokenType.Date)
                                {
                                    property.Value = ((DateTime)property.Value).ToString("yyyy-MM-ddTHH:mm:ss");
                                }
                            }

                            batchRequest.records.Add(jsonObject);
                        }

                        string restRequest = $"{InstanceUrl}{API_ENDPOINT}/composite/sobjects/";
                        //Latlog(latlog.LogLevel.Info, $"REST Request URL For Upsert: {restRequest}"); // Debug statement

                        string batchRequestJson = JsonConvert.SerializeObject(batchRequest);
                        //Latlog(latlog.LogLevel.Info, $"Batch Request Fields: {batchRequestJson}");

                        var request = new HttpRequestMessage(new HttpMethod("PATCH"), restRequest);
                        request.Headers.Add("Authorization", "Bearer " + AuthToken);
                        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        request.Headers.Add("X-PrettyPrint", "1");

                        request.Content = new StringContent(batchRequestJson, Encoding.UTF8, "application/json");

                        var response = client.SendAsync(request).Result;

                        if (response.IsSuccessStatusCode)
                        {
                            JArray responseArray = JArray.Parse(response.Content.ReadAsStringAsync().Result);

                            Latlog.Log(latlog.LogLevel.Info, "Data upserted Successfully");


                        }
                        else
                        {
                            Latlog.Log(latlog.LogLevel.Error, $"Data Upsert Failed HTTP Status Code: {response.StatusCode}, Response: {response.Content.ReadAsStringAsync().Result}");

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Latlog.Log(latlog.LogLevel.Error, $"Exception in Upsert: {ex.Message}");

            }
        }






    }
}